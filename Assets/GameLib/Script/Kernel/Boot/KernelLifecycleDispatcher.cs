#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;

namespace Game.Kernel.Boot
{
    public static class KernelLifecycleDispatchCodes
    {
        public const string StepExecutionFailed = "KERNEL_LIFECYCLE_STEP_EXECUTION_FAILED";
        public const string PartialAcquireFailed = "KERNEL_LIFECYCLE_PARTIAL_ACQUIRE_FAILED";
        public const string RollbackStepFailed = "KERNEL_LIFECYCLE_ROLLBACK_STEP_FAILED";
        public const string TickCardinalityForbidden = "LIFECYCLE_TICK_CARDINALITY_FORBIDDEN";
        public const string AsyncUntracked = "LIFECYCLE_ASYNC_UNTRACKED";
        public const string AsyncTimeout = "LIFECYCLE_ASYNC_TIMEOUT";
        public const string AsyncCancelled = "LIFECYCLE_ASYNC_CANCELLED";
        public const string UnsupportedTargetKind = "KERNEL_LIFECYCLE_UNSUPPORTED_TARGET_KIND";
        public const string UnsupportedPhase = "KERNEL_LIFECYCLE_UNSUPPORTED_PHASE";
    }

    public interface ILifecyclePlanResolver
    {
        bool TryGetLifecycleDispatcher(LifecyclePlanId planId, out KernelLifecycleDispatcher? dispatcher);
    }

    public sealed class KernelLifecyclePlanResolver : ILifecyclePlanResolver
    {
        readonly Dictionary<int, KernelLifecycleDispatcher> dispatchersByPlanId;

        public KernelLifecyclePlanResolver(ReadOnlySpan<KernelLifecycleDispatcher> dispatchers)
        {
            dispatchersByPlanId = new Dictionary<int, KernelLifecycleDispatcher>(dispatchers.Length);

            for (int index = 0; index < dispatchers.Length; index++)
            {
                KernelLifecycleDispatcher dispatcher = dispatchers[index] ?? throw new ArgumentNullException(nameof(dispatchers), "Lifecycle dispatchers must not contain null entries.");
                int planId = dispatcher.LifecyclePlan.Header.PlanId.Value;

                if (dispatchersByPlanId.ContainsKey(planId))
                    throw new ArgumentException("Lifecycle plan resolver cannot contain duplicate plan identities.", nameof(dispatchers));

                dispatchersByPlanId.Add(planId, dispatcher);
            }
        }

        public bool TryGetLifecycleDispatcher(LifecyclePlanId planId, out KernelLifecycleDispatcher? dispatcher)
        {
            if (dispatchersByPlanId.TryGetValue(planId.Value, out KernelLifecycleDispatcher? resolvedDispatcher))
            {
                dispatcher = resolvedDispatcher;
                return true;
            }

            dispatcher = null;
            return false;
        }
    }

    public interface ILifecycleDispatchExecutor
    {
        bool TryDispatchService(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic);

        bool TryDispatchScope(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic);

        bool TryDispatchRuntimeQuery(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic);

        bool TryRollbackService(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic);

        bool TryRollbackScope(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic);

        bool TryRollbackRuntimeQuery(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic);
    }

    public readonly struct LifecycleDispatchStepOutcome
    {
        public LifecycleDispatchStepOutcome(bool succeeded, KernelDiagnostic? diagnostic)
        {
            Succeeded = succeeded;
            Diagnostic = diagnostic;
        }

        public bool Succeeded { get; }

        public KernelDiagnostic? Diagnostic { get; }
    }

    public interface IAsyncLifecycleDispatchExecutor : ILifecycleDispatchExecutor
    {
        Task<LifecycleDispatchStepOutcome> TryDispatchServiceAsync(LifecycleDispatchStep step, CancellationToken cancellationToken);

        Task<LifecycleDispatchStepOutcome> TryDispatchScopeAsync(LifecycleDispatchStep step, CancellationToken cancellationToken);

        Task<LifecycleDispatchStepOutcome> TryDispatchRuntimeQueryAsync(LifecycleDispatchStep step, CancellationToken cancellationToken);
    }

    public readonly struct LifecycleRollbackResult
    {
        public LifecycleRollbackResult(int completedStepCount, int attemptedStepCount, int succeededStepCount, int failedStepCount, bool stoppedEarly, KernelDiagnostic? firstDiagnostic)
        {
            CompletedStepCount = completedStepCount;
            AttemptedStepCount = attemptedStepCount;
            SucceededStepCount = succeededStepCount;
            FailedStepCount = failedStepCount;
            StoppedEarly = stoppedEarly;
            FirstDiagnostic = firstDiagnostic;
        }

        public int CompletedStepCount { get; }

        public int AttemptedStepCount { get; }

        public int SucceededStepCount { get; }

        public int FailedStepCount { get; }

        public bool StoppedEarly { get; }

        public KernelDiagnostic? FirstDiagnostic { get; }

        public bool HasFailures => FailedStepCount > 0;
    }

