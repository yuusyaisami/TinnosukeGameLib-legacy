using System;
using UnityEngine;
using Game.Common;
using Game.Animation;

namespace Game.Commands
{
    public enum ValueSource
    {
        VariableKey,
        Literal,
        LiteralAddVariable
    }

    /// <summary>
    /// 変数キーまたはリテラルを扱う汎用バインディング。
    /// </summary>
    [Serializable]
    public struct FlexibleValue<T>
    {
        [SerializeField] ValueSource source;
        [SerializeField, VariableKeyPicker] string variableKey;
        [SerializeField, VariableKeyPicker] string addVariableKey;
        [SerializeField] T literal;

        public ValueSource Source => source;
        public string VariableKey => variableKey;
        public string AddVariableKey => addVariableKey;
        public T Literal => literal;

        public bool TryResolve(IVarStore variables, out T value)
        {
            switch (source)
            {
                case ValueSource.VariableKey:
                    if (!string.IsNullOrWhiteSpace(variableKey) &&
                        variables != null &&
                        TryGetFromVars(variables, variableKey, out value))
                    {
                        return true;
                    }
                    value = default;
                    return false;

                case ValueSource.LiteralAddVariable:
                    value = literal;
                    if (!string.IsNullOrWhiteSpace(addVariableKey) && variables != null)
                    {
                        TrySetToVars(variables, addVariableKey, value);
                    }
                    return true;

                case ValueSource.Literal:
                default:
                    value = literal;
                    return true;
            }
        }

        static bool TryGetFromVars(IVarStore vars, string key, out T value)
        {
            value = default;
            if (vars == null || string.IsNullOrEmpty(key))
                return false;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return false;

            // Pure(DynamicVariant)
            if (vars.TryGetVariant(varId, out var variant) && variant.TryGet(out value))
                return true;

            // RuntimeRef(object)
            if (vars.TryGetManagedRef(varId, out var managedRef))
            {
                if (managedRef is T typed)
                {
                    value = typed;
                    return true;
                }
            }

            return false;
        }

        static void TrySetToVars(IVarStore vars, string key, T value)
        {
            if (vars == null || string.IsNullOrEmpty(key))
                return;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return;

            object boxed = value;
            if (boxed == null)
            {
                vars.TryUnset(varId);
                return;
            }

            // DynamicVariant が表現できる範囲は Variant に落として保存
            if (boxed is int i) { vars.TrySetVariant(varId, DynamicVariant.FromInt(i)); return; }
            if (boxed is float f) { vars.TrySetVariant(varId, DynamicVariant.FromFloat(f)); return; }
            if (boxed is bool b) { vars.TrySetVariant(varId, DynamicVariant.FromBool(b)); return; }
            if (boxed is string s) { vars.TrySetVariant(varId, DynamicVariant.FromString(s)); return; }
            if (boxed is Vector2 v2) { vars.TrySetVariant(varId, DynamicVariant.FromVector2(v2)); return; }
            if (boxed is Vector3 v3) { vars.TrySetVariant(varId, DynamicVariant.FromVector3(v3)); return; }
            if (boxed is Vector4 v4) { vars.TrySetVariant(varId, DynamicVariant.FromVector4(v4)); return; }
            if (boxed is Color c) { vars.TrySetVariant(varId, DynamicVariant.FromColor(c)); return; }
            if (boxed is UnityEngine.Object uo) { vars.TrySetVariant(varId, DynamicVariant.FromUnityObject(uo)); return; }

            // それ以外は ManagedRef として保持
            vars.TrySetManagedRef(varId, boxed);
        }
    }

    // 利用頻度の高い型エイリアス
    [Serializable] public struct FlexibleString : IFlexibleValue<string> { [SerializeField] FlexibleValue<string> value; public FlexibleValue<string> Inner => value; public bool TryResolve(IVarStore vars, out string v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleBool : IFlexibleValue<bool> { [SerializeField] FlexibleValue<bool> value; public FlexibleValue<bool> Inner => value; public bool TryResolve(IVarStore vars, out bool v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleFloat : IFlexibleValue<float> { [SerializeField] FlexibleValue<float> value; public FlexibleValue<float> Inner => value; public bool TryResolve(IVarStore vars, out float v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleInt : IFlexibleValue<int> { [SerializeField] FlexibleValue<int> value; public FlexibleValue<int> Inner => value; public bool TryResolve(IVarStore vars, out int v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleVector2 : IFlexibleValue<Vector2> { [SerializeField] FlexibleValue<Vector2> value; public FlexibleValue<Vector2> Inner => value; public bool TryResolve(IVarStore vars, out Vector2 v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleVector3 : IFlexibleValue<Vector3> { [SerializeField] FlexibleValue<Vector3> value; public FlexibleValue<Vector3> Inner => value; public bool TryResolve(IVarStore vars, out Vector3 v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleVector4 : IFlexibleValue<Vector4> { [SerializeField] FlexibleValue<Vector4> value; public FlexibleValue<Vector4> Inner => value; public bool TryResolve(IVarStore vars, out Vector4 v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleColor : IFlexibleValue<Color> { [SerializeField] FlexibleValue<Color> value; public FlexibleValue<Color> Inner => value; public bool TryResolve(IVarStore vars, out Color v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleQuaternion : IFlexibleValue<Quaternion> { [SerializeField] FlexibleValue<Quaternion> value; public FlexibleValue<Quaternion> Inner => value; public bool TryResolve(IVarStore vars, out Quaternion v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleTransform : IFlexibleValue<Transform> { [SerializeField] FlexibleValue<Transform> value; public FlexibleValue<Transform> Inner => value; public bool TryResolve(IVarStore vars, out Transform v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleAnimationCurve : IFlexibleValue<AnimationCurve> { [SerializeField] FlexibleValue<AnimationCurve> value; public FlexibleValue<AnimationCurve> Inner => value; public bool TryResolve(IVarStore vars, out AnimationCurve v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleTexture : IFlexibleValue<Texture> { [SerializeField] FlexibleValue<Texture> value; public FlexibleValue<Texture> Inner => value; public bool TryResolve(IVarStore vars, out Texture v) => value.TryResolve(vars, out v); }
    [Serializable] public struct FlexibleAnimationData : IFlexibleValue<AnimationData> { [SerializeField] FlexibleValue<AnimationData> value; public FlexibleValue<AnimationData> Inner => value; public bool TryResolve(IVarStore vars, out AnimationData v) => value.TryResolve(vars, out v); }

    public interface IFlexibleValue<T>
    {
        FlexibleValue<T> Inner { get; }

        public bool TryResolve(IVarStore variables, out T value)
        {
            return Inner.TryResolve(variables, out value);
        }
    }
}
