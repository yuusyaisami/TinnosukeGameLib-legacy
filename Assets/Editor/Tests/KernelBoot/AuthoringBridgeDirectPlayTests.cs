#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Game;
using Game.Kernel.Abstractions;
using Game.Kernel.Authoring;
using Game.Kernel.Boot;
using Game.Kernel.Contributions;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Game.Kernel.Validation;
using NUnit.Framework;
using TinnosukeGameLib.Editor.KernelBoot;
using UnityEngine;

using KernelHash128 = Game.Kernel.IR.Hash128;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class AuthoringBridgeDirectPlayTests
    {
        const string ButtonChannelHubServiceName = "ButtonChannelHubService";
        const string ButtonChannelHubServiceContractName = "Game.UI.IButtonChannelHubService";

        [Test]
        public void PrepareDirectPlay_BootsVerifiedPlanAndCommitsAfterBoot()
        {
            GameObject rootObject = new GameObject("DirectPlayRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 101, "DirectPlayModule", "Assets/Scenes/DirectPlay.unity", "DirectPlayRoot", "ScopeAuthoringRoot", "module");

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlay.unity",
                    12001,
                    "Assets/Scenes/DirectPlay.unity",
                    "DirectPlayRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                KernelIR kernelIR = CreateKernelIR();
                FakeKernelBootRuntimeSurfaceFactory runtimeSurfaceFactory = new FakeKernelBootRuntimeSurfaceFactory();

                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404),
                    ArtifactSetPublicationState.Empty,
                    runtimeSurfaceFactory);

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input);

                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.FailedStage, Is.EqualTo(AuthoringDirectPlayStage.None));
                Assert.That(result.ExtractionReport.IsValid, Is.True);
                Assert.That(result.NormalizationReport.IsValid, Is.True);
                Assert.That(result.DependencyValidationReport.Status, Is.EqualTo(ValidationResultStatus.Passed));
                Assert.That(result.GenerationResult, Is.Not.Null);
                Assert.That(result.GenerationResult!.IsVerified, Is.True);
                Assert.That(result.PromotionStageResult, Is.Not.Null);
                Assert.That(result.PromotionStageResult!.IsSuccessful, Is.True);
                Assert.That(result.PromotionStageResult!.IsStaged, Is.True);
                Assert.That(result.BootValidationReport, Is.Not.Null);
                Assert.That(result.BootValidationReport!.HasBlockingIssues, Is.False);
                Assert.That(result.BootBoundaryResult, Is.Not.Null);
                Assert.That(result.BootBoundaryResult!.IsReady, Is.True);
                Assert.That(result.PromotionCommitResult, Is.Not.Null);
                Assert.That(result.PromotionCommitResult!.IsPromoted, Is.True);
                Assert.That(result.PromotionCommitResult.PublicationState.Current, Is.EqualTo(result.PromotionStageResult.StagingRecord!.Candidate));
                Assert.That(result.Manifest, Is.Not.Null);
                Assert.That(result.Manifest!.ProfileId, Is.EqualTo(input.Profile.Id));
                Assert.That(result.Manifest.ArtifactSet.KernelIRHash, Is.EqualTo(result.GenerationResult!.GeneratedPlan.Header.SourceHash.ToString()));
                Assert.That(result.Manifest.ArtifactSet.PlanId, Is.EqualTo(result.PromotionStageResult.StagingRecord!.Candidate.Header.PlanId));
                Assert.That(runtimeSurfaceFactory.CreatedContext, Is.Not.Null);
                Assert.That(runtimeSurfaceFactory.CreatedContext!.Manifest, Is.EqualTo(result.Manifest));
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.RootState.AvailableRootServices.Length, Is.GreaterThan(0));
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.RootState.AvailableRootScopes.Length, Is.GreaterThan(0));
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.EntityRegistrationPlan, Is.Not.Null);
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.EntityRegistrationPlan!.Entries.Length, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(linkObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_ExtractsCommandExecutorBindingsAndCarriesExecutorTableIntoBoot()
        {
            GameObject rootObject = new GameObject("DirectPlayCommandRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 111, "DirectPlayCommandModule", "Assets/Scenes/DirectPlayCommand.unity", "DirectPlayCommandRoot", "ScopeAuthoringRoot", "module");
                AddCommandRunnerInstaller(rootObject);

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlayCommand.unity",
                    12001,
                    "Assets/Scenes/DirectPlayCommand.unity",
                    "DirectPlayCommandRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                int debugCommandExecutorId = GetDebugCommandExecutorId();
                KernelIR kernelIR = CreateKernelIR(debugCommandExecutorId);
                FakeKernelBootRuntimeSurfaceFactory runtimeSurfaceFactory = new FakeKernelBootRuntimeSurfaceFactory();

                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404),
                    ArtifactSetPublicationState.Empty,
                    runtimeSurfaceFactory);

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input);

                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.GenerationResult, Is.Not.Null);
                Assert.That(result.GenerationResult!.Projections.CommandExecutorTable.Entries.Length, Is.GreaterThan(0));

                bool hasDebugExecutorBinding = false;
                ReadOnlySpan<CommandExecutorEntryPlan> entries = result.GenerationResult.Projections.CommandExecutorTable.Entries;
                for (int index = 0; index < entries.Length; index++)
                {
                    if (entries[index].ExecutorId != new CommandExecutorId(debugCommandExecutorId))
                        continue;

                    hasDebugExecutorBinding = true;
                    Assert.That(entries[index].BindingToken, Does.Contain("CommandDebugExecutor"));
                    Assert.That(entries[index].BindingKind, Is.EqualTo(CommandExecutorBindingKind.Singleton));
                    break;
                }

                Assert.That(hasDebugExecutorBinding, Is.True);
                Assert.That(runtimeSurfaceFactory.CreatedContext, Is.Not.Null);
                Assert.That(runtimeSurfaceFactory.CreatedContext!.Input.CommandExecutorTablePlan, Is.Not.Null);
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.CommandExecutorTablePlan!.Entries.Length, Is.EqualTo(entries.Length));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(linkObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_AppendsEntityAuthoringProvenanceToEffectiveKernelIR()
        {
            GameObject rootObject = new GameObject("DirectPlayEntityRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 102, "DirectPlayEntityModule", "Assets/Scenes/DirectPlayEntity.unity", "DirectPlayEntityRoot", "ScopeAuthoringRoot", "module");

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlayEntity.unity",
                    12001,
                    "Assets/Scenes/DirectPlayEntity.unity",
                    "DirectPlayEntityRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                EntityIdentityMB entity = CreateEntity(rootObject.transform, "PlayerEntity", "entity.player", "Assets/Scenes/DirectPlayEntity.unity", "DirectPlayEntityRoot/PlayerEntity", 13001);
                CreateDeclaration(entity.transform, entity, "UIButtonDecl", "Assets/Scenes/DirectPlayEntity.unity", "DirectPlayEntityRoot/PlayerEntity/UIButtonDecl", 13002);

                KernelIR kernelIR = CreateKernelIR();
                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404),
                    ArtifactSetPublicationState.Empty,
                    new FakeKernelBootRuntimeSurfaceFactory());

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input);

                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.ExtractionReport.EntityInputs.Count, Is.EqualTo(1));
                Assert.That(result.ExtractionReport.DeclarationInputs.Count, Is.EqualTo(1));
                Assert.That(result.GenerationResult, Is.Not.Null);
                Assert.That(result.GenerationResult!.Projections.EntityRegistrationPlan.Entries.Length, Is.EqualTo(1));
                Assert.That(result.GenerationResult.Projections.EntityRegistrationPlan.Entries[0].EntityRef.Value, Is.EqualTo("entity.player"));
                Assert.That(result.EffectiveKernelIR.DiagnosticSeeds.Length, Is.EqualTo(kernelIR.DiagnosticSeeds.Length + 2));
                Assert.That(result.EffectiveKernelIR.Sources.Count, Is.GreaterThanOrEqualTo(kernelIR.Sources.Count + 2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_ProjectsRepresentativeServiceDeclarationIntoVerifiedServiceGraph()
        {
            GameObject rootObject = new GameObject("DirectPlayNavigationRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 103, "DirectPlayNavigationModule", "Assets/Scenes/DirectPlayNavigation.unity", "DirectPlayNavigationRoot", "ScopeAuthoringRoot", "module");

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlayNavigation.unity",
                    12001,
                    "Assets/Scenes/DirectPlayNavigation.unity",
                    "DirectPlayNavigationRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                EntityIdentityMB entity = CreateEntity(rootObject.transform, "NavigationEntity", "entity.navigation", "Assets/Scenes/DirectPlayNavigation.unity", "DirectPlayNavigationRoot/NavigationEntity", 17001);
                CreateNavigationDeclaration(entity, "NavigationDecl", "Assets/Scenes/DirectPlayNavigation.unity", "DirectPlayNavigationRoot/NavigationEntity/NavigationDecl", 17002, 6101, 6102, 100, 110);

                KernelIR kernelIR = CreateKernelIR();
                FakeKernelBootRuntimeSurfaceFactory runtimeSurfaceFactory = new FakeKernelBootRuntimeSurfaceFactory();
                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404),
                    ArtifactSetPublicationState.Empty,
                    runtimeSurfaceFactory);

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input);

                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.ExtractionReport.ServiceDeclarations.Count, Is.EqualTo(2));
                Assert.That(result.EffectiveKernelIR.Services.Length, Is.EqualTo(kernelIR.Services.Length + 2));
                Assert.That(result.EffectiveKernelIR.DiagnosticSeeds.Length, Is.EqualTo(kernelIR.DiagnosticSeeds.Length + 4));
                Assert.That(result.GenerationResult, Is.Not.Null);
                Assert.That(result.GenerationResult!.Projections.ServiceGraph.Services.Length, Is.EqualTo(kernelIR.Services.Length + 2));
                Assert.That(result.GenerationResult.Projections.ServiceRegistrationPlan.Entries.Length, Is.EqualTo(2));
                Assert.That(result.GenerationResult.Projections.ServiceRegistrationPlan.Entries[0].EntityRef.Value, Is.EqualTo("entity.navigation"));
                Assert.That(result.EffectiveKernelIR.Services[^2].Dependencies.Length, Is.EqualTo(3));
                Assert.That(result.EffectiveKernelIR.Services[^2].Dependencies[0].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(100))));
                Assert.That(result.EffectiveKernelIR.Services[^2].Dependencies[1].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(110))));
                Assert.That(result.EffectiveKernelIR.Services[^2].Dependencies[2].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(6102))));
                Assert.That(result.EffectiveKernelIR.Services[^1].Dependencies.Length, Is.EqualTo(1));
                Assert.That(result.EffectiveKernelIR.Services[^1].Dependencies[0].Target, Is.EqualTo(new DependencyNodeIR(new ServiceId(100))));
                Assert.That(runtimeSurfaceFactory.CreatedContext, Is.Not.Null);
                RuntimeIdentityRef[] availableRootServices = runtimeSurfaceFactory.CreatedContext!.Input.RootState.AvailableRootServices.ToArray();
                Assert.That(availableRootServices, Has.Some.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.Service, 6101)));
                Assert.That(availableRootServices, Has.Some.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.Service, 6102)));
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.EntityRegistrationPlan, Is.Not.Null);
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.EntityRegistrationPlan!.Entries[0].EntityRef.Value, Is.EqualTo("entity.navigation"));
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.ServiceRegistrationPlan, Is.Not.Null);
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.ServiceRegistrationPlan!.Entries[0].ServiceId, Is.EqualTo(new ServiceId(6101)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(linkObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_ProjectsButtonChannelHubServiceDeclarationIntoVerifiedServiceGraph()
        {
            GameObject rootObject = new GameObject("DirectPlayButtonChannelRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 104, "DirectPlayButtonChannelModule", "Assets/Scenes/DirectPlayButtonChannel.unity", "DirectPlayButtonChannelRoot", "ScopeAuthoringRoot", "module");

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlayButtonChannel.unity",
                    12001,
                    "Assets/Scenes/DirectPlayButtonChannel.unity",
                    "DirectPlayButtonChannelRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                EntityIdentityMB entity = CreateEntity(rootObject.transform, "ButtonChannelEntity", "entity.button", "Assets/Scenes/DirectPlayButtonChannel.unity", "DirectPlayButtonChannelRoot/ButtonChannelEntity", 17001);
                CreateButtonChannelHubDeclaration(entity, "ButtonChannelDecl", "Assets/Scenes/DirectPlayButtonChannel.unity", "DirectPlayButtonChannelRoot/ButtonChannelEntity/ButtonChannelDecl", 17002, 6201);

                KernelIR kernelIR = CreateKernelIR();
                FakeKernelBootRuntimeSurfaceFactory runtimeSurfaceFactory = new FakeKernelBootRuntimeSurfaceFactory();
                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404),
                    ArtifactSetPublicationState.Empty,
                    runtimeSurfaceFactory);

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input);

                Assert.That(result.IsSuccessful, Is.True);
                Assert.That(result.ExtractionReport.ServiceDeclarations.Count, Is.EqualTo(1));
                Assert.That(result.EffectiveKernelIR.Services.Length, Is.EqualTo(kernelIR.Services.Length + 1));
                Assert.That(result.GenerationResult, Is.Not.Null);
                Assert.That(result.GenerationResult!.Projections.ServiceGraph.Services.Length, Is.EqualTo(kernelIR.Services.Length + 1));
                Assert.That(result.GenerationResult.Projections.ServiceRegistrationPlan.Entries.Length, Is.EqualTo(1));
                Assert.That(result.GenerationResult.Projections.ServiceRegistrationPlan.Entries[0].EntityRef.Value, Is.EqualTo("entity.button"));
                Assert.That(result.EffectiveKernelIR.Services[^1].Dependencies.Length, Is.EqualTo(0));
                Assert.That(runtimeSurfaceFactory.CreatedContext, Is.Not.Null);
                RuntimeIdentityRef[] availableRootServices = runtimeSurfaceFactory.CreatedContext!.Input.RootState.AvailableRootServices.ToArray();
                Assert.That(availableRootServices, Has.Some.EqualTo(new RuntimeIdentityRef(RuntimeIdentityKind.Service, 6201)));
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.ServiceRegistrationPlan, Is.Not.Null);
                Assert.That(runtimeSurfaceFactory.CreatedContext.Input.ServiceRegistrationPlan!.Entries[0].ServiceId, Is.EqualTo(new ServiceId(6201)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(linkObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_BlocksOnNormalizationMismatch()
        {
            GameObject rootObject = new GameObject("DirectPlayRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 101, "DirectPlayModule", "Assets/Scenes/DirectPlay.unity", "DirectPlayRoot", "ScopeAuthoringRoot", "module");

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlay.unity",
                    12001,
                    "Assets/Scenes/DirectPlay.unity",
                    "DirectPlayRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                KernelIR kernelIR = CreateKernelIRWithMismatchedHashes();

                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404));

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input);

                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.FailedStage, Is.EqualTo(AuthoringDirectPlayStage.Normalization));
                Assert.That(result.ExtractionReport.IsValid, Is.True);
                Assert.That(result.NormalizationReport.IsValid, Is.False);
                Assert.That(result.GenerationResult, Is.Null);
                Assert.That(result.PromotionStageResult, Is.Null);
                Assert.That(result.BootBoundaryResult, Is.Null);
                Assert.That(result.PromotionCommitResult, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(linkObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_BlocksOnInvalidExtraction()
        {
            GameObject rootObject = new GameObject("BrokenDirectPlayRoot");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 101, "BrokenDirectPlayModule", "Assets/Scenes/BrokenDirectPlay.unity", "BrokenDirectPlayRoot", "ScopeAuthoringRoot", "module");

                KernelIR kernelIR = CreateKernelIR();

                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404));

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input);

                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.FailedStage, Is.EqualTo(AuthoringDirectPlayStage.Extraction));
                Assert.That(result.ExtractionReport.IsValid, Is.False);
                Assert.That(result.GenerationResult, Is.Null);
                Assert.That(result.PromotionStageResult, Is.Null);
                Assert.That(result.BootBoundaryResult, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_EmitsDiagnosticsThroughCentralService()
        {
            GameObject rootObject = new GameObject("DirectPlayRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 101, "DirectPlayModule", "Assets/Scenes/DirectPlay.unity", "DirectPlayRoot", "ScopeAuthoringRoot", "module");

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlay.unity",
                    12001,
                    "Assets/Scenes/DirectPlay.unity",
                    "DirectPlayRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                KernelIR kernelIR = CreateKernelIRWithMismatchedHashes();
                TestDiagnosticSink sink = new TestDiagnosticSink();
                KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });

                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404));

                AuthoringDirectPlayResult result = AuthoringBridge.PrepareDirectPlay(input, diagnosticService);

                Assert.That(result.IsSuccessful, Is.False);
                Assert.That(result.FailedStage, Is.EqualTo(AuthoringDirectPlayStage.Normalization));
                Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(AuthoringDirectPlayDiagnosticCodes.NormalizationMismatch));
                Assert.That(sink.Diagnostics[0].Context.Phase, Is.EqualTo("AuthoringDirectPlay/Normalization"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(linkObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PrepareDirectPlay_EmitsUnexpectedFailureDiagnosticWhenRuntimeFactoryThrows()
        {
            GameObject rootObject = new GameObject("DirectPlayRoot");
            GameObject linkObject = new GameObject("DirectPlayLink");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ConfigureRoot(root, 101, "DirectPlayModule", "Assets/Scenes/DirectPlay.unity", "DirectPlayRoot", "ScopeAuthoringRoot", "module");

                linkObject.transform.SetParent(rootObject.transform, false);
                ScopeAuthoringLink link = linkObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(1));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                    "Assets/Scenes/DirectPlay.unity",
                    12001,
                    "Assets/Scenes/DirectPlay.unity",
                    "DirectPlayRoot/DirectPlayLink",
                    "ScopeAuthoringLink",
                    "scope");

                KernelIR kernelIR = CreateKernelIR();
                TestDiagnosticSink sink = new TestDiagnosticSink();
                KernelDiagnosticService diagnosticService = new KernelDiagnosticService(new IKernelDiagnosticSink[] { sink });

                AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                    new[] { root },
                    kernelIR,
                    new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                    new PlanId(101),
                    new ArtifactSetId(202),
                    4,
                    "1.0.0",
                    new ManifestId(303),
                    new BootPolicyId(404),
                    ArtifactSetPublicationState.Empty,
                    new ThrowingKernelBootRuntimeSurfaceFactory());

                Assert.That(
                    () => AuthoringBridge.PrepareDirectPlay(input, diagnosticService),
                    Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("runtime surface failed"));

                Assert.That(sink.Diagnostics, Has.Count.EqualTo(1));
                Assert.That(sink.Diagnostics[0].Code.Value, Is.EqualTo(AuthoringDirectPlayDiagnosticCodes.UnexpectedFailure));
                Assert.That(sink.Diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Fatal));
                Assert.That(sink.Diagnostics[0].Context.Phase, Is.EqualTo("AuthoringDirectPlay/UnexpectedFailure"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(linkObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void DirectPlayDiagnostics_PreservesStageOrder()
        {
            ScopeAuthoringExtractionReport extractionReport = new ScopeAuthoringExtractionReport(
                Array.Empty<ModuleContributionData>(),
                Array.Empty<EntityAuthoringInput>(),
                Array.Empty<EntityDeclarationPlanInput>(),
                Array.Empty<EntityServiceDeclarationInput>(),
                Array.Empty<CommandDeclarationInput>(),
                new[]
                {
                    new AuthoringValidationIssue(
                        ScopeAuthoringValidationCodes.RootInvalid,
                        ValidationSeverity.Error,
                        ValidationIssueCategory.LocalNode,
                        new ModuleId(11),
                        "broken root",
                        subjectName: "BrokenRoot"),
                });

            AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                Array.Empty<ScopeAuthoringRoot>(),
                CreateKernelIR(),
                new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                new ManifestId(303),
                new BootPolicyId(404));

            KernelIRNormalizationReport normalizationReport = new KernelIRNormalizationReport(
                new KernelHash128(1, 2, 3, 4),
                new KernelHash128(4, 3, 2, 1),
                new KernelHash128(5, 6, 7, 8),
                new KernelHash128(8, 7, 6, 5));

            DependencyValidationReport dependencyValidationReport = new DependencyValidationReport(input.Profile.Kind.ToString(), Array.Empty<DependencyValidationIssue>());

            AuthoringDirectPlayResult result = new AuthoringDirectPlayResult(
                input,
                input.KernelIR,
                AuthoringDirectPlayStage.Extraction,
                extractionReport,
                normalizationReport,
                dependencyValidationReport,
                null,
                null,
                null,
                null,
                null,
                null);

            KernelDiagnostic[] diagnostics = AuthoringDirectPlayDiagnostics.ToKernelDiagnostics(result);

            Assert.That(diagnostics, Has.Length.EqualTo(2));
            Assert.That(diagnostics[0].Code.Value, Is.EqualTo(ScopeAuthoringValidationCodes.RootInvalid));
            Assert.That(diagnostics[1].Code.Value, Is.EqualTo(AuthoringDirectPlayDiagnosticCodes.NormalizationMismatch));
        }

        [Test]
        public void DirectPlayDiagnostics_RespectsBootDiagnosticSuppressionWhenBoundaryIsPresent()
        {
            AuthoringDirectPlayInput input = new AuthoringDirectPlayInput(
                Array.Empty<ScopeAuthoringRoot>(),
                CreateKernelIR(),
                new KernelProfile(new KernelProfileId(7), KernelProfileKind.Development),
                new PlanId(101),
                new ArtifactSetId(202),
                4,
                "1.0.0",
                new ManifestId(303),
                new BootPolicyId(404));

            ScopeAuthoringExtractionReport extractionReport = new ScopeAuthoringExtractionReport(Array.Empty<ModuleContributionData>(), Array.Empty<EntityAuthoringInput>(), Array.Empty<EntityDeclarationPlanInput>(), Array.Empty<EntityServiceDeclarationInput>(), Array.Empty<CommandDeclarationInput>(), Array.Empty<AuthoringValidationIssue>());
            KernelIRNormalizationReport normalizationReport = new KernelIRNormalizationReport(
                input.KernelIR.Header.SourceHash,
                input.KernelIR.Header.SourceHash,
                input.KernelIR.Header.NormalizedHash,
                input.KernelIR.Header.NormalizedHash);
            DependencyValidationReport dependencyValidationReport = new DependencyValidationReport(input.Profile.Kind.ToString(), Array.Empty<DependencyValidationIssue>());
            BootValidationIssue bootIssue = new BootValidationIssue(
                "BOOT_TEST_BLOCKED",
                ValidationSeverity.Error,
                BootValidationGateKind.KernelIRHashMismatch,
                "boot blocked by test");
            BootValidationReport bootValidationReport = new BootValidationReport(null, input.Profile, new[] { bootIssue });
            KernelBootBoundaryResult bootBoundaryResult = KernelBootBoundaryResult.Failed(bootValidationReport, KernelBootBoundaryFailureKind.ValidationBlocked, Array.Empty<KernelDiagnostic>());

            AuthoringDirectPlayResult result = new AuthoringDirectPlayResult(
                input,
                input.KernelIR,
                AuthoringDirectPlayStage.Boot,
                extractionReport,
                normalizationReport,
                dependencyValidationReport,
                null,
                null,
                null,
                bootValidationReport,
                bootBoundaryResult,
                null);

            KernelDiagnostic[] diagnostics = AuthoringDirectPlayDiagnostics.ToKernelDiagnostics(result);

            Assert.That(diagnostics, Is.Empty);
        }

        static EntityIdentityMB CreateEntity(Transform parent, string name, string entityRef, string assetPath, string gameObjectPath, long localFileId)
        {
            GameObject entityObject = new GameObject(name);
            entityObject.transform.SetParent(parent, false);

            EntityIdentityMB entity = entityObject.AddComponent<EntityIdentityMB>();
            entity.id = entityRef;
            entity.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                localFileId,
                assetPath,
                gameObjectPath,
                nameof(EntityIdentityMB),
                "entity");
            return entity;
        }

        static TestEntityDeclarationMB CreateDeclaration(Transform parent, EntityIdentityMB entity, string name, string assetPath, string gameObjectPath, long localFileId)
        {
            GameObject declarationObject = new GameObject(name);
            declarationObject.transform.SetParent(parent, false);

            TestEntityDeclarationMB declaration = declarationObject.AddComponent<TestEntityDeclarationMB>();
            declaration.SetEntityIdentity(entity);
            declaration.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                localFileId,
                assetPath,
                gameObjectPath,
                nameof(TestEntityDeclarationMB),
                "declaration");
            return declaration;
        }

        static TestNavigationDeclarationMB CreateNavigationDeclaration(EntityIdentityMB entity, string name, string assetPath, string gameObjectPath, long localFileId, int navigationServiceId, int inputNavigateServiceId, int selectionServiceId, int controlSchemeServiceId)
        {
            GameObject declarationObject = new GameObject(name);
            declarationObject.transform.SetParent(entity.transform, false);

            TestNavigationDeclarationMB declaration = declarationObject.AddComponent<TestNavigationDeclarationMB>();
            declaration.SetEntityIdentity(entity);
            declaration.SetServiceIds(navigationServiceId, inputNavigateServiceId, selectionServiceId, controlSchemeServiceId);
            declaration.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                localFileId,
                assetPath,
                gameObjectPath,
                nameof(TestNavigationDeclarationMB),
                "declaration");
            return declaration;
        }

        static TestButtonChannelHubDeclarationMB CreateButtonChannelHubDeclaration(EntityIdentityMB entity, string name, string assetPath, string gameObjectPath, long localFileId, int serviceId)
        {
            GameObject declarationObject = new GameObject(name);
            declarationObject.transform.SetParent(entity.transform, false);

            TestButtonChannelHubDeclarationMB declaration = declarationObject.AddComponent<TestButtonChannelHubDeclarationMB>();
            declaration.SetEntityIdentity(entity);
            declaration.SetServiceId(serviceId);
            declaration.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                localFileId,
                assetPath,
                gameObjectPath,
                nameof(TestButtonChannelHubDeclarationMB),
                "declaration");
            return declaration;
        }

        static KernelIR CreateKernelIRWithMismatchedHashes()
        {
            KernelIR provisional = CreateKernelIRCore(new KernelHash128(1, 2, 3, 4), new KernelHash128(5, 6, 7, 8));
            KernelHash128 normalizedHash = KernelIRHashing.ComputeNormalizedHash(provisional);
            return CreateKernelIRCore(normalizedHash, new KernelHash128(normalizedHash.A ^ 1u, normalizedHash.B, normalizedHash.C, normalizedHash.D));
        }

        static KernelIR CreateKernelIR(int commandExecutorId = 202)
        {
            KernelIR provisional = CreateKernelIRCore(new KernelHash128(1, 2, 3, 4), new KernelHash128(5, 6, 7, 8), commandExecutorId);
            KernelHash128 normalizedHash = KernelIRHashing.ComputeNormalizedHash(provisional);
            return CreateKernelIRCore(normalizedHash, normalizedHash, commandExecutorId);
        }

        static void AddCommandRunnerInstaller(GameObject target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            Type? installerType = Type.GetType("Game.Commands.CommandRunnerMB, Assembly-CSharp", throwOnError: false);
            if (installerType == null)
                throw new InvalidOperationException("CommandRunnerMB type is unavailable from Assembly-CSharp.");

            target.AddComponent(installerType);
        }

        static int GetDebugCommandExecutorId()
        {
            Type? commandIdsType = Type.GetType("Game.Commands.VNext.CommandIds, Assembly-CSharp", throwOnError: false);
            if (commandIdsType == null)
                throw new InvalidOperationException("CommandIds type is unavailable from Assembly-CSharp.");

            FieldInfo? field = commandIdsType.GetField("DebugCommandContext", BindingFlags.Public | BindingFlags.Static);
            if (field?.GetValue(null) is int commandId && commandId > 0)
                return commandId;

            throw new InvalidOperationException("CommandIds.DebugCommandContext is unavailable from Assembly-CSharp.");
        }

        static KernelIR CreateKernelIRCore(KernelHash128 sourceHash, KernelHash128 normalizedHash, int commandExecutorId = 202)
        {
            ModuleIR module = new ModuleIR(
                new ModuleId(10),
                "Core",
                ModuleKind.Feature,
                new ModuleVersion(1),
                new ModuleAvailabilityIR(new AvailabilityIR(KernelProfileMask.All, true, null)),
                new SourceLocationId(1));

            ServiceContractIR[] service100Contracts = new[]
            {
                new ServiceContractIR("IService100A", new SourceLocationId(2)),
                new ServiceContractIR("IService100B", new SourceLocationId(3)),
            };

            ServiceDependencyIR[] service100Dependencies = new[]
            {
                new ServiceDependencyIR(new DependencyNodeIR(new ServiceId(110)), DependencyStrength.Required, new SourceLocationId(4)),
                new ServiceDependencyIR(new DependencyNodeIR(new RuntimeQueryId(400)), DependencyStrength.Optional, new SourceLocationId(5)),
            };

            ServiceIR service100 = new ServiceIR(
                new ServiceId(100),
                "Service100",
                ServiceLifetimeKind.Singleton,
                new ModuleId(10),
                service100Contracts,
                service100Dependencies,
                ServiceFactoryKind.GeneratedFactory,
                new SourceLocationId(6));

            ServiceIR service110 = new ServiceIR(
                new ServiceId(110),
                "Service110",
                ServiceLifetimeKind.Scoped,
                new ModuleId(10),
                new[]
                {
                    new ServiceContractIR("IService110", new SourceLocationId(7)),
                },
                null,
                ServiceFactoryKind.ProvidedInstance,
                new SourceLocationId(8));

            ScopeServiceRequirementIR[] scopeRequiredServices = new[]
            {
                new ScopeServiceRequirementIR(new ServiceId(100), DependencyStrength.Required, new SourceLocationId(9)),
                new ScopeServiceRequirementIR(new ServiceId(110), DependencyStrength.Optional, new SourceLocationId(10)),
            };

            ScopeValueInitRefIR[] scopeValueInitPlans = new[]
            {
                new ScopeValueInitRefIR(new ValueInitPlanId(400), new SourceLocationId(11)),
                new ScopeValueInitRefIR(new ValueInitPlanId(401), new SourceLocationId(12)),
            };

            ScopeIR scope = new ScopeIR(
                new ScopeAuthoringId(300),
                new ScopePlanId(301),
                "RootScope",
                ScopeKind.Root,
                new ModuleId(10),
                default,
                scopeRequiredServices,
                scopeValueInitPlans,
                new ScopeServiceBoundaryIR(ScopeServiceBoundaryKind.OwnedLocal, 1, new SourceLocationId(3)),
                new LifecyclePlanRefIR(new LifecyclePlanId(500), new SourceLocationId(13)),
                new SourceLocationId(14));

            CommandDependencyIR[] commandDependencies = new[]
            {
                new CommandDependencyIR(new DependencyNodeIR(new ServiceId(100)), DependencyStrength.Required, new SourceLocationId(16)),
                new CommandDependencyIR(new DependencyNodeIR(new RuntimeQueryId(400)), DependencyStrength.Optional, new SourceLocationId(17)),
            };

            CommandIR command = new CommandIR(
                new CommandTypeId(200),
                "Command200",
                new CommandAuthoringKeyRefIR(new CommandAuthoringKeyId(203), "command.200", new SourceLocationId(17)),
                new CommandCategoryId(1),
                new ModuleId(10),
                new CommandPayloadSchemaRefIR(new CommandPayloadSchemaId(201), new SourceLocationId(18)),
                new CommandExecutorRefIR(new CommandExecutorId(commandExecutorId), new SourceLocationId(19)),
                commandDependencies,
                new SourceLocationId(15));

            ValueKeyIR[] valueKeys = new[]
            {
                new ValueKeyIR(
                    new ValueKeyId(300),
                    "value.current",
                    "Value Current",
                    ValueKind.Int,
                    new ModuleId(10),
                    new ValueSchemaRefIR(new ValueSchemaId(301), new SourceLocationId(21)),
                    new SavePolicyIR(false, false, null),
                    new SourceLocationId(20)),
                new ValueKeyIR(
                    new ValueKeyId(310),
                    "value.target",
                    "Value Target",
                    ValueKind.String,
                    new ModuleId(10),
                    new ValueSchemaRefIR(new ValueSchemaId(311), new SourceLocationId(23)),
                    new SavePolicyIR(true, true, "slot"),
                    new SourceLocationId(22)),
            };

            LifecycleStepIR[] lifecycleSteps = new[]
            {
                new LifecycleStepIR(
                    new LifecycleStepId(501),
                    LifecyclePhase.Create,
                    1,
                    new LifecycleTargetRefIR(new ServiceId(100)),
                    LifecycleActionKind.ServiceMethod,
                    new[] { new DependencyEdgeId(9001) },
                    new SourceLocationId(26)),
                new LifecycleStepIR(
                    new LifecycleStepId(502),
                    LifecyclePhase.Release,
                    2,
                    new LifecycleTargetRefIR(new RuntimeQueryId(400)),
                    LifecycleActionKind.RuntimeQueryNotify,
                    new[] { new DependencyEdgeId(9002) },
                    new SourceLocationId(27)),
            };

            LifecycleIR lifecycle = new LifecycleIR(
                new LifecyclePlanId(500),
                "RootLifecycle",
                new ModuleId(10),
                lifecycleSteps,
                new SourceLocationId(28),
                LifecycleFailurePolicy.FailOperation);

            RuntimeIdentityFieldIR[] runtimeQueryFields = new[]
            {
                new RuntimeIdentityFieldIR("id", "int", true),
                new RuntimeIdentityFieldIR("name", "string", true),
            };

            RuntimeQueryIR runtimeQuery = new RuntimeQueryIR(
                new RuntimeQueryId(400),
                "RuntimeQuery400",
                RuntimeQueryTargetKind.Service,
                runtimeQueryFields,
                new RuntimeQueryPolicyIR(true, false, DependencyPhase.Runtime),
                new ModuleId(10),
                new SourceLocationId(29));

            DependencyEdgeIR[] dependencies = new[]
            {
                new DependencyEdgeIR(
                    new DependencyEdgeId(9001),
                    new DependencyNodeIR(new ServiceId(100)),
                    new DependencyNodeIR(new ServiceId(110)),
                    DependencyKind.Requires,
                    DependencyPhase.Build,
                    DependencyStrength.Required,
                    new SourceLocationId(24)),
                new DependencyEdgeIR(
                    new DependencyEdgeId(9002),
                    new DependencyNodeIR(new RuntimeQueryId(400)),
                    new DependencyNodeIR(new ServiceId(100)),
                    DependencyKind.References,
                    DependencyPhase.Runtime,
                    DependencyStrength.Optional,
                    new SourceLocationId(25)),
            };

            SourceLocationTable sources = new SourceLocationTable(new[]
            {
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Module")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service110")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100ContractA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100ContractB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100DependencyA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service100DependencyB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Service110Contract")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Scope")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeRequiredA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeRequiredB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeValueInitA")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ScopeValueInitB")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Lifecycle")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Command")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "CommandPayload")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "CommandExecutor")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueKeyCurrent")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueSchemaCurrent")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueKeyTarget")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "ValueSchemaTarget")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "RuntimeQuery")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra23")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra24")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra25")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra26")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra27")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra28")),
                new SourceLocationIR(new GeneratedSourceLocation("Test", "KernelIR", "Extra29")),
            });

            KernelIRHeader header = new KernelIRHeader(
                "DOC",
                1,
                "TestProject",
                "Development",
                "1.0.0",
                sourceHash,
                normalizedHash);

            KernelProfileIR profile = new KernelProfileIR("Development", KernelProfileMask.Development, new AvailabilityIR(KernelProfileMask.All, true, null));

            return new KernelIR(
                header,
                profile,
                new[] { module },
                new[] { scope },
                new[] { service100, service110 },
                new[] { command },
                valueKeys,
                new[] { lifecycle },
                new[] { runtimeQuery },
                dependencies,
                sources,
                null);
        }

        static void ConfigureRoot(ScopeAuthoringRoot root, int moduleId, string moduleName, string assetPath, string gameObjectPath, string componentType, string propertyPath)
        {
            root.SetModuleMetadata(moduleId, moduleName, ModuleKind.Feature, 1);
            root.SetContributionAvailability("Battle", "Windows", "Desktop", ContributionEnvironment.Release);
            root.SetSourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath,
                12001,
                assetPath,
                gameObjectPath,
                componentType,
                propertyPath);
        }

        sealed class FakeKernelBootRuntimeSurfaceFactory : IKernelBootRuntimeSurfaceFactory
        {
            public KernelBootBoundaryContext? CreatedContext { get; private set; }

            public IKernelBootRuntimeSurface Create(KernelBootBoundaryContext context)
            {
                CreatedContext = context;
                return new FakeKernelBootRuntimeSurface(context);
            }
        }

        sealed class ThrowingKernelBootRuntimeSurfaceFactory : IKernelBootRuntimeSurfaceFactory
        {
            public IKernelBootRuntimeSurface Create(KernelBootBoundaryContext context)
            {
                throw new InvalidOperationException("runtime surface failed");
            }
        }

        sealed class FakeKernelBootRuntimeSurface : IKernelBootRuntimeSurface
        {
            readonly KernelLifecycleDispatcher lifecycleDispatcher;

            public FakeKernelBootRuntimeSurface(KernelBootBoundaryContext context)
            {
                lifecycleDispatcher = new KernelLifecycleDispatcher(context.LifecyclePlan ?? throw new ArgumentException("Direct play boot tests require a lifecycle plan.", nameof(context)));
                LifecyclePlanResolver = new KernelLifecyclePlanResolver(new[] { lifecycleDispatcher });
                EntityRegistrationPlan = context.EntityRegistrationPlan;
                ServiceRegistrationPlan = context.ServiceRegistrationPlan;
                EntityServiceRoutePlan = context.EntityServiceRoutePlan;
                DebugMap = context.Input.DebugMap ?? throw new ArgumentException("Direct play boot tests require a debug map.", nameof(context));
                Diagnostics = new KernelRuntimeDiagnostics(context.ValidationReport, DebugMap);
            }

            public EntityRegistrationPlan? EntityRegistrationPlan { get; }

            public ServiceRegistrationPlan? ServiceRegistrationPlan { get; }

            public EntityServiceRoutePlan? EntityServiceRoutePlan { get; }

            public CommandCatalogPlan? CommandCatalogPlan => null;

            public CommandExecutorTablePlan? CommandExecutorTablePlan => null;

            public KernelRuntimeDiagnostics Diagnostics { get; }

            public KernelDebugMap DebugMap { get; }

            public KernelLifecycleDispatcher? LifecycleDispatcher => lifecycleDispatcher;

            public ILifecyclePlanResolver LifecyclePlanResolver { get; }

            public Task<LifecycleDispatchResult> DispatchAllLifecycleAsync(IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new LifecycleDispatchResult(0, 0, 0, false, null));
            }

            public Task<LifecycleDispatchResult> DispatchPhaseLifecycleAsync(LifecyclePhase phase, IAsyncLifecycleDispatchExecutor executor, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new LifecycleDispatchResult(0, 0, 0, false, null));
            }
        }

        sealed class TestEntityDeclarationMB : EntityDeclarationMB
        {
        }

        sealed class TestNavigationDeclarationMB : EntityDeclarationMB, IEntityServiceDeclarationAuthoring
        {
            [SerializeField]
            int navigationServiceId;

            [SerializeField]
            int inputNavigateServiceId;

            [SerializeField]
            int selectionServiceId;

            [SerializeField]
            int controlSchemeServiceId;

#if UNITY_EDITOR
            public void SetServiceIds(int newNavigationServiceId, int newInputNavigateServiceId, int newSelectionServiceId, int newControlSchemeServiceId)
            {
                if (Application.isPlaying)
                    throw new InvalidOperationException("Test authoring state may only be mutated in edit mode.");

                navigationServiceId = newNavigationServiceId;
                inputNavigateServiceId = newInputNavigateServiceId;
                selectionServiceId = newSelectionServiceId;
                controlSchemeServiceId = newControlSchemeServiceId;
            }
#endif

            public bool TryCreateServiceDeclarations(in EntityDeclarationPlanInput declarationInput, out EntityServiceDeclarationInput[] declarations, out string failureReason)
            {
                if (navigationServiceId <= 0 || inputNavigateServiceId <= 0 || selectionServiceId <= 0 || controlSchemeServiceId <= 0)
                {
                    declarations = Array.Empty<EntityServiceDeclarationInput>();
                    failureReason = "TestNavigationDeclarationMB requires positive service ids.";
                    return false;
                }

                if (navigationServiceId == inputNavigateServiceId
                    || navigationServiceId == selectionServiceId
                    || navigationServiceId == controlSchemeServiceId
                    || inputNavigateServiceId == selectionServiceId
                    || inputNavigateServiceId == controlSchemeServiceId
                    || selectionServiceId == controlSchemeServiceId)
                {
                    declarations = Array.Empty<EntityServiceDeclarationInput>();
                    failureReason = "TestNavigationDeclarationMB requires distinct service ids.";
                    return false;
                }

                declarations = new[]
                {
                    new EntityServiceDeclarationInput(
                        declarationInput.OwnerModule,
                        declarationInput.OwnerEntityRef,
                        new ServiceId(navigationServiceId),
                        CreateStableId(declarationInput.OwnerEntityRef, navigationServiceId, "navigation"),
                        "UINavigationService",
                        "test-navigation",
                        new[]
                        {
                            "Game.UI.IUINavigationService",
                            "Game.UI.IUINavigationTelemetry",
                        },
                        new[]
                        {
                            new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(selectionServiceId)), DependencyStrength.Required),
                            new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(controlSchemeServiceId)), DependencyStrength.Required),
                            new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(inputNavigateServiceId)), DependencyStrength.Required),
                        },
                        Array.Empty<ServiceLifecycleContributionInput>(),
                        SourceKind,
                        ServiceLifetimeKind.Singleton,
                        ServiceFactoryKind.GeneratedFactory,
                        declarationInput.Source),
                    new EntityServiceDeclarationInput(
                        declarationInput.OwnerModule,
                        declarationInput.OwnerEntityRef,
                        new ServiceId(inputNavigateServiceId),
                        CreateStableId(declarationInput.OwnerEntityRef, inputNavigateServiceId, "input-navigate"),
                        "UIInputNavigateManagerService",
                        "test-input-navigate",
                        new[]
                        {
                            "Game.UI.IUIInputNavigateService",
                        },
                        new[]
                        {
                            new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(selectionServiceId)), DependencyStrength.Required),
                        },
                        new[]
                        {
                            new ServiceLifecycleContributionInput(
                                LifecyclePhase.Acquire,
                                10,
                                LifecycleActionKind.ServiceMethod,
                                CreateLifecycleStableId(declarationInput.OwnerEntityRef, inputNavigateServiceId, "acquire"),
                                "IUIInputNavigateService.Acquire",
                                declarationInput.Source),
                            new ServiceLifecycleContributionInput(
                                LifecyclePhase.Release,
                                10,
                                LifecycleActionKind.ServiceMethod,
                                CreateLifecycleStableId(declarationInput.OwnerEntityRef, inputNavigateServiceId, "release"),
                                "IUIInputNavigateService.Release",
                                declarationInput.Source),
                        },
                        SourceKind,
                        ServiceLifetimeKind.Singleton,
                        ServiceFactoryKind.GeneratedFactory,
                        declarationInput.Source),
                };

                failureReason = string.Empty;
                return true;
            }

            static string CreateStableId(EntityRef ownerEntityRef, int serviceId, string suffix)
            {
                return "entity-service:" + ownerEntityRef.Value + ':' + suffix + ':' + serviceId.ToString("D10");
            }

            static string CreateLifecycleStableId(EntityRef ownerEntityRef, int serviceId, string phase)
            {
                return "entity-lifecycle:" + ownerEntityRef.Value + ':' + serviceId.ToString("D10") + ':' + phase;
            }
        }

        sealed class TestButtonChannelHubDeclarationMB : EntityDeclarationMB, IEntityServiceDeclarationAuthoring
        {
            [SerializeField]
            int buttonChannelHubServiceId;

#if UNITY_EDITOR
            public void SetServiceId(int newServiceId)
            {
                if (Application.isPlaying)
                    throw new InvalidOperationException("Test authoring state may only be mutated in edit mode.");

                buttonChannelHubServiceId = Math.Max(0, newServiceId);
            }
#endif

            public bool TryCreateServiceDeclarations(in EntityDeclarationPlanInput declarationInput, out EntityServiceDeclarationInput[] declarations, out string failureReason)
            {
                if (buttonChannelHubServiceId <= 0)
                {
                    declarations = Array.Empty<EntityServiceDeclarationInput>();
                    failureReason = "TestButtonChannelHubDeclarationMB requires a positive service id.";
                    return false;
                }

                declarations = new[]
                {
                    new EntityServiceDeclarationInput(
                        declarationInput.OwnerModule,
                        declarationInput.OwnerEntityRef,
                        new ServiceId(buttonChannelHubServiceId),
                        CreateStableId(declarationInput.OwnerEntityRef, buttonChannelHubServiceId),
                        ButtonChannelHubServiceName,
                        "test-button-channel-hub",
                        new[]
                        {
                            ButtonChannelHubServiceContractName,
                        },
                        Array.Empty<EntityServiceDependencyInput>(),
                        Array.Empty<ServiceLifecycleContributionInput>(),
                        SourceKind,
                        ServiceLifetimeKind.Singleton,
                        ServiceFactoryKind.GeneratedFactory,
                        declarationInput.Source),
                };

                failureReason = string.Empty;
                return true;
            }

            static string CreateStableId(EntityRef ownerEntityRef, int serviceId)
            {
                return "entity-service:" + ownerEntityRef.Value + ":button-channel-hub:" + serviceId.ToString("D10");
            }
        }
    }
}
