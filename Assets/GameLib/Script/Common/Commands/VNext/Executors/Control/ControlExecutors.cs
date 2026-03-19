#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Input;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    static class ControlExecutorBackground
    {
        public static void Run(
            ICommandRunner runner,
            CommandListData commands,
            CommandListData onCanceled,
            CommandContext ctx,
            CancellationTokenSource cts)
        {
            RunAsync(runner, commands, onCanceled, ctx, cts).Forget();
        }

        static async UniTaskVoid RunAsync(
            ICommandRunner runner,
            CommandListData commands,
            CommandListData onCanceled,
            CommandContext ctx,
            CancellationTokenSource cts)
        {
            try
            {
                var ct = cts.Token;
                var result = onCanceled != null && onCanceled.Count > 0
                    ? await runner.ExecuteWithCancelAsync(commands, onCanceled, ctx, ct, ctx.Options)
                    : await runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);

                if (result.Status == CommandRunStatus.Error)
                {
                    Debug.LogError($"[ControlExecutorBackground] Background command error: {result.FailureKind} {result.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                Debug.LogError("[ControlExecutorBackground] Background command threw an exception.");
                Debug.LogException(ex);
            }
            finally
            {
                try { cts.Dispose(); } catch { }
            }
        }
    }

    public sealed class ActionBlockExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ActionBlock;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ActionBlockCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ActionBlockCommandData is required.");

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var resolver = ctx.Scope?.Resolver;
            if (resolver == null)
                return;
            if (!resolver.TryResolve<IActionBlockService>(out var blockService) || blockService == null)
                return;

            if (typed.FireAndForget)
            {
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                RunBlockFireAndForgetAsync(blockService, typed, runner, ctx, linkedCts).Forget();
                return;
            }

            await RunBlockAsync(blockService, typed, runner, ctx, ct);
        }

        static async UniTask RunBlockAsync(IActionBlockService blockService, ActionBlockCommandData typed, ICommandRunner runner, CommandContext ctx, CancellationToken ct)
        {
            using var blockGuard = AcquireBlockGuard(blockService, typed);
            await ExecuteBodyCommandsAsync(runner, typed, ctx, ct);
        }

        static async UniTask RunBlockFireAndForgetAsync(IActionBlockService blockService, ActionBlockCommandData typed, ICommandRunner runner, CommandContext ctx, CancellationTokenSource linkedCts)
        {
            try
            {
                await RunBlockAsync(blockService, typed, runner, ctx, linkedCts.Token);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                // Cancellation was requested, swallow.
            }
            catch (Exception ex)
            {
                Debug.LogError("[ActionBlockExecutor] Background command threw an exception.");
                Debug.LogException(ex);
            }
            finally
            {
                try { linkedCts.Dispose(); } catch { }
            }
        }

        static IDisposable AcquireBlockGuard(IActionBlockService blockService, ActionBlockCommandData typed)
        {
            return typed.Mode switch
            {
                ActionBlockMode.Tag => new TagBlockGuard(blockService, typed.Kinds, typed.Tag, typed.TagShouldBlock, typed.TagPersistent),
                _ => blockService.Block(typed.Kinds, string.IsNullOrEmpty(typed.Reason) ? null : typed.Reason),
            };
        }

        static async UniTask ExecuteBodyCommandsAsync(ICommandRunner runner, ActionBlockCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            if (typed.BodyCommands == null || typed.BodyCommands.Count == 0)
                return;

            var result = typed.OnCanceledCommands != null && typed.OnCanceledCommands.Count > 0
                ? await runner.ExecuteWithCancelAsync(typed.BodyCommands, typed.OnCanceledCommands, ctx, ct, ctx.Options)
                : await runner.ExecuteListAsync(typed.BodyCommands, ctx, ct, ctx.Options);

            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }

        sealed class TagBlockGuard : IDisposable
        {
            const string DefaultTag = "actionblock:tag";

            readonly IActionBlockService _service;
            readonly string _kinds;
            readonly string _tagKey;
            readonly bool _shouldBlock;
            readonly bool _persistent;
            bool _disposed;

            public TagBlockGuard(IActionBlockService service, string kinds, string tag, bool shouldBlock, bool persistent)
            {
                _service = service;
                _kinds = kinds;
                _tagKey = string.IsNullOrEmpty(tag) ? DefaultTag : tag;
                _shouldBlock = shouldBlock;
                _persistent = persistent;
                _service.SetBlockFlag(_kinds, _shouldBlock, _tagKey);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                if (!_persistent)
                {
                    _service.SetBlockFlag(_kinds, !_shouldBlock, _tagKey);
                }
            }
        }
    }

    public sealed class ForgetExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Forget;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ForgetCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ForgetCommandData is required.");

            if (typed.Commands == null || typed.Commands.Count == 0)
                return UniTask.CompletedTask;

            var runner = ctx.Runner;
            if (runner == null)
                return UniTask.CompletedTask;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            ControlExecutorBackground.Run(runner, typed.Commands, typed.OnCanceledCommands, ctx, cts);
            return UniTask.CompletedTask;
        }
    }

    public sealed class DelayExecutorExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.DelayExecutor;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not DelayExecutorCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "DelayExecutorCommandData is required.");

            var runner = ctx.Runner;
            if (runner == null)
                return;

            if (typed.FirstCommands != null && typed.FirstCommands.Count > 0)
            {
                var firstCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                ControlExecutorBackground.Run(runner, typed.FirstCommands, typed.OnCanceledCommands, ctx, firstCts);
            }

            var seconds = typed.DelaySeconds.GetOrDefault(ctx, 0f);
            if (seconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);

            if (typed.SecondCommands == null || typed.SecondCommands.Count == 0)
                return;

            var result = typed.OnCanceledCommands != null && typed.OnCanceledCommands.Count > 0
                ? await runner.ExecuteWithCancelAsync(typed.SecondCommands, typed.OnCanceledCommands, ctx, ct, ctx.Options)
                : await runner.ExecuteListAsync(typed.SecondCommands, ctx, ct, ctx.Options);

            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }
    }

    public sealed class WaitExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Wait;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WaitCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WaitCommandData is required.");

            var seconds = typed.WaitTime.GetOrDefault(ctx, 0f);
            if (seconds <= 0f)
                return;

            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
        }
    }

    public sealed class AdvanceWaitExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.AdvanceWait;

        static readonly System.Collections.Generic.List<AdvanceWaitEventEntry> EmptyEvents = new();

        sealed class TriggerResult
        {
            public readonly AdvanceWaitEventEntry Entry;
            public readonly IVarStore Payload;

            public TriggerResult(AdvanceWaitEventEntry entry, IVarStore payload)
            {
                Entry = entry;
                Payload = payload;
            }
        }

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not AdvanceWaitCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AdvanceWaitCommandData is required.");

            var hasCondition = typed.Condition.HasSource;
            var events = typed.Events ?? EmptyEvents;
            var hasEventEntries = events.Count > 0;

            if (!hasCondition && !hasEventEntries)
                return;

            if (hasCondition && typed.Condition.EvaluateBool(ctx))
                return;

            UniTaskCompletionSource<TriggerResult>? triggerTcs = null;
            var subscriptions = hasEventEntries ? new System.Collections.Generic.List<IDisposable>(events.Count) : null;
            var cancelReg = default(CancellationTokenRegistration);

            try
            {
                if (hasEventEntries)
                {
                    triggerTcs = new UniTaskCompletionSource<TriggerResult>();
                    cancelReg = ct.Register(() => triggerTcs.TrySetCanceled(ct));

                    for (int i = 0; i < events.Count; i++)
                    {
                        var entry = events[i];
                        if (entry == null)
                            continue;
                        if (string.IsNullOrWhiteSpace(entry.EventKey))
                            throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AdvanceWait event key is required.");

                        var (scope, error) = await ActorScopeResolver.ResolveAsync(entry.EventScope, ctx, ct);
                        if (scope == null || scope.Resolver == null)
                            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "AdvanceWait event scope could not be resolved.");

                        if (!TryResolveEventService(scope, out var eventService) || eventService == null)
                            throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IEventService is missing.");

                        IDisposable sub = eventService.Subscribe(entry.EventKey, (payload, handlerCt) =>
                        {
                            if (handlerCt.IsCancellationRequested)
                                return UniTask.CompletedTask;

                            var store = payload ?? NullVarStore.Instance;
                            triggerTcs.TrySetResult(new TriggerResult(entry, store));
                            return UniTask.CompletedTask;
                        });
                        subscriptions?.Add(sub);
                    }
                }

                var hasEventSubscriptions = subscriptions != null && subscriptions.Count > 0;
                if (!hasCondition && !hasEventSubscriptions)
                    return;

                if (hasCondition && hasEventSubscriptions)
                {
                    if (triggerTcs == null)
                        return;

                    var conditionTask = UniTask.WaitUntil(() => typed.Condition.EvaluateBool(ctx), cancellationToken: ct);
                    var (eventTriggered, trigger) = await UniTask.WhenAny(triggerTcs.Task, conditionTask);
                    if (!eventTriggered)
                        return;

                    await ExecuteTriggeredCommandsAsync(ctx, trigger, ct);
                    return;
                }

                if (hasCondition)
                {
                    await UniTask.WaitUntil(() => typed.Condition.EvaluateBool(ctx), cancellationToken: ct);
                    return;
                }

                if (triggerTcs == null)
                    return;

                var result = await triggerTcs.Task;
                await ExecuteTriggeredCommandsAsync(ctx, result, ct);
            }
            finally
            {
                cancelReg.Dispose();
                if (subscriptions != null)
                {
                    for (int i = 0; i < subscriptions.Count; i++)
                    {
                        try { subscriptions[i]?.Dispose(); } catch { }
                    }
                }
            }
        }

        static async UniTask ExecuteTriggeredCommandsAsync(CommandContext ctx, TriggerResult trigger, CancellationToken ct)
        {
            if (trigger?.Entry == null)
                return;

            var commands = trigger.Entry.Commands;
            if (commands == null || commands.Count == 0)
                return;

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var mergedVars = new VarStore();
            (ctx.Vars ?? NullVarStore.Instance).MergeInto(mergedVars, overwrite: true);
            trigger.Payload?.MergeInto(mergedVars, overwrite: true);

            var runCtx = new CommandContext(ctx.Scope, mergedVars, runner, ctx.Actor, ctx.Options, ctx.CommandRootScope, ctx.RootActor, ctx.CallerActor, ctx);
            var result = await runner.ExecuteListAsync(commands, runCtx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }

        static bool TryResolveEventService(IScopeNode scope, out IEventService? eventService)
        {
            eventService = null;
            if (scope == null || scope.Resolver == null)
                return false;

            if (scope.Resolver.TryResolve<IEntityEventService>(out var entityEvent) && entityEvent != null)
            {
                eventService = entityEvent;
                return true;
            }

            if (scope.Resolver.TryResolve<IEventService>(out var resolved) && resolved != null)
            {
                eventService = resolved;
                return true;
            }

            return false;
        }
    }

    public sealed class IfExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.If;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not IfCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "IfCommandData is required.");

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var branch = typed.Condition.GetOrDefault(ctx, false)
                ? typed.ThenCommands
                : typed.ElseCommands;

            if (branch == null || branch.Count == 0)
                return;

            var result = typed.OnCanceledCommands != null && typed.OnCanceledCommands.Count > 0
                ? await runner.ExecuteWithCancelAsync(branch, typed.OnCanceledCommands, ctx, ct, ctx.Options)
                : await runner.ExecuteListAsync(branch, ctx, ct, ctx.Options);

            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }
    }

    public sealed class SwitchExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Switch;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SwitchCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SwitchCommandData is required.");

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var value = typed.SwitchValue.Evaluate(ctx);
            CommandListData? matched = null;

            if (typed.Cases != null)
            {
                for (int i = 0; i < typed.Cases.Count; i++)
                {
                    var entry = typed.Cases[i];
                    if (entry == null)
                        continue;

                    var caseValue = entry.CaseValue.Evaluate(ctx);
                    if (value.Equals(caseValue))
                    {
                        matched = entry.Commands;
                        break;
                    }
                }
            }

            matched ??= typed.DefaultCommands;
            if (matched == null || matched.Count == 0)
                return;

            var result = typed.OnCanceledCommands != null && typed.OnCanceledCommands.Count > 0
                ? await runner.ExecuteWithCancelAsync(matched, typed.OnCanceledCommands, ctx, ct, ctx.Options)
                : await runner.ExecuteListAsync(matched, ctx, ct, ctx.Options);

            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }
    }

    public sealed class ForExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.For;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ForCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ForCommandData is required.");

            if (typed.BodyCommands == null || typed.BodyCommands.Count == 0)
                return;

            if (!typed.WaitForCompletion)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                RunLoopFireAndForgetAsync(typed, ctx, cts).Forget();
                return;
            }

            await ExecuteLoopCoreAsync(typed, ctx, ct, throwOnCanceled: true);
        }

        async UniTaskVoid RunLoopFireAndForgetAsync(ForCommandData typed, CommandContext ctx, CancellationTokenSource linkedCts)
        {
            try
            {
                await ExecuteLoopCoreAsync(typed, ctx, linkedCts.Token, throwOnCanceled: false);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ForExecutor] Background loop threw an exception.");
                Debug.LogException(ex);
            }
            finally
            {
                try { linkedCts.Dispose(); } catch { }
            }
        }

        async UniTask ExecuteLoopCoreAsync(ForCommandData typed, CommandContext ctx, CancellationToken ct, bool throwOnCanceled)
        {
            try
            {
                switch (typed.Mode)
                {
                    case ForLoopMode.Count:
                        await ExecuteCountLoop(typed, ctx, ct);
                        break;
                    case ForLoopMode.While:
                        await ExecuteWhileLoop(typed, ctx, ct);
                        break;
                    case ForLoopMode.Until:
                        await ExecuteUntilLoop(typed, ctx, ct);
                        break;
                    case ForLoopMode.Random:
                        await ExecuteRandomLoop(typed, ctx, ct);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                await ExecuteOnCanceled(typed, ctx);
                if (throwOnCanceled)
                    throw;
            }
        }

        async UniTask ExecuteCountLoop(ForCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            var runner = ctx.Runner;
            if (runner == null)
                return;

            var maxIterations = Mathf.Max(0, typed.MaxIterations);
            var count = Mathf.Max(0, typed.Count.GetOrDefault(ctx, 1));
            count = Mathf.Min(count, maxIterations);

            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (ShouldBreak(typed, ctx))
                    break;

                SetCounter(typed, ctx, i);
                await RunBody(typed, runner, ctx, ct);
            }
        }

        async UniTask ExecuteWhileLoop(ForCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            var runner = ctx.Runner;
            if (runner == null)
                return;

            var maxIterations = Mathf.Max(0, typed.MaxIterations);
            var iterations = 0;
            while (typed.Condition.GetOrDefault(ctx, false) && iterations < maxIterations)
            {
                ct.ThrowIfCancellationRequested();
                if (ShouldBreak(typed, ctx))
                    break;

                SetCounter(typed, ctx, iterations);
                await RunBody(typed, runner, ctx, ct);
                iterations++;
            }
        }

        async UniTask ExecuteUntilLoop(ForCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            var runner = ctx.Runner;
            if (runner == null)
                return;

            var maxIterations = Mathf.Max(0, typed.MaxIterations);
            var iterations = 0;
            while (iterations < maxIterations)
            {
                ct.ThrowIfCancellationRequested();
                if (ShouldBreak(typed, ctx))
                    break;

                SetCounter(typed, ctx, iterations);
                await RunBody(typed, runner, ctx, ct);
                iterations++;

                if (typed.Condition.GetOrDefault(ctx, false))
                    break;
            }
        }

        async UniTask ExecuteRandomLoop(ForCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            var runner = ctx.Runner;
            if (runner == null)
                return;

            var maxIterations = Mathf.Max(0, typed.MaxIterations);
            var count = Mathf.Max(0, typed.Count.GetOrDefault(ctx, 1));
            count = Mathf.Min(count, maxIterations);

            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (ShouldBreak(typed, ctx))
                    break;

                SetCounter(typed, ctx, i);
                var breakRequested = await WaitRandomExecutionTimingAsync(typed, ctx, ct);
                if (breakRequested)
                    break;

                ct.ThrowIfCancellationRequested();
                if (ShouldBreak(typed, ctx))
                    break;

                await RunBody(typed, runner, ctx, ct);
            }
        }

        static async UniTask<bool> WaitRandomExecutionTimingAsync(ForCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            var minimum = Mathf.Max(0f, typed.RandomMinNoExecuteSeconds.GetOrDefault(ctx, 0f));
            var jitter = Mathf.Max(0f, typed.RandomJitterSeconds.GetOrDefault(ctx, 0f));
            var threshold = minimum + (jitter > 0f ? UnityEngine.Random.Range(0f, jitter) : 0f);
            if (threshold <= 0f)
                return ShouldBreak(typed, ctx);

            var timer = 0f;
            while (timer < threshold)
            {
                ct.ThrowIfCancellationRequested();
                if (ShouldBreak(typed, ctx))
                    return true;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                timer += Mathf.Max(0f, Time.deltaTime);
            }

            return ShouldBreak(typed, ctx);
        }

        async UniTask RunBody(ForCommandData typed, ICommandRunner runner, CommandContext ctx, CancellationToken ct)
        {
            var result = await runner.ExecuteListAsync(typed.BodyCommands, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }

        static void SetCounter(ForCommandData typed, CommandContext ctx, int value)
        {
            var varId = typed.CounterVar.VarId;
            if (varId <= 0)
                return;

            ctx.Vars?.TrySetVariant(varId, DynamicVariant.FromInt(value));
        }

        static bool ShouldBreak(ForCommandData typed, CommandContext ctx)
        {
            var breakVarId = typed.BreakSwitchVar.VarId;
            if (breakVarId <= 0)
                return false;

            var vars = ctx.Vars;
            if (vars == null || !vars.TryGetVariant(breakVarId, out var value))
                return false;

            if (value.TryGet<bool>(out var boolValue))
                return boolValue;
            if (value.TryGet<int>(out var intValue))
                return intValue != 0;
            if (value.TryGet<float>(out var floatValue))
                return !Mathf.Approximately(floatValue, 0f);

            return false;
        }

        static async UniTask ExecuteOnCanceled(ForCommandData typed, CommandContext ctx)
        {
            var runner = ctx.Runner;
            if (runner == null || typed.OnCanceledCommands == null || typed.OnCanceledCommands.Count == 0)
                return;

            _ = await runner.ExecuteListAsync(typed.OnCanceledCommands, ctx, CancellationToken.None, ctx.Options);
        }
    }

    public sealed class SequenceExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Sequence;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SequenceCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SequenceCommandData is required.");

            if (typed.BodyCommands == null || typed.BodyCommands.Count == 0)
                return;

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var result = typed.OnCanceledCommands != null && typed.OnCanceledCommands.Count > 0
                ? await runner.ExecuteWithCancelAsync(typed.BodyCommands, typed.OnCanceledCommands, ctx, ct, ctx.Options)
                : await runner.ExecuteListAsync(typed.BodyCommands, ctx, ct, ctx.Options);

            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }
    }
}
