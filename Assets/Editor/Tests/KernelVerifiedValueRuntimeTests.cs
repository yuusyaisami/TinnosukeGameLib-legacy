#nullable enable
using System;
using Game;
using Game.Common;
using Game.DI;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using Game.Project.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using Game.Kernel.Abstractions;
using KernelHash128 = Game.Kernel.IR.Hash128;
using KernelValueKind = Game.Kernel.IR.ValueKind;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelVerifiedValueRuntimeTests
    {
        const int RootScopePlanIdValue = 8201;
        const int ValueInitPlanIdValue = 8202;
        const int ValueKeyIdValue = 8301;

        [SetUp]
        public void SetUp()
        {
            KernelVerifiedValueRuntime.Deactivate();
            KernelVerifiedCompositionRuntime.Deactivate();
        }

        [TearDown]
        public void TearDown()
        {
            KernelVerifiedValueRuntime.Deactivate();
            KernelVerifiedCompositionRuntime.Deactivate();
        }

        [Test]
        public void Activate_ThrowsWhenRequiredValueSchemaPlanIsMissing()
        {
            KernelBootRuntimeSurface runtimeSurface = CreateRuntimeSurface(includeValueSchemaPlan: false, useDynamicEntry: false);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                KernelVerifiedValueRuntime.Activate(runtimeSurface));

            Assert.That(exception!.Message, Does.Contain("ValueSchemaPlan"));
        }

        [Test]
        public void Activate_ResolvesVerifiedStableKeyAndAppliesLiteralLocalInit()
        {
            KernelBootRuntimeSurface runtimeSurface = CreateRuntimeSurface(includeValueSchemaPlan: true, useDynamicEntry: false);

            KernelVerifiedCompositionRuntime.Activate(runtimeSurface);
            KernelVerifiedValueRuntime.Activate(runtimeSurface);

            GameObject scopeObject = new GameObject("verified-value-scope");
            try
            {
                TestScope scope = scopeObject.AddComponent<TestScope>();
                Assert.That(KernelVerifiedCompositionRuntime.TryBindRootScope(scope, new ScopePlanId(RootScopePlanIdValue)), Is.True);

                Assert.That(VerifiedValueRuntimeBridge.TryGetSession(out IVerifiedValueRuntimeSession? session), Is.True);
                Assert.That(session, Is.Not.Null);
                Assert.That(session!.TryResolveValueKey("value.test", out int valueKeyId), Is.True);
                Assert.That(valueKeyId, Is.EqualTo(ValueKeyIdValue));

                BlackboardService blackboard = new BlackboardService(scope);
                DynamicEvaluationRuntime evaluationRuntime = new DynamicEvaluationRuntime();

                VerifiedValueInitApplyResult result = session.ApplyLocalBlackboardInit(scope, blackboard, VerifiedValueInitPhase.Create, evaluationRuntime);

                Assert.That(result.Kind, Is.EqualTo(VerifiedValueInitApplyResultKind.Applied));
                Assert.That(result.AppliedEntryCount, Is.EqualTo(1));
                Assert.That(blackboard.LocalVars.TryGetVariant(ValueKeyIdValue, out DynamicVariant value), Is.True);
                Assert.That(value.AsInt, Is.EqualTo(7));

                Assert.That(VarIdResolver.TryResolve("value.test", out int resolvedVarId), Is.True);
                Assert.That(resolvedVarId, Is.EqualTo(ValueKeyIdValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        [Test]
        public void ApplyLocalBlackboardInit_RejectsDynamicEntriesWithoutEvaluationPlanAuthority()
        {
            KernelBootRuntimeSurface runtimeSurface = CreateRuntimeSurface(includeValueSchemaPlan: true, useDynamicEntry: true);

            KernelVerifiedCompositionRuntime.Activate(runtimeSurface);
            KernelVerifiedValueRuntime.Activate(runtimeSurface);

            GameObject scopeObject = new GameObject("verified-dynamic-value-scope");
            try
            {
                TestScope scope = scopeObject.AddComponent<TestScope>();
                Assert.That(KernelVerifiedCompositionRuntime.TryBindRootScope(scope, new ScopePlanId(RootScopePlanIdValue)), Is.True);

                Assert.That(VerifiedValueRuntimeBridge.TryGetSession(out IVerifiedValueRuntimeSession? session), Is.True);
                Assert.That(session, Is.Not.Null);

                BlackboardService blackboard = new BlackboardService(scope);
                VerifiedValueInitApplyResult result = session!.ApplyLocalBlackboardInit(scope, blackboard, VerifiedValueInitPhase.Create, new DynamicEvaluationRuntime());

                Assert.That(result.Kind, Is.EqualTo(VerifiedValueInitApplyResultKind.Rejected));
                Assert.That(result.FailureReason, Does.Contain("evaluation-plan"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scopeObject);
            }
        }

        static KernelBootRuntimeSurface CreateRuntimeSurface(bool includeValueSchemaPlan, bool useDynamicEntry)
        {
            KernelBootPublishedArtifactBundle bundle = CreateBundle(includeValueSchemaPlan, useDynamicEntry);
            BootValidationInput input = bundle.CreateValidationInput(new BootFallbackValidationState(false, false, false, false, false, false));
            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result, Is.TypeOf<KernelBootBoundaryResult.Success>());
            return (KernelBootRuntimeSurface)((KernelBootBoundaryResult.Success)result).RuntimeSurface;
        }

        static KernelBootPublishedArtifactBundle CreateBundle(bool includeValueSchemaPlan, bool useDynamicEntry)
        {
            KernelProfile profile = new KernelProfile(new KernelProfileId(82001), KernelProfileKind.Development);

            ValueInitPlanIR valueInitPlan = CreateValueInitPlan(useDynamicEntry);
            ScopeIR[] scopes =
            {
                CreateRootScope(
                    RootScopePlanIdValue,
                    authoringId: 1,
                    sourceId: 41,
                    valueInitRefs: new[]
                    {
                        new ScopeValueInitRefIR(new ValueInitPlanId(ValueInitPlanIdValue), new SourceLocationId(42)),
                    }),
            };

            ServiceIR[] services = Array.Empty<ServiceIR>();
            ValueInitPlanIR[] valueInitPlans = { valueInitPlan };
            KernelDebugMapEntry[] debugEntries = Array.Empty<KernelDebugMapEntry>();
            ValueKeyIR[] valueKeys =
            {
                new ValueKeyIR(
                    new ValueKeyId(ValueKeyIdValue),
                    "value.test",
                    "Value Test",
                    KernelValueKind.Int,
                    new ModuleId(82010),
                    new ValueSchemaRefIR(new ValueSchemaId(82011), new SourceLocationId(82012)),
                    new SavePolicyIR(false, false, null),
                    new SourceLocationId(82013)),
            };

            KernelHash128 sourceHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedValueRuntimeTests",
                includeValueSchemaPlan ? "ValueSchema:Present" : "ValueSchema:Missing",
                useDynamicEntry ? "Entry:Dynamic" : "Entry:Literal",
            });

            KernelHash128 registryHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedValueRuntimeTests",
                "Registry:82001",
            });

            KernelHash128 profileHash = VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[]
            {
                "KernelVerifiedValueRuntimeTests",
                "Profile:" + profile.Kind,
            });

            KernelHash128 scopeHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes, valueInitPlans);
            KernelHash128 serviceHash = KernelProjectionHashing.ComputeServiceGraphHash(services);
            KernelHash128 valueSchemaHash = KernelProjectionHashing.ComputeValueSchemaHash(valueKeys);
            KernelHash128 runtimeQueryHash = KernelProjectionHashing.ComputeRuntimeQueryHash(Array.Empty<RuntimeQueryIR>());
            KernelHash128 debugMapHash = KernelProjectionHashing.ComputeDebugMapHash(debugEntries);

            ServiceGraphPlan serviceGraphPlan = new ServiceGraphPlan(
                CreateHeader(new ArtifactId(1), ArtifactKind.ServiceGraph, sourceHash, registryHash, profileHash, debugMapHash, serviceHash),
                services);

            ScopeGraphPlan scopeGraphPlan = new ScopeGraphPlan(
                CreateHeader(new ArtifactId(2), ArtifactKind.ScopeGraph, sourceHash, registryHash, profileHash, debugMapHash, scopeHash),
                scopes,
                valueInitPlans);

            CommandCatalogPlan commandCatalogPlan = new CommandCatalogPlan(
                CreateHeader(new ArtifactId(4), ArtifactKind.CommandCatalog, sourceHash, registryHash, profileHash, debugMapHash, ComputeHash("empty-command-catalog-82001")),
                Array.Empty<CommandIR>());

            ValueSchemaPlan? valueSchemaPlan = includeValueSchemaPlan
                ? new ValueSchemaPlan(
                    CreateHeader(new ArtifactId(5), ArtifactKind.ValueSchema, sourceHash, registryHash, profileHash, debugMapHash, valueSchemaHash),
                    valueKeys)
                : null;

            RuntimeQueryPlan runtimeQueryPlan = new RuntimeQueryPlan(
                CreateHeader(new ArtifactId(6), ArtifactKind.RuntimeQuery, sourceHash, registryHash, profileHash, debugMapHash, runtimeQueryHash),
                Array.Empty<RuntimeQueryIR>());

            KernelDebugMap debugMap = new KernelDebugMap(
                CreateHeader(new ArtifactId(7), ArtifactKind.KernelDebugMap, sourceHash, registryHash, profileHash, debugMapHash, debugMapHash),
                debugEntries);

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                new ArtifactSetId(82001),
                new PlanId(82001),
                sourceHash.ToString(),
                profileHash.ToString(),
                1,
                registryHash.ToString(),
                debugMapHash.ToString());

            KernelBootManifest manifest = new KernelBootManifest(
                new ManifestId(82001),
                profile.Id,
                artifactSet,
                new BootPolicyId(82001),
                BootDiagnosticsPolicy.ForKind(profile.Kind));

            return new KernelBootPublishedArtifactBundle(
                manifest,
                profile,
                serviceGraphPlan,
                scopeGraphPlan,
                lifecyclePlan: null,
                debugMap,
                commandCatalogPlan: commandCatalogPlan,
                valueSchemaPlan: valueSchemaPlan,
                runtimeQueryPlan: runtimeQueryPlan,
                availableRootScopes: new[]
                {
                    ScopeIdentity(RootScopePlanIdValue),
                });
        }

        static VerifiedArtifactHeader CreateHeader(
            ArtifactId artifactId,
            ArtifactKind artifactKind,
            KernelHash128 sourceHash,
            KernelHash128 registryHash,
            KernelHash128 profileHash,
            KernelHash128 debugMapHash,
            KernelHash128 contentHash)
        {
            return new VerifiedArtifactHeader(
                new PlanId(82001),
                new ArtifactSetId(82001),
                artifactId,
                artifactKind,
                1,
                sourceHash,
                registryHash,
                profileHash,
                debugMapHash,
                contentHash,
                "KernelVerifiedValueRuntimeTests");
        }

        static KernelHash128 ComputeHash(string value)
        {
            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(new[] { value });
        }

        static ValueInitPlanIR CreateValueInitPlan(bool useDynamicEntry)
        {
            ValueInitEntryIR entry = useDynamicEntry
                ? new ValueInitEntryIR(
                    new ValueKeyId(ValueKeyIdValue),
                    ValueInitEntrySourceKind.DynamicEvaluation,
                    KernelValueKind.Int,
                    10,
                    ValueInitOverwritePolicy.Overwrite,
                    new SourceLocationId(82022),
                    evaluationLocalRef: "battle.value.verified.local")
                : new ValueInitEntryIR(
                    new ValueKeyId(ValueKeyIdValue),
                    ValueInitEntrySourceKind.Literal,
                    KernelValueKind.Int,
                    10,
                    ValueInitOverwritePolicy.Overwrite,
                    new SourceLocationId(82022),
                    serializedValue: "7");

            return new ValueInitPlanIR(
                new ValueInitPlanId(ValueInitPlanIdValue),
                new ModuleId(82020),
                new ScopePlanId(RootScopePlanIdValue),
                "local:blackboard",
                LifecyclePhase.Create,
                10,
                new AvailabilityIR(KernelProfileMask.Development, true, null),
                new[] { entry },
                new SourceLocationId(82021));
        }

        static ScopeIR CreateRootScope(int planId, int authoringId, int sourceId, ScopeValueInitRefIR[]? valueInitRefs = null)
        {
            return new ScopeIR(
                new ScopeAuthoringId(authoringId),
                new ScopePlanId(planId),
                "Scope" + planId,
                ScopeKind.Root,
                new ModuleId(10),
                default,
                Array.Empty<ScopeServiceRequirementIR>(),
                valueInitRefs ?? Array.Empty<ScopeValueInitRefIR>(),
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(sourceId)),
                new LifecyclePlanRefIR(new LifecyclePlanId(sourceId + 100), new SourceLocationId(sourceId + 200)),
                new SourceLocationId(sourceId));
        }

        static RuntimeIdentityRef ScopeIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, value);
        }

        [DisallowMultipleComponent]
        sealed class TestScope : KernelScopeHost
        {
            protected override bool UseBuildCoordinator => false;

            protected override bool AutoBuildOnAwake => false;

            protected override void ConfigureBase(IRuntimeContainerBuilder builder)
            {
                _ = builder;
            }
        }
    }
}
