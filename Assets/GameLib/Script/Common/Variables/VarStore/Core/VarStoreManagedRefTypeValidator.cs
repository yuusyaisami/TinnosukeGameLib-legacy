#nullable enable

using System;
using Game.StatusEffect;
using Game.Vars.Generated;

namespace Game.Common
{
    /// <summary>
    /// ManagedRef を受け入れる varId に対して、期待される具体型を返す。
    /// まずは StatusEffect 系の誤配置を確実に弾くための最小セットを持つ。
    /// </summary>
    internal static class VarStoreManagedRefTypeValidator
    {
        public static bool TryGetExpectedType(int varId, out Type expectedType)
        {
            switch (varId)
            {
                case VarIds.GameLogic.NailProfile.UpgradePanel.StackPreset:
                case VarIds.GameLogic.NailProfile.UpgradePanel.StatusEffectStackTableFolder.StackPreset:
                case VarIds.GameLib.Base.StatusEffect.Stack.preset:
                    expectedType = typeof(StatusEffectStackPreset);
                    return true;

                case VarIds.GameLogic.NailProfile.UpgradePanel.StatusEffect:
                case VarIds.GameLogic.NailProfile.UpgradePanel.StatusEffectStackTableFolder.StatusEffect:
                case VarIds.GameLib.Base.StatusEffect.Definition.Element.definitionAsset:
                    expectedType = typeof(BaseStatusEffectDefinitionData);
                    return true;

                default:
                    expectedType = null!;
                    return false;
            }
        }
    }
}