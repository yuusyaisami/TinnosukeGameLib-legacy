#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelBootBoundaryTests
    {
        [Test]
        public void Execute_ReturnsReadyResult_WhenValidationPassesAndDefaultRuntimeSurfaceIsCreated()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);
            BootValidationInput input = CreatePassingInput(manifest, profile);

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Ready));
            Assert.That(result.IsReady, Is.True);
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            Assert.That(success.RuntimeSurface, Is.InstanceOf<KernelBootRuntimeSurface>());
            Assert.That(success.Context.Manifest, Is.EqualTo(manifest));
            Assert.That(success.Context.SelectedProfile, Is.EqualTo(profile));
            Assert.That(success.Context.ValidationReport.Status, Is.EqualTo(ValidationResultStatus.Passed));

            KernelBootRuntimeSurface runtimeSurface = (KernelBootRuntimeSurface)success.RuntimeSurface;
            Assert.That(runtimeSurface.Runtime.Manifest, Is.EqualTo(manifest));
            Assert.That(runtimeSurface.Runtime.SelectedProfile, Is.EqualTo(profile));
            Assert.That(runtimeSurface.Runtime.Diagnostics.ValidationReport, Is.EqualTo(success.Context.ValidationReport));
            Assert.That(runtimeSurface.Runtime.Diagnostics.DebugMapHash, Is.EqualTo(manifest.ArtifactSet.DebugMapHash));
            Assert.That(runtimeSurface.Runtime.DebugMap, Is.Not.Null);
            Assert.That(runtimeSurface.Runtime.DebugMap.ContentHash.ToString(), Is.EqualTo(manifest.ArtifactSet.DebugMapHash));
            Assert.That(runtimeSurface.Runtime.DebugMap.Entries.Length, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.Diagnostics.HasDiagnostics, Is.False);
            Assert.That(runtimeSurface.Runtime.ServiceGraph.IsEmpty, Is.False);
            Assert.That(runtimeSurface.Runtime.ServiceGraph.ServiceSlots[0].FactoryKind, Is.EqualTo(ServiceFactoryKind.GeneratedFactory));
            Assert.That(runtimeSurface.Runtime.RootScopeGraph.IsEmpty, Is.False);
            Assert.That(runtimeSurface.Runtime.RootScopeGraph.RootScopeCount, Is.EqualTo(1));
        }

        [Test]
        public void Execute_ReturnsEmptyBootShell_WhenValidationPassesForEmptyIr()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);
            BootValidationInput input = CreateEmptyIrInput(manifest, profile);

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Ready));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            KernelBootRuntimeSurface runtimeSurface = (KernelBootRuntimeSurface)success.RuntimeSurface;

            Assert.That(runtimeSurface.Runtime.Diagnostics.ValidationReport.HasBlockingIssues, Is.False);
            Assert.That(runtimeSurface.Runtime.Diagnostics.Diagnostics, Is.Empty);
            Assert.That(runtimeSurface.Runtime.DebugMap, Is.Not.Null);
            Assert.That(runtimeSurface.Runtime.DebugMap.ContentHash.ToString(), Is.EqualTo(manifest.ArtifactSet.DebugMapHash));
            Assert.That(runtimeSurface.Runtime.DebugMap.Entries.Length, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.ServiceGraph.RootServiceCount, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.RootScopeGraph.RootScopeCount, Is.EqualTo(0));
            Assert.That(runtimeSurface.Runtime.ServiceGraph.IsEmpty, Is.True);
            Assert.That(runtimeSurface.Runtime.RootScopeGraph.IsEmpty, Is.True);
        }

        [Test]
        public void Execute_RejectsLegacyFactoryPlan_WhenRuntimeConstructionUsesUnsupportedFactories()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);
            BootValidationInput input = CreateLegacyFactoryInput(manifest, profile);

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Failed));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Failure>());

            KernelBootBoundaryResult.Failure failure = (KernelBootBoundaryResult.Failure)result;
            Assert.That(failure.FailureKind, Is.EqualTo(KernelBootBoundaryFailureKind.RuntimeConstructionFailed));
            Assert.That(failure.Context, Is.Not.Null);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code.Value, Is.EqualTo(KernelBootBoundaryCodes.RuntimeConstructionFailed));
            Assert.That(result.Diagnostics[0].Exception, Is.Not.Null);
            Assert.That(result.Diagnostics[0].Exception!.Message, Does.Contain(KernelRuntimeServiceGraphCodes.ServiceFactoryUnsupported));
        }

        [Test]
        public void Execute_StopsBeforeRuntimeFactory_WhenValidationFails()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(9), KernelProfileKind.Release);
            BootValidationInput input = CreatePassingInput(manifest, profile);

            bool factoryCalled = false;

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input, new DelegatingRuntimeSurfaceFactory(_ =>
            {
                factoryCalled = true;
                return new TestRuntimeSurface();
            }));

            Assert.That(factoryCalled, Is.False);
            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Failed));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Failure>());

            KernelBootBoundaryResult.Failure failure = (KernelBootBoundaryResult.Failure)result;
            Assert.That(failure.FailureKind, Is.EqualTo(KernelBootBoundaryFailureKind.ValidationBlocked));
            Assert.That(failure.Context, Is.Null);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code.Value, Is.EqualTo(BootValidationCodes.ProfileMismatch));
            Assert.That(result.Diagnostics[0].Domain, Is.EqualTo(DiagnosticDomain.Boot));
            Assert.That(result.Diagnostics[0].FailureBoundary, Is.EqualTo(DiagnosticFailureBoundary.Kernel));
        }

        [Test]
        public void Execute_ConvertsRuntimeConstructionExceptionIntoFatalDiagnostic()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);
            BootValidationInput input = CreatePassingInput(manifest, profile);

            bool factoryCalled = false;

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input, new DelegatingRuntimeSurfaceFactory(_ =>
            {
                factoryCalled = true;
                throw new InvalidOperationException("runtime factory failed");
            }));

            Assert.That(factoryCalled, Is.True);
            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Failed));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Failure>());

            KernelBootBoundaryResult.Failure failure = (KernelBootBoundaryResult.Failure)result;
            Assert.That(failure.FailureKind, Is.EqualTo(KernelBootBoundaryFailureKind.RuntimeConstructionFailed));
            Assert.That(failure.Context, Is.Not.Null);
            Assert.That(failure.Context!.Manifest, Is.EqualTo(manifest));
            Assert.That(failure.Context.SelectedProfile, Is.EqualTo(profile));
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code.Value, Is.EqualTo(KernelBootBoundaryCodes.RuntimeConstructionFailed));
            Assert.That(result.Diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Fatal));
            Assert.That(result.Diagnostics[0].Context.ProfileId, Is.EqualTo(profile.Id.Value));
            Assert.That(result.Diagnostics[0].Context.Artifact.ArtifactSetId, Is.EqualTo(manifest.ArtifactSet.ArtifactSetId.Value));
            Assert.That(result.Diagnostics[0].Exception, Is.Not.Null);
            Assert.That(result.Diagnostics[0].Exception!.Type, Does.Contain(nameof(InvalidOperationException)));
            Assert.That(result.Diagnostics[0].Exception!.StackTrace, Is.Null);
            AssertPayloadEntry(result.Diagnostics[0], "BootDiagnosticsDetail", KernelProfileDiagnosticsDetail.MinimalRequired.ToString());
        }

        [Test]
        public void Execute_ConvertsRuntimeConstructionExceptionIntoCapturedDiagnostic_ForTestProfile()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Test);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Test);
            BootValidationInput input = CreatePassingInput(manifest, profile);

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input, new DelegatingRuntimeSurfaceFactory(_ =>
            {
                throw new InvalidOperationException("runtime factory failed");
            }));

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Failed));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Failure>());

            KernelBootBoundaryResult.Failure failure = (KernelBootBoundaryResult.Failure)result;
            Assert.That(failure.FailureKind, Is.EqualTo(KernelBootBoundaryFailureKind.RuntimeConstructionFailed));
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Exception, Is.Not.Null);
            Assert.That(result.Diagnostics[0].Exception!.StackTrace, Is.Not.Null.And.Not.Empty);
            AssertPayloadEntry(result.Diagnostics[0], "BootDiagnosticsDetail", KernelProfileDiagnosticsDetail.FullCaptured.ToString());
        }

        [Test]
        public void Execute_SortsDiagnosticsDeterministically_ForTestProfileValidationFailures()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Test);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Test);
            BootValidationInput input = CreateMismatchedArtifactInput(manifest, profile);

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Failed));
            Assert.That(result.Diagnostics.Count, Is.EqualTo(4));
            Assert.That(result.Diagnostics[0].Code.Value, Is.EqualTo(BootValidationCodes.DebugMapHashMismatch));
            Assert.That(result.Diagnostics[1].Code.Value, Is.EqualTo(BootValidationCodes.KernelIRHashMismatch));
            Assert.That(result.Diagnostics[2].Code.Value, Is.EqualTo(BootValidationCodes.ProfileHashMismatch));
            Assert.That(result.Diagnostics[3].Code.Value, Is.EqualTo(BootValidationCodes.RegistryHashMismatch));
        }

        [Test]
        public void Execute_RejectsNullRuntimeSurface_WhenValidationPasses()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);
            BootValidationInput input = CreatePassingInput(manifest, profile);

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input, new DelegatingRuntimeSurfaceFactory(_ => null));

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Failed));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Failure>());

            KernelBootBoundaryResult.Failure failure = (KernelBootBoundaryResult.Failure)result;
            Assert.That(failure.FailureKind, Is.EqualTo(KernelBootBoundaryFailureKind.RuntimeSurfaceMissing));
            Assert.That(failure.Context, Is.Not.Null);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code.Value, Is.EqualTo(KernelBootBoundaryCodes.RuntimeSurfaceMissing));
            Assert.That(result.Diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Fatal));
        }

        [Test]
        public void Execute_ExposesLifecycleDispatcher_WhenLifecyclePlanIsPresent()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);
            BootValidationInput input = CreatePassingInput(manifest, profile, CreateLifecyclePlan());

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Ready));
            Assert.That(result, Is.InstanceOf<KernelBootBoundaryResult.Success>());

            KernelBootBoundaryResult.Success success = (KernelBootBoundaryResult.Success)result;
            Assert.That(success.RuntimeSurface.LifecycleDispatcher, Is.Not.Null);
            Assert.That(success.RuntimeSurface.LifecycleDispatcher!.LifecyclePlan.Header.PlanId.Value, Is.EqualTo(41));
            Assert.That(success.RuntimeSurface.LifecyclePlanResolver.TryGetLifecycleDispatcher(new LifecyclePlanId(41), out KernelLifecycleDispatcher? resolvedDispatcher), Is.True);
            Assert.That(resolvedDispatcher, Is.Not.Null);
            Assert.That(resolvedDispatcher!.LifecyclePlan.Header.PlanId.Value, Is.EqualTo(41));
        }

        static BootValidationInput CreatePassingInput(KernelBootManifest manifest, KernelProfile profile, LifecyclePlan? lifecyclePlan = null)
        {
            ServiceGraphPlan serviceGraphPlan = CreateServiceGraphPlan(ServiceFactoryKind.GeneratedFactory);

            return new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: new BootArtifactValidationState(
                    artifactSetComplete: true,
                    artifactHeadersCompatible: true,
                    artifactStale: false,
                    debugMapRequired: true,
                    kernelIRHash: manifest.ArtifactSet.KernelIRHash,
                    registryHash: manifest.ArtifactSet.RegistryHash,
                    profileHash: manifest.ArtifactSet.ProfileHash,
                    debugMapHash: manifest.ArtifactSet.DebugMapHash),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    new[] { ServiceIdentity(11) },
                    new[] { ScopeIdentity(21) },
                    new[] { ScopeIdentity(21) }),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false),
                serviceGraphPlan: serviceGraphPlan,
                scopeGraphPlan: CreateScopeGraphPlan(new[] { CreateScope(21, 21, ScopeKind.Root, 0, 41, 21, false) }),
                lifecyclePlan: lifecyclePlan,
                debugMap: CreateDebugMap(manifest));
        }

        static BootValidationInput CreateEmptyIrInput(KernelBootManifest manifest, KernelProfile profile)
        {
            ServiceGraphPlan serviceGraphPlan = CreateEmptyServiceGraphPlan();

            return new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: new BootArtifactValidationState(
                    artifactSetComplete: true,
                    artifactHeadersCompatible: true,
                    artifactStale: false,
                    debugMapRequired: true,
                    kernelIRHash: manifest.ArtifactSet.KernelIRHash,
                    registryHash: manifest.ArtifactSet.RegistryHash,
                    profileHash: manifest.ArtifactSet.ProfileHash,
                    debugMapHash: manifest.ArtifactSet.DebugMapHash),
                rootState: new BootRootValidationState(
                    Array.Empty<RuntimeIdentityRef>(),
                    Array.Empty<RuntimeIdentityRef>(),
                    Array.Empty<RuntimeIdentityRef>(),
                    Array.Empty<RuntimeIdentityRef>()),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false),
                serviceGraphPlan: serviceGraphPlan,
                scopeGraphPlan: CreateScopeGraphPlan(Array.Empty<ScopeIR>()),
                debugMap: CreateDebugMap(manifest));
        }

        static BootValidationInput CreateMismatchedArtifactInput(KernelBootManifest manifest, KernelProfile profile)
        {
            ServiceGraphPlan serviceGraphPlan = CreateServiceGraphPlan(ServiceFactoryKind.GeneratedFactory);
            KernelDebugMap debugMap = CreateDebugMap(manifest, includeServiceEntry: true);

            return new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: new BootArtifactValidationState(
                    artifactSetComplete: true,
                    artifactHeadersCompatible: true,
                    artifactStale: false,
                    debugMapRequired: true,
                    kernelIRHash: new UnityEngine.Hash128(9, 9, 9, 9).ToString(),
                    registryHash: new UnityEngine.Hash128(8, 8, 8, 8).ToString(),
                    profileHash: new UnityEngine.Hash128(7, 7, 7, 7).ToString(),
                    debugMapHash: debugMap.ContentHash.ToString()),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    new[] { ServiceIdentity(11) },
                    new[] { ScopeIdentity(21) },
                    new[] { ScopeIdentity(21) }),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false),
                serviceGraphPlan: serviceGraphPlan,
                scopeGraphPlan: CreateScopeGraphPlan(new[] { CreateScope(21, 21, ScopeKind.Root, 0, 41, 21, false) }),
                debugMap: debugMap);
        }

        static BootValidationInput CreateLegacyFactoryInput(KernelBootManifest manifest, KernelProfile profile)
        {
            return new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: new BootArtifactValidationState(
                    artifactSetComplete: true,
                    artifactHeadersCompatible: true,
                    artifactStale: false,
                    debugMapRequired: true,
                    kernelIRHash: manifest.ArtifactSet.KernelIRHash,
                    registryHash: manifest.ArtifactSet.RegistryHash,
                    profileHash: manifest.ArtifactSet.ProfileHash,
                    debugMapHash: manifest.ArtifactSet.DebugMapHash),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    new[] { ServiceIdentity(11) },
                    new[] { ScopeIdentity(21) },
                    new[] { ScopeIdentity(21) }),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false),
                serviceGraphPlan: CreateServiceGraphPlan(ServiceFactoryKind.LegacyAdapter),
                scopeGraphPlan: CreateScopeGraphPlan(new[] { CreateScope(21, 21, ScopeKind.Root, 0, 41, 21, false) }),
                debugMap: CreateDebugMap(manifest));
        }

        [Test]
        public void Execute_BlocksWhenVerifiedDebugMapInputIsMissing()
        {
            KernelBootManifest manifest = CreateManifest(new KernelProfileId(7), KernelProfileKind.Release);
            KernelProfile profile = new KernelProfile(new KernelProfileId(7), KernelProfileKind.Release);

            BootValidationInput input = new BootValidationInput(
                manifest,
                profile,
                artifactSetReferencePresent: true,
                dependencyValidationStatus: ValidationResultStatus.Passed,
                artifactState: new BootArtifactValidationState(
                    artifactSetComplete: true,
                    artifactHeadersCompatible: true,
                    artifactStale: false,
                    debugMapRequired: true,
                    kernelIRHash: manifest.ArtifactSet.KernelIRHash,
                    registryHash: manifest.ArtifactSet.RegistryHash,
                    profileHash: manifest.ArtifactSet.ProfileHash,
                    debugMapHash: manifest.ArtifactSet.DebugMapHash),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    new[] { ServiceIdentity(11) },
                    new[] { ScopeIdentity(21) },
                    new[] { ScopeIdentity(21) }),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false),
                serviceGraphPlan: CreateServiceGraphPlan(ServiceFactoryKind.GeneratedFactory),
                scopeGraphPlan: CreateScopeGraphPlan(new[] { CreateScope(21, 21, ScopeKind.Root, 0, 41, 21, false) }));

            KernelBootBoundaryResult result = KernelBootBoundary.Execute(input);

            Assert.That(result.Status, Is.EqualTo(KernelBootBoundaryStatus.Failed));
            Assert.That(result.Diagnostics, Has.Some.Matches<KernelDiagnostic>(diagnostic => diagnostic.Code.Value == BootValidationCodes.DebugMapInputMissing));
        }

        static ServiceGraphPlan CreateEmptyServiceGraphPlan()
        {
            return CreateServiceGraphPlan(Array.Empty<ServiceIR>());
        }

        static ScopeGraphPlan CreateScopeGraphPlan(ScopeIR[] scopes)
        {
            Hash128 generatedHash = KernelProjectionHashing.ComputeScopeGraphHash(scopes);

            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(32),
                new ArtifactSetId(11),
                new ArtifactId(2),
                ArtifactKind.ScopeGraph,
                11,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 9, 9, 9),
                new Hash128(6, 6, 6, 6),
                generatedHash,
                "KernelBootBoundaryTests");

            return new ScopeGraphPlan(header, scopes);
        }

        static LifecyclePlan CreateLifecyclePlan()
        {
            LifecycleIR lifecycle = CreateLifecycle(
                41,
                "BootLifecycle",
                new[]
                {
                    CreateStep(42, LifecyclePhase.Boot, 10, new LifecycleTargetRefIR(new ScopePlanId(21)), LifecycleActionKind.ScopeStateTransition, 4101),
                });

            Hash128 generatedHash = KernelProjectionHashing.ComputeLifecyclePlanHash(new[] { lifecycle });

            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(41),
                new ArtifactSetId(11),
                new ArtifactId(3),
                ArtifactKind.LifecyclePlan,
                11,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 9, 9, 9),
                new Hash128(6, 6, 6, 6),
                generatedHash,
                "KernelBootBoundaryTests");

            return new LifecyclePlan(header, new[] { lifecycle });
        }

        static LifecycleIR CreateLifecycle(int lifecycleId, string name, LifecycleStepIR[] steps)
        {
            return new LifecycleIR(
                new LifecyclePlanId(lifecycleId),
                name,
                new ModuleId(10),
                steps,
                new SourceLocationId(lifecycleId),
                LifecycleFailurePolicy.FailOperation);
        }

        static LifecycleStepIR CreateStep(int stepId, LifecyclePhase phase, int order, LifecycleTargetRefIR target, LifecycleActionKind actionKind, int sourceId)
        {
            return new LifecycleStepIR(
                new LifecycleStepId(stepId),
                phase,
                order,
                target,
                actionKind,
                null,
                new SourceLocationId(sourceId));
        }

        static ScopeIR CreateScope(int authoringId, int planId, ScopeKind kind, int parentAuthoringId, int lifecyclePlanId, int sourceId, bool ownedLocal)
        {
            return new ScopeIR(
                new ScopeAuthoringId(authoringId),
                new ScopePlanId(planId),
                "Scope" + planId,
                kind,
                new ModuleId(10),
                parentAuthoringId == 0 ? default : new ScopeAuthoringId(parentAuthoringId),
                Array.Empty<ScopeServiceRequirementIR>(),
                Array.Empty<ScopeValueInitRefIR>(),
                ownedLocal
                    ? new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(sourceId))
                    : new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.Detached, 0, new SourceLocationId(sourceId)),
                new LifecyclePlanRefIR(new LifecyclePlanId(lifecyclePlanId), new SourceLocationId(sourceId + 100)),
                new SourceLocationId(sourceId));
        }

        static ServiceGraphPlan CreateServiceGraphPlan(ServiceFactoryKind factoryKind)
        {
            return CreateServiceGraphPlan(new[] { CreateService(11, factoryKind) });
        }

        static ServiceGraphPlan CreateServiceGraphPlan(ServiceIR[] services)
        {
            Hash128 generatedHash = KernelProjectionHashing.ComputeServiceGraphHash(services);

            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                new PlanId(31),
                new ArtifactSetId(11),
                new ArtifactId(1),
                ArtifactKind.ServiceGraph,
                11,
                new Hash128(1, 2, 3, 4),
                new Hash128(5, 6, 7, 8),
                new Hash128(9, 9, 9, 9),
                new Hash128(6, 6, 6, 6),
                generatedHash,
                "KernelBootBoundaryTests");

            return new ServiceGraphPlan(header, services);
        }

        static ServiceIR CreateService(int serviceId, ServiceFactoryKind factoryKind)
        {
            return new ServiceIR(
                new ServiceId(serviceId),
                "Service" + serviceId,
                ServiceLifetimeKind.Singleton,
                new ModuleId(10),
                new[] { new ServiceContractIR("IService" + serviceId, new SourceLocationId(serviceId)) },
                Array.Empty<ServiceDependencyIR>(),
                factoryKind,
                new SourceLocationId(serviceId),
                ServiceCardinalityKind.SingletonGlobal);
        }

        static KernelBootManifest CreateManifest(KernelProfileId profileId, KernelProfileKind profileKind)
        {
            string debugMapHash = EmptyDebugMapHash();

            VerifiedArtifactSetRef artifactSet = new VerifiedArtifactSetRef(
                new ArtifactSetId(11),
                new PlanId(31),
                new UnityEngine.Hash128(1, 2, 3, 4).ToString(),
                new UnityEngine.Hash128(5, 6, 7, 8).ToString(),
                11,
                new UnityEngine.Hash128(9, 9, 9, 9).ToString(),
                debugMapHash);

            return new KernelBootManifest(
                new ManifestId(5),
                profileId,
                artifactSet,
                new BootPolicyId(9),
                BootDiagnosticsPolicy.ForKind(profileKind));
        }

        static RuntimeIdentityRef ServiceIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.Service, value);
        }

        static RuntimeIdentityRef ScopeIdentity(int value)
        {
            return new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, value);
        }

        static string EmptyDebugMapHash()
        {
            return VerifiedArtifactHeaderHashing.ComputeGeneratedHash(Array.Empty<string>()).ToString();
        }

        static KernelDebugMap CreateDebugMap(KernelBootManifest manifest, bool includeServiceEntry = false)
        {
            KernelDebugMapEntry[] entries = includeServiceEntry
                ? new[]
                {
                    new KernelDebugMapEntry(
                        ServiceIdentity(11),
                        "Service11",
                        new ModuleId(10),
                        new SourceLocationId(11),
                        KernelProfileMask.Release,
                        new Hash128(7, 7, 7, 7))
                }
                : Array.Empty<KernelDebugMapEntry>();

            Hash128 contentHash = KernelProjectionHashing.ComputeDebugMapHash(entries);
            VerifiedArtifactHeader header = new VerifiedArtifactHeader(
                manifest.ArtifactSet.PlanId,
                manifest.ArtifactSet.ArtifactSetId,
                new ArtifactId(7),
                ArtifactKind.KernelDebugMap,
                manifest.ArtifactSet.FormatVersion,
                Hash128Serialization.Parse(manifest.ArtifactSet.KernelIRHash),
                Hash128Serialization.Parse(manifest.ArtifactSet.RegistryHash!),
                Hash128Serialization.Parse(manifest.ArtifactSet.ProfileHash),
                contentHash,
                contentHash,
                "KernelBootBoundaryTests");

            return new KernelDebugMap(header, entries);
        }

        static void AssertPayloadEntry(KernelDiagnostic diagnostic, string key, string expectedValue)
        {
            for (int index = 0; index < diagnostic.Payload.Entries.Count; index++)
            {
                if (diagnostic.Payload.Entries[index].Key != key)
                    continue;

                Assert.That(diagnostic.Payload.Entries[index].Value.ToString(), Is.EqualTo(expectedValue), key);
                return;
            }

            Assert.Fail("Missing payload entry: " + key);
        }

        sealed class TestRuntimeSurface : IKernelBootRuntimeSurface
        {
            public KernelLifecycleDispatcher? LifecycleDispatcher => null;

            public ILifecyclePlanResolver LifecyclePlanResolver => throw new NotImplementedException();

            public Task<LifecycleDispatchResult> DispatchAllLifecycleAsync(IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<LifecycleDispatchResult> DispatchPhaseLifecycleAsync(LifecyclePhase phase, IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        sealed class DelegatingRuntimeSurfaceFactory : IKernelBootRuntimeSurfaceFactory
        {
            readonly Func<KernelBootBoundaryContext, IKernelBootRuntimeSurface> factory;

            public DelegatingRuntimeSurfaceFactory(Func<KernelBootBoundaryContext, IKernelBootRuntimeSurface> factory)
            {
                this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            public IKernelBootRuntimeSurface Create(KernelBootBoundaryContext context)
            {
                return factory(context);
            }
        }
    }
}