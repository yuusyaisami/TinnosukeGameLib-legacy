#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Health;
using Game.StatusEffect;
using Game.Vars.Generated;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WriteStatusEffectDataExecutor : ICommandExecutor
    {
        readonly List<EffectState> _states = new();

        public int CommandId => CommandIds.WriteStatusEffectData;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;

            if (data is not WriteStatusEffectDataCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteStatusEffectDataCommandData is required.");

            var targetScope = ResolveTargetScope(ctx, typed);
            var blackboard = ResolveBlackboard(targetScope);

            switch (typed.SourceMode)
            {
                case WriteStatusEffectDataSourceMode.Definition:
                    WriteDefinitionData(ctx, typed, targetScope, blackboard);
                    break;

                case WriteStatusEffectDataSourceMode.Runtime:
                    WriteRuntimeData(ctx, typed, targetScope, blackboard);
                    break;

                case WriteStatusEffectDataSourceMode.StackPreset:
                    WriteStackPresetData(ctx, typed, targetScope, blackboard);
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void WriteDefinitionData(CommandContext ctx, WriteStatusEffectDataCommandData typed, IScopeNode? targetScope, IBlackboardService? blackboard)
        {
            var definition = typed.DefinitionSource.GetOrDefault(ctx, default!);
            if (definition == null)
                return;

            WriteDefinitionCore(ctx.Vars, typed.Target, targetScope, blackboard, definition, typed.Overwrite);
        }

        void WriteRuntimeData(CommandContext ctx, WriteStatusEffectDataCommandData typed, IScopeNode? targetScope, IBlackboardService? blackboard)
        {
            var serviceScope = ResolveServiceScope(ctx, typed);
            if (serviceScope?.Resolver == null)
                return;

            if (!serviceScope.Resolver.TryResolve<IStatusEffectService>(out var service) || service == null)
                return;

            _states.Clear();
            service.GetStates(_states, typed.Filter);
            if (_states.Count == 0)
                return;

            // Runtime source means the effect is already registered.
            // In that case, write definition first (if resolvable), then write runtime snapshot.
            if (service.TryGetRegisteredDefinition(typed.Filter, out var definition) && definition != null)
                WriteDefinitionCore(ctx.Vars, typed.Target, targetScope, blackboard, definition, typed.Overwrite);

            WriteRuntimeCore(ctx.Vars, typed.Target, targetScope, blackboard, _states[0], typed.Overwrite);
        }

        static void WriteStackPresetData(CommandContext ctx, WriteStatusEffectDataCommandData typed, IScopeNode? targetScope, IBlackboardService? blackboard)
        {
            var preset = typed.StackPresetSource.GetOrDefault(ctx, default!);
            if (preset == null)
                return;

            WriteManagedRef(
                typed.Target,
                ctx.Vars,
                targetScope,
                blackboard,
                VarIds.GameLib.Base.StatusEffect.Stack.preset,
                preset,
                typed.Overwrite);

            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.ignoreIfExisting, DynamicVariant.FromBool(preset.IgnoreIfExisting), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyIntensityA, DynamicVariant.FromBool(preset.ApplyIntensityA), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyIntensityB, DynamicVariant.FromBool(preset.ApplyIntensityB), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyIntensityC, DynamicVariant.FromBool(preset.ApplyIntensityC), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyIntensityD, DynamicVariant.FromBool(preset.ApplyIntensityD), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyIntensityE, DynamicVariant.FromBool(preset.ApplyIntensityE), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyIntensityF, DynamicVariant.FromBool(preset.ApplyIntensityF), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyIntensityG, DynamicVariant.FromBool(preset.ApplyIntensityG), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyDuration, DynamicVariant.FromBool(preset.ApplyDuration), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyCurrentCount, DynamicVariant.FromBool(preset.ApplyCurrentCount), typed.Overwrite);
            WriteVariant(typed.Target, ctx.Vars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Stack.applyMaxCount, DynamicVariant.FromBool(preset.ApplyMaxCount), typed.Overwrite);

            for (int i = 0; i < StatusEffectIntensitySlotUtility.OrderedSlots.Length; i++)
            {
                var slot = StatusEffectIntensitySlotUtility.OrderedSlots[i];
                WriteStackRule(
                    typed.Target,
                    ctx.Vars,
                    targetScope,
                    blackboard,
                    ctx,
                    preset.GetIntensityRule(slot),
                    StatusEffectIntensitySlotUtility.GetStackOperationVarId(slot),
                    StatusEffectIntensitySlotUtility.GetStackLocalVarId(slot),
                    StatusEffectIntensitySlotUtility.GetStackUseGlobalVarId(slot),
                    StatusEffectIntensitySlotUtility.GetStackGlobalVarId(slot),
                    StatusEffectIntensitySlotUtility.GetStackIgnoreGlobalWhenMinusOneVarId(slot),
                    typed.Overwrite);
            }

            WriteStackRule(
                typed.Target,
                ctx.Vars,
                targetScope,
                blackboard,
                ctx,
                preset.Duration,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.operation,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.local,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.useGlobal,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.global,
                VarIds.GameLib.Base.StatusEffect.Stack.Duration.ignoreGlobalWhenMinusOne,
                typed.Overwrite);

            WriteStackRule(
                typed.Target,
                ctx.Vars,
                targetScope,
                blackboard,
                ctx,
                preset.CurrentCount,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.operation,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.local,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.useGlobal,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.global,
                VarIds.GameLib.Base.StatusEffect.Stack.CurrentCount.ignoreGlobalWhenMinusOne,
                typed.Overwrite);

            WriteStackRule(
                typed.Target,
                ctx.Vars,
                targetScope,
                blackboard,
                ctx,
                preset.MaxCount,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.operation,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.local,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.useGlobal,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.global,
                VarIds.GameLib.Base.StatusEffect.Stack.MaxCount.ignoreGlobalWhenMinusOne,
                typed.Overwrite);
        }

        static void WriteStackRule(
            VarStoreTarget target,
            IVarStore? commandVars,
            IScopeNode? targetScope,
            IBlackboardService? blackboard,
            IDynamicContext context,
            StatusEffectStackRule? rule,
            int operationVarId,
            int localVarId,
            int useGlobalVarId,
            int globalVarId,
            int ignoreGlobalWhenMinusOneVarId,
            bool overwrite)
        {
            var operation = rule?.Operation ?? StatusEffectStackOperation.Add;
            var local = rule?.LocalValue.GetOrDefault(context, 0f) ?? 0f;
            var useGlobal = rule?.ApplyGlobalValue ?? false;
            var global = rule?.GlobalValue.GetOrDefault(context, 0f) ?? 0f;
            var ignoreGlobalWhenMinusOne = rule?.IgnoreGlobalWhenMinusOne ?? false;

            WriteVariant(target, commandVars, targetScope, blackboard, operationVarId, DynamicVariant.FromInt((int)operation), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, localVarId, DynamicVariant.FromFloat(local), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, useGlobalVarId, DynamicVariant.FromBool(useGlobal), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, globalVarId, DynamicVariant.FromFloat(global), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, ignoreGlobalWhenMinusOneVarId, DynamicVariant.FromBool(ignoreGlobalWhenMinusOne), overwrite);
        }

        static IScopeNode? ResolveTargetScope(CommandContext ctx, WriteStatusEffectDataCommandData typed)
        {
            return ActorSourceFastResolver.Resolve(ctx, typed.TargetActorSource, ctx.Actor ?? ctx.Scope) ?? ctx.Actor ?? ctx.Scope;
        }

        static IScopeNode? ResolveServiceScope(CommandContext ctx, WriteStatusEffectDataCommandData typed)
        {
            return ActorSourceFastResolver.Resolve(ctx, typed.ServiceActorSource, ctx.Actor ?? ctx.Scope) ?? ctx.Actor ?? ctx.Scope;
        }

        static IBlackboardService? ResolveBlackboard(IScopeNode? scope)
        {
            if (scope?.Resolver == null)
                return null;

            if (scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                return blackboard;

            return null;
        }

        static void WriteDefinitionCore(IVarStore? commandVars, VarStoreTarget target, IScopeNode? targetScope, IBlackboardService? blackboard, BaseStatusEffectDefinitionData definition, bool overwrite)
        {
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.definitionId, DynamicVariant.FromString(definition.DefinitionId ?? string.Empty), overwrite);
            WriteManagedRef(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.definitionAsset, definition, overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.defaultRuntimeTag, DynamicVariant.FromString(definition.DefaultRuntimeTag ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.useDuration, DynamicVariant.FromBool(definition.UseDuration), overwrite);
            WriteManagedRef(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.durationDefinition, definition.DurationDefinition, overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.useUseCooldown, DynamicVariant.FromBool(definition.UseUseCooldown), overwrite);
            WriteManagedRef(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.useCooldownDefinition, definition.UseCooldownDefinition, overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.useCount, DynamicVariant.FromBool(definition.UseCount), overwrite);
            WriteManagedRef(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.countDefinition, definition.CountDefinition, overwrite);
            WriteManagedRef(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.operations, definition.Operations, overwrite);
            WriteManagedRef(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.defaultHooks, definition.DefaultHooks, overwrite);

            var visualData = definition.VisualData;
            WriteManagedRef(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.visualData, visualData, overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.nameTemplate, DynamicVariant.FromString(visualData?.DisplayNameText ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.descriptionTemplate, DynamicVariant.FromString(visualData?.DescriptionText ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.nameKey, DynamicVariant.FromString(string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.descriptionKey, DynamicVariant.FromString(string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Definition.Element.effectType, DynamicVariant.FromInt((int)(visualData?.EffectType ?? EffectType.Neutral)), overwrite);
        }

        static void WriteRuntimeCore(IVarStore? commandVars, VarStoreTarget target, IScopeNode? targetScope, IBlackboardService? blackboard, EffectState state, bool overwrite)
        {
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectId, DynamicVariant.FromString(state.EffectId ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.instanceId, DynamicVariant.FromString(state.InstanceId ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.runtimeTag, DynamicVariant.FromString(state.RuntimeTag ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectType, DynamicVariant.FromInt((int)state.Type), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.isEnabled, DynamicVariant.FromBool(state.IsEnabled), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.isApplied, DynamicVariant.FromBool(state.IsApplied), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.isActive, DynamicVariant.FromBool(state.IsActive), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.isUseBlocked, DynamicVariant.FromBool(state.IsUseBlocked), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.stackCount, DynamicVariant.FromInt(state.StackCount), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityA, DynamicVariant.FromFloat(state.IntensityA), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityB, DynamicVariant.FromFloat(state.IntensityB), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityC, DynamicVariant.FromFloat(state.IntensityC), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityD, DynamicVariant.FromFloat(state.IntensityD), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityE, DynamicVariant.FromFloat(state.IntensityE), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityF, DynamicVariant.FromFloat(state.IntensityF), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityG, DynamicVariant.FromFloat(state.IntensityG), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.usedCount, DynamicVariant.FromInt(state.UsedCount), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingDuration, DynamicVariant.FromFloat(state.RemainingTime), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.totalDuration, DynamicVariant.FromFloat(state.TotalDuration), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingInverseInterval, DynamicVariant.FromFloat(state.RemainingUseCooldown), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingUseCount, DynamicVariant.FromInt(state.RemainingUseCount), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.maxUseCount, DynamicVariant.FromInt(state.MaxUseCount), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.nameTemplate, DynamicVariant.FromString(state.DisplayName ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.descriptionTemplate, DynamicVariant.FromString(state.DescriptionKey ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.nameKey, DynamicVariant.FromString(state.NameKey ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.descriptionKey, DynamicVariant.FromString(state.DescriptionKey ?? string.Empty), overwrite);
            WriteVariant(target, commandVars, targetScope, blackboard, VarIds.GameLib.Base.StatusEffect.Runtime.Element.visualData, DynamicVariant.FromUnityObject(state.Icon), overwrite);
        }

        static void WriteVariant(VarStoreTarget target, IVarStore? commandVars, IScopeNode? targetScope, IBlackboardService? blackboard, int varId, DynamicVariant value, bool overwrite)
        {
            if (value.Kind == ValueKind.Null)
            {
                WriteUnset(target, targetScope, blackboard, commandVars, varId, overwrite);
                return;
            }

            WriteSet(target, targetScope, blackboard, commandVars, varId, value, overwrite);
        }

        static void WriteManagedRef(VarStoreTarget target, IVarStore? commandVars, IScopeNode? targetScope, IBlackboardService? blackboard, int varId, object? value, bool overwrite)
        {
            if (value == null)
            {
                WriteUnset(target, targetScope, blackboard, commandVars, varId, overwrite);
                return;
            }

            WriteSetManagedRef(target, targetScope, blackboard, commandVars, varId, value, overwrite);
        }

        static void WriteSet(VarStoreTarget target, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore? commandVars, int varId, DynamicVariant value, bool overwrite)
        {
            if (varId == 0)
                return;

            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    if (commandVars == null || (!overwrite && commandVars.Contains(varId)))
                        return;
                    commandVars.TrySetVariant(varId, in value);
                    return;

                case VarStoreTarget.BlackboardLocal:
                    if (blackboard?.LocalVars == null || (!overwrite && blackboard.LocalVars.Contains(varId)))
                        return;
                    blackboard.LocalVars.TrySetVariant(varId, in value);
                    return;

                case VarStoreTarget.BlackboardGlobal:
                    WriteGlobalVariant(targetScope, varId, value, overwrite);
                    return;
            }
        }

        static void WriteSetManagedRef(VarStoreTarget target, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore? commandVars, int varId, object value, bool overwrite)
        {
            if (varId == 0)
                return;

            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    if (commandVars == null || (!overwrite && commandVars.Contains(varId)))
                        return;
                    commandVars.TrySetManagedRef(varId, value);
                    return;

                case VarStoreTarget.BlackboardLocal:
                    if (blackboard?.LocalVars == null || (!overwrite && blackboard.LocalVars.Contains(varId)))
                        return;
                    blackboard.LocalVars.TrySetManagedRef(varId, value);
                    return;

                case VarStoreTarget.BlackboardGlobal:
                    WriteGlobalManagedRef(targetScope, varId, value, overwrite);
                    return;
            }
        }

        static void WriteUnset(VarStoreTarget target, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore? commandVars, int varId, bool overwrite)
        {
            if (varId == 0)
                return;

            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    if (commandVars == null || (!overwrite && !commandVars.Contains(varId)))
                        return;
                    commandVars.TryUnset(varId);
                    return;

                case VarStoreTarget.BlackboardLocal:
                    if (blackboard?.LocalVars == null || (!overwrite && !blackboard.LocalVars.Contains(varId)))
                        return;
                    blackboard.LocalVars.TryUnset(varId);
                    return;

                case VarStoreTarget.BlackboardGlobal:
                    WriteGlobalUnset(targetScope, varId, overwrite);
                    return;
            }
        }

        static void WriteGlobalVariant(IScopeNode? origin, int varId, DynamicVariant value, bool overwrite)
        {
            if (origin == null)
                return;

            IBlackboardService? fallback = null;
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                var blackboard = ResolveBlackboard(node);
                if (blackboard == null)
                    continue;

                fallback ??= blackboard;
                var local = blackboard.LocalVars;
                if (local != null && local.Contains(varId))
                {
                    if (!overwrite && local.Contains(varId))
                        return;

                    local.TrySetVariant(varId, in value);
                    return;
                }
            }

            if (fallback?.LocalVars == null)
                return;

            if (!overwrite && fallback.LocalVars.Contains(varId))
                return;

            fallback.LocalVars.TrySetVariant(varId, in value);
        }

        static void WriteGlobalManagedRef(IScopeNode? origin, int varId, object value, bool overwrite)
        {
            if (origin == null)
                return;

            IBlackboardService? fallback = null;
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                var blackboard = ResolveBlackboard(node);
                if (blackboard == null)
                    continue;

                fallback ??= blackboard;
                var local = blackboard.LocalVars;
                if (local != null && local.Contains(varId))
                {
                    if (!overwrite && local.Contains(varId))
                        return;

                    local.TrySetManagedRef(varId, value);
                    return;
                }
            }

            if (fallback?.LocalVars == null)
                return;

            if (!overwrite && fallback.LocalVars.Contains(varId))
                return;

            fallback.LocalVars.TrySetManagedRef(varId, value);
        }

        static void WriteGlobalUnset(IScopeNode? origin, int varId, bool overwrite)
        {
            if (origin == null)
                return;

            IBlackboardService? fallback = null;
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                var blackboard = ResolveBlackboard(node);
                if (blackboard == null)
                    continue;

                fallback ??= blackboard;
                var local = blackboard.LocalVars;
                if (local != null && local.Contains(varId))
                {
                    if (!overwrite && !local.Contains(varId))
                        return;

                    local.TryUnset(varId);
                    return;
                }
            }

            if (fallback?.LocalVars == null)
                return;

            if (!overwrite && !fallback.LocalVars.Contains(varId))
                return;

            fallback.LocalVars.TryUnset(varId);
        }
    }
}
