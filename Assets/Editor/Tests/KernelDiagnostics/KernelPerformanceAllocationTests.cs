#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.Kernel.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests
{
    public sealed class KernelPerformanceAllocationTests
    {
        const string PerformanceProfile = "Performance";
        const string HotPath = "HotPath";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            KernelPerformanceReportCollector.Reset();
        }

        [Test]
        public void DiagnosticsReport_IsAllocationFree_WhenNoSessionAndSinksAreHealthy()
        {
            var service = new KernelDiagnosticService(Array.Empty<IKernelDiagnosticSink>());
            var diagnostic = new KernelDiagnostic(
                new DiagnosticCode("PERF_DIAG_REPORT"),
                DiagnosticSeverity.Info,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Operation,
                message: "steady-state trace path");

            var measurement = KernelAllocationAssert.Measure(() => service.Report(in diagnostic));
            Assert.That(measurement.AllocatedBytes, Is.EqualTo(0));

            RecordPerformanceEntry(
                testId: "M13.5.DiagnosticsDisabledTracePath",
                subsystem: nameof(KernelDiagnosticService),
                operation: nameof(KernelDiagnosticService.Report),
                pathKind: HotPath,
                fixtureSize: 0,
                measurement: measurement,
                threshold: CreateDiagnosticsDisabledTracePathThreshold(),
                observedMarkerSamples: new[] { "Kernel.Diagnostics.TraceDisabled" });
        }

        [Test]
        public void RuntimeResolve_IsAllocationFree_OnCachedHit()
        {
            var service = new MeasuredService();
            var builder = new RuntimeContainerBuilder();
            builder.RegisterInstance(typeof(IMeasuredService), service);

            var resolver = builder.Build();
            try
            {
                Assert.That(resolver.TryResolve<IMeasuredService>(out var warmupService), Is.True);
                Assert.That(warmupService, Is.SameAs(service));

                IMeasuredService? measuredService = null;
                bool resolved = false;
                var measurement = KernelAllocationAssert.Measure(() => resolved = resolver.TryResolve<IMeasuredService>(out measuredService));

                Assert.That(measurement.AllocatedBytes, Is.EqualTo(0));
                Assert.That(resolved, Is.True);
                Assert.That(measuredService, Is.SameAs(service));

                RecordPerformanceEntry(
                    testId: "M13.5.ResolveCacheHit",
                    subsystem: nameof(RuntimeResolver),
                    operation: "TryResolve<IMeasuredService>",
                    pathKind: HotPath,
                    fixtureSize: 1,
                    measurement: measurement,
                    threshold: CreateResolveCacheHitThreshold(),
                    observedMarkerSamples: new[] { "Kernel.Resolve.CacheHit" });
            }
            finally
            {
                resolver.Dispose();
            }
        }

        [Test]
        public void HandleValidation_IsAllocationFree_OnDirectScopeOwnerMatch()
        {
            var resolver = new RuntimeContainerBuilder().Build();
            try
            {
                var scope = new PerfScopeNode(resolver);
                var handler = scope;

                var builder = new RuntimeContainerBuilder();
                builder.RegisterInstance(typeof(IScopeAcquireHandler), handler);
                var dispatchResolver = builder.Build();
                try
                {
                    var dispatcher = new ScopeAcquireReleaseDispatcher(dispatchResolver);

                    var measurement = KernelAllocationAssert.Measure(() => dispatcher.Acquire(scope, isReset: false));

                    Assert.That(measurement.AllocatedBytes, Is.EqualTo(0));
                    RecordPerformanceEntry(
                        testId: "M13.5.HandleValidation",
                        subsystem: nameof(ScopeAcquireReleaseDispatcher),
                        operation: nameof(ScopeAcquireReleaseDispatcher.Acquire),
                        pathKind: HotPath,
                        fixtureSize: 1,
                        measurement: measurement,
                        threshold: CreateHandleValidationThreshold(),
                        observedMarkerSamples: new[] { "Kernel.Scope.OwnerMatch" });

                    Assert.That(handler.AcquireCount, Is.EqualTo(2));
                }
                finally
                {
                    dispatchResolver.Dispose();
                }
            }
            finally
            {
                resolver.Dispose();
            }
        }

        [Test]
        public void TickDispatch_IsAllocationFree_OnStableUpdateLoop()
        {
            var gameObject = new GameObject("KernelPerformanceAllocationTests.TickDispatch");
            try
            {
                var hub = gameObject.AddComponent<RuntimeTickHub>();
                var handler = new RecordingTickHandler();
                hub.RegisterRange(new IScopeTickHandler[] { handler });

                MethodInfo updateMethod = typeof(RuntimeTickHub).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("RuntimeTickHub.Update was not found.");
                var update = (Action)Delegate.CreateDelegate(typeof(Action), hub, updateMethod);

                var measurement = KernelAllocationAssert.Measure(update);

                Assert.That(measurement.AllocatedBytes, Is.EqualTo(0));
                RecordPerformanceEntry(
                    testId: "M13.5.TickDispatch",
                    subsystem: nameof(RuntimeTickHub),
                    operation: "Update",
                    pathKind: HotPath,
                    fixtureSize: 1,
                    measurement: measurement,
                    threshold: CreateTickDispatchThreshold(),
                    observedMarkerSamples: new[] { "Kernel.Tick.Dispatch" });

                Assert.That(handler.TickCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CommandDispatch_IsAllocationFree_WhenExecutorCompletesSynchronously()
        {
            var resolver = new RuntimeContainerBuilder().Build();
            try
            {
                var scope = new PerfScopeNode(resolver);
                var vars = new VarStore();
                var runner = new NoOpCommandRunner(scope);
                var context = new CommandContext(scope, vars, runner);
                var data = new NoOpCommandData(42);
                var executor = new NoOpCommandExecutor(42);

                var measurement = KernelAllocationAssert.Measure(() => executor.Execute(data, context, CancellationToken.None).GetAwaiter().GetResult());

                Assert.That(measurement.AllocatedBytes, Is.EqualTo(0));
                RecordPerformanceEntry(
                    testId: "M13.5.CommandDispatch",
                    subsystem: nameof(CommandRunner),
                    operation: "ICommandExecutor.Execute",
                    pathKind: HotPath,
                    fixtureSize: 1,
                    measurement: measurement,
                    threshold: CreateCommandDispatchThreshold(),
                    observedMarkerSamples: new[] { "Kernel.Command.Execute" });

                Assert.That(executor.ExecuteCount, Is.EqualTo(2));
            }
            finally
            {
                resolver.Dispose();
            }
        }

        [Test]
        public void VarReadAndWrite_AreAllocationFree_OnSteadyStateCacheHits()
        {
            var store = new VarStore();
            const int varId = 901;
            var initialValue = DynamicVariant.FromInt(17);

            Assert.That(store.TrySetVariant(varId, initialValue), Is.True);

            DynamicVariant readValue = default;
            var readMeasurement = KernelAllocationAssert.Measure(() => store.TryGetVariant(varId, out readValue));
            Assert.That(readValue, Is.EqualTo(initialValue));
            Assert.That(readMeasurement.AllocatedBytes, Is.EqualTo(0));

            RecordPerformanceEntry(
                testId: "M13.5.ValueRead",
                subsystem: nameof(VarStore),
                operation: nameof(VarStore.TryGetVariant),
                pathKind: HotPath,
                fixtureSize: 1,
                measurement: readMeasurement,
                threshold: CreateValueReadThreshold(),
                observedMarkerSamples: new[] { "Kernel.Value.Read" });

            bool writeSucceeded = false;
            var writeMeasurement = KernelAllocationAssert.Measure(() => writeSucceeded = store.TrySetVariant(varId, initialValue));
            Assert.That(writeMeasurement.AllocatedBytes, Is.EqualTo(0));
            Assert.That(writeSucceeded, Is.True);

            RecordPerformanceEntry(
                testId: "M13.5.ValueWrite",
                subsystem: nameof(VarStore),
                operation: nameof(VarStore.TrySetVariant),
                pathKind: HotPath,
                fixtureSize: 1,
                measurement: writeMeasurement,
                threshold: CreateValueWriteThreshold(),
                observedMarkerSamples: new[] { "Kernel.Value.Write" });
        }

        [Test]
        public void DynamicCachedRead_IsAllocationFree_OnCacheHit()
        {
            var runtime = new DynamicEvaluationRuntime();
            var resolver = new RuntimeContainerBuilder().Build();
            try
            {
                var scope = new PerfScopeNode(resolver);
                var legacyContext = new SimpleDynamicContext(NullVarStore.Instance, scope);
                var plan = new DynamicEvaluationPlan
                {
                    PlanId = new DynamicEvaluationPlanId(910),
                    RootSource = new DynamicSourceHandle(1),
                    Phase = DynamicEvaluationPhase.ExplicitRead,
                    DependencyMode = DynamicDependencyDeclarationMode.Tracked,
                    FallbackPolicy = DynamicFallbackPolicy.Forbidden,
                    CachePolicy = DynamicCachePolicy.SharedTracked,
                    RequirePlan = true,
                    SourceLocation = "Assets/Editor/Tests/KernelDiagnostics/KernelPerformanceAllocationTests.cs",
                };
                var stamp = DynamicDependencyStamp.FromContext(legacyContext, sourceVersion: 1);
                var context = new DynamicEvaluationContext(legacyContext, runtime, plan, DynamicEvaluationPhase.ExplicitRead, stamp, requirePlan: true);
                var source = new CountingDynamicSource(DynamicVariant.FromFloat(3.5f));
                var value = DynamicValue.FromSource(source);

                var measurement = KernelAllocationAssert.Measure(() => _ = value.Evaluate(context));

                Assert.That(measurement.AllocatedBytes, Is.EqualTo(0));
                Assert.That(source.Evaluations, Is.EqualTo(1));

                RecordPerformanceEntry(
                    testId: "M13.5.DynamicCachedRead",
                    subsystem: nameof(DynamicEvaluationRuntime),
                    operation: nameof(DynamicEvaluationRuntime.TryEvaluate),
                    pathKind: HotPath,
                    fixtureSize: 1,
                    measurement: measurement,
                    threshold: CreateDynamicCachedReadThreshold(),
                    observedMarkerSamples: new[] { "Kernel.DynamicEvaluation.CacheHit" });
            }
            finally
            {
                resolver.Dispose();
            }
        }

        sealed class MeasuredService : IMeasuredService
        {
        }

        interface IMeasuredService
        {
        }

        sealed class RecordingTickHandler : IScopeTickHandler
        {
            public int TickCount { get; private set; }

            public void Tick()
            {
                TickCount++;
            }
        }

        sealed class NoOpCommandExecutor : ICommandExecutor
        {
            readonly int _commandId;

            public NoOpCommandExecutor(int commandId)
            {
                _commandId = commandId;
            }

            public int CommandId => _commandId;

            public int ExecuteCount { get; private set; }

            public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
            {
                _ = data;
                _ = ctx;
                _ = ct;
                ExecuteCount++;
                return UniTask.CompletedTask;
            }
        }

        sealed class NoOpCommandData : ICommandData
        {
            public NoOpCommandData(int commandId)
            {
                CommandId = commandId;
            }

            public int CommandId { get; }

            public string DebugData => string.Empty;
        }

        sealed class NoOpCommandRunner : ICommandRunner
        {
            public NoOpCommandRunner(IScopeNode scope)
            {
                Scope = scope;
            }

            public IScopeNode Scope { get; }

            public UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                _ = data;
                _ = ctx;
                _ = ct;
                _ = options;
                throw new NotSupportedException();
            }

            public UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                _ = list;
                _ = ctx;
                _ = ct;
                _ = options;
                throw new NotSupportedException();
            }

            public UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                _ = list;
                _ = onCanceled;
                _ = ctx;
                _ = ct;
                _ = options;
                throw new NotSupportedException();
            }
        }

        sealed class PerfScopeNode : IScopeNode, IScopeAcquireHandler, IScopeReleaseHandler
        {
            readonly IRuntimeResolver _resolver;

            public PerfScopeNode(IRuntimeResolver resolver)
            {
                _resolver = resolver;
            }

            public IScopeNode? Parent => null;

            public ILTSIdentityService? Identity => null;

            public LifetimeScopeKind Kind => LifetimeScopeKind.Scene;

            public IRuntimeResolver? Resolver => _resolver;

            public bool IsVisible => true;

            public bool IsActive => true;

            public int AcquireCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }

            public void OnAcquire(IScopeNode scope, bool isReset)
            {
                _ = scope;
                _ = isReset;
                AcquireCount++;
            }

            public void OnRelease(IScopeNode scope, bool isReset)
            {
                _ = scope;
                _ = isReset;
                ReleaseCount++;
            }
        }

        sealed class CountingDynamicSource : IDynamicSource
        {
            readonly DynamicVariant _value;

            public CountingDynamicSource(DynamicVariant value)
            {
                _value = value;
            }

            public int Evaluations { get; private set; }

            public DynamicVariant Evaluate(IDynamicContext context)
            {
                _ = context;
                Evaluations++;
                return _value;
            }

            public string SourceTypeName => nameof(CountingDynamicSource);

            public string GetDebugData => string.Empty;
        }

        static void RecordPerformanceEntry(
            string testId,
            string subsystem,
            string operation,
            string pathKind,
            int fixtureSize,
            KernelAllocationAssert.Measurement measurement,
            KernelPerformanceThresholdSpec threshold,
            string[] observedMarkerSamples,
            string baselineLabel = "",
            long baselineAllocationBytes = 0,
            double baselineElapsedMilliseconds = 0d)
        {
            bool hasBaseline = !string.IsNullOrWhiteSpace(baselineLabel);
            var regressionResult = KernelPerformanceRegressionGate.Evaluate(
                measurement,
                threshold,
                observedMarkerSamples,
                hasBaseline,
                baselineAllocationBytes,
                baselineElapsedMilliseconds);

            KernelPerformanceReportCollector.Record(new KernelPerformanceReportEntry
            {
                TestId = testId,
                Subsystem = subsystem,
                Operation = operation,
                PathKind = pathKind,
                FixtureSize = fixtureSize,
                Profile = PerformanceProfile,
                ElapsedMilliseconds = measurement.ElapsedMilliseconds,
                AllocationBytes = measurement.AllocatedBytes,
                CallCount = 1,
                MarkerSamples = observedMarkerSamples,
                Passed = regressionResult.Passed,
                ExpectedMaxAllocationBytes = threshold.ExpectedMaxAllocationBytes,
                ExpectedMaxElapsedMilliseconds = threshold.ExpectedMaxElapsedMilliseconds,
                AllowedAllocationRegressionBytes = threshold.AllowedAllocationRegressionBytes,
                AllowedElapsedRegressionMilliseconds = threshold.AllowedElapsedRegressionMilliseconds,
                FailureCode = regressionResult.FailureCode,
                BaselineLabel = baselineLabel,
                HasBaseline = hasBaseline,
                BaselineAllocationBytes = baselineAllocationBytes,
                BaselineElapsedMilliseconds = baselineElapsedMilliseconds,
                AllocationDeltaBytes = hasBaseline ? measurement.AllocatedBytes - baselineAllocationBytes : 0,
                ElapsedDeltaMilliseconds = hasBaseline ? measurement.ElapsedMilliseconds - baselineElapsedMilliseconds : 0d,
                FailureReason = regressionResult.FailureReason,
            });

            Assert.That(regressionResult.Passed, Is.True, regressionResult.FailureReason);
        }

        static KernelPerformanceThresholdSpec CreateDiagnosticsDisabledTracePathThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 15d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Diagnostics.TraceDisabled" });
        }

        static KernelPerformanceThresholdSpec CreateResolveCacheHitThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 10d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Resolve.CacheHit" });
        }

        static KernelPerformanceThresholdSpec CreateHandleValidationThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 10d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Scope.OwnerMatch" });
        }

        static KernelPerformanceThresholdSpec CreateTickDispatchThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 10d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Tick.Dispatch" });
        }

        static KernelPerformanceThresholdSpec CreateCommandDispatchThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 10d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Command.Execute" });
        }

        static KernelPerformanceThresholdSpec CreateValueReadThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 8d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Value.Read" });
        }

        static KernelPerformanceThresholdSpec CreateValueWriteThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 8d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.Value.Write" });
        }

        static KernelPerformanceThresholdSpec CreateDynamicCachedReadThreshold()
        {
            return KernelPerformanceRegressionGate.CreateHotPathThreshold(
                expectedMaxAllocationBytes: 0,
                expectedMaxElapsedMilliseconds: 10d,
                allowedAllocationRegressionBytes: 0,
                allowedElapsedRegressionMilliseconds: 1d,
                requiredMarkerSamples: new[] { "Kernel.DynamicEvaluation.CacheHit" });
        }
    }
}