    public readonly struct LifecycleDispatchResult
    {
        public LifecycleDispatchResult(int attemptedStepCount, int succeededStepCount, int failedStepCount, bool stoppedEarly, KernelDiagnostic? firstDiagnostic)
            : this(attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly, firstDiagnostic, default)
        {
        }

        public LifecycleDispatchResult(int attemptedStepCount, int succeededStepCount, int failedStepCount, bool stoppedEarly, KernelDiagnostic? firstDiagnostic, LifecycleRollbackResult rollback)
        {
            AttemptedStepCount = attemptedStepCount;
            SucceededStepCount = succeededStepCount;
            FailedStepCount = failedStepCount;
            StoppedEarly = stoppedEarly;
            FirstDiagnostic = firstDiagnostic;
            Rollback = rollback;
        }

        public int AttemptedStepCount { get; }

        public int SucceededStepCount { get; }

        public int FailedStepCount { get; }

        public bool StoppedEarly { get; }

        public KernelDiagnostic? FirstDiagnostic { get; }

        public LifecycleRollbackResult Rollback { get; }

        public bool HasFailures => FailedStepCount > 0 || Rollback.HasFailures;
    }

    public sealed class KernelLifecycleDispatcher
    {
        readonly LifecyclePlan lifecyclePlan;
        readonly IKernelDiagnosticService? diagnosticService;

        public KernelLifecycleDispatcher(LifecyclePlan lifecyclePlan, IKernelDiagnosticService? diagnosticService = null)
        {
            this.lifecyclePlan = lifecyclePlan ?? throw new ArgumentNullException(nameof(lifecyclePlan));
            this.diagnosticService = diagnosticService;
        }

        public LifecyclePlan LifecyclePlan => lifecyclePlan;

        public LifecycleDispatchTable DispatchTable => lifecyclePlan.DispatchTable;

        public LifecycleDispatchResult DispatchAll(ILifecycleDispatchExecutor executor)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            return DispatchRange(DispatchTable.AllSteps, executor, allowAcquireRollback: false, allowTrackedAsync: false);
        }

