#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    /// <summary>
    /// CommandData から「直指定のUnityEngine.Object」または「VarStore内のUnityObject」を解決するための小さなヘルパ。
    /// - VarKey が指定されていて、VarStore から解決できた場合はそれを優先。
    /// - 解決できない場合は Asset をフォールバックとして使用。
    /// </summary>
    [Serializable]
    public class VarUnityObjectSource<T> where T : UnityEngine.Object
    {
        [LabelText("Use Var")]
        public bool PreferVar = true;

        [LabelText("Var Id")]
        [ShowIf(nameof(PreferVar))]
        [VarIdDropdown]
        public int VarId = 0;

        [LabelText("Asset")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        public T? Asset;

        public virtual bool TryResolve(IVarStore? vars, out T? resolved)
        {
            resolved = null;

            if (PreferVar && VarId != 0 && vars != null)
            {
                var varId = VarId;
                if (vars.TryGetVariant(varId, out var variant) &&
                    variant.TryGet<UnityEngine.Object>(out var obj) &&
                    obj is T typed)
                {
                    resolved = typed;
                    return true;
                }

                if (vars.TryGetManagedRef(varId, out var managed) && managed is T managedTyped)
                {
                    resolved = managedTyped;
                    return true;
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

    [Serializable]
    public sealed class VarUnityObjectSource : VarUnityObjectSource<UnityEngine.Object>
    {
        public bool TryResolve<T>(IVarStore? vars, out T? resolved) where T : UnityEngine.Object
        {
            if (TryResolve(vars, out var obj) && obj is T typed)
            {
                resolved = typed;
                return true;
            }

            resolved = null;
            return false;
        }
    }
}
