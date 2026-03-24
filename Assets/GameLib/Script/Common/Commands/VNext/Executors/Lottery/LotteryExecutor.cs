#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Scalar;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class LotteryExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Lottery;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not LotteryCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "LotteryCommandData is required.");

            var drawCount = Mathf.Max(0, typed.DrawCount.GetOrDefault(ctx, 0));
            if (drawCount <= 0)
                return;

            switch (typed.Mode)
            {
                case LotteryMode.ActorSource:
                    await ExecuteActorModeAsync(typed, drawCount, ctx, ct);
                    return;
                case LotteryMode.Scalar:
                    await ExecuteScalarModeAsync(typed, drawCount, ctx, ct);
                    return;
                case LotteryMode.Blackboard:
                    await ExecuteBlackboardModeAsync(typed, drawCount, ctx, ct);
                    return;
                case LotteryMode.VarStore:
                    await ExecuteVarStoreModeAsync(typed, drawCount, ctx, ct);
                    return;
                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unknown lottery mode: {typed.Mode}");
            }
        }

        async UniTask ExecuteActorModeAsync(LotteryCommandData typed, int drawCount, CommandContext ctx, CancellationToken ct)
        {
            var candidates = new List<ActorEntryCandidate>(typed.ActorEntries?.Count ?? 0);
            if (typed.ActorEntries != null)
            {
                for (var i = 0; i < typed.ActorEntries.Count; i++)
                {
                    var entry = typed.ActorEntries[i];
                    if (entry == null || !entry.Enabled || entry.Weight <= 0f || entry.Items == null || entry.Items.Count == 0)
                        continue;

                    var items = new List<ActorItemCandidate>(entry.Items.Count);
                    for (var n = 0; n < entry.Items.Count; n++)
                    {
                        var item = entry.Items[n];
                        if (item == null || !item.Enabled)
                            continue;

                        var (scope, _) = await ActorScopeResolver.ResolveAsync(item.Target, ctx, ct);
                        if (scope == null)
                            continue;

                        items.Add(new ActorItemCandidate(item, scope));
                    }

                    if (items.Count > 0)
                        candidates.Add(new ActorEntryCandidate(entry, items));
                }
            }

            if (candidates.Count == 0)
                return;

            var selectedCountByIndex = Draw(candidates, static c => c.Entry.Weight, drawCount, typed.WithReplacement, typed.ShortagePolicy);
            for (var i = 0; i < candidates.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var candidate = candidates[i];
                var selectedCount = selectedCountByIndex[i];
                if (selectedCount > 0)
                {
                    for (var n = 0; n < selectedCount; n++)
                    {
                        for (var j = 0; j < candidate.Items.Count; j++)
                        {
                            var item = candidate.Items[j];
                            await ExecuteCommandsOnScopeAsync(item.Item.OnSelectedCommands, item.Scope, ctx, ct);
                        }
                    }
                }
                else
                {
                    for (var j = 0; j < candidate.Items.Count; j++)
                    {
                        var item = candidate.Items[j];
                        await ExecuteCommandsOnScopeAsync(item.Item.OnUnselectedCommands, item.Scope, ctx, ct);
                    }
                }
            }
        }

        async UniTask ExecuteScalarModeAsync(LotteryCommandData typed, int drawCount, CommandContext ctx, CancellationToken ct)
        {
            var candidates = new List<ScalarEntryCandidate>(typed.ScalarEntries?.Count ?? 0);
            if (typed.ScalarEntries != null)
            {
                for (var i = 0; i < typed.ScalarEntries.Count; i++)
                {
                    var entry = typed.ScalarEntries[i];
                    if (entry == null || !entry.Enabled || entry.Weight <= 0f || entry.Items == null || entry.Items.Count == 0)
                        continue;

                    var items = new List<ScalarItemCandidate>(entry.Items.Count);
                    for (var n = 0; n < entry.Items.Count; n++)
                    {
                        var item = entry.Items[n];
                        if (item == null || !item.Enabled)
                            continue;

                        var (scope, _) = await ActorScopeResolver.ResolveAsync(item.Target, ctx, ct);
                        if (scope?.Resolver == null)
                            continue;

                        if (!scope.Resolver.TryResolve<IBaseScalarService>(out var scalar) || scalar == null)
                            continue;

                        items.Add(new ScalarItemCandidate(item, scope, scalar));
                    }

                    if (items.Count > 0)
                        candidates.Add(new ScalarEntryCandidate(entry, items));
                }
            }

            if (candidates.Count == 0)
                return;

            var selectedCountByIndex = Draw(candidates, static c => c.Entry.Weight, drawCount, typed.WithReplacement, typed.ShortagePolicy);
            for (var i = 0; i < candidates.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var candidate = candidates[i];
                var selectedCount = selectedCountByIndex[i];
                if (selectedCount > 0)
                {
                    for (var n = 0; n < selectedCount; n++)
                    {
                        for (var j = 0; j < candidate.Items.Count; j++)
                        {
                            var item = candidate.Items[j];
                            ApplyScalarOp(item, item.Item.SelectedOp, item.Item.SelectedValue, item.Item.SelectedLayer, item.Item.SelectedMulPhase, item.Item.SelectedDurationSeconds, item.Item.SelectedTag, ctx);
                        }
                    }
                }
                else
                {
                    for (var j = 0; j < candidate.Items.Count; j++)
                    {
                        var item = candidate.Items[j];
                        ApplyScalarOp(item, item.Item.UnselectedOp, item.Item.UnselectedValue, item.Item.UnselectedLayer, item.Item.UnselectedMulPhase, item.Item.UnselectedDurationSeconds, item.Item.UnselectedTag, ctx);
                    }
                }
            }
        }

        async UniTask ExecuteBlackboardModeAsync(LotteryCommandData typed, int drawCount, CommandContext ctx, CancellationToken ct)
        {
            var candidates = new List<BlackboardEntryCandidate>(typed.BlackboardEntries?.Count ?? 0);
            if (typed.BlackboardEntries != null)
            {
                for (var i = 0; i < typed.BlackboardEntries.Count; i++)
                {
                    var entry = typed.BlackboardEntries[i];
                    if (entry == null || !entry.Enabled || entry.Weight <= 0f || entry.Items == null || entry.Items.Count == 0)
                        continue;

                    var items = new List<BlackboardItemCandidate>(entry.Items.Count);
                    for (var n = 0; n < entry.Items.Count; n++)
                    {
                        var item = entry.Items[n];
                        if (item == null || !item.Enabled || item.Key.VarId <= 0)
                            continue;

                        var (scope, _) = await ActorScopeResolver.ResolveAsync(item.Target, ctx, ct);
                        if (scope?.Resolver == null)
                            continue;

                        if (!scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                            continue;

                        items.Add(new BlackboardItemCandidate(item, scope, blackboard));
                    }

                    if (items.Count > 0)
                        candidates.Add(new BlackboardEntryCandidate(entry, items));
                }
            }

            if (candidates.Count == 0)
                return;

            var selectedCountByIndex = Draw(candidates, static c => c.Entry.Weight, drawCount, typed.WithReplacement, typed.ShortagePolicy);
            for (var i = 0; i < candidates.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var candidate = candidates[i];
                var selectedCount = selectedCountByIndex[i];
                if (selectedCount > 0)
                {
                    for (var n = 0; n < selectedCount; n++)
                    {
                        for (var j = 0; j < candidate.Items.Count; j++)
                        {
                            var item = candidate.Items[j];
                            ApplyBlackboardOp(item, item.Item.SelectedOp, item.Item.SelectedValue, ctx);
                        }
                    }
                }
                else
                {
                    for (var j = 0; j < candidate.Items.Count; j++)
                    {
                        var item = candidate.Items[j];
                        ApplyBlackboardOp(item, item.Item.UnselectedOp, item.Item.UnselectedValue, ctx);
                    }
                }
            }
        }

        async UniTask ExecuteVarStoreModeAsync(LotteryCommandData typed, int drawCount, CommandContext ctx, CancellationToken ct)
        {
            var candidates = new List<VarStoreEntryCandidate>(typed.VarStoreEntries?.Count ?? 0);
            if (typed.VarStoreEntries != null)
            {
                for (var i = 0; i < typed.VarStoreEntries.Count; i++)
                {
                    var entry = typed.VarStoreEntries[i];
                    if (entry == null || !entry.Enabled || entry.Weight <= 0f || entry.Items == null || entry.Items.Count == 0)
                        continue;

                    var items = new List<VarStoreItemCandidate>(entry.Items.Count);
                    for (var n = 0; n < entry.Items.Count; n++)
                    {
                        var item = entry.Items[n];
                        if (item == null || !item.Enabled || item.Key.VarId <= 0)
                            continue;

                        var (scope, _) = await ActorScopeResolver.ResolveAsync(item.Target, ctx, ct);
                        if (scope?.Resolver == null)
                            continue;

                        if (!scope.Resolver.TryResolve<IVarStore>(out var vars) || vars == null)
                            continue;

                        items.Add(new VarStoreItemCandidate(item, vars));
                    }

                    if (items.Count > 0)
                        candidates.Add(new VarStoreEntryCandidate(entry, items));
                }
            }

            if (candidates.Count == 0)
                return;

            var selectedCountByIndex = Draw(candidates, static c => c.Entry.Weight, drawCount, typed.WithReplacement, typed.ShortagePolicy);
            for (var i = 0; i < candidates.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var candidate = candidates[i];
                var selectedCount = selectedCountByIndex[i];
                if (selectedCount > 0)
                {
                    for (var n = 0; n < selectedCount; n++)
                    {
                        for (var j = 0; j < candidate.Items.Count; j++)
                        {
                            var item = candidate.Items[j];
                            ApplyVarStoreOp(item, item.Item.SelectedOp, item.Item.SelectedValue, ctx);
                        }
                    }
                }
                else
                {
                    for (var j = 0; j < candidate.Items.Count; j++)
                    {
                        var item = candidate.Items[j];
                        ApplyVarStoreOp(item, item.Item.UnselectedOp, item.Item.UnselectedValue, ctx);
                    }
                }
            }
        }

        static int[] Draw<T>(List<T> candidates, Func<T, float> getWeight, int drawCount, bool withReplacement, LotteryShortagePolicy shortagePolicy)
        {
            var selectedCountByIndex = new int[candidates.Count];
            if (drawCount <= 0 || candidates.Count == 0)
                return selectedCountByIndex;

            if (withReplacement)
            {
                for (var i = 0; i < drawCount; i++)
                {
                    if (!TryPickIndex(candidates, getWeight, out var picked) || picked < 0)
                        break;
                    selectedCountByIndex[picked]++;
                }
                return selectedCountByIndex;
            }

            var available = new List<int>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
                available.Add(i);

            var maxUnique = Mathf.Min(drawCount, available.Count);
            for (var i = 0; i < maxUnique; i++)
            {
                if (!TryPickIndex(available, idx => getWeight(candidates[idx]), out var pickedAvailable) || pickedAvailable < 0)
                    break;

                var pickedIndex = available[pickedAvailable];
                selectedCountByIndex[pickedIndex]++;
                available.RemoveAt(pickedAvailable);
            }

            if (shortagePolicy == LotteryShortagePolicy.ContinueWithReplacement)
            {
                var selectedTotal = 0;
                for (var i = 0; i < selectedCountByIndex.Length; i++)
                    selectedTotal += selectedCountByIndex[i];

                var rest = drawCount - selectedTotal;
                for (var i = 0; i < rest; i++)
                {
                    if (!TryPickIndex(candidates, getWeight, out var picked) || picked < 0)
                        break;
                    selectedCountByIndex[picked]++;
                }
            }

            return selectedCountByIndex;
        }

        static bool TryPickIndex<T>(IReadOnlyList<T> entries, Func<T, float> getWeight, out int index)
        {
            index = -1;
            if (entries == null || entries.Count == 0)
                return false;

            var total = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                var w = getWeight(entries[i]);
                if (w > 0f)
                    total += w;
            }

            if (total <= 0f)
                return false;

            var r = UnityEngine.Random.value * total;
            var acc = 0f;
            var lastPositive = -1;
            for (var i = 0; i < entries.Count; i++)
            {
                var w = getWeight(entries[i]);
                if (w <= 0f)
                    continue;

                lastPositive = i;
                acc += w;
                if (r <= acc)
                {
                    index = i;
                    return true;
                }
            }

            if (lastPositive < 0)
                return false;

            index = lastPositive;
            return true;
        }

        static async UniTask ExecuteCommandsOnScopeAsync(CommandListData? commands, IScopeNode targetScope, CommandContext sourceCtx, CancellationToken ct)
        {
            if (commands == null || commands.Count == 0)
                return;

            var resolver = targetScope.Resolver;
            var runner = sourceCtx.Runner;
            if (resolver != null && resolver.TryResolve<ICommandRunner>(out var resolvedRunner) && resolvedRunner != null)
                runner = resolvedRunner;

            if (runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"ICommandRunner is missing in scope: {DescribeScope(targetScope)}");

            IVarStore vars = sourceCtx.Vars;
            if (resolver != null && resolver.TryResolve<IVarStore>(out var resolvedVars) && resolvedVars != null)
                vars = resolvedVars;

            var actorCtx = new CommandContext(
                targetScope,
                vars,
                runner,
                targetScope,
                sourceCtx.Options,
                commandRootScope: sourceCtx.CommandRootScope,
                rootActor: sourceCtx.RootActor,
                callerActor: sourceCtx.Actor,
                sourceContext: sourceCtx);

            var result = await runner.ExecuteListAsync(commands, actorCtx, ct, sourceCtx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }

        static void ApplyScalarOp(
            ScalarItemCandidate candidate,
            LotteryScalarOpKind op,
            DynamicValue<float> value,
            string layer,
            ScalarMulPhase mulPhase,
            DynamicValue<float> duration,
            string tag,
            CommandContext ctx)
        {
            if (op == LotteryScalarOpKind.None)
                return;

            var scalar = candidate.Scalar;
            var key = candidate.Item.Key;
            var source = (object)(candidate.Scope ?? ctx.Actor ?? ctx.Scope);

            switch (op)
            {
                case LotteryScalarOpKind.SetLocalBase:
                    scalar.SetLocalBase(key, value.GetOrDefault(ctx, 0f));
                    return;
                case LotteryScalarOpKind.SetGlobalBase:
                    scalar.SetGlobalBase(key, value.GetOrDefault(ctx, 0f));
                    return;
                case LotteryScalarOpKind.LocalAdd:
                    scalar.LocalAdd(key, layer ?? string.Empty, value.GetOrDefault(ctx, 0f), Mathf.Max(-1f, duration.GetOrDefault(ctx, -1f)), source, tag);
                    return;
                case LotteryScalarOpKind.GlobalAdd:
                    scalar.GlobalAdd(key, layer ?? string.Empty, value.GetOrDefault(ctx, 0f), Mathf.Max(-1f, duration.GetOrDefault(ctx, -1f)), source, tag);
                    return;
                case LotteryScalarOpKind.LocalMul:
                    scalar.LocalMul(key, layer ?? string.Empty, value.GetOrDefault(ctx, 1f), mulPhase, Mathf.Max(-1f, duration.GetOrDefault(ctx, -1f)), source, tag);
                    return;
                case LotteryScalarOpKind.GlobalMul:
                    scalar.GlobalMul(key, layer ?? string.Empty, value.GetOrDefault(ctx, 1f), mulPhase, Mathf.Max(-1f, duration.GetOrDefault(ctx, -1f)), source, tag);
                    return;
                case LotteryScalarOpKind.ClearKey:
                    scalar.ClearAll(key);
                    return;
                case LotteryScalarOpKind.ClearAll:
                    scalar.ClearAll(null);
                    return;
                default:
                    return;
            }
        }

        static void ApplyBlackboardOp(BlackboardItemCandidate candidate, LotteryVarOpKind op, DynamicValue value, CommandContext ctx)
        {
            if (op == LotteryVarOpKind.None)
                return;

            var varId = candidate.Item.Key.VarId;
            if (varId <= 0)
                return;

            switch (op)
            {
                case LotteryVarOpKind.Unset:
                    UnsetBlackboard(candidate, varId);
                    return;
                case LotteryVarOpKind.Set:
                    {
                        var v = value.Evaluate(ctx);
                        SetBlackboard(candidate, varId, v);
                        return;
                    }
                case LotteryVarOpKind.Add:
                    {
                        var add = value.GetOrDefault<float>(ctx, 0f);
                        var cur = GetBlackboardNumeric(candidate, varId, 0f);
                        SetBlackboard(candidate, varId, DynamicVariant.FromFloat(cur + add));
                        return;
                    }
                case LotteryVarOpKind.Mul:
                    {
                        var mul = value.GetOrDefault<float>(ctx, 1f);
                        var cur = GetBlackboardNumeric(candidate, varId, 1f);
                        SetBlackboard(candidate, varId, DynamicVariant.FromFloat(cur * mul));
                        return;
                    }
                default:
                    return;
            }
        }

        static void ApplyVarStoreOp(VarStoreItemCandidate candidate, LotteryVarOpKind op, DynamicValue value, CommandContext ctx)
        {
            if (op == LotteryVarOpKind.None)
                return;

            var varId = candidate.Item.Key.VarId;
            if (varId <= 0)
                return;

            switch (op)
            {
                case LotteryVarOpKind.Unset:
                    candidate.Vars.TryUnset(varId);
                    return;
                case LotteryVarOpKind.Set:
                    {
                        var v = value.Evaluate(ctx);
                        candidate.Vars.TrySetVariant(varId, v);
                        return;
                    }
                case LotteryVarOpKind.Add:
                    {
                        var add = value.GetOrDefault<float>(ctx, 0f);
                        var cur = GetVarStoreNumeric(candidate.Vars, varId, 0f);
                        candidate.Vars.TrySetVariant(varId, DynamicVariant.FromFloat(cur + add));
                        return;
                    }
                case LotteryVarOpKind.Mul:
                    {
                        var mul = value.GetOrDefault<float>(ctx, 1f);
                        var cur = GetVarStoreNumeric(candidate.Vars, varId, 1f);
                        candidate.Vars.TrySetVariant(varId, DynamicVariant.FromFloat(cur * mul));
                        return;
                    }
                default:
                    return;
            }
        }

        static float GetVarStoreNumeric(IVarStore vars, int varId, float defaultValue)
        {
            if (!vars.TryGetVariant(varId, out var current))
                return defaultValue;

            if (current.TryGet<float>(out var asFloat))
                return asFloat;
            if (current.TryGet<int>(out var asInt))
                return asInt;
            return defaultValue;
        }

        static float GetBlackboardNumeric(BlackboardItemCandidate candidate, int varId, float defaultValue)
        {
            if (candidate.Item.WriteScope == LotteryBlackboardWriteScope.Global)
            {
                if (!candidate.Blackboard.TryGlobalGetVariant(varId, out var current))
                    return defaultValue;
                if (current.TryGet<float>(out var asFloat))
                    return asFloat;
                if (current.TryGet<int>(out var asInt))
                    return asInt;
                return defaultValue;
            }

            if (!candidate.Blackboard.TryLocalGetVariant(varId, out var local))
                return defaultValue;
            if (local.TryGet<float>(out var localFloat))
                return localFloat;
            if (local.TryGet<int>(out var localInt))
                return localInt;
            return defaultValue;
        }

        static void SetBlackboard(BlackboardItemCandidate candidate, int varId, DynamicVariant value)
        {
            if (candidate.Item.WriteScope == LotteryBlackboardWriteScope.Global)
            {
                candidate.Blackboard.TryGlobalSetVariant(varId, value);
                return;
            }

            candidate.Blackboard.TryLocalSetVariant(varId, value);
        }

        static void UnsetBlackboard(BlackboardItemCandidate candidate, int varId)
        {
            if (candidate.Item.WriteScope == LotteryBlackboardWriteScope.Global)
            {
                var globalScope = candidate.Blackboard.FindGlobalVariantScope(varId);
                if (globalScope?.Resolver == null)
                    return;
                if (globalScope.Resolver.TryResolve<IBlackboardService>(out var globalBlackboard) && globalBlackboard != null)
                    globalBlackboard.LocalVars.TryUnset(varId);

                return;
            }

            candidate.Blackboard.LocalVars.TryUnset(varId);
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "null";

            var id = scope.Identity?.Id ?? "(no id)";
            return $"{scope.Kind}:{id}";
        }

        sealed class ActorEntryCandidate
        {
            public LotteryActorEntry Entry { get; }
            public List<ActorItemCandidate> Items { get; }

            public ActorEntryCandidate(LotteryActorEntry entry, List<ActorItemCandidate> items)
            {
                Entry = entry;
                Items = items;
            }
        }

        readonly struct ActorItemCandidate
        {
            public readonly LotteryActorItem Item;
            public readonly IScopeNode Scope;

            public ActorItemCandidate(LotteryActorItem item, IScopeNode scope)
            {
                Item = item;
                Scope = scope;
            }
        }

        sealed class ScalarEntryCandidate
        {
            public LotteryScalarEntry Entry { get; }
            public List<ScalarItemCandidate> Items { get; }

            public ScalarEntryCandidate(LotteryScalarEntry entry, List<ScalarItemCandidate> items)
            {
                Entry = entry;
                Items = items;
            }
        }

        readonly struct ScalarItemCandidate
        {
            public readonly LotteryScalarItem Item;
            public readonly IScopeNode Scope;
            public readonly IBaseScalarService Scalar;

            public ScalarItemCandidate(LotteryScalarItem item, IScopeNode scope, IBaseScalarService scalar)
            {
                Item = item;
                Scope = scope;
                Scalar = scalar;
            }
        }

        sealed class BlackboardEntryCandidate
        {
            public LotteryBlackboardEntry Entry { get; }
            public List<BlackboardItemCandidate> Items { get; }

            public BlackboardEntryCandidate(LotteryBlackboardEntry entry, List<BlackboardItemCandidate> items)
            {
                Entry = entry;
                Items = items;
            }
        }

        readonly struct BlackboardItemCandidate
        {
            public readonly LotteryBlackboardItem Item;
            public readonly IScopeNode Scope;
            public readonly IBlackboardService Blackboard;

            public BlackboardItemCandidate(LotteryBlackboardItem item, IScopeNode scope, IBlackboardService blackboard)
            {
                Item = item;
                Scope = scope;
                Blackboard = blackboard;
            }
        }

        sealed class VarStoreEntryCandidate
        {
            public LotteryVarStoreEntry Entry { get; }
            public List<VarStoreItemCandidate> Items { get; }

            public VarStoreEntryCandidate(LotteryVarStoreEntry entry, List<VarStoreItemCandidate> items)
            {
                Entry = entry;
                Items = items;
            }
        }

        readonly struct VarStoreItemCandidate
        {
            public readonly LotteryVarStoreItem Item;
            public readonly IVarStore Vars;

            public VarStoreItemCandidate(LotteryVarStoreItem item, IVarStore vars)
            {
                Item = item;
                Vars = vars;
            }
        }
    }
}