        public LifecycleDispatchResult DispatchPhase(LifecyclePhase phase, ILifecycleDispatchExecutor executor)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            return DispatchRange(DispatchTable.GetSteps(phase), executor, allowAcquireRollback: phase == LifecyclePhase.Acquire, allowTrackedAsync: false);
        }

        public Task<LifecycleDispatchResult> DispatchAllAsync(IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            return DispatchRangeAsync(DispatchTable.AllSteps.ToArray(), executor, allowAcquireRollback: false, cancellationToken);
        }

        public Task<LifecycleDispatchResult> DispatchPhaseAsync(LifecyclePhase phase, IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            return DispatchRangeAsync(DispatchTable.GetSteps(phase).ToArray(), executor, allowAcquireRollback: phase == LifecyclePhase.Acquire, cancellationToken);
        }

        LifecycleDispatchResult DispatchRange(ReadOnlySpan<LifecycleDispatchStep> steps, ILifecycleDispatchExecutor executor, bool allowAcquireRollback, bool allowTrackedAsync)
        {
            int attemptedStepCount = 0;
            int succeededStepCount = 0;
            int failedStepCount = 0;
            KernelDiagnostic? firstDiagnostic = null;
            LifecycleRollbackResult rollbackResult = default;

            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                LifecycleDispatchStep step = steps[stepIndex];
                attemptedStepCount++;

                if (!allowTrackedAsync && step.ExecutionMode == LifecycleExecutionModeKind.TrackedAsync)
                {
                    KernelDiagnostic asyncDiagnostic = CreateAsyncUntrackedDiagnostic(step);
                    failedStepCount++;
                    if (firstDiagnostic == null)
                        firstDiagnostic = asyncDiagnostic;

                    diagnosticService?.Report(in asyncDiagnostic);
                    return new LifecycleDispatchResult(attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly: true, firstDiagnostic, rollbackResult);
                }

                if (TryCreateTickCardinalityDiagnostic(in step, out KernelDiagnostic? cardinalityDiagnostic))
                {
                    failedStepCount++;
                    if (firstDiagnostic == null)
                        firstDiagnostic = cardinalityDiagnostic;

                    diagnosticService?.Report(in cardinalityDiagnostic);
                    return new LifecycleDispatchResult(attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly: true, firstDiagnostic, rollbackResult);
                }

                if (TryDispatchStep(in step, executor, out KernelDiagnostic? diagnostic))
                {
                    succeededStepCount++;
                    continue;
                }

                failedStepCount++;
                KernelDiagnostic effectiveDiagnostic = diagnostic ?? CreateStepFailureDiagnostic(step);
                if (firstDiagnostic == null)
                    firstDiagnostic = effectiveDiagnostic;

                diagnosticService?.Report(in effectiveDiagnostic);

                if (allowAcquireRollback && step.Phase == LifecyclePhase.Acquire && succeededStepCount > 0 && step.FailurePolicy != LifecycleFailurePolicy.ContinueWithError)
                {
                    rollbackResult = RollbackCompletedAcquireSteps(steps, succeededStepCount, executor);
                    KernelDiagnostic partialAcquireFailureDiagnostic = CreatePartialAcquireFailureDiagnostic(step, attemptedStepCount, succeededStepCount, failedStepCount, rollbackResult);
                    diagnosticService?.Report(in partialAcquireFailureDiagnostic);
                }

                if (step.FailurePolicy != LifecycleFailurePolicy.ContinueWithError)
                    return new LifecycleDispatchResult(attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly: true, firstDiagnostic, rollbackResult);
            }

            return new LifecycleDispatchResult(attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly: false, firstDiagnostic, rollbackResult);
        }

        async Task<LifecycleDispatchResult> DispatchRangeAsync(LifecycleDispatchStep[] steps, IAsyncLifecycleDispatchExecutor executor, bool allowAcquireRollback, CancellationToken cancellationToken)
        {
            int attemptedStepCount = 0;
            int succeededStepCount = 0;
            int failedStepCount = 0;
            bool stoppedEarly = false;
            KernelDiagnostic? firstDiagnostic = null;
            LifecycleRollbackResult rollbackResult = default;
            List<PendingAsyncDispatch>? pendingAsyncDispatches = null;

            using CancellationTokenSource phaseCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            for (int stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                LifecycleDispatchStep step = steps[stepIndex];
                attemptedStepCount++;

                if (TryCreateTickCardinalityDiagnostic(in step, out KernelDiagnostic? cardinalityDiagnostic))
                {
                    failedStepCount++;
                    if (firstDiagnostic == null)
                        firstDiagnostic = cardinalityDiagnostic;

                    diagnosticService?.Report(in cardinalityDiagnostic);
                    stoppedEarly = true;
                    phaseCancellationSource.Cancel();
                    break;
                }

                if (step.ExecutionMode == LifecycleExecutionModeKind.Synchronous)
                {
                    if (TryDispatchStep(in step, executor, out KernelDiagnostic? diagnostic))
                    {
                        succeededStepCount++;
                        continue;
                    }

                    failedStepCount++;
                    KernelDiagnostic effectiveDiagnostic = diagnostic ?? CreateStepFailureDiagnostic(step);
                    if (firstDiagnostic == null)
                        firstDiagnostic = effectiveDiagnostic;

                    diagnosticService?.Report(in effectiveDiagnostic);

                    if (allowAcquireRollback && step.Phase == LifecyclePhase.Acquire && succeededStepCount > 0 && step.FailurePolicy != LifecycleFailurePolicy.ContinueWithError)
                    {
                        rollbackResult = RollbackCompletedAcquireSteps(steps, succeededStepCount, executor);
                        KernelDiagnostic partialAcquireFailureDiagnostic = CreatePartialAcquireFailureDiagnostic(step, attemptedStepCount, succeededStepCount, failedStepCount, rollbackResult);
                        diagnosticService?.Report(in partialAcquireFailureDiagnostic);
                    }

                    if (step.FailurePolicy != LifecycleFailurePolicy.ContinueWithError)
                    {
                        stoppedEarly = true;
                        phaseCancellationSource.Cancel();
                        break;
                    }

                    continue;
                }

                if (step.AsyncPolicy == null)
                {
                    KernelDiagnostic asyncDiagnostic = CreateAsyncUntrackedDiagnostic(step);
                    failedStepCount++;
                    if (firstDiagnostic == null)
                        firstDiagnostic = asyncDiagnostic;

                    diagnosticService?.Report(in asyncDiagnostic);
                    stoppedEarly = true;
                    phaseCancellationSource.Cancel();
                    break;
                }

                PendingAsyncDispatch pendingDispatch = StartTrackedAsyncDispatch(step, executor, phaseCancellationSource.Token);
                if (step.AsyncPolicy.WaitForNextStep)
                {
                    LifecycleDispatchStepOutcome outcome = await AwaitTrackedAsyncDispatchAsync(pendingDispatch, phaseCancellationSource.Token).ConfigureAwait(false);
                    pendingDispatch.Dispose();

                    if (outcome.Succeeded)
                    {
                        succeededStepCount++;
                        continue;
                    }

                    failedStepCount++;
                    KernelDiagnostic effectiveDiagnostic = outcome.Diagnostic ?? CreateAsyncStepFailureDiagnostic(step);
                    if (firstDiagnostic == null)
                        firstDiagnostic = effectiveDiagnostic;

                    diagnosticService?.Report(in effectiveDiagnostic);

                    if (allowAcquireRollback && step.Phase == LifecyclePhase.Acquire && succeededStepCount > 0 && step.FailurePolicy != LifecycleFailurePolicy.ContinueWithError)
                    {
                        rollbackResult = RollbackCompletedAcquireSteps(steps, succeededStepCount, executor);
                        KernelDiagnostic partialAcquireFailureDiagnostic = CreatePartialAcquireFailureDiagnostic(step, attemptedStepCount, succeededStepCount, failedStepCount, rollbackResult);
                        diagnosticService?.Report(in partialAcquireFailureDiagnostic);
                    }

                    if (step.FailurePolicy != LifecycleFailurePolicy.ContinueWithError)
                    {
                        stoppedEarly = true;
                        phaseCancellationSource.Cancel();
                        break;
                    }

                    continue;
                }

                pendingAsyncDispatches ??= new List<PendingAsyncDispatch>(4);
                pendingAsyncDispatches.Add(pendingDispatch);
            }

            if (pendingAsyncDispatches != null)
            {
                AsyncDispatchProgress progress = new AsyncDispatchProgress
                {
                    SucceededStepCount = succeededStepCount,
                    FailedStepCount = failedStepCount,
                    StoppedEarly = stoppedEarly,
                    FirstDiagnostic = firstDiagnostic,
                };

                await FinalizePendingAsyncDispatchesAsync(pendingAsyncDispatches, phaseCancellationSource, progress).ConfigureAwait(false);
                succeededStepCount = progress.SucceededStepCount;
                failedStepCount = progress.FailedStepCount;
                stoppedEarly = progress.StoppedEarly;
                firstDiagnostic = progress.FirstDiagnostic;
            }

            return new LifecycleDispatchResult(attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly, firstDiagnostic, rollbackResult);
        }

        sealed class AsyncDispatchProgress
        {
            public int SucceededStepCount;

            public int FailedStepCount;

            public bool StoppedEarly;

            public KernelDiagnostic? FirstDiagnostic;
        }

        sealed class PendingAsyncDispatch : IDisposable
        {
            public PendingAsyncDispatch(LifecycleDispatchStep step, LifecycleAsyncPolicyIR asyncPolicy, CancellationTokenSource cancellationSource, IDisposable? cancellationRegistration, Task<LifecycleDispatchStepOutcome> dispatchTask)
            {
                Step = step;
                AsyncPolicy = asyncPolicy;
                CancellationSource = cancellationSource;
                CancellationRegistration = cancellationRegistration;
                DispatchTask = dispatchTask;
            }

            public LifecycleDispatchStep Step { get; }

            public LifecycleAsyncPolicyIR AsyncPolicy { get; }

            public CancellationTokenSource CancellationSource { get; }

            public IDisposable? CancellationRegistration { get; }

            public Task<LifecycleDispatchStepOutcome> DispatchTask { get; }

            public void Dispose()
            {
                CancellationRegistration?.Dispose();
                CancellationSource.Dispose();
            }
        }

        LifecycleRollbackResult RollbackCompletedAcquireSteps(ReadOnlySpan<LifecycleDispatchStep> steps, int completedStepCount, ILifecycleDispatchExecutor executor)
        {
            int attemptedStepCount = 0;
            int succeededStepCount = 0;
            int failedStepCount = 0;
            KernelDiagnostic? firstDiagnostic = null;

            for (int stepIndex = completedStepCount - 1; stepIndex >= 0; stepIndex--)
            {
                LifecycleDispatchStep step = steps[stepIndex];

                if (step.AcquireRollbackPolicy != LifecycleAcquireRollbackPolicy.ReverseCompletedAcquireSteps)
                    continue;

                attemptedStepCount++;

                if (TryRollbackStep(in step, executor, out KernelDiagnostic? diagnostic))
                {
                    succeededStepCount++;
                    continue;
                }

                failedStepCount++;
                KernelDiagnostic effectiveDiagnostic = diagnostic ?? CreateRollbackStepFailureDiagnostic(step, completedStepCount, attemptedStepCount);
                if (firstDiagnostic == null)
                    firstDiagnostic = effectiveDiagnostic;

                diagnosticService?.Report(in effectiveDiagnostic);
                return new LifecycleRollbackResult(completedStepCount, attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly: true, firstDiagnostic);
            }

            return new LifecycleRollbackResult(completedStepCount, attemptedStepCount, succeededStepCount, failedStepCount, stoppedEarly: false, firstDiagnostic);
        }

        static PendingAsyncDispatch StartTrackedAsyncDispatch(LifecycleDispatchStep step, IAsyncLifecycleDispatchExecutor executor, CancellationToken phaseCancellationToken)
        {
            LifecycleAsyncPolicyIR asyncPolicy = step.AsyncPolicy ?? throw new InvalidOperationException("Tracked async lifecycle steps must provide an async policy.");
            CancellationTokenSource cancellationSource;
            IDisposable? cancellationRegistration = null;

            switch (asyncPolicy.CancellationSourceKind)
            {
                case LifecycleAsyncCancellationSourceKind.DispatcherOwned:
                    cancellationSource = new CancellationTokenSource();
                    cancellationRegistration = phaseCancellationToken.Register(static state => ((CancellationTokenSource)state!).Cancel(), cancellationSource);
                    break;

                case LifecycleAsyncCancellationSourceKind.LinkedToCaller:
                    cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(phaseCancellationToken);
                    break;

                default:
                    throw new InvalidOperationException("Tracked async lifecycle steps must provide a defined cancellation source kind.");
            }

            Task<LifecycleDispatchStepOutcome> dispatchTask = TryDispatchAsyncStep(step, executor, cancellationSource.Token);
            return new PendingAsyncDispatch(step, asyncPolicy, cancellationSource, cancellationRegistration, dispatchTask);
        }

        static async Task<LifecycleDispatchStepOutcome> AwaitTrackedAsyncDispatchAsync(PendingAsyncDispatch pendingDispatch, CancellationToken phaseCancellationToken)
        {
            try
            {
                if (phaseCancellationToken.IsCancellationRequested)
                    return new LifecycleDispatchStepOutcome(false, CreateAsyncCancelledDiagnostic(pendingDispatch.Step));

                if (pendingDispatch.AsyncPolicy.TimeoutPolicyKind == LifecycleAsyncTimeoutPolicyKind.DurationMilliseconds)
                {
                    Task timeoutTask = Task.Delay(pendingDispatch.AsyncPolicy.TimeoutMilliseconds);
                    Task phaseCancellationTask = Task.Delay(Timeout.Infinite, phaseCancellationToken);
                    Task completedTask = await Task.WhenAny(pendingDispatch.DispatchTask, timeoutTask, phaseCancellationTask).ConfigureAwait(false);

                    if (ReferenceEquals(completedTask, pendingDispatch.DispatchTask))
                        return await pendingDispatch.DispatchTask.ConfigureAwait(false);

                    if (ReferenceEquals(completedTask, timeoutTask))
                    {
                        pendingDispatch.CancellationSource.Cancel();
                        return new LifecycleDispatchStepOutcome(false, CreateAsyncTimeoutDiagnostic(pendingDispatch.Step));
                    }

                    if (timeoutTask.IsCompleted)
                    {
                        pendingDispatch.CancellationSource.Cancel();
                        return new LifecycleDispatchStepOutcome(false, CreateAsyncTimeoutDiagnostic(pendingDispatch.Step));
                    }

                    return new LifecycleDispatchStepOutcome(false, CreateAsyncCancelledDiagnostic(pendingDispatch.Step));
                }

                if (phaseCancellationToken.IsCancellationRequested)
                    return new LifecycleDispatchStepOutcome(false, CreateAsyncCancelledDiagnostic(pendingDispatch.Step));

                return await pendingDispatch.DispatchTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new LifecycleDispatchStepOutcome(false, CreateAsyncCancelledDiagnostic(pendingDispatch.Step));
            }
            catch (Exception exception)
            {
                return new LifecycleDispatchStepOutcome(false, CreateAsyncStepFailureDiagnostic(pendingDispatch.Step, exception));
            }
        }

        async Task FinalizePendingAsyncDispatchesAsync(List<PendingAsyncDispatch> pendingAsyncDispatches, CancellationTokenSource phaseCancellationSource, AsyncDispatchProgress progress)
        {
            for (int index = 0; index < pendingAsyncDispatches.Count; index++)
            {
                PendingAsyncDispatch pendingDispatch = pendingAsyncDispatches[index];
                LifecycleDispatchStepOutcome outcome = await AwaitTrackedAsyncDispatchAsync(pendingDispatch, phaseCancellationSource.Token).ConfigureAwait(false);
                pendingDispatch.Dispose();

                if (outcome.Succeeded)
                {
                    progress.SucceededStepCount++;
                    continue;
                }

                progress.FailedStepCount++;
                KernelDiagnostic effectiveDiagnostic = outcome.Diagnostic ?? CreateAsyncStepFailureDiagnostic(pendingDispatch.Step);
                if (progress.FirstDiagnostic == null)
                    progress.FirstDiagnostic = effectiveDiagnostic;

                diagnosticService?.Report(in effectiveDiagnostic);

                if (pendingDispatch.Step.FailurePolicy != LifecycleFailurePolicy.ContinueWithError)
                {
                    progress.StoppedEarly = true;
                    phaseCancellationSource.Cancel();
                    for (int remainingIndex = index + 1; remainingIndex < pendingAsyncDispatches.Count; remainingIndex++)
                        pendingAsyncDispatches[remainingIndex].Dispose();

                    break;
                }
            }
        }

        static Task<LifecycleDispatchStepOutcome> TryDispatchAsyncStep(LifecycleDispatchStep step, IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken)
        {
            switch (step.Target.Kind)
            {
                case LifecycleTargetKind.Service:
                    return executor.TryDispatchServiceAsync(step, cancellationToken);
                case LifecycleTargetKind.Scope:
                    return executor.TryDispatchScopeAsync(step, cancellationToken);
                case LifecycleTargetKind.RuntimeQuery:
                    return executor.TryDispatchRuntimeQueryAsync(step, cancellationToken);
                default:
                    return Task.FromResult(new LifecycleDispatchStepOutcome(false, CreateUnsupportedTargetKindDiagnostic(step, rollbackMode: false)));
            }
        }

        static bool TryDispatchStep(in LifecycleDispatchStep step, ILifecycleDispatchExecutor executor, out KernelDiagnostic? diagnostic)
        {
            switch (step.Target.Kind)
            {
                case LifecycleTargetKind.Service:
                    return executor.TryDispatchService(in step, out diagnostic);
                case LifecycleTargetKind.Scope:
                    return executor.TryDispatchScope(in step, out diagnostic);
                case LifecycleTargetKind.RuntimeQuery:
                    return executor.TryDispatchRuntimeQuery(in step, out diagnostic);
                default:
                    diagnostic = new KernelDiagnostic(
                        new DiagnosticCode(KernelLifecycleDispatchCodes.UnsupportedTargetKind),
                        DiagnosticSeverity.Error,
                        DiagnosticDomain.Lifecycle,
                        DiagnosticFailureBoundary.Kernel,
                        message: "Lifecycle dispatch encountered an unsupported target kind.",
                        context: new DiagnosticContext(
                            runtimeIdentities: new[] { step.LifecycleIdentity, step.StepIdentity },
                            ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                            source: new SourceLocationRef(step.Source.Value),
                            phase: step.Phase.ToString()),
                        payload: new DiagnosticPayload(new[]
                        {
                            new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                            new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                            new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                        }));
                    return false;
            }
        }

        static bool TryRollbackStep(in LifecycleDispatchStep step, ILifecycleDispatchExecutor executor, out KernelDiagnostic? diagnostic)
        {
            switch (step.Target.Kind)
            {
                case LifecycleTargetKind.Service:
                    return executor.TryRollbackService(in step, out diagnostic);
                case LifecycleTargetKind.Scope:
                    return executor.TryRollbackScope(in step, out diagnostic);
                case LifecycleTargetKind.RuntimeQuery:
                    return executor.TryRollbackRuntimeQuery(in step, out diagnostic);
                default:
                    diagnostic = new KernelDiagnostic(
                        new DiagnosticCode(KernelLifecycleDispatchCodes.UnsupportedTargetKind),
                        DiagnosticSeverity.Error,
                        DiagnosticDomain.Lifecycle,
                        DiagnosticFailureBoundary.Kernel,
                        message: "Lifecycle rollback encountered an unsupported target kind.",
                        context: new DiagnosticContext(
                            runtimeIdentities: new[] { step.LifecycleIdentity, step.StepIdentity },
                            ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                            source: new SourceLocationRef(step.Source.Value),
                            phase: step.Phase.ToString()),
                        payload: new DiagnosticPayload(new[]
                        {
                            new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                            new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                            new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                            new DiagnosticPayloadEntry("RollbackMode", DiagnosticPayloadValue.FromBoolean(true)),
                        }));
                    return false;
            }
        }

        static bool TryCreateTickCardinalityDiagnostic(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
        {
            if (step.Phase != LifecyclePhase.Tick && step.Phase != LifecyclePhase.FixedTick && step.Phase != LifecyclePhase.LateTick)
            {
                diagnostic = null;
                return false;
            }

            if (step.TickCardinality != LifecycleTickCardinalityKind.PerEntity)
            {
                diagnostic = null;
                return false;
            }

            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("TickCardinality", DiagnosticPayloadValue.FromString(step.TickCardinality.ToString())),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            diagnostic = new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.TickCardinalityForbidden),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                DiagnosticFailureBoundary.Kernel,
                message: "Lifecycle tick step uses a forbidden per-entity cardinality.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
            return true;
        }

        static KernelDiagnostic CreateStepFailureDiagnostic(LifecycleDispatchStep step)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("FailurePolicy", DiagnosticPayloadValue.FromString(step.FailurePolicy.ToString())),
                new DiagnosticPayloadEntry("FailurePolicyIsExplicit", DiagnosticPayloadValue.FromBoolean(step.FailurePolicyIsExplicit)),
                new DiagnosticPayloadEntry("FailurePolicyJustificationProfiles", DiagnosticPayloadValue.FromInt32((int)step.FailurePolicyJustificationProfiles)),
                new DiagnosticPayloadEntry("FailurePolicyJustification", DiagnosticPayloadValue.FromString(step.FailurePolicyJustification ?? string.Empty)),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.StepExecutionFailed),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                MapFailureBoundary(step.FailurePolicy),
                message: "Lifecycle dispatch step failed.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
        }

        static KernelDiagnostic CreatePartialAcquireFailureDiagnostic(LifecycleDispatchStep step, int attemptedStepCount, int succeededStepCount, int failedStepCount, LifecycleRollbackResult rollbackResult)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("FailurePolicy", DiagnosticPayloadValue.FromString(step.FailurePolicy.ToString())),
                new DiagnosticPayloadEntry("AcquireRollbackPolicy", DiagnosticPayloadValue.FromString(step.AcquireRollbackPolicy.ToString())),
                new DiagnosticPayloadEntry("AttemptedStepCount", DiagnosticPayloadValue.FromInt32(attemptedStepCount)),
                new DiagnosticPayloadEntry("SucceededStepCount", DiagnosticPayloadValue.FromInt32(succeededStepCount)),
                new DiagnosticPayloadEntry("FailedStepCount", DiagnosticPayloadValue.FromInt32(failedStepCount)),
                new DiagnosticPayloadEntry("RollbackCompletedStepCount", DiagnosticPayloadValue.FromInt32(rollbackResult.CompletedStepCount)),
                new DiagnosticPayloadEntry("RollbackAttemptedStepCount", DiagnosticPayloadValue.FromInt32(rollbackResult.AttemptedStepCount)),
                new DiagnosticPayloadEntry("RollbackSucceededStepCount", DiagnosticPayloadValue.FromInt32(rollbackResult.SucceededStepCount)),
                new DiagnosticPayloadEntry("RollbackFailedStepCount", DiagnosticPayloadValue.FromInt32(rollbackResult.FailedStepCount)),
                new DiagnosticPayloadEntry("RollbackStoppedEarly", DiagnosticPayloadValue.FromBoolean(rollbackResult.StoppedEarly)),
                new DiagnosticPayloadEntry("RollbackSkippedStepCount", DiagnosticPayloadValue.FromInt32(rollbackResult.CompletedStepCount - rollbackResult.AttemptedStepCount)),
                new DiagnosticPayloadEntry("RollbackRequired", DiagnosticPayloadValue.FromBoolean(rollbackResult.AttemptedStepCount > 0)),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.PartialAcquireFailed),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                MapFailureBoundary(step.FailurePolicy),
                message: "Lifecycle acquire failed after partial completion.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
        }

        static KernelDiagnostic CreateRollbackStepFailureDiagnostic(LifecycleDispatchStep step, int completedStepCount, int rollbackAttemptedStepCount)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("FailurePolicy", DiagnosticPayloadValue.FromString(step.FailurePolicy.ToString())),
                new DiagnosticPayloadEntry("FailurePolicyIsExplicit", DiagnosticPayloadValue.FromBoolean(step.FailurePolicyIsExplicit)),
                new DiagnosticPayloadEntry("FailurePolicyJustificationProfiles", DiagnosticPayloadValue.FromInt32((int)step.FailurePolicyJustificationProfiles)),
                new DiagnosticPayloadEntry("FailurePolicyJustification", DiagnosticPayloadValue.FromString(step.FailurePolicyJustification ?? string.Empty)),
                new DiagnosticPayloadEntry("AcquireRollbackPolicy", DiagnosticPayloadValue.FromString(step.AcquireRollbackPolicy.ToString())),
                new DiagnosticPayloadEntry("RollbackMode", DiagnosticPayloadValue.FromBoolean(true)),
                new DiagnosticPayloadEntry("CompletedStepCount", DiagnosticPayloadValue.FromInt32(completedStepCount)),
                new DiagnosticPayloadEntry("RollbackAttemptedStepCount", DiagnosticPayloadValue.FromInt32(rollbackAttemptedStepCount)),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.RollbackStepFailed),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                MapFailureBoundary(step.FailurePolicy),
                message: "Lifecycle acquire rollback step failed.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
        }

        static KernelDiagnostic CreateUnsupportedTargetKindDiagnostic(LifecycleDispatchStep step, bool rollbackMode)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayloadEntry[] payloadEntries = rollbackMode
                ? new[]
                {
                    new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                    new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                    new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                    new DiagnosticPayloadEntry("RollbackMode", DiagnosticPayloadValue.FromBoolean(true)),
                }
                : new[]
                {
                    new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                    new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                    new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                };

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.UnsupportedTargetKind),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                DiagnosticFailureBoundary.Kernel,
                message: rollbackMode ? "Lifecycle rollback encountered an unsupported target kind." : "Lifecycle dispatch encountered an unsupported target kind.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: new DiagnosticPayload(payloadEntries));
        }

        static KernelDiagnostic CreateAsyncUntrackedDiagnostic(LifecycleDispatchStep step)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("ExecutionMode", DiagnosticPayloadValue.FromString(step.ExecutionMode.ToString())),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.AsyncUntracked),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                MapFailureBoundary(step.FailurePolicy),
                message: "Tracked async lifecycle steps require the async dispatch entrypoint.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
        }

        static KernelDiagnostic CreateAsyncTimeoutDiagnostic(LifecycleDispatchStep step)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("ExecutionMode", DiagnosticPayloadValue.FromString(step.ExecutionMode.ToString())),
                new DiagnosticPayloadEntry("TimeoutPolicyKind", DiagnosticPayloadValue.FromString(step.AsyncPolicy?.TimeoutPolicyKind.ToString() ?? string.Empty)),
                new DiagnosticPayloadEntry("TimeoutMilliseconds", DiagnosticPayloadValue.FromInt32(step.AsyncPolicy?.TimeoutMilliseconds ?? 0)),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.AsyncTimeout),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                MapFailureBoundary(step.FailurePolicy),
                message: "Tracked async lifecycle step timed out.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
        }

        static KernelDiagnostic CreateAsyncCancelledDiagnostic(LifecycleDispatchStep step)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("ExecutionMode", DiagnosticPayloadValue.FromString(step.ExecutionMode.ToString())),
                new DiagnosticPayloadEntry("CancellationSourceKind", DiagnosticPayloadValue.FromString(step.AsyncPolicy?.CancellationSourceKind.ToString() ?? string.Empty)),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.AsyncCancelled),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                MapFailureBoundary(step.FailurePolicy),
                message: "Tracked async lifecycle step was cancelled.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
        }

        static KernelDiagnostic CreateAsyncStepFailureDiagnostic(LifecycleDispatchStep step)
        {
            return CreateStepFailureDiagnostic(step);
        }

        static KernelDiagnostic CreateAsyncStepFailureDiagnostic(LifecycleDispatchStep step, Exception exception)
        {
            RuntimeIdentityRef[] runtimeIdentities = BuildRuntimeIdentities(step);
            DiagnosticPayload payload = new DiagnosticPayload(new[]
            {
                new DiagnosticPayloadEntry("LifecyclePlanId", DiagnosticPayloadValue.FromInt32(step.LifecyclePlanId.Value)),
                new DiagnosticPayloadEntry("LifecycleName", DiagnosticPayloadValue.FromString(step.LifecycleName)),
                new DiagnosticPayloadEntry("StepId", DiagnosticPayloadValue.FromInt32(step.StepId.Value)),
                new DiagnosticPayloadEntry("Phase", DiagnosticPayloadValue.FromString(step.Phase.ToString())),
                new DiagnosticPayloadEntry("Order", DiagnosticPayloadValue.FromInt32(step.Order)),
                new DiagnosticPayloadEntry("Action", DiagnosticPayloadValue.FromString(step.Action.ToString())),
                new DiagnosticPayloadEntry("ExecutionMode", DiagnosticPayloadValue.FromString(step.ExecutionMode.ToString())),
                new DiagnosticPayloadEntry("ExceptionType", DiagnosticPayloadValue.FromString(exception.GetType().FullName ?? exception.GetType().Name)),
                new DiagnosticPayloadEntry("ExceptionMessage", DiagnosticPayloadValue.FromString(exception.Message)),
                new DiagnosticPayloadEntry("TargetKind", DiagnosticPayloadValue.FromString(step.Target.Kind.ToString())),
                new DiagnosticPayloadEntry("TargetRef", DiagnosticPayloadValue.FromString(FormatTarget(step))),
            });

            return new KernelDiagnostic(
                new DiagnosticCode(KernelLifecycleDispatchCodes.StepExecutionFailed),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Lifecycle,
                MapFailureBoundary(step.FailurePolicy),
                message: "Tracked async lifecycle step failed.",
                context: new DiagnosticContext(
                    runtimeIdentities,
                    ownerModule: new ModuleIdentityRef(step.OwnerModule.Value),
                    source: new SourceLocationRef(step.Source.Value),
                    phase: step.Phase.ToString()),
                payload: payload);
        }

        static RuntimeIdentityRef[] BuildRuntimeIdentities(LifecycleDispatchStep step)
        {
            if (step.TryGetTargetIdentity(out RuntimeIdentityRef targetIdentity))
            {
                return new[]
                {
                    step.LifecycleIdentity,
                    step.StepIdentity,
                    targetIdentity,
                };
            }

            return new[]
            {
                step.LifecycleIdentity,
                step.StepIdentity,
            };
        }

        static string FormatTarget(LifecycleDispatchStep step)
        {
            switch (step.Target.Kind)
            {
                case LifecycleTargetKind.Service:
                    return "Service:" + step.Target.TargetService.Value;
                case LifecycleTargetKind.Scope:
                    return "Scope:" + step.Target.TargetScope.Value;
                case LifecycleTargetKind.RuntimeQuery:
                    return "RuntimeQuery:" + step.Target.TargetRuntimeQuery.Value;
                case LifecycleTargetKind.ValueStore:
                case LifecycleTargetKind.RuntimeObjectOwner:
                case LifecycleTargetKind.LegacyAdapter:
                    return step.Target.Kind + ":" + (step.Target.TargetLocalRef ?? string.Empty);
                default:
                    return step.Target.Kind.ToString();
            }
        }

        static DiagnosticFailureBoundary MapFailureBoundary(LifecycleFailurePolicy policy)
        {
            switch (policy)
            {
                case LifecycleFailurePolicy.FailOperation:
                    return DiagnosticFailureBoundary.Operation;
                case LifecycleFailurePolicy.FailScope:
                    return DiagnosticFailureBoundary.Scope;
                case LifecycleFailurePolicy.FailScene:
                    return DiagnosticFailureBoundary.Scene;
                case LifecycleFailurePolicy.FailKernel:
                    return DiagnosticFailureBoundary.Kernel;
                case LifecycleFailurePolicy.ContinueWithError:
                    return DiagnosticFailureBoundary.Operation;
                default:
                    return DiagnosticFailureBoundary.Operation;
            }
        }
    }
}