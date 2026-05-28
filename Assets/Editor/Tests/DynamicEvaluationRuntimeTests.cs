#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Commands;
using Game.Times;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests
{
    public sealed class DynamicEvaluationRuntimeTests
    {
        [Test]
        public void Runtime_CachesSameSourceForSameStamp()
        {
            var runtime = new DynamicEvaluationRuntime();
            var source = new CountingSource(DynamicVariant.FromFloat(3.5f));
            var context = CreateContext(runtime, phase: DynamicEvaluationPhase.ExplicitRead, planId: 10);
            var value = DynamicValue.FromSource(source);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(3.5f));
            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(3.5f));
            Assert.That(source.Evaluations, Is.EqualTo(1));
            Assert.That(runtime.LastDependencies, Is.Empty);
        }

        [Test]
        public void Runtime_InvalidatesCacheWhenStampChanges()
        {
            var runtime = new DynamicEvaluationRuntime();
            var source = new MutableCountingSource(DynamicVariant.FromFloat(8.25f));
            var value = DynamicValue.FromSource(source);
            var firstContext = CreateContext(runtime, planId: 11, extraStamp: 0);
            var secondContext = CreateContext(runtime, planId: 11, extraStamp: 1);

            Assert.That(value.Evaluate(firstContext).AsFloat, Is.EqualTo(8.25f));
            Assert.That(value.Evaluate(firstContext).AsFloat, Is.EqualTo(8.25f));
            Assert.That(value.Evaluate(secondContext).AsFloat, Is.EqualTo(8.25f));
            Assert.That(source.Evaluations, Is.EqualTo(2));
        }

        [Test]
        public void Runtime_InvalidatesCacheWhenSourceConfigChanges()
        {
            var runtime = new DynamicEvaluationRuntime();
            var source = new MutableCountingSource(DynamicVariant.FromFloat(4f));
            var value = DynamicValue.FromSource(source);
            var context = CreateContext(runtime, planId: 12);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(4f));
            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(4f));
            Assert.That(source.Evaluations, Is.EqualTo(1));

            source.SetValue(DynamicVariant.FromFloat(6f));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(6f));
            Assert.That(source.Evaluations, Is.EqualTo(2));
        }

        [Test]
        public void Runtime_InvalidatesCacheWhenVarSlotRevisionChanges()
        {
            var runtime = new DynamicEvaluationRuntime();
            var vars = new VarStore();
            var varId = 101;
            vars.TrySetVariant(varId, DynamicVariant.FromFloat(1f));
            var context = CreateConstantStampContext(runtime, vars, planId: 13);
            var value = DynamicValue.FromVarId(varId);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(1f));
            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(1f));

            vars.TrySetVariant(varId, DynamicVariant.FromFloat(2f));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(2f));
        }

        [Test]
        public void Runtime_InvalidatesCacheWhenTableCellRevisionChanges()
        {
            var runtime = new DynamicEvaluationRuntime();
            var vars = new VarStore();
            var tableVarId = 202;
            var rowIndex = 0;
            var columnIndex = 0;
            Assert.That(vars.TryGetOrEnsureTableCellStore(tableVarId, rowIndex, columnIndex, autoCreateRow: true, out var cellStore), Is.True);
            cellStore.TrySetVariant(1, DynamicVariant.FromFloat(3f));
            var context = CreateConstantStampContext(runtime, vars, planId: 14);
            var value = DynamicValue.FromSource(new TableCellValueSource(tableVarId, rowIndex, columnIndex, 1));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(3f));
            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(3f));

            cellStore.TrySetVariant(1, DynamicVariant.FromFloat(5f));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(5f));
        }

        [Test]
        public void Runtime_InvalidatesParentWhenChildDependencyRevisionChanges()
        {
            var runtime = new DynamicEvaluationRuntime();
            var vars = new VarStore();
            var varId = 303;
            vars.TrySetVariant(varId, DynamicVariant.FromFloat(7f));
            var context = CreateConstantStampContext(runtime, vars, planId: 15);
            var child = DynamicValue<float>.FromSource(VarStoreSource.FromVarId(varId));
            var value = DynamicValue.FromSource(new BridgedNestedSource(child));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(8f));
            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(8f));

            vars.TrySetVariant(varId, DynamicVariant.FromFloat(9f));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(10f));
        }

        [Test]
        public void Runtime_SelfBlackboardSourceReadsFromScopeVarsWithoutBlackboardService()
        {
            var runtime = new DynamicEvaluationRuntime();
            var vars = new VarStore();
            var varId = 404;
            Assert.That(vars.TrySetVariant(varId, DynamicVariant.FromFloat(12f)), Is.True);

            using var resolver = new TestRuntimeResolver();
            resolver.Add<IVarStore>(vars);
            var scope = new TestScopeNode(
                new TestIdentityService("dynamic-self-blackboard-local", "group", LifetimeScopeKind.Scene),
                resolver);
            var context = CreateConstantStampContext(runtime, NullVarStore.Instance, scope, planId: 16);
            var value = DynamicValue.FromSource(SelfBlackboardSource.FromVarId(varId));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(12f));
            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(12f));

            Assert.That(vars.TrySetVariant(varId, DynamicVariant.FromFloat(15f)), Is.True);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(15f));
        }

        [Test]
        public void Runtime_SelfBlackboardSourceReadsParentScopeVarsWithoutBlackboardService()
        {
            var runtime = new DynamicEvaluationRuntime();
            var parentVars = new VarStore();
            var varId = 405;
            Assert.That(parentVars.TrySetVariant(varId, DynamicVariant.FromInt(9)), Is.True);

            using var parentResolver = new TestRuntimeResolver();
            parentResolver.Add<IVarStore>(parentVars);
            var parentScope = new TestScopeNode(
                new TestIdentityService("dynamic-self-blackboard-parent", "group", LifetimeScopeKind.Scene),
                parentResolver);
            var childScope = new TestScopeNode(
                new TestIdentityService("dynamic-self-blackboard-child", "group", LifetimeScopeKind.Entity),
                parent: parentScope);
            var context = CreateConstantStampContext(runtime, NullVarStore.Instance, childScope, planId: 17);
            var value = DynamicValue.FromSource(SelfBlackboardSource.FromVarId(varId, BlackboardReadScope.Global));

            Assert.That(value.Evaluate(context).AsInt, Is.EqualTo(9));
            Assert.That(value.Evaluate(context).AsInt, Is.EqualTo(9));

            Assert.That(parentVars.TrySetVariant(varId, DynamicVariant.FromInt(11)), Is.True);

            Assert.That(value.Evaluate(context).AsInt, Is.EqualTo(11));
        }

        [Test]
        public void Runtime_CapturesNestedDynamicValueRead()
        {
            var runtime = new DynamicEvaluationRuntime();
            var childSource = new MutableCountingSource(DynamicVariant.FromFloat(7f));
            var parentSource = new NestedSource(DynamicValue<float>.FromSource(childSource));
            var context = CreateContext(runtime, planId: 20);
            var value = DynamicValue.FromSource(parentSource);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(8f));
            Assert.That(childSource.Evaluations, Is.EqualTo(1));
            Assert.That(runtime.LastDependencies, Has.Count.EqualTo(1));

            var edge = runtime.LastDependencies[0];
            Assert.That(edge.ParentSourceType, Is.EqualTo(nameof(NestedSource)));
            Assert.That(edge.ChildSourceType, Is.EqualTo(nameof(MutableCountingSource)));
        }

        [Test]
        public void Runtime_InvalidatesNestedParentWhenChildConfigChanges()
        {
            var runtime = new DynamicEvaluationRuntime();
            var childSource = new MutableCountingSource(DynamicVariant.FromFloat(7f));
            var parentSource = new NestedSource(DynamicValue<float>.FromSource(childSource));
            var context = CreateContext(runtime, planId: 21);
            var value = DynamicValue.FromSource(parentSource);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(8f));
            Assert.That(parentSource.Evaluations, Is.EqualTo(1));

            childSource.SetValue(DynamicVariant.FromFloat(10f));

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(11f));
            Assert.That(childSource.Evaluations, Is.EqualTo(2));
            Assert.That(parentSource.Evaluations, Is.EqualTo(2));
        }

        [Test]
        public void Runtime_RejectsMissingPlanWhenRequired()
        {
            var runtime = new DynamicEvaluationRuntime();
            var diagnostics = new RecordingDiagnosticService();
            var source = new CountingSource(DynamicVariant.FromFloat(1f));
            var context = new DynamicEvaluationContext(
                new SimpleDynamicContext(null, null),
                runtime,
                plan: null,
                phase: DynamicEvaluationPhase.ExplicitRead,
                dependencyStamp: DynamicDependencyStamp.Empty,
                requirePlan: true,
                diagnostics: diagnostics);

            Assert.That(runtime.TryEvaluate(source, context, out var value), Is.False);
            Assert.That(source.Evaluations, Is.Zero);
            Assert.That(diagnostics.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics.Diagnostics[0].Code, Is.EqualTo("DYN_EVAL_PLAN_MISSING"));
        }

        [Test]
        public void Runtime_RejectsPhaseMismatch()
        {
            var runtime = new DynamicEvaluationRuntime();
            var diagnostics = new RecordingDiagnosticService();
            var source = new CountingSource(DynamicVariant.FromFloat(2f));
            var context = CreateContext(runtime, planId: 30, phase: DynamicEvaluationPhase.Tick, planPhase: DynamicEvaluationPhase.Acquire, diagnostics: diagnostics);

            Assert.That(runtime.TryEvaluate(source, context, out var value), Is.False);
            Assert.That(source.Evaluations, Is.Zero);
            Assert.That(diagnostics.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics.Diagnostics[0].Code, Is.EqualTo("DYN_EVAL_PHASE_MISMATCH"));
        }

        [Test]
        public void Runtime_RejectsTrackedEvaluationWithoutExplicitStamp()
        {
            var runtime = new DynamicEvaluationRuntime();
            var diagnostics = new RecordingDiagnosticService();
            var source = new CountingSource(DynamicVariant.FromFloat(5f));
            var plan = new DynamicEvaluationPlan
            {
                PlanId = new DynamicEvaluationPlanId(40),
                RootSource = new DynamicSourceHandle(1),
                Phase = DynamicEvaluationPhase.ExplicitRead,
                DependencyMode = DynamicDependencyDeclarationMode.Tracked,
                FallbackPolicy = DynamicFallbackPolicy.Forbidden,
                CachePolicy = DynamicCachePolicy.SharedTracked,
                RequirePlan = true,
                SourceLocation = "Assets/Editor/Tests/DynamicEvaluationRuntimeTests.cs",
            };
            var context = new DynamicEvaluationContext(
                new SimpleDynamicContext(null, null),
                runtime,
                plan,
                DynamicEvaluationPhase.ExplicitRead,
                dependencyStamp: null,
                requirePlan: true,
                diagnostics: diagnostics);

            Assert.That(runtime.TryEvaluate(source, context, out var value), Is.False);
            Assert.That(source.Evaluations, Is.Zero);
            Assert.That(diagnostics.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(diagnostics.Diagnostics[0].Code, Is.EqualTo("DYN_EVAL_STAMP_MISSING"));
        }

        [Test]
        public void Runtime_KeepsDependencySnapshotsPerPlan()
        {
            var runtime = new DynamicEvaluationRuntime();
            var firstChild = new CountingSource(DynamicVariant.FromFloat(1f));
            var secondChild = new CountingSource(DynamicVariant.FromFloat(2f));
            var firstSource = new NestedSource(DynamicValue<float>.FromSource(firstChild));
            var secondSource = new NestedSource(DynamicValue<float>.FromSource(secondChild));

            var firstContext = CreateContext(runtime, planId: 50, extraStamp: 1);
            var secondContext = CreateContext(runtime, planId: 60, extraStamp: 2);

            Assert.That(DynamicValue.FromSource(firstSource).Evaluate(firstContext).AsFloat, Is.EqualTo(2f));
            Assert.That(DynamicValue.FromSource(secondSource).Evaluate(secondContext).AsFloat, Is.EqualTo(3f));

            Assert.That(runtime.TryGetLastDependencies(new DynamicEvaluationPlanId(50), out var firstDependencies), Is.True);
            Assert.That(runtime.TryGetLastDependencies(new DynamicEvaluationPlanId(60), out var secondDependencies), Is.True);
            Assert.That(firstDependencies, Is.Not.Empty);
            Assert.That(secondDependencies, Is.Not.Empty);
        }

        [Test]
        public void Runtime_UsesExplicitDependencyTokensInDependencyStamp()
        {
            var leftScope = new TestScopeNode(new TestIdentityService("scope-a", "group", LifetimeScopeKind.Scene));
            var rightScope = new TestScopeNode(new TestIdentityService("scope-b", "other", LifetimeScopeKind.Entity));
            var tokens = new DynamicDependencyTokenSet(runtimeQueryVersion: 5, scopeVersion: 7, commandVersion: 11, extraVersion: 13);
            var leftContext = new SimpleDynamicContext(null, leftScope, dependencyTokens: tokens);
            var rightContext = new SimpleDynamicContext(null, rightScope, dependencyTokens: tokens);

            var leftStamp = DynamicDependencyStamp.FromContext(leftContext, sourceVersion: 17);
            var rightStamp = DynamicDependencyStamp.FromContext(rightContext, sourceVersion: 17);

            Assert.That(leftStamp, Is.EqualTo(rightStamp));
            Assert.That(leftStamp.RuntimeQueryVersion, Is.EqualTo(5));
            Assert.That(leftStamp.ScopeVersion, Is.EqualTo(rightStamp.ScopeVersion));
            Assert.That(leftStamp.CommandVersion, Is.EqualTo(rightStamp.CommandVersion));
            Assert.That(leftStamp.SourceVersion, Is.EqualTo(17));
            Assert.That(leftStamp.ExtraVersion, Is.EqualTo(13));
            Assert.That(leftStamp.ValueStoreVersion, Is.EqualTo(rightStamp.ValueStoreVersion));
        }

        [Test]
        public void Runtime_DoesNotShareCacheAcrossDifferentOrigins()
        {
            var runtime = new DynamicEvaluationRuntime();
            var source = new CountingSource(DynamicVariant.FromFloat(9f));
            var plan = CreatePlan(65, DynamicEvaluationPhase.ExplicitRead);
            var leftContext = new DynamicEvaluationContext(
                new SimpleDynamicContext(
                    null,
                    new TestScopeNode(new TestIdentityService("left", "group", LifetimeScopeKind.Scene)),
                    origin: new DynamicEvaluationOrigin(101, 201)),
                runtime,
                plan,
                DynamicEvaluationPhase.ExplicitRead,
                new DynamicDependencyStamp(0, 0, 0, 0, 1),
                requirePlan: true);
            var rightContext = new DynamicEvaluationContext(
                new SimpleDynamicContext(
                    null,
                    new TestScopeNode(new TestIdentityService("right", "group", LifetimeScopeKind.Scene)),
                    origin: new DynamicEvaluationOrigin(102, 202)),
                runtime,
                plan,
                DynamicEvaluationPhase.ExplicitRead,
                new DynamicDependencyStamp(0, 0, 0, 0, 1),
                requirePlan: true);

            Assert.That(DynamicValue.FromSource(source).Evaluate(leftContext).AsFloat, Is.EqualTo(9f));
            Assert.That(DynamicValue.FromSource(source).Evaluate(rightContext).AsFloat, Is.EqualTo(9f));
            Assert.That(source.Evaluations, Is.EqualTo(2));
        }

        [Test]
        public void Runtime_RespectsReactivePlanInvalidationThroughDynamicValuePath()
        {
            var runtime = new DynamicEvaluationRuntime();
            var source = new CountingSource(DynamicVariant.FromFloat(13f));
            var value = DynamicValue.FromSource(source);
            var plan = new ReactiveEvaluationPlan
            {
                PlanId = new ReactiveEvaluationPlanId(70),
                RootSource = new DynamicSourceHandle(3),
                Phase = DynamicEvaluationPhase.Tick,
                DependencyMode = DynamicDependencyDeclarationMode.Tracked,
                Invalidation = DynamicInvalidationPolicy.OnDependencyStampChange,
                CachePolicy = DynamicCachePolicy.SharedTracked,
                Scheduling = DynamicSchedulingPolicy.OnDependencyChange,
                SourceLocation = "Assets/Editor/Tests/DynamicEvaluationRuntimeTests.cs",
            };
            var baseContext = new SimpleDynamicContext(
                null,
                new TestScopeNode(new TestIdentityService("reactive", "group", LifetimeScopeKind.Scene)),
                dependencyTokens: new DynamicDependencyTokenSet(scopeVersion: 1),
                origin: new DynamicEvaluationOrigin(501, 0));
            var context = new DynamicEvaluationContext(
                baseContext,
                runtime,
                plan: null,
                phase: DynamicEvaluationPhase.Tick,
                dependencyStamp: DynamicDependencyStamp.FromContext(baseContext, sourceVersion: 3),
                requirePlan: true,
                reactivePlan: plan);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(13f));
            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(13f));
            Assert.That(source.Evaluations, Is.EqualTo(1));

            runtime.InvalidateReactivePlan(plan.PlanId);

            Assert.That(value.Evaluate(context).AsFloat, Is.EqualTo(13f));
            Assert.That(source.Evaluations, Is.EqualTo(2));
        }

        [Test]
        public void Runtime_StoresDependenciesPerReactivePlan()
        {
            var runtime = new DynamicEvaluationRuntime();
            var child = new CountingSource(DynamicVariant.FromFloat(4f));
            var source = new NestedSource(DynamicValue<float>.FromSource(child));
            var plan = new ReactiveEvaluationPlan
            {
                PlanId = new ReactiveEvaluationPlanId(71),
                RootSource = new DynamicSourceHandle(4),
                Phase = DynamicEvaluationPhase.Tick,
                DependencyMode = DynamicDependencyDeclarationMode.Tracked,
                Invalidation = DynamicInvalidationPolicy.OnDependencyStampChange,
                CachePolicy = DynamicCachePolicy.SharedTracked,
                Scheduling = DynamicSchedulingPolicy.OnDependencyChange,
                SourceLocation = "Assets/Editor/Tests/DynamicEvaluationRuntimeTests.cs",
            };
            var baseContext = new SimpleDynamicContext(
                null,
                new TestScopeNode(new TestIdentityService("reactive-snapshot", "group", LifetimeScopeKind.Scene)),
                dependencyTokens: new DynamicDependencyTokenSet(scopeVersion: 2),
                origin: new DynamicEvaluationOrigin(700, 0));
            var context = new DynamicEvaluationContext(
                baseContext,
                runtime,
                plan: null,
                phase: DynamicEvaluationPhase.Tick,
                dependencyStamp: DynamicDependencyStamp.FromContext(baseContext, sourceVersion: 4),
                requirePlan: true,
                reactivePlan: plan);

            Assert.That(DynamicValue.FromSource(source).Evaluate(context).AsFloat, Is.EqualTo(5f));
            Assert.That(runtime.TryGetLastDependencies(plan.PlanId, out var dependencies), Is.True);
            Assert.That(dependencies, Has.Count.EqualTo(1));
        }

        static DynamicEvaluationContext CreateContext(
            DynamicEvaluationRuntime runtime,
            int planId,
            int extraStamp = 0,
            DynamicEvaluationPhase phase = DynamicEvaluationPhase.ExplicitRead,
            DynamicEvaluationPhase planPhase = DynamicEvaluationPhase.ExplicitRead,
            IDynamicEvaluationDiagnosticSink? diagnostics = null)
        {
            var plan = CreatePlan(planId, planPhase);
            var baseContext = new SimpleDynamicContext(
                null,
                null,
                dependencyTokens: new DynamicDependencyTokenSet(scopeVersion: 1),
                origin: new DynamicEvaluationOrigin(planId, 0));

            return new DynamicEvaluationContext(
                baseContext,
                runtime,
                plan,
                phase,
                DynamicDependencyStamp.FromContext(baseContext, sourceVersion: plan.RootSource.Value).WithExtraVersion(extraStamp),
                requirePlan: true,
                diagnostics: diagnostics);
        }

        static DynamicEvaluationPlan CreatePlan(int planId, DynamicEvaluationPhase planPhase)
        {
            return new DynamicEvaluationPlan
            {
                PlanId = new DynamicEvaluationPlanId(planId),
                RootSource = new DynamicSourceHandle(1),
                Phase = planPhase,
                DependencyMode = DynamicDependencyDeclarationMode.Tracked,
                FallbackPolicy = DynamicFallbackPolicy.Forbidden,
                CachePolicy = DynamicCachePolicy.SharedTracked,
                RequirePlan = true,
                SourceLocation = "Assets/Editor/Tests/DynamicEvaluationRuntimeTests.cs",
            };
        }

        static DynamicEvaluationContext CreateConstantStampContext(DynamicEvaluationRuntime runtime, IVarStore vars, int planId)
        {
            return CreateConstantStampContext(runtime, vars, null, planId);
        }

        static DynamicEvaluationContext CreateConstantStampContext(DynamicEvaluationRuntime runtime, IVarStore vars, IScopeNode? scope, int planId)
        {
            var plan = CreatePlan(planId, DynamicEvaluationPhase.ExplicitRead);
            var baseContext = new SimpleDynamicContext(
                vars,
                scope,
                dependencyTokens: new DynamicDependencyTokenSet(scopeVersion: 1),
                origin: new DynamicEvaluationOrigin(planId, 0));

            return new DynamicEvaluationContext(
                baseContext,
                runtime,
                plan,
                DynamicEvaluationPhase.ExplicitRead,
                new DynamicDependencyStamp(0, 0, 0, 0, 1),
                requirePlan: true);
        }

        sealed class CountingSource : IDynamicSource
        {
            readonly DynamicVariant _value;

            public CountingSource(DynamicVariant value)
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

            public string SourceTypeName => nameof(CountingSource);
            public string GetDebugData => Evaluations.ToString();
        }

        sealed class MutableCountingSource : IDynamicSource, IDynamicSourceConfigurationRevisionProvider
        {
            DynamicVariant _value;

            public MutableCountingSource(DynamicVariant value)
            {
                _value = value;
            }

            public int Evaluations { get; private set; }
            public int ConfigurationRevision { get; private set; }

            public void SetValue(DynamicVariant value)
            {
                _value = value;
                ConfigurationRevision++;
            }

            public DynamicVariant Evaluate(IDynamicContext context)
            {
                _ = context;
                Evaluations++;
                return _value;
            }

            public int GetSourceConfigurationRevision() => ConfigurationRevision;
            public string SourceTypeName => nameof(MutableCountingSource);
            public string GetDebugData => $"rev={ConfigurationRevision}; evals={Evaluations}";
        }

        sealed class NestedSource : IDynamicSource, IDynamicSourceConfigurationRevisionProvider
        {
            readonly DynamicValue<float> _child;

            public NestedSource(DynamicValue<float> child)
            {
                _child = child;
            }

            public int Evaluations { get; private set; }

            public DynamicVariant Evaluate(IDynamicContext context)
            {
                Evaluations++;
                return DynamicVariant.FromFloat(_child.GetOrDefault(context, 0f) + 1f);
            }

            public int GetSourceConfigurationRevision() => _child.GetSourceConfigurationRevision();
            public string SourceTypeName => nameof(NestedSource);
            public string GetDebugData => string.Empty;
        }

        sealed class BridgedNestedSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
        {
            readonly DynamicValue<float> _child;

            public BridgedNestedSource(DynamicValue<float> child)
            {
                _child = child;
            }

            public DynamicVariant Evaluate(IDynamicContext context)
            {
                return DynamicVariant.FromFloat(_child.GetOrDefault(context, 0f) + 1f);
            }

            public int GetSourceDependencyRevision(IDynamicContext context)
                => _child.GetSourceDependencyRevision(context);

            public string SourceTypeName => nameof(BridgedNestedSource);
            public string GetDebugData => string.Empty;
        }

        sealed class TableCellValueSource : IDynamicSource, IDynamicSourceDependencyRevisionProvider
        {
            readonly int _tableVarId;
            readonly int _rowIndex;
            readonly int _columnIndex;
            readonly int _varId;

            public TableCellValueSource(int tableVarId, int rowIndex, int columnIndex, int varId)
            {
                _tableVarId = tableVarId;
                _rowIndex = rowIndex;
                _columnIndex = columnIndex;
                _varId = varId;
            }

            public DynamicVariant Evaluate(IDynamicContext context)
            {
                if (context?.Vars == null)
                    return DynamicVariant.Null;

                if (!context.Vars.TryGetTableCellStore(_tableVarId, _rowIndex, _columnIndex, out var cellStore))
                    return DynamicVariant.Null;

                return cellStore.TryGetVariant(_varId, out var value) ? value : DynamicVariant.Null;
            }

            public int GetSourceDependencyRevision(IDynamicContext context)
            {
                if (context?.Vars == null)
                    return 0;

                return context.Vars.TryGetTableCellVersion(_tableVarId, _rowIndex, _columnIndex, out var cellVersion)
                    ? cellVersion
                    : 0;
            }

            public string SourceTypeName => nameof(TableCellValueSource);
            public string GetDebugData => $"table={_tableVarId};row={_rowIndex};col={_columnIndex};var={_varId}";
        }

        sealed class RecordingDiagnosticService : IDynamicEvaluationDiagnosticSink
        {
            public readonly List<DynamicEvaluationDiagnostic> Diagnostics = new();

            public void Report(in DynamicEvaluationDiagnostic diagnostic)
            {
                Diagnostics.Add(diagnostic);
            }
        }

        sealed class TestIdentityService : ILTSIdentityService
        {
            public TestIdentityService(string id, string category, LifetimeScopeKind kind)
            {
                Id = id;
                Category = category;
                Kind = kind;
                IsActive = true;
            }

            public LifetimeScopeKind Kind { get; }
            public string Id { get; }
            public string Category { get; }
            public bool IsActive { get; set; }
            public Transform SelfTransform => null!;
            public float Radius => 0f;
            public TimeScaleBehavior TimeScaleBehavior => TimeScaleBehavior.Scaled;
        }

        sealed class TestScopeNode : IScopeNode
        {
            public TestScopeNode(
                ILTSIdentityService identity,
                IRuntimeResolver? resolver = null,
                IScopeNode? parent = null)
            {
                Identity = identity;
                Resolver = resolver;
                Parent = parent;
            }

            public IScopeNode? Parent { get; }
            public ILTSIdentityService? Identity { get; }
            public LifetimeScopeKind Kind => Identity?.Kind ?? LifetimeScopeKind.None;
            public IRuntimeResolver? Resolver { get; }
            public bool IsVisible => true;
            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return false;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return false;
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
        }

        sealed class TestRuntimeResolver : IRuntimeResolver
        {
            readonly Dictionary<Type, object> _instances = new();

            public void Add<T>(T instance)
            {
                _instances[typeof(T)] = instance!;
            }

            public bool TryResolve(Type type, out object? instance)
            {
                if (_instances.TryGetValue(type, out var exact))
                {
                    instance = exact;
                    return true;
                }

                foreach (var pair in _instances)
                {
                    if (type.IsAssignableFrom(pair.Key) || type.IsInstanceOfType(pair.Value))
                    {
                        instance = pair.Value;
                        return true;
                    }
                }

                instance = null;
                return false;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (TryResolve(typeof(T), out var resolved) && resolved is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default!;
                return false;
            }

            public object Resolve(Type type)
            {
                return TryResolve(type, out var instance)
                    ? instance!
                    : throw new InvalidOperationException($"Type '{type.FullName}' is not registered in the test resolver.");
            }

            public T Resolve<T>()
            {
                return TryResolve<T>(out var instance)
                    ? instance
                    : throw new InvalidOperationException($"Type '{typeof(T).FullName}' is not registered in the test resolver.");
            }

            public object? ResolveOrDefault(Type type)
            {
                TryResolve(type, out var instance);
                return instance;
            }

            public void Inject(object instance)
            {
                _ = instance;
            }

            public void Dispose()
            {
                _instances.Clear();
            }
        }
    }
}
