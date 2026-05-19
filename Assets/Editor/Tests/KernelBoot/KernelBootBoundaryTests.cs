#nullable enable
using System;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
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
            Assert.That(runtimeSurface.Runtime.RootScopeGraph.IsEmpty, Is.False);
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
            Assert.That(result.Diagnostics[0].Context.Artifact.Value, Is.EqualTo(manifest.ArtifactSet.ArtifactSetId.Value));
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

        static BootValidationInput CreatePassingInput(KernelBootManifest manifest, KernelProfile profile)
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
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false));
        }

        static BootValidationInput CreateEmptyIrInput(KernelBootManifest manifest, KernelProfile profile)
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
                    Array.Empty<RuntimeIdentityRef>(),
                    Array.Empty<RuntimeIdentityRef>(),
                    Array.Empty<RuntimeIdentityRef>(),
                    Array.Empty<RuntimeIdentityRef>()),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false));
        }

        static BootValidationInput CreateMismatchedArtifactInput(KernelBootManifest manifest, KernelProfile profile)
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
                    kernelIRHash: new UnityEngine.Hash128(9, 9, 9, 9).ToString(),
                    registryHash: new UnityEngine.Hash128(8, 8, 8, 8).ToString(),
                    profileHash: new UnityEngine.Hash128(7, 7, 7, 7).ToString(),
                    debugMapHash: new UnityEngine.Hash128(6, 6, 6, 6).ToString()),
                rootState: new BootRootValidationState(
                    new[] { ServiceIdentity(11) },
                    new[] { ServiceIdentity(11) },
                    new[] { ScopeIdentity(21) },
                    new[] { ScopeIdentity(21) }),
                fallbackState: new BootFallbackValidationState(false, false, false, false, false, false));
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