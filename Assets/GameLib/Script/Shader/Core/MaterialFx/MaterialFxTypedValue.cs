#nullable enable
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Game.MaterialFx
{
    /// <summary>
    /// Layer 合成モード。
    /// </summary>
    public enum MaterialFxBlendMode
    {
        /// <summary>その時点の合成結果を上書き</summary>
        Override,
        /// <summary>その時点の合成結果に乗算（Float/Vector/Color のみ有効）</summary>
        Mul
    }

    /// <summary>
    /// 非boxing の値共用体。Layer 内部・合成・Fade で使用。
    /// struct なので値コピーで GC Alloc を回避。
    /// ★Size=96 固定: IL2CPP や将来のフィールド追加での破壊を防止。
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct MaterialFxTypedValue
    {
        [FieldOffset(0)] public ValueKind Type;

        // 数値型（重ねて配置）- Vector4 以下は 16 bytes
        [FieldOffset(4)] public float Float;
        [FieldOffset(4)] public int Int;
        [FieldOffset(4)] public Vector2 Float2;
        [FieldOffset(4)] public Vector3 Float3;
        [FieldOffset(4)] public Vector4 Float4;
        [FieldOffset(4)] public Color Color;

        // Matrix4x4 は 64 bytes なので別オフセット
        [FieldOffset(20)] public Matrix4x4 Matrix;

        // ★修正: 参照型は 8byte 境界にアラインする必要がある（64bit環境）
        // Matrix (64 bytes @ offset 20) の後 = 84 → 8byte 境界に繰り上げ = 88
        // Object を格納する汎用ポインタとして使用します（Texture/AnimationCurve など）
        [FieldOffset(88)] public object? Object;
        [FieldOffset(88)] public Texture? Texture;

        // ===== Factory Methods =====

        public static MaterialFxTypedValue FromFloat(float v)
            => new() { Type = ValueKind.Float, Float = v };

        public static MaterialFxTypedValue FromInt(int v)
            => new() { Type = ValueKind.Int, Int = v };

        public static MaterialFxTypedValue FromBool(bool v)
            => new() { Type = ValueKind.Bool, Int = v ? 1 : 0 };

        public static MaterialFxTypedValue FromVector2(Vector2 v)
            => new() { Type = ValueKind.Float2, Float2 = v };

        public static MaterialFxTypedValue FromVector3(Vector3 v)
            => new() { Type = ValueKind.Float3, Float3 = v };

        public static MaterialFxTypedValue FromVector4(Vector4 v)
            => new() { Type = ValueKind.Float4, Float4 = v };

        public static MaterialFxTypedValue FromColor(Color v)
            => new() { Type = ValueKind.Color, Color = v };

        public static MaterialFxTypedValue FromMatrix(Matrix4x4 v)
            => new() { Type = ValueKind.Matrix4x4, Matrix = v };

        public static MaterialFxTypedValue FromTexture(Texture? v)
            => new() { Type = ValueKind.Texture, Texture = v };

        public static MaterialFxTypedValue FromTextureArray(Texture? v)
        {
            // ★TextureArray 型安全チェック: Texture2DArray のみ許可（または null）
            if (v != null && v is not Texture2DArray)
            {
                throw new InvalidCastException(
                    $"TextureArray expects Texture2DArray or null, got {v.GetType().Name}");
            }
            return new() { Type = ValueKind.TextureArray, Texture = v };
        }

        public static MaterialFxTypedValue FromAnimationCurve(AnimationCurve? v)
            => new() { Type = ValueKind.AnimationCurve, Object = v };

        // ===== object からの変換（API境界で1回だけ使用）=====

        /// <summary>
        /// object を expectedType に変換。変換失敗時は例外。
        /// </summary>
        public static MaterialFxTypedValue Convert(object? value, ValueKind expectedType)
        {
            if (!TryConvert(value, expectedType, out var result))
                throw new InvalidCastException(
                    $"Cannot convert '{value?.GetType().Name ?? "null"}' to {expectedType}");
            return result;
        }

        /// <summary>
        /// object を expectedType に変換。変換失敗時は false を返す。
        /// </summary>
        public static bool TryConvert(object? value, ValueKind expectedType, out MaterialFxTypedValue result)
        {
            result = default;

            // 参照型は null 許容
            if (value == null)
            {
                if (IsReferenceType(expectedType))
                {
                    result = expectedType == ValueKind.Texture
                        ? FromTexture(null)
                        : FromTextureArray(null);
                    return true;
                }
                return false;
            }

            try
            {
                result = expectedType switch
                {
                    ValueKind.Float => FromFloat(global::System.Convert.ToSingle(value)),
                    ValueKind.Int => FromInt(global::System.Convert.ToInt32(value)),
                    ValueKind.Bool => FromBool(global::System.Convert.ToBoolean(value)),
                    ValueKind.Float2 => value is Vector2 v2 ? FromVector2(v2) : throw new InvalidCastException(),
                    ValueKind.Float3 => value is Vector3 v3 ? FromVector3(v3) : throw new InvalidCastException(),
                    ValueKind.Float4 => value is Vector4 v4 ? FromVector4(v4) : throw new InvalidCastException(),
                    ValueKind.Color => value is Color c ? FromColor(c) : throw new InvalidCastException(),
                    ValueKind.Matrix4x4 => value is Matrix4x4 m ? FromMatrix(m) : throw new InvalidCastException(),
                    // ★修正: 型チェック必須。`value as Texture` だと非Texture が null として成功する
                    ValueKind.Texture => value is Texture tex ? FromTexture(tex) : throw new InvalidCastException(),
                    ValueKind.TextureArray => value is Texture2DArray arr ? FromTextureArray(arr) : throw new InvalidCastException(),
                    ValueKind.AnimationCurve => value is AnimationCurve ac ? FromAnimationCurve(ac) : throw new InvalidCastException(),
                    _ => throw new ArgumentOutOfRangeException(nameof(expectedType))
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// TypedValue を object に変換（Query API 用）
        /// </summary>
        public object? ToObject()
        {
            return Type switch
            {
                ValueKind.Float => Float,
                ValueKind.Int => Int,
                ValueKind.Bool => Int != 0,
                ValueKind.Float2 => Float2,
                ValueKind.Float3 => Float3,
                ValueKind.Float4 => Float4,
                ValueKind.Color => Color,
                ValueKind.Matrix4x4 => Matrix,
                ValueKind.Texture => Texture,
                ValueKind.TextureArray => Texture,
                ValueKind.AnimationCurve => Object as AnimationCurve,
                _ => null
            };
        }

        static bool IsReferenceType(ValueKind t)
            => t is ValueKind.Texture or ValueKind.TextureArray or ValueKind.AnimationCurve;

        // ===== デフォルト値取得 =====

        /// <summary>
        /// Material から値を読めない場合のフォールバック値。
        /// </summary>
        public static MaterialFxTypedValue GetDefaultFallback(ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => FromFloat(0f),
                ValueKind.Int => FromInt(0),
                ValueKind.Bool => FromBool(false),
                ValueKind.Float2 => FromVector2(Vector2.zero),
                ValueKind.Float3 => FromVector3(Vector3.zero),
                ValueKind.Float4 => FromVector4(Vector4.zero),
                ValueKind.Color => FromColor(UnityEngine.Color.white),
                ValueKind.Matrix4x4 => FromMatrix(Matrix4x4.identity),
                ValueKind.Texture => FromTexture(null),
                ValueKind.TextureArray => FromTextureArray(null),
                ValueKind.AnimationCurve => FromAnimationCurve(null),
                _ => default
            };
        }

        public override string ToString()
        {
            return Type switch
            {
                ValueKind.Float => $"Float({Float})",
                ValueKind.Int => $"Int({Int})",
                ValueKind.Bool => $"Bool({Int != 0})",
                ValueKind.Float2 => $"Float2({Float2})",
                ValueKind.Float3 => $"Float3({Float3})",
                ValueKind.Float4 => $"Float4({Float4})",
                ValueKind.Color => $"Color({Color})",
                ValueKind.Matrix4x4 => $"Matrix4x4",
                ValueKind.Texture => $"Texture({Texture?.name ?? "null"})",
                ValueKind.TextureArray => $"TextureArray({Texture?.name ?? "null"})",
                ValueKind.AnimationCurve => $"AnimationCurve({(Object as AnimationCurve)?.length ?? 0})",
                _ => $"Unknown({Type})"
            };
        }
    }

    /// <summary>
    /// MaterialFxTypedValue の演算メソッド群。
    /// </summary>
    public static class MaterialFxTypedValueOps
    {
        /// <summary>
        /// Mul（乗算）がサポートされる型か。
        /// </summary>
        public static bool IsMulSupported(ValueKind t)
            => t is ValueKind.Float
                or ValueKind.Float2
                or ValueKind.Float3
                or ValueKind.Float4
                or ValueKind.Color;

        /// <summary>
        /// Lerp（線形補間）がサポートされる型か。
        /// </summary>
        public static bool IsLerpSupported(ValueKind t)
            => t is ValueKind.Float
                or ValueKind.Float2
                or ValueKind.Float3
                or ValueKind.Float4
                or ValueKind.Color;

        /// <summary>
        /// Fade 補間用 Lerp。
        /// </summary>
        public static MaterialFxTypedValue Lerp(in MaterialFxTypedValue a, in MaterialFxTypedValue b, float t)
        {
            if (a.Type != b.Type)
            {
                // Color と Float4 は実質的に同じ4成分値として扱えるため、
                // 型差があっても補間を継続して Fade が途切れないようにする。
                if (TryGetColorLikeVector(a, out var av) && TryGetColorLikeVector(b, out var bv))
                {
                    var lerped = Vector4.Lerp(av, bv, t);
                    if (b.Type == ValueKind.Color)
                        return MaterialFxTypedValue.FromColor(new Color(lerped.x, lerped.y, lerped.z, lerped.w));
                    if (b.Type == ValueKind.Float4)
                        return MaterialFxTypedValue.FromVector4(lerped);
                }

                return b;
            }

            return a.Type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(Mathf.Lerp(a.Float, b.Float, t)),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2(Vector2.Lerp(a.Float2, b.Float2, t)),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3(Vector3.Lerp(a.Float3, b.Float3, t)),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(Vector4.Lerp(a.Float4, b.Float4, t)),
                ValueKind.Color => MaterialFxTypedValue.FromColor(UnityEngine.Color.Lerp(a.Color, b.Color, t)),
                // Int/Bool/Matrix/Texture は補間不可 → t >= 0.5 で切り替え
                _ => t < 0.5f ? a : b
            };
        }

        static bool TryGetColorLikeVector(in MaterialFxTypedValue value, out Vector4 vector)
        {
            switch (value.Type)
            {
                case ValueKind.Color:
                    var c = value.Color;
                    vector = new Vector4(c.r, c.g, c.b, c.a);
                    return true;
                case ValueKind.Float4:
                    vector = value.Float4;
                    return true;
                default:
                    vector = default;
                    return false;
            }
        }

        /// <summary>
        /// Mul 合成用（成分乗算）。
        /// ★Mul 非対応型は例外ではなく incoming を返す（安全フォールバック）。
        /// </summary>
        public static MaterialFxTypedValue Multiply(in MaterialFxTypedValue a, in MaterialFxTypedValue b)
        {
            if (a.Type != b.Type)
                return b;

            return a.Type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(a.Float * b.Float),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2(
                    new Vector2(a.Float2.x * b.Float2.x, a.Float2.y * b.Float2.y)),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3(
                    new Vector3(a.Float3.x * b.Float3.x, a.Float3.y * b.Float3.y, a.Float3.z * b.Float3.z)),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(
                    new Vector4(a.Float4.x * b.Float4.x, a.Float4.y * b.Float4.y,
                               a.Float4.z * b.Float4.z, a.Float4.w * b.Float4.w)),
                ValueKind.Color => MaterialFxTypedValue.FromColor(a.Color * b.Color),

                // ★Mul 非対応型は Override と同等（incoming を採用）。例外を出さない。
                _ => b
            };
        }

        /// <summary>
        /// BlendMode に応じて合成。
        /// </summary>
        public static MaterialFxTypedValue Blend(
            in MaterialFxTypedValue current,
            in MaterialFxTypedValue incoming,
            MaterialFxBlendMode mode)
        {
            return mode switch
            {
                MaterialFxBlendMode.Override => incoming,
                MaterialFxBlendMode.Mul => Multiply(current, incoming),
                _ => incoming
            };
        }

        /// <summary>
        /// Weight 付き合成。Weight (0..1) でレイヤーの影響度を調整。
        /// ★Mul 非対応型・Lerp 非対応型の安全フォールバックを含む。
        /// </summary>
        public static MaterialFxTypedValue BlendWithWeight(
            in MaterialFxTypedValue current,
            in MaterialFxTypedValue incoming,
            MaterialFxBlendMode mode,
            float weight)
        {
            // Weight が 0 なら current をそのまま返す
            if (weight <= 0f) return current;
            // Weight が 1 なら通常の Blend
            if (weight >= 1f) return Blend(current, incoming, mode);

            // 型違いは incoming 優先（安全に逃がす）
            if (current.Type != incoming.Type)
                return incoming;

            // ★Mul 非対応型の場合、Override 相当の Weight 適用に切り替え
            if (mode == MaterialFxBlendMode.Mul && !IsMulSupported(current.Type))
            {
                // Lerp 可能なら Lerp、不可なら段階切替（t < 0.5 なら current, t >= 0.5 なら incoming）
                return IsLerpSupported(current.Type)
                    ? Lerp(current, incoming, weight)
                    : (weight < 0.5f ? current : incoming);
            }

            // ★Override でも Lerp 非対応型は段階切替
            if (mode == MaterialFxBlendMode.Override && !IsLerpSupported(current.Type))
            {
                return weight < 0.5f ? current : incoming;
            }

            return mode switch
            {
                MaterialFxBlendMode.Override => Lerp(current, incoming, weight),
                MaterialFxBlendMode.Mul => MultiplyWithWeight(current, incoming, weight),
                _ => Lerp(current, incoming, weight),
            };
        }

        /// <summary>
        /// Weight 付き Multiply。
        /// mul_factor = lerp(identity, incoming, weight)
        /// result = current * mul_factor
        /// </summary>
        static MaterialFxTypedValue MultiplyWithWeight(
            in MaterialFxTypedValue current,
            in MaterialFxTypedValue incoming,
            float weight)
        {
            // まず incoming を identity (1) と lerp して "適用度" を調整
            var identity = GetMultiplyIdentity(current.Type);
            var weightedIncoming = Lerp(identity, incoming, weight);

            // その後 current と乗算
            return Multiply(current, weightedIncoming);
        }

        /// <summary>
        /// Multiply の単位元（乗算しても変化しない値）を取得。
        /// </summary>
        static MaterialFxTypedValue GetMultiplyIdentity(ValueKind type)
        {
            return type switch
            {
                ValueKind.Float => MaterialFxTypedValue.FromFloat(1f),
                ValueKind.Float2 => MaterialFxTypedValue.FromVector2(Vector2.one),
                ValueKind.Float3 => MaterialFxTypedValue.FromVector3(Vector3.one),
                ValueKind.Float4 => MaterialFxTypedValue.FromVector4(Vector4.one),
                ValueKind.Color => MaterialFxTypedValue.FromColor(UnityEngine.Color.white),
                _ => MaterialFxTypedValue.GetDefaultFallback(type)
            };
        }
    }
}
