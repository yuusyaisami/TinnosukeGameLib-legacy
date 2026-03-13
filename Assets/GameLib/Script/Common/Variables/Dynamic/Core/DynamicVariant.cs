// Game.Common.DynamicVariant.cs
//
// DynamicVariant - 動的値の評価結果を表す軽量構造体
//
// 設計決定:
// - 数値/Bool/String/Vector/UnityObject参照を保持可能
// - object を外部に露出しない
// - 型変換は旧 VariableBag と同等の規則を適用

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Common
{
    /// <summary>
    /// DynamicValue の評価結果の種別。
    /// </summary>
    public enum ValueKind : byte
    {
        Null = 0,
        Bool = 1,
        Int = 2,
        Float = 3,
        String = 4,
        Vector2 = 5,
        Vector3 = 6,
        Vector4 = 7,
        Color = 8,
        UnityObject = 9,
        /// <summary>
        /// VarStore 専用の種別（式/ DynamicVariant では表現しない）。
        /// - IVarStore.TryGetVariant は ManagedRef を返さない
        /// - 式エンジンは ManagedRef varId を依存に持たない（別途 vNext でコンパイルエラー扱い）
        /// </summary>
        ManagedRef = 10,
        /// <summary>
        /// 期待型の自動推論を表すヒント値。
        /// 既存 ValueKind の数値互換を崩さないため 255 を使用する。
        /// </summary>
        Auto = 255,
    }

    /// <summary>
    /// 動的値の評価結果を表す軽量構造体。
    /// object を外部に露出せず、型安全なアクセスを提供。
    /// </summary>
    public readonly struct DynamicVariant : IEquatable<DynamicVariant>
    {
        // ストレージ（Union的に使用）
        readonly double _numericValue;      // bool/int/float/double を格納
        readonly string _stringValue;       // string を格納
        readonly Vector4 _vectorValue;      // Vector2/3/4/Color を格納
        readonly Object _objectValue;       // UnityEngine.Object を格納
        readonly object _managedRef;        // 非UnityEngine.Object の参照型を格納

        public readonly ValueKind Kind;

        static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        // ================================================================
        // コンストラクタ（内部用）
        // ================================================================

        DynamicVariant(ValueKind kind, double numeric, string str, Vector4 vec, Object obj, object managedRef = null)
        {
            Kind = kind;
            _numericValue = numeric;
            _stringValue = str;
            _vectorValue = vec;
            _objectValue = obj;
            _managedRef = managedRef;
        }

        // ================================================================
        // ファクトリメソッド
        // ================================================================

        public static DynamicVariant Null => new(ValueKind.Null, 0, null, default, null);

        public static DynamicVariant FromBool(bool value)
            => new(ValueKind.Bool, value ? 1 : 0, null, default, null);

        public static DynamicVariant FromInt(int value)
            => new(ValueKind.Int, value, null, default, null);

        public static DynamicVariant FromFloat(float value)
            => new(ValueKind.Float, value, null, default, null);

        public static DynamicVariant FromString(string value)
            => new(ValueKind.String, 0, value ?? string.Empty, default, null);

        public static DynamicVariant FromVector2(Vector2 value)
            => new(ValueKind.Vector2, 0, null, new Vector4(value.x, value.y, 0, 0), null);

        public static DynamicVariant FromVector3(Vector3 value)
            => new(ValueKind.Vector3, 0, null, new Vector4(value.x, value.y, value.z, 0), null);

        public static DynamicVariant FromVector4(Vector4 value)
            => new(ValueKind.Vector4, 0, null, value, null);

        public static DynamicVariant FromColor(Color value)
            => new(ValueKind.Color, 0, null, new Vector4(value.r, value.g, value.b, value.a), null);

        public static DynamicVariant FromUnityObject(Object value)
            => new(ValueKind.UnityObject, 0, null, default, value);

        /// <summary>
        /// 非UnityEngine.Object の参照型から DynamicVariant を生成。
        /// VarStore の ManagedRef サポート用。
        /// </summary>
        public static DynamicVariant FromManagedRef(object value)
        {
            if (value == null)
                return Null;
            // UnityEngine.Object の場合は FromUnityObject を使用
            if (value is Object unityObj)
                return FromUnityObject(unityObj);
            return new(ValueKind.ManagedRef, 0, null, default, null, value);
        }

        /// <summary>
        /// 任意のobjectからDynamicVariantを生成。
        /// 内部使用のみ（デバッグ観測API用）。
        /// </summary>
        internal static DynamicVariant FromObject(object value)
        {
            return value switch
            {
                null => Null,
                bool b => FromBool(b),
                int i => FromInt(i),
                float f => FromFloat(f),
                double d => FromFloat((float)d),
                long l => FromInt((int)l),
                string s => FromString(s),
                Vector2 v2 => FromVector2(v2),
                Vector3 v3 => FromVector3(v3),
                Vector4 v4 => FromVector4(v4),
                Color c => FromColor(c),
                Object obj => FromUnityObject(obj),
                _ => FromManagedRef(value)   // 非プリミティブ型は ManagedRef として格納
            };
        }

        // ================================================================
        // 型付きアクセサ
        // ================================================================

        public bool IsNull => Kind == ValueKind.Null;

        public bool AsBool => _numericValue != 0;
        public int AsInt => (int)_numericValue;
        public float AsFloat => (float)_numericValue;
        public string AsString => _stringValue ?? string.Empty;
        public Vector2 AsVector2 => new(_vectorValue.x, _vectorValue.y);
        public Vector3 AsVector3 => new(_vectorValue.x, _vectorValue.y, _vectorValue.z);
        public Vector4 AsVector4 => _vectorValue;
        public Color AsColor => new(_vectorValue.x, _vectorValue.y, _vectorValue.z, _vectorValue.w);
        public Object AsUnityObject => _objectValue;
        public object AsManagedRef => _managedRef;

        // ================================================================
        // TryGet（型変換付き）
        // ================================================================

        /// <summary>
        /// 型安全に値を取得。必要に応じて変換を試みる。
        /// </summary>
        public bool TryGet<T>(out T value)
        {
            value = default;
            var targetType = typeof(T);

            // 完全一致ケース
            if (TryGetDirect(out value))
                return true;

            // 型変換
            return TryConvert(out value);
        }

        bool TryGetDirect<T>(out T value)
        {
            value = default;
            var targetType = typeof(T);

            switch (Kind)
            {
                case ValueKind.Bool when targetType == typeof(bool):
                    value = (T)(object)AsBool;
                    return true;
                case ValueKind.Int when targetType == typeof(int):
                    value = (T)(object)AsInt;
                    return true;
                case ValueKind.Float when targetType == typeof(float):
                    value = (T)(object)AsFloat;
                    return true;
                case ValueKind.String when targetType == typeof(string):
                    value = (T)(object)AsString;
                    return true;
                case ValueKind.Vector2 when targetType == typeof(Vector2):
                    value = (T)(object)AsVector2;
                    return true;
                case ValueKind.Vector3 when targetType == typeof(Vector3):
                    value = (T)(object)AsVector3;
                    return true;
                case ValueKind.Vector4 when targetType == typeof(Vector4):
                    value = (T)(object)AsVector4;
                    return true;
                case ValueKind.Color when targetType == typeof(Color):
                    value = (T)(object)AsColor;
                    return true;
                case ValueKind.UnityObject when typeof(Object).IsAssignableFrom(targetType):
                    if (_objectValue == null || targetType.IsInstanceOfType(_objectValue))
                    {
                        value = (T)(object)_objectValue;
                        return true;
                    }
                    return false;
                case ValueKind.ManagedRef:
                    // ManagedRef: 型互換性をチェックして返す
                    if (_managedRef == null)
                    {
                        // null は参照型なら成功とみなす
                        if (!targetType.IsValueType)
                        {
                            value = default;
                            return true;
                        }
                        return false;
                    }
                    if (targetType.IsInstanceOfType(_managedRef))
                    {
                        value = (T)_managedRef;
                        return true;
                    }
                    return false;
            }

            return false;
        }

        bool TryConvert<T>(out T value)
        {
            value = default;
            var targetType = typeof(T);

            // 数値間変換
            if (IsNumericKind(Kind) && IsNumericType(targetType))
            {
                return TryConvertNumeric(targetType, out value);
            }

            // Vector変換
            if (IsVectorKind(Kind) && IsVectorType(targetType))
            {
                return TryConvertVector(targetType, out value);
            }

            // 文字列要求
            if (targetType == typeof(string))
            {
                value = (T)(object)ToString();
                return true;
            }

            return false;
        }

        bool TryConvertNumeric<T>(Type targetType, out T value)
        {
            value = default;
            var d = _numericValue;

            try
            {
                object result;
                if (targetType == typeof(int)) result = (int)d;
                else if (targetType == typeof(float)) result = (float)d;
                else if (targetType == typeof(double)) result = d;
                else if (targetType == typeof(long)) result = (long)d;
                else if (targetType == typeof(bool)) result = d != 0;
                else return false;

                value = (T)result;
                return true;
            }
            catch
            {
                return false;
            }
        }

        bool TryConvertVector<T>(Type targetType, out T value)
        {
            value = default;

            if (targetType == typeof(Vector2))
            {
                value = (T)(object)AsVector2;
                return true;
            }
            if (targetType == typeof(Vector3))
            {
                value = (T)(object)AsVector3;
                return true;
            }
            if (targetType == typeof(Vector4))
            {
                value = (T)(object)AsVector4;
                return true;
            }
            if (targetType == typeof(Color))
            {
                value = (T)(object)AsColor;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsNumericKind(ValueKind kind)
            => kind == ValueKind.Bool || kind == ValueKind.Int || kind == ValueKind.Float;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsVectorKind(ValueKind kind)
            => kind == ValueKind.Vector2 || kind == ValueKind.Vector3 ||
               kind == ValueKind.Vector4 || kind == ValueKind.Color;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsNumericType(Type type)
            => type == typeof(int) || type == typeof(float) || type == typeof(double) ||
               type == typeof(long) || type == typeof(bool);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsVectorType(Type type)
            => type == typeof(Vector2) || type == typeof(Vector3) ||
               type == typeof(Vector4) || type == typeof(Color);

        // ================================================================
        // 比較
        // ================================================================

        public bool Equals(DynamicVariant other)
        {
            if (Kind != other.Kind)
                return false;

            return Kind switch
            {
                ValueKind.Null => true,
                ValueKind.Bool or ValueKind.Int => (int)_numericValue == (int)other._numericValue,
                ValueKind.Float => Math.Abs(_numericValue - other._numericValue) < float.Epsilon,
                ValueKind.String => _stringValue == other._stringValue,
                ValueKind.Vector2 or ValueKind.Vector3 or
                ValueKind.Vector4 or ValueKind.Color => _vectorValue == other._vectorValue,
                ValueKind.UnityObject => ReferenceEquals(_objectValue, other._objectValue),
                ValueKind.ManagedRef => ReferenceEquals(_managedRef, other._managedRef),
                _ => false
            };
        }

        public override bool Equals(object obj) => obj is DynamicVariant other && Equals(other);

        public override int GetHashCode()
        {
            return Kind switch
            {
                ValueKind.Null => 0,
                ValueKind.Bool or ValueKind.Int or ValueKind.Float => _numericValue.GetHashCode(),
                ValueKind.String => _stringValue?.GetHashCode() ?? 0,
                ValueKind.Vector2 or ValueKind.Vector3 or
                ValueKind.Vector4 or ValueKind.Color => _vectorValue.GetHashCode(),
                ValueKind.UnityObject => _objectValue?.GetHashCode() ?? 0,
                ValueKind.ManagedRef => _managedRef?.GetHashCode() ?? 0,
                _ => 0
            };
        }

        public static bool operator ==(DynamicVariant left, DynamicVariant right) => left.Equals(right);
        public static bool operator !=(DynamicVariant left, DynamicVariant right) => !left.Equals(right);

        // ================================================================
        // ToString
        // ================================================================

        public override string ToString()
        {
            return Kind switch
            {
                ValueKind.Null => "null",
                ValueKind.Bool => AsBool ? "true" : "false",
                ValueKind.Int => AsInt.ToString(InvariantCulture),
                ValueKind.Float => AsFloat.ToString(InvariantCulture),
                ValueKind.String => _stringValue ?? string.Empty,
                ValueKind.Vector2 => $"({_vectorValue.x.ToString(InvariantCulture)}, {_vectorValue.y.ToString(InvariantCulture)})",
                ValueKind.Vector3 => $"({_vectorValue.x.ToString(InvariantCulture)}, {_vectorValue.y.ToString(InvariantCulture)}, {_vectorValue.z.ToString(InvariantCulture)})",
                ValueKind.Vector4 => $"({_vectorValue.x.ToString(InvariantCulture)}, {_vectorValue.y.ToString(InvariantCulture)}, {_vectorValue.z.ToString(InvariantCulture)}, {_vectorValue.w.ToString(InvariantCulture)})",
                ValueKind.Color => $"RGBA({_vectorValue.x.ToString(InvariantCulture)}, {_vectorValue.y.ToString(InvariantCulture)}, {_vectorValue.z.ToString(InvariantCulture)}, {_vectorValue.w.ToString(InvariantCulture)})",
                ValueKind.UnityObject => _objectValue != null ? _objectValue.name : "null",
                ValueKind.ManagedRef => _managedRef?.ToString() ?? "null",
                _ => "unknown"
            };
        }
    }
}
