#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Collision;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class HitColliderRuleControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.HitColliderRuleControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not HitColliderRuleControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "HitColliderRuleControlCommandData is required.");

            var (resolvedScope, _) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            var scope = resolvedScope ?? ctx.Scope;
            if (scope == null)
                return;

            var root = scope.Identity?.SelfTransform;
            if (root == null)
                return;

            var controller = root.GetComponentInChildren<HitColliderControllerMB>(includeInactive: true);
            if (controller == null)
                return;

            var targets = CollectTargetRules(controller, typed, ctx);
            if (targets.Count == 0)
                return;

            var resolver = scope.Resolver;
            HitColliderControllerService? controllerService = null;
            ICommandListRuntimeMutationService? mutationService = null;
            if (resolver != null)
            {
                resolver.TryResolve(out controllerService);
                resolver.TryResolve(out mutationService);
            }

            var needsRebind = false;
            for (int i = 0; i < targets.Count; i++)
            {
                var rule = targets[i];
                if (rule == null)
                    continue;

                if (ApplyRuleSettings(rule, typed, ctx))
                    needsRebind = true;

                ApplyCommandListOperation(rule, typed, mutationService, controllerService);
            }

            if (needsRebind)
                controllerService?.RebindRules();
        }

        static List<HitColliderControllerRule> CollectTargetRules(
            HitColliderControllerMB controller,
            HitColliderRuleControlCommandData typed,
            CommandContext ctx)
        {
            var rules = controller.Rules;
            var result = new List<HitColliderControllerRule>(rules?.Count ?? 0);
            if (rules == null || rules.Count == 0)
                return result;

            switch (typed.SelectMode)
            {
                case HitColliderRuleSelectMode.All:
                    for (int i = 0; i < rules.Count; i++)
                    {
                        var rule = rules[i];
                        if (rule != null)
                            result.Add(rule);
                    }
                    return result;

                case HitColliderRuleSelectMode.ByIndex:
                {
                    var index = typed.RuleIndex.GetOrDefault(ctx, 0);
                    if (index >= 0 && index < rules.Count && rules[index] != null)
                        result.Add(rules[index]);
                    return result;
                }

                default:
                {
                    var targetName = typed.RuleName ?? string.Empty;
                    if (string.IsNullOrEmpty(targetName))
                        return result;

                    for (int i = 0; i < rules.Count; i++)
                    {
                        var rule = rules[i];
                        if (rule == null)
                            continue;
                        if (string.Equals(rule.Name, targetName, System.StringComparison.Ordinal))
                            result.Add(rule);
                    }
                    return result;
                }
            }
        }

        static bool ApplyRuleSettings(HitColliderControllerRule rule, HitColliderRuleControlCommandData typed, CommandContext ctx)
        {
            var needsRebind = false;

            if (typed.ApplyEnabled)
            {
                rule.SetEnabledRuntime(typed.Enabled.GetOrDefault(ctx, true));
                needsRebind = true;
            }

            if (typed.ApplyWatchFlags)
            {
                rule.SetWatchFlagsRuntime(typed.WatchFlags);
                needsRebind = true;
            }

            if (typed.ApplyEventMask)
            {
                rule.SetEventMaskRuntime(typed.EventMask);
                needsRebind = true;
            }

            if (typed.ApplyMatchAnyInclude)
            {
                rule.SetMatchAnyIncludeRuntime(typed.MatchAnyInclude.GetOrDefault(ctx, true));
                needsRebind = true;
            }

            if (typed.ApplyUseFilter)
            {
                rule.SetUseFilterRuntime(typed.UseFilter.GetOrDefault(ctx, false));
                needsRebind = true;
            }

            if (typed.ApplyFilterValue)
            {
                rule.SetFilterRuntime(typed.Filter);
                needsRebind = true;
            }

            if (typed.ApplyUseStaleFrameThreshold)
            {
                rule.SetUseStaleFrameThresholdRuntime(typed.UseStaleFrameThreshold.GetOrDefault(ctx, false));
                needsRebind = true;
            }

            if (typed.ApplyStaleFrameThreshold)
            {
                rule.SetStaleFrameThresholdRuntime(typed.StaleFrameThreshold.GetOrDefault(ctx, 2));
                needsRebind = true;
            }

            if (typed.ApplyIncludeStaticKinds)
            {
                rule.SetIncludeStaticKindsRuntime(typed.UseIncludeStaticKinds, typed.IncludeStaticKinds);
                needsRebind = true;
            }

            if (typed.ApplyIncludeDynamicSets)
            {
                rule.SetIncludeDynamicSetsRuntime(typed.UseIncludeDynamicSets, typed.IncludeDynamicSets);
                needsRebind = true;
            }

            if (typed.ApplyExcludeStaticKinds)
            {
                rule.SetExcludeStaticKindsRuntime(typed.UseExcludeStaticKinds, typed.ExcludeStaticKinds);
                needsRebind = true;
            }

            if (typed.ApplyExcludeDynamicSets)
            {
                rule.SetExcludeDynamicSetsRuntime(typed.UseExcludeDynamicSets, typed.ExcludeDynamicSets);
                needsRebind = true;
            }

            if (typed.ApplyCommandTarget)
                rule.SetCommandTargetRuntime(typed.CommandTarget);

            if (typed.ApplyParallelWhenBoth)
                rule.SetParallelWhenBothRuntime(typed.ParallelWhenBoth.GetOrDefault(ctx, true));

            if (typed.ApplyStayInterval)
                rule.SetStayIntervalSecondsRuntime(typed.StayIntervalSeconds.GetOrDefault(ctx, 0f));

            return needsRebind;
        }

        static void ApplyCommandListOperation(
            HitColliderControllerRule rule,
            HitColliderRuleControlCommandData typed,
            ICommandListRuntimeMutationService? mutationService,
            HitColliderControllerService? controllerService)
        {
            if (typed.CommandListOperation == HitColliderRuleCommandListOperation.None)
                return;

            var list = rule.ResolveCommandList(typed.CommandListSlot);
            if (list == null)
                return;

            switch (typed.CommandListOperation)
            {
                case HitColliderRuleCommandListOperation.Append:
                    if (typed.Commands != null && typed.Commands.Count > 0)
                    {
                        list.AddRuntimeCommands(typed.Commands);
                        mutationService?.Register(list);
                        controllerService?.RegisterRuntimeMutatedCommandList(list);
                    }
                    break;

                case HitColliderRuleCommandListOperation.Override:
                    list.SetRuntimeOverride(typed.Commands);
                    mutationService?.Register(list);
                    controllerService?.RegisterRuntimeMutatedCommandList(list);
                    break;

                case HitColliderRuleCommandListOperation.ClearOverride:
                    list.ClearRuntimeOverride();
                    mutationService?.Register(list);
                    controllerService?.RegisterRuntimeMutatedCommandList(list);
                    break;

                case HitColliderRuleCommandListOperation.ClearAppended:
                    list.ClearRuntimeAppendedCommands();
                    mutationService?.Register(list);
                    controllerService?.RegisterRuntimeMutatedCommandList(list);
                    break;

                case HitColliderRuleCommandListOperation.ClearRuntimeMutations:
                    list.ClearRuntimeMutations();
                    mutationService?.Register(list);
                    controllerService?.RegisterRuntimeMutatedCommandList(list);
                    break;
            }
        }
    }
}
