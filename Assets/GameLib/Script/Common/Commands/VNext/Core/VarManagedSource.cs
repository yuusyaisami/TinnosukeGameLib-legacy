#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    /// <summary>
    /// Non-Unity managed type source: resolve from VarStore managed ref or fallback to an inline serialized payload.
    /// Useful for serializable classes (not UnityEngine.Object) such as presets, payloads, etc.
    /// </summary>
    [Serializable]
    [Obsolete("Use DynamicValue<T> to resolve from VarStore/Blackboard.")]
    public class VarManagedSource<T> where T : class
    {
        [LabelText("Use Var")]
        public bool PreferVar = true;

        [LabelText("Var Id")]
        [ShowIf(nameof(PreferVar))]
        [VarIdDropdown]
        public int VarId = 0;

        [LabelText("Asset")]
        [InlineProperty]
        [HideLabel]
        public T? Asset;

        public virtual bool TryResolve(IVarStore? vars, out T? resolved)
        {
            resolved = null;

            if (PreferVar && VarId != 0 && vars != null)
            {
                var varId = VarId;
                if (vars.TryGetManagedRef(varId, out var managed) && managed is T typed)
                {
                    resolved = typed;
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"VarManagedSource: Failed to resolve managed ref varId={varId} as {typeof(T).Name}");
                }
            }

            if (Asset != null)
            {
                resolved = Asset;
                return true;
            }

            return false;
        }
    }
}
