#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelLifecycleDispatcherTests
    {
        [Test]
        public void LifecyclePlan_BuildsDeterministicPhaseTables_AndDispatchAllUsesCanonicalPhaseOrder()
        {
            LifecycleIR firstLifecycle = CreateLifecycle(
                100,
                "FirstLifecycle",
                new[]
                {
                    CreateStep(101, LifecyclePhase.Boot, 20, new LifecycleTargetRefIR(new ScopePlanId(5001)), LifecycleActionKind.ScopeStateTransition, 1001),
                    CreateStep(102, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ScopePlanId(5002)), LifecycleActionKind.ScopeStateTransition, 1002),
                });

            LifecycleIR secondLifecycle = CreateLifecycle(
                200,
                "SecondLifecycle",
                new[]
                {
                    CreateStep(201, LifecyclePhase.Acquire, 5, new LifecycleTargetRefIR(new ScopePlanId(5003)), LifecycleActionKind.ScopeStateTransition, 2001),
                    CreateStep(202, LifecyclePhase.Tick, 15, new LifecycleTargetRefIR(new ServiceId(6001)), LifecycleActionKind.ServiceMethod, 2002),
                });

            LifecyclePlan plan = CreatePlan(firstLifecycle, secondLifecycle);

            Assert.That(plan.DispatchTable.BootSteps.Length, Is.EqualTo(1));
            Assert.That(plan.DispatchTable.AcquireSteps.Length, Is.EqualTo(2));
            Assert.That(plan.DispatchTable.TickSteps.Length, Is.EqualTo(1));
            Assert.That(plan.DispatchTable.TickSteps[0].TickCardinality, Is.EqualTo(LifecycleTickCardinalityKind.Hub));
            Assert.That(plan.DispatchTable.AllSteps.Length, Is.EqualTo(4));
            Assert.That(plan.DispatchTable.AcquireSteps[0].LifecyclePlanId, Is.EqualTo(new LifecyclePlanId(100)));
            Assert.That(plan.DispatchTable.AcquireSteps[0].StepId, Is.EqualTo(new LifecycleStepId(102)));
            Assert.That(plan.DispatchTable.AcquireSteps[1].LifecyclePlanId, Is.EqualTo(new LifecyclePlanId(200)));
            Assert.That(plan.DispatchTable.AcquireSteps[1].StepId, Is.EqualTo(new LifecycleStepId(201)));
            Assert.That(plan.DispatchTable.AllSteps[0].Phase, Is.EqualTo(LifecyclePhase.Boot));
            Assert.That(plan.DispatchTable.AllSteps[1].Phase, Is.EqualTo(LifecyclePhase.Acquire));

            RecordingExecutor executor = new RecordingExecutor(Array.Empty<int>());
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan);

            LifecycleDispatchResult result = dispatcher.DispatchAll(executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(4));
            Assert.That(result.SucceededStepCount, Is.EqualTo(4));
            Assert.That(result.FailedStepCount, Is.EqualTo(0));
            Assert.That(result.StoppedEarly, Is.False);
            Assert.That(executor.AttemptedStepIds, Is.EqualTo(new[] { 101, 102, 201, 202 }));
        }

        [Test]
        public void DispatchPhase_ReportsDiagnostics_AndContinuesWhenPolicyAllows()
        {
            LifecycleIR lifecycle = CreateLifecycle(
                300,
                "ContinueLifecycle",
                new[]
                {
                    CreateStep(301, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ScopePlanId(7001)), LifecycleActionKind.ScopeStateTransition, 3001),
                    CreateStep(302, LifecyclePhase.Acquire, 20, new LifecycleTargetRefIR(new ScopePlanId(7002)), LifecycleActionKind.ScopeStateTransition, 3002),
                },
                failurePolicy: LifecycleFailurePolicy.ContinueWithError,
                failurePolicyJustificationProfiles: KernelProfileMask.Development,
                failurePolicyJustification: "continue for test coverage");

            LifecyclePlan plan = CreatePlan(lifecycle);
            TestDiagnosticSink sink = new TestDiagnosticSink();
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan, diagnosticService);
            RecordingExecutor executor = new RecordingExecutor(new[] { 301 });

            LifecycleDispatchResult result = dispatcher.DispatchPhase(LifecyclePhase.Acquire, executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(2));
            Assert.That(result.SucceededStepCount, Is.EqualTo(1));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(result.StoppedEarly, Is.False);
            Assert.That(result.Rollback.CompletedStepCount, Is.EqualTo(0));
            Assert.That(result.Rollback.AttemptedStepCount, Is.EqualTo(0));
            Assert.That(executor.AttemptedStepIds, Is.EqualTo(new[] { 301, 302 }));
            Assert.That(executor.RollbackAttemptedStepIds, Is.Empty);
            Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.StepExecutionFailed));
            Assert.That(sink.Diagnostics[0].Domain, Is.EqualTo(DiagnosticDomain.Lifecycle));
            Assert.That(sink.Diagnostics[0].FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Operation));
            Assert.That(sink.Diagnostics[0].Context.Phase, Is.EqualTo(LifecyclePhase.Acquire.ToString()));
            Assert.That(sink.Diagnostics[0].Context.RuntimeIdentities, Has.Count.EqualTo(3));
        }

        [Test]
        public void DispatchPhase_StopsEarly_WhenFailurePolicyRequiresIt_AndSkipsRollbackWhenPolicyDisablesIt()
        {
            LifecycleIR lifecycle = CreateLifecycle(
                400,
                "FailScopeLifecycle",
                new[]
                {
                    CreateStep(401, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ScopePlanId(8001)), LifecycleActionKind.ScopeStateTransition, 4001),
                    CreateStep(402, LifecyclePhase.Acquire, 20, new LifecycleTargetRefIR(new ScopePlanId(8002)), LifecycleActionKind.ScopeStateTransition, 4002),
                    CreateStep(403, LifecyclePhase.Acquire, 30, new LifecycleTargetRefIR(new ScopePlanId(8003)), LifecycleActionKind.ScopeStateTransition, 4003),
                },
                failurePolicy: LifecycleFailurePolicy.FailScope,
                acquireRollbackPolicy: LifecycleAcquireRollbackPolicy.None);

            LifecyclePlan plan = CreatePlan(lifecycle);
            TestDiagnosticSink sink = new TestDiagnosticSink();
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan, diagnosticService);
            RecordingExecutor executor = new RecordingExecutor(new[] { 403 });

            LifecycleDispatchResult result = dispatcher.DispatchPhase(LifecyclePhase.Acquire, executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(3));
            Assert.That(result.SucceededStepCount, Is.EqualTo(2));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(result.StoppedEarly, Is.True);
            Assert.That(result.Rollback.CompletedStepCount, Is.EqualTo(2));
            Assert.That(result.Rollback.AttemptedStepCount, Is.EqualTo(0));
            Assert.That(result.Rollback.SucceededStepCount, Is.EqualTo(0));
            Assert.That(executor.AttemptedStepIds, Is.EqualTo(new[] { 401, 402, 403 }));
            Assert.That(executor.RollbackAttemptedStepIds, Is.Empty);
            Assert.That(sink.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.StepExecutionFailed));
            Assert.That(sink.Diagnostics[1].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.PartialAcquireFailed));
            Assert.That(sink.Diagnostics[1].FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Scope));
        }

        [Test]
        public void DispatchPhase_RollsBackCompletedAcquireSteps_InReverseOrder_WhenPolicyRequiresIt()
        {
            LifecycleIR lifecycle = CreateLifecycle(
                500,
                "RollbackLifecycle",
                new[]
                {
                    CreateStep(501, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(new ScopePlanId(9001)), LifecycleActionKind.ScopeStateTransition, 5001),
                    CreateStep(502, LifecyclePhase.Acquire, 20, new LifecycleTargetRefIR(new ScopePlanId(9002)), LifecycleActionKind.ScopeStateTransition, 5002),
                    CreateStep(503, LifecyclePhase.Acquire, 30, new LifecycleTargetRefIR(new ScopePlanId(9003)), LifecycleActionKind.ScopeStateTransition, 5003),
                },
                failurePolicy: LifecycleFailurePolicy.FailScope);

            LifecyclePlan plan = CreatePlan(lifecycle);
            TestDiagnosticSink sink = new TestDiagnosticSink();
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan, diagnosticService);
            RecordingExecutor executor = new RecordingExecutor(new[] { 503 });

            LifecycleDispatchResult result = dispatcher.DispatchPhase(LifecyclePhase.Acquire, executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(3));
            Assert.That(result.SucceededStepCount, Is.EqualTo(2));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(result.StoppedEarly, Is.True);
            Assert.That(result.Rollback.CompletedStepCount, Is.EqualTo(2));
            Assert.That(result.Rollback.AttemptedStepCount, Is.EqualTo(2));
            Assert.That(result.Rollback.SucceededStepCount, Is.EqualTo(2));
            Assert.That(result.Rollback.FailedStepCount, Is.EqualTo(0));
            Assert.That(executor.AttemptedStepIds, Is.EqualTo(new[] { 501, 502, 503 }));
            Assert.That(executor.RollbackAttemptedStepIds, Is.EqualTo(new[] { 502, 501 }));
            Assert.That(sink.Diagnostics, Has.Count.EqualTo(2));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.StepExecutionFailed));
            Assert.That(sink.Diagnostics[1].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.PartialAcquireFailed));
        }

        [Test]
        public void DispatchPhase_RejectsPerEntityTickCardinality_WithDiagnostic()
        {
            LifecycleIR lifecycle = CreateLifecycle(
                600,
                "PerEntityTickLifecycle",
                new[]
                {
                    CreateStep(601, LifecyclePhase.Tick, 10, new LifecycleTargetRefIR(new ServiceId(6101)), LifecycleActionKind.ServiceMethod, 6001, tickCardinality: LifecycleTickCardinalityKind.PerEntity),
                });

            LifecyclePlan plan = CreatePlan(lifecycle);
            TestDiagnosticSink sink = new TestDiagnosticSink();
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan, diagnosticService);
            RecordingExecutor executor = new RecordingExecutor(Array.Empty<int>());

            LifecycleDispatchResult result = dispatcher.DispatchPhase(LifecyclePhase.Tick, executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.SucceededStepCount, Is.EqualTo(0));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(result.StoppedEarly, Is.True);
            Assert.That(result.Rollback.CompletedStepCount, Is.EqualTo(0));
            Assert.That(result.Rollback.AttemptedStepCount, Is.EqualTo(0));
            Assert.That(executor.AttemptedStepIds, Is.Empty);
            Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.TickCardinalityForbidden));
            Assert.That(sink.Diagnostics[0].FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Kernel));
            Assert.That(sink.Diagnostics[0].Context.Phase, Is.EqualTo(LifecyclePhase.Tick.ToString()));
        }

        [Test]
        public void DispatchPhase_RejectsTrackedAsyncSteps_WhenUsingSyncEntryPoint()
        {
            LifecycleAsyncPolicyIR asyncPolicy = new LifecycleAsyncPolicyIR(
                LifecycleAsyncCancellationSourceKind.DispatcherOwned,
                LifecycleAsyncTimeoutPolicyKind.None,
                0,
                LifecycleAsyncCompletionRequirementKind.BeforeNextStep,
                waitForNextStep: true);

            LifecycleIR lifecycle = CreateLifecycle(
                700,
                "TrackedAsyncLifecycle",
                new[]
                {
                    CreateStep(701, LifecyclePhase.Boot, 10, new LifecycleTargetRefIR(new ServiceId(7101)), LifecycleActionKind.ServiceMethod, 7001, executionMode: LifecycleExecutionModeKind.TrackedAsync, asyncPolicy: asyncPolicy),
                });

            LifecyclePlan plan = CreatePlan(lifecycle);
            TestDiagnosticSink sink = new TestDiagnosticSink();
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan, diagnosticService);
            RecordingExecutor executor = new RecordingExecutor(Array.Empty<int>());

            LifecycleDispatchResult result = dispatcher.DispatchPhase(LifecyclePhase.Boot, executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.SucceededStepCount, Is.EqualTo(0));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(result.StoppedEarly, Is.True);
            Assert.That(executor.AttemptedStepIds, Is.Empty);
            Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.AsyncUntracked));
            Assert.That(sink.Diagnostics[0].FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Operation));
        }

        [Test]
        public async Task DispatchPhaseAsync_CompletesTrackedAsyncSteps()
        {
            LifecycleAsyncPolicyIR asyncPolicy = new LifecycleAsyncPolicyIR(
                LifecycleAsyncCancellationSourceKind.DispatcherOwned,
                LifecycleAsyncTimeoutPolicyKind.None,
                0,
                LifecycleAsyncCompletionRequirementKind.BeforeNextStep,
                waitForNextStep: true);

            LifecycleIR lifecycle = CreateLifecycle(
                710,
                "AsyncSuccessLifecycle",
                new[]
                {
                    CreateStep(711, LifecyclePhase.Boot, 10, new LifecycleTargetRefIR(new ServiceId(7201)), LifecycleActionKind.ServiceMethod, 7101, executionMode: LifecycleExecutionModeKind.TrackedAsync, asyncPolicy: asyncPolicy),
                });

            LifecyclePlan plan = CreatePlan(lifecycle);
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan);
            RecordingExecutor executor = new RecordingExecutor(Array.Empty<int>());

            LifecycleDispatchResult result = await dispatcher.DispatchPhaseAsync(LifecyclePhase.Boot, executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.SucceededStepCount, Is.EqualTo(1));
            Assert.That(result.FailedStepCount, Is.EqualTo(0));
            Assert.That(result.StoppedEarly, Is.False);
            Assert.That(executor.AttemptedStepIds, Is.EqualTo(new[] { 711 }));
        }

        [Test]
        public async Task DispatchPhaseAsync_ReportsTrackedAsyncTimeouts()
        {
            LifecycleAsyncPolicyIR asyncPolicy = new LifecycleAsyncPolicyIR(
                LifecycleAsyncCancellationSourceKind.DispatcherOwned,
                LifecycleAsyncTimeoutPolicyKind.DurationMilliseconds,
                5,
                LifecycleAsyncCompletionRequirementKind.BeforeNextStep,
                waitForNextStep: true);

            LifecycleIR lifecycle = CreateLifecycle(
                720,
                "AsyncTimeoutLifecycle",
                new[]
                {
                    CreateStep(721, LifecyclePhase.Boot, 10, new LifecycleTargetRefIR(new ServiceId(7301)), LifecycleActionKind.ServiceMethod, 7201, executionMode: LifecycleExecutionModeKind.TrackedAsync, asyncPolicy: asyncPolicy),
                });

            LifecyclePlan plan = CreatePlan(lifecycle);
            TestDiagnosticSink sink = new TestDiagnosticSink();
            KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });
            KernelLifecycleDispatcher dispatcher = new KernelLifecycleDispatcher(plan, diagnosticService);
            RecordingExecutor executor = new RecordingExecutor(Array.Empty<int>(), asyncDispatchDelayMilliseconds: 50);

            LifecycleDispatchResult result = await dispatcher.DispatchPhaseAsync(LifecyclePhase.Boot, executor);

            Assert.That(result.AttemptedStepCount, Is.EqualTo(1));
            Assert.That(result.SucceededStepCount, Is.EqualTo(0));
            Assert.That(result.FailedStepCount, Is.EqualTo(1));
            Assert.That(result.StoppedEarly, Is.True);
            Assert.That(executor.AttemptedStepIds, Is.EqualTo(new[] { 721 }));
            Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(KernelLifecycleDispatchCodes.AsyncTimeout));
        }

        [Test]
        public void LifecyclePlanHash_ChangesWhenTrackedAsyncMetadataChanges()
        {
            LifecycleAsyncPolicyIR asyncPolicy = new LifecycleAsyncPolicyIR(
                LifecycleAsyncCancellationSourceKind.DispatcherOwned,
                LifecycleAsyncTimeoutPolicyKind.None,
                0,
                LifecycleAsyncCompletionRequirementKind.BeforeNextStep,
                waitForNextStep: true);

            LifecycleIR synchronousLifecycle = CreateLifecycle(
                730,
                "SynchronousHashLifecycle",
                new[]
                {
                    CreateStep(731, LifecyclePhase.Boot, 10, new LifecycleTargetRefIR(new ServiceId(7401)), LifecycleActionKind.ServiceMethod, 7301),
                });

            LifecycleIR asyncLifecycle = CreateLifecycle(
                730,
                "SynchronousHashLifecycle",
                new[]
                {
                    CreateStep(731, LifecyclePhase.Boot, 10, new LifecycleTargetRefIR(new ServiceId(7401)), LifecycleActionKind.ServiceMethod, 7301, executionMode: LifecycleExecutionModeKind.TrackedAsync, asyncPolicy: asyncPolicy),
                });

            Hash128 synchronousHash = KernelProjectionHashing.ComputeLifecyclePlanHash(new[] { synchronousLifecycle });
            Hash128 asyncHash = KernelProjectionHashing.ComputeLifecyclePlanHash(new[] { asyncLifecycle });

            Assert.That(synchronousHash, Is.Not.EqualTo(asyncHash));
        }

        [Test]
        public void LifecyclePlan_RejectsUnsupportedTargetKinds_WhenBuildingDispatchTables()
        {
            LifecycleIR lifecycle = CreateLifecycle(
                500,
                "UnsupportedTargetLifecycle",
                new[]
                {
                    CreateStep(501, LifecyclePhase.Acquire, 10, new LifecycleTargetRefIR(LifecycleTargetKind.LegacyAdapter, "legacy-bridge"), LifecycleActionKind.LegacyAdapterCall, 5001),
                });

            Assert.That(() => CreatePlan(lifecycle), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        static LifecyclePlan CreatePlan(params LifecycleIR[] lifecycles)
        {
            Hash128 contentHash = KernelProjectionHashing.ComputeLifecyclePlanHash(lifecycles);
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(901),
                new ArtifactSetId(902),
                new ArtifactId(903),
                ArtifactKind.LifecyclePlan,
                4,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 10, 11, 12),
                new Hash128(13, 14, 15, 16),
                contentHash,
                "KernelLifecycleDispatcherTests");

            return new LifecyclePlan(header, lifecycles);
        }

        static LifecycleIR CreateLifecycle(
            int planId,
            string name,
            LifecycleStepIR[] steps,
            LifecycleFailurePolicy failurePolicy = LifecycleFailurePolicy.FailOperation,
            bool failurePolicyIsExplicit = true,
            KernelProfileMask failurePolicyJustificationProfiles = KernelProfileMask.None,
            string? failurePolicyJustification = null,
            LifecycleAcquireRollbackPolicy acquireRollbackPolicy = LifecycleAcquireRollbackPolicy.ReverseCompletedAcquireSteps)
        {
            return new LifecycleIR(
                new LifecyclePlanId(planId),
                name,
                new ModuleId(1000 + planId),
                steps,
                new SourceLocationId(2000 + planId),
                failurePolicy,
                failurePolicyIsExplicit,
                failurePolicyJustificationProfiles,
                failurePolicyJustification,
                acquireRollbackPolicy);
        }

        static LifecycleStepIR CreateStep(int id, LifecyclePhase phase, int order, LifecycleTargetRefIR target, LifecycleActionKind action, int sourceId, LifecycleTickCardinalityKind tickCardinality = LifecycleTickCardinalityKind.Unknown, LifecycleExecutionModeKind executionMode = LifecycleExecutionModeKind.Synchronous, LifecycleAsyncPolicyIR? asyncPolicy = null)
        {
            return new LifecycleStepIR(
                new LifecycleStepId(id),
                phase,
                order,
                target,
                action,
                Array.Empty<DependencyEdgeId>(),
                new SourceLocationId(sourceId),
                tickCardinality,
                executionMode,
                asyncPolicy);
        }

        sealed class RecordingExecutor : IAsyncLifecycleDispatchExecutor
        {
            readonly HashSet<int> failingStepIds;
            readonly int asyncDispatchDelayMilliseconds;

            public RecordingExecutor(IEnumerable<int> failingStepIds, int asyncDispatchDelayMilliseconds = 0)
            {
                this.failingStepIds = new HashSet<int>(failingStepIds);
                this.asyncDispatchDelayMilliseconds = asyncDispatchDelayMilliseconds;
            }

            public List<int> AttemptedStepIds { get; } = new List<int>();

            public List<int> RollbackAttemptedStepIds { get; } = new List<int>();

            public bool TryDispatchService(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryExecute(step, out diagnostic);
            }

            public bool TryDispatchScope(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryExecute(step, out diagnostic);
            }

            public bool TryDispatchRuntimeQuery(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryExecute(step, out diagnostic);
            }

            public bool TryRollbackService(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryRollback(step, out diagnostic);
            }

            public bool TryRollbackScope(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryRollback(step, out diagnostic);
            }

            public bool TryRollbackRuntimeQuery(in LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                return TryRollback(step, out diagnostic);
            }

            public Task<LifecycleDispatchStepOutcome> TryDispatchServiceAsync(LifecycleDispatchStep step, CancellationToken cancellationToken)
            {
                return TryExecuteAsync(step, cancellationToken);
            }

            public Task<LifecycleDispatchStepOutcome> TryDispatchScopeAsync(LifecycleDispatchStep step, CancellationToken cancellationToken)
            {
                return TryExecuteAsync(step, cancellationToken);
            }

            public Task<LifecycleDispatchStepOutcome> TryDispatchRuntimeQueryAsync(LifecycleDispatchStep step, CancellationToken cancellationToken)
            {
                return TryExecuteAsync(step, cancellationToken);
            }

            async Task<LifecycleDispatchStepOutcome> TryExecuteAsync(LifecycleDispatchStep step, CancellationToken cancellationToken)
            {
                AttemptedStepIds.Add(step.StepId.Value);

                if (asyncDispatchDelayMilliseconds > 0)
                    await Task.Delay(asyncDispatchDelayMilliseconds, cancellationToken).ConfigureAwait(false);

                if (failingStepIds.Contains(step.StepId.Value))
                    return new LifecycleDispatchStepOutcome(false, null);

                return new LifecycleDispatchStepOutcome(true, null);
            }

            bool TryExecute(LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                AttemptedStepIds.Add(step.StepId.Value);

                if (failingStepIds.Contains(step.StepId.Value))
                {
                    diagnostic = null;
                    return false;
                }

                diagnostic = null;
                return true;
            }

            bool TryRollback(LifecycleDispatchStep step, out KernelDiagnostic? diagnostic)
            {
                RollbackAttemptedStepIds.Add(step.StepId.Value);

                if (failingStepIds.Contains(step.StepId.Value))
                {
                    diagnostic = null;
                    return false;
                }

                diagnostic = null;
                return true;
            }
        }
    }
}
