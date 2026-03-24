#nullable enable

using System;
using System.Collections.Generic;
using Game.StatusEffect;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    [Serializable]
    public sealed class StatusEffectStackDescriptionSource : IDynamicSource
    {
        [LabelText("Definition")]
        [Tooltip("StackDescription を取得する StatusEffect 定義です。")]
        public DynamicValue<BaseStatusEffectDefinitionData> Definition;

        [LabelText("Stack Preset")]
        [Tooltip("説明文計算時に Stack var としてマージする preset です。")]
        public DynamicValue<StatusEffectStackPreset> StackPreset;

        [NonSerialized]
        readonly RichTextSource _richText = new();

        [NonSerialized]
        readonly List<GridBlackboardCellSnapshot> _gridCells = new(32);

        public string SourceTypeName => "StatusEffectStackDescription";

        public string GetDebugData
            => $"Def={Definition.SourceTypeName},Stack={StackPreset.SourceTypeName}";

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (context == null || !Definition.HasSource)
                return DynamicVariant.FromString(string.Empty);

            var definition = Definition.GetOrDefault(context, default!);
            var preset = StackPreset.HasSource
                ? StackPreset.GetOrDefault(context, default!)
                : StatusEffectStackPreset.CreateDurationRefreshPreset();

            if (definition == null || preset == null)
                TryResolveFromKnownVars(context.Vars, ref definition, ref preset);

            if (definition == null || preset == null)
                TryResolveFromGridFallback(context, ref definition, ref preset);

            if (definition == null)
                return DynamicVariant.FromString(string.Empty);

            var templateData = definition.VisualData?.StackDescription;
            var template = templateData?.Template ?? string.Empty;
            if (string.IsNullOrEmpty(template))
                return DynamicVariant.FromString(string.Empty);

            if (preset == null)
                preset = StatusEffectStackPreset.CreateDurationRefreshPreset();

            var mergedVars = new VarStore();
            (context.Vars ?? NullVarStore.Instance).MergeInto(mergedVars, overwrite: true);
            WriteStackPresetVars(mergedVars, preset, context);

            _richText.AllowImplicitKeys = true;
            _richText.Template = template;
            if (templateData?.Variables != null && templateData.Variables.Count > 0)
                _richText.SetExternalVariables(templateData.Variables, includeLocalVariables: false);
            else
                _richText.ClearExternalVariables();

            var evalContext = new SimpleDynamicContext(mergedVars, context.Scope);
            var result = _richText.Evaluate(evalContext);
            return DynamicVariant.FromString(result.AsString ?? string.Empty);
        }

        static void TryResolveFromKnownVars(
            IVarStore? vars,
            ref BaseStatusEffectDefinitionData? definition,
            ref StatusEffectStackPreset? preset)
        {
            if (vars == null)
                return;

            if (definition == null)
            {
                TryResolveManagedRefByKey(vars, "StatusEffect", out definition);
                if (definition == null)
                    TryResolveManagedRefByKey(vars, "GameLogic.NailProfile.UpgradePanel.StatusEffect", out definition);
            }

            if (preset == null)
            {
                if (!TryResolveManagedRefByVarId(vars, VarIds.GameLib.Base.StatusEffect.Stack.preset, out preset) || preset == null)
                {
                    TryResolveManagedRefByKey(vars, "StackPreset", out preset);
                }
            }
        }

        void TryResolveFromGridFallback(
            IDynamicContext context,
            ref BaseStatusEffectDefinitionData? definition,
            ref StatusEffectStackPreset? preset)
        {
            if (context?.Scope == null)
                return;

            var definitionVarId = ResolveSourceVarId(Definition.SourceDebugData);
            var stackPresetVarId = ResolveSourceVarId(StackPreset.SourceDebugData);
            if (definitionVarId <= 0 && stackPresetVarId <= 0)
                return;

            for (var node = context.Scope; node != null; node = node.Parent)
            {
                var resolver = node.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<IGridBlackboardService>(out var grid) || grid == null)
                    continue;

                _gridCells.Clear();
                if (!grid.TryCollectAllCells(_gridCells) || _gridCells.Count == 0)
                    continue;

                if (TryResolveFromSameCell(_gridCells, definitionVarId, stackPresetVarId, ref definition, ref preset))
                    return;

                TryResolveIndividually(_gridCells, definitionVarId, stackPresetVarId, ref definition, ref preset);
                if (definition != null && preset != null)
                    return;
            }
        }

        static bool TryResolveFromSameCell(
            List<GridBlackboardCellSnapshot> cells,
            int definitionVarId,
            int stackPresetVarId,
            ref BaseStatusEffectDefinitionData? definition,
            ref StatusEffectStackPreset? preset)
        {
            if (definitionVarId <= 0 || stackPresetVarId <= 0)
                return false;

            for (int i = 0; i < cells.Count; i++)
            {
                var defCell = cells[i];
                if (defCell.VarId != definitionVarId)
                    continue;

                if (!TryGetManagedRef(defCell.Value, out BaseStatusEffectDefinitionData? resolvedDefinition) || resolvedDefinition == null)
                    continue;

                for (int j = 0; j < cells.Count; j++)
                {
                    var stackCell = cells[j];
                    if (stackCell.VarId != stackPresetVarId)
                        continue;

                    if (stackCell.Row != defCell.Row || stackCell.Column != defCell.Column)
                        continue;

                    if (!TryGetManagedRef(stackCell.Value, out StatusEffectStackPreset? resolvedPreset) || resolvedPreset == null)
                        continue;

                    definition = resolvedDefinition;
                    preset = resolvedPreset;
                    return true;
                }
            }

            return false;
        }

        static void TryResolveIndividually(
            List<GridBlackboardCellSnapshot> cells,
            int definitionVarId,
            int stackPresetVarId,
            ref BaseStatusEffectDefinitionData? definition,
            ref StatusEffectStackPreset? preset)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];

                if (definition == null && cell.VarId == definitionVarId)
                    TryGetManagedRef(cell.Value, out definition);

                if (preset == null && cell.VarId == stackPresetVarId)
                    TryGetManagedRef(cell.Value, out preset);

                if (definition != null && preset != null)
                    return;
            }
        }

        static bool TryGetManagedRef<T>(DynamicVariant value, out T? resolved) where T : class
        {
            resolved = null;
            if (value.Kind != ValueKind.ManagedRef)
                return false;

            resolved = value.AsManagedRef as T;
            return resolved != null;
        }

        static bool TryResolveManagedRefByKey<T>(IVarStore vars, string key, out T? resolved) where T : class
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (!VarIdResolver.TryResolve(key, out var varId) || varId == 0)
                return false;

            return TryResolveManagedRefByVarId(vars, varId, out resolved);
        }

        static bool TryResolveManagedRefByVarId<T>(IVarStore vars, int varId, out T? resolved) where T : class
        {
            resolved = null;
            if (varId == 0)
                return false;

            if (vars.GetVarKind(varId) != ValueKind.ManagedRef)
                return false;

            if (!vars.TryGetManagedRef(varId, out var managed) || managed == null)
                return false;

            resolved = managed as T;
            return resolved != null;
        }

        static int ResolveSourceVarId(string? sourceDebugData)
        {
            if (string.IsNullOrWhiteSpace(sourceDebugData))
                return 0;

            var key = sourceDebugData.Trim();
            var separatorIndex = key.IndexOf(" (varId=", StringComparison.Ordinal);
            if (separatorIndex > 0)
                key = key.Substring(0, separatorIndex).Trim();

            if (VarIdResolver.TryResolve(key, out var resolvedByKey) && resolvedByKey > 0)
                return resolvedByKey;

            const string marker = "varId=";
            var markerIndex = sourceDebugData.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                return 0;

            markerIndex += marker.Length;
            var end = markerIndex;
            while (end < sourceDebugData.Length && char.IsDigit(sourceDebugData[end]))
                end++;

            if (end <= markerIndex)
                return 0;

            return int.TryParse(sourceDebugData.Substring(markerIndex, end - markerIndex), out var parsed)
                ? parsed
                : 0;
        }

        static void WriteStackPresetVars(IVarStore vars, StatusEffectStackPreset preset, IDynamicContext context)
        {
            vars.TrySetManagedRef(VarIds.GameLib.Base.StatusEffect.Stack.preset, preset);
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Stack.ignoreIfExisting, DynamicVariant.FromBool(preset.IgnoreIfExisting));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Stack.applyIntensity, DynamicVariant.FromBool(preset.ApplyIntensity));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Stack.applyDuration, DynamicVariant.FromBool(preset.ApplyDuration));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Stack.applyCurrentCount, DynamicVariant.FromBool(preset.ApplyCurrentCount));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Stack.applyMaxCount, DynamicVariant.FromBool(preset.ApplyMaxCount));

            WriteStackRule(vars, context, preset.Intensity,
                VarIds.GameLib.Base.StatusEffect.Stack.Intensity.operation,
                VarIds.GameLib.Base.StatusEffect.Stack.Intensity.local,
                VarIds.GameLib.Base.StatusEffect.Stack.Intensity.useGlobal,
                VarIds.GameLib.Base.StatusEffect.Stack.Intensity.global,
                VarIds.GameLib.Base.StatusEffect.Stack.Intensity.ignoreGlobalWhenMinusOne);

            WriteStackRule(vars, context, preset.Duration,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.operation,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.local,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.useGlobal,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.global,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.ignoreGlobalWhenMinusOne);

            WriteStackRule(vars, context, preset.CurrentCount,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.operation,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.local,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.useGlobal,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.global,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.ignoreGlobalWhenMinusOne);

            WriteStackRule(vars, context, preset.MaxCount,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.operation,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.local,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.useGlobal,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.global,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.ignoreGlobalWhenMinusOne);
        }

        static void WriteStackRule(
            IVarStore vars,
            IDynamicContext context,
            StatusEffectStackRule? rule,
            int operationVarId,
            int localVarId,
            int useGlobalVarId,
            int globalVarId,
            int ignoreGlobalWhenMinusOneVarId)
        {
            var operation = rule?.Operation ?? StatusEffectStackOperation.Add;
            var local = rule?.LocalValue.GetOrDefault(context, 0f) ?? 0f;
            var useGlobal = rule?.UseGlobalValue ?? false;
            var global = rule?.GlobalValue.GetOrDefault(context, 0f) ?? 0f;
            var ignoreGlobalWhenMinusOne = rule?.IgnoreGlobalWhenMinusOne ?? false;

            vars.TrySetVariant(operationVarId, DynamicVariant.FromInt((int)operation));
            vars.TrySetVariant(localVarId, DynamicVariant.FromFloat(local));
            vars.TrySetVariant(useGlobalVarId, DynamicVariant.FromBool(useGlobal));
            vars.TrySetVariant(globalVarId, DynamicVariant.FromFloat(global));
            vars.TrySetVariant(ignoreGlobalWhenMinusOneVarId, DynamicVariant.FromBool(ignoreGlobalWhenMinusOne));
        }
    }
}
