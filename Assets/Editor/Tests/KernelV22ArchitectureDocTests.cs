using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelV22ArchitectureDocTests
    {
        static string ReadDoc(string relativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, relativePath);
            Assert.That(File.Exists(fullPath), Is.True, "Missing doc file: " + relativePath);
            return File.ReadAllText(fullPath);
        }

        [Test]
        public void V22Readme_ExposesKernelOnlyCompletionPackage()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "README.md"));

            Assert.That(content, Does.Contain("00_KernelV22CompletionOverviewSpec.md"));
            Assert.That(content, Does.Contain("01_KernelV22AuthorityAndServiceCensusSpec.md"));
            Assert.That(content, Does.Contain("02_KernelV22KernelOnlyHostSpec.md"));
            Assert.That(content, Does.Contain("02_1_KernelV22CommandAndValueHostRemovalSpec.md"));
            Assert.That(content, Does.Contain("03_KernelV22ServiceFamilyCutoverSpec.md"));
            Assert.That(content, Does.Contain("03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md"));
            Assert.That(content, Does.Contain("04_KernelV22LegacyDeletionAndHardeningSpec.md"));
            Assert.That(content, Does.Contain("04_1_KernelV22FullProofAndReleaseHardeningSpec.md"));
            Assert.That(content, Does.Contain("05_KernelV22MilestoneOrderSpec.md"));
            Assert.That(content, Does.Contain("06_KernelV22ImplementationPlan.md"));
            Assert.That(content, Does.Contain("Index/README.md"));
            Assert.That(content, Does.Contain("DynamicValue authoring"));
            Assert.That(content, Does.Contain("generated value-key identity"));
            Assert.That(content, Does.Contain("command/value host removal"));
            Assert.That(content, Does.Contain("kernel-only runtime authority"));
            Assert.That(content, Does.Contain("representative gameplay/application cutover"));
            Assert.That(content, Does.Contain("TC-V22-README-12"));
            Assert.That(content, Does.Contain("TC-V22-README-13"));
            Assert.That(content, Does.Contain("TC-V22-README-14"));
        }

        [Test]
        public void V22IndexReadme_ExposesCompletionArtifactsAndUpstreamInputs()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "Index", "README.md"));

            Assert.That(content, Does.Contain("01_KernelV22AuthorityAndServiceCensusSpec.md"));
            Assert.That(content, Does.Contain("KernelV22BaselineLedger.md"));
            Assert.That(content, Does.Contain("KernelV22ProofAnchorCatalog.md"));
            Assert.That(content, Does.Contain("05_KernelV22MilestoneOrderSpec.md"));
            Assert.That(content, Does.Contain("KernelV21BaselineLedger.md"));
            Assert.That(content, Does.Contain("KernelV21ProofAnchorCatalog.md"));
            Assert.That(content, Does.Contain("ForbiddenPatternRegistry.md"));
            Assert.That(content, Does.Contain("17_AssemblyDefinitionAndCompileBoundarySpec.md"));
            Assert.That(content, Does.Contain("06_KernelV22ImplementationPlan.md"));
            Assert.That(content, Does.Contain("not part of the V22-M0 claim surface"));
            Assert.That(content, Does.Contain("delete targets"));
            Assert.That(content, Does.Contain("TC-V22-INDEX-06"));
        }

        [Test]
        public void V22BaselineLedger_JoinsCompletionCriticalDomains()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "Index", "KernelV22BaselineLedger.md"));

            Assert.That(content, Does.Contain("V22-BL-HOST-001"));
            Assert.That(content, Does.Contain("V22-BL-HOST-003"));
            Assert.That(content, Does.Contain("V22-BL-CMD-001"));
            Assert.That(content, Does.Contain("V22-BL-VALUE-001"));
            Assert.That(content, Does.Contain("V22-BL-FAMILY-001"));
            Assert.That(content, Does.Contain("V22-BL-GAME-001"));
            Assert.That(content, Does.Contain("V22-BL-HARD-001"));
            Assert.That(content, Does.Contain("DeleteTarget"));
            Assert.That(content, Does.Contain("AuthoringBridge.cs"));
            Assert.That(content, Does.Contain("ModalStackChannelHubService.cs"));
            Assert.That(content, Does.Contain("StatusEffectService.cs"));
            Assert.That(content, Does.Contain("TC-V22-BL-01"));
        }

        [Test]
        public void V22ProofAnchorCatalog_SeparatesCompletionProofFamilies()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "Index", "KernelV22ProofAnchorCatalog.md"));

            Assert.That(content, Does.Contain("Spec package proof"));
            Assert.That(content, Does.Contain("Live kernel-only host proof"));
            Assert.That(content, Does.Contain("Command/value host-removal proof"));
            Assert.That(content, Does.Contain("Service-family cutover proof"));
            Assert.That(content, Does.Contain("Release hardening proof"));
            Assert.That(content, Does.Contain("KernelLiveBootOrchestrator.cs"));
            Assert.That(content, Does.Contain("KernelRuntimeShell.cs"));
            Assert.That(content, Does.Contain("02_KernelV22KernelOnlyHostSpec.md"));
            Assert.That(content, Does.Contain("02_1_KernelV22CommandAndValueHostRemovalSpec.md"));
            Assert.That(content, Does.Contain("VerifiedCommandRuntimeBridge.cs"));
            Assert.That(content, Does.Contain("BlackboardMB.cs"));
            Assert.That(content, Does.Contain("SceneFlowInstallerMB.cs"));
            Assert.That(content, Does.Contain("SceneService.cs"));
            Assert.That(content, Does.Contain("LoadingScreenService.cs"));
            Assert.That(content, Does.Contain("ModalStackChannelHubService.cs"));
            Assert.That(content, Does.Contain("TooltipChannelHubService.cs"));
            Assert.That(content, Does.Contain("03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md"));
            Assert.That(content, Does.Contain("AuthoringBridgeDirectPlayTests.cs"));
            Assert.That(content, Does.Contain("GameStateMachineExecutors.cs"));
            Assert.That(content, Does.Contain("ConversationExecutors.cs"));
            Assert.That(content, Does.Contain("TraitListChannelRuntime.cs"));
            Assert.That(content, Does.Contain("CommandExecutionTrace.cs"));
            Assert.That(content, Does.Contain("DynamicRuntimeLogUtility.cs"));
            Assert.That(content, Does.Contain("TargetChannelHubService.cs"));
            Assert.That(content, Does.Contain("AreaChannelHubService.cs"));
            Assert.That(content, Does.Contain("04_KernelV22LegacyDeletionAndHardeningSpec.md"));
            Assert.That(content, Does.Contain("04_1_KernelV22FullProofAndReleaseHardeningSpec.md"));
            Assert.That(content, Does.Contain("ScopeFeatureInstallerUtility.cs"));
            Assert.That(content, Does.Contain("KernelScopeHost.cs"));
            Assert.That(content, Does.Contain("LegacyMigrationModel.cs"));
            Assert.That(content, Does.Contain("GameLib.Kernel.Boot.asmdef"));
            Assert.That(content, Does.Contain("GameLib.Tests.Kernel.Boot.Editor.asmdef"));
            Assert.That(content, Does.Contain("KernelMinimalBootPlayModeTests.cs"));
            Assert.That(content, Does.Contain("KernelV22LiveBootBundleTests.cs"));
            Assert.That(content, Does.Contain("KernelV22RepresentativeGameSceneBundleTests.cs"));
            Assert.That(content, Does.Contain("GameStateMachineMigrationTests.cs"));
            Assert.That(content, Does.Contain("ConversationDialogueMigrationTests.cs"));
            Assert.That(content, Does.Contain("StatusEffectServiceDependencyCaptureTests.cs"));
            Assert.That(content, Does.Contain("KernelDiagnosticsModelTests.cs"));
            Assert.That(content, Does.Contain("KernelDiagnosticServiceTests.cs"));
            Assert.That(content, Does.Contain("DiagnosticCodeTraceabilityTests.cs"));
            Assert.That(content, Does.Contain("KernelPerformanceAllocationTests.cs"));
            Assert.That(content, Does.Contain("KernelPerformanceRegressionGateTests.cs"));
            Assert.That(content, Does.Contain("KernelProfilerMarkerTaxonomyTests.cs"));
            Assert.That(content, Does.Contain("LegacyCompatBoundaryTests.cs"));
            Assert.That(content, Does.Contain("KernelForbiddenPatternTests.cs"));
            Assert.That(content, Does.Contain("KernelForbiddenPatternScannerTests.cs"));
            Assert.That(content, Does.Contain("KernelDiagnosticsAsmdefBoundaryTests.cs"));
            Assert.That(content, Does.Contain("Tools/Run-UnityTests.ps1"));
            Assert.That(content, Does.Contain("Tools/Run-M15.4Gate.ps1"));
            Assert.That(content, Does.Contain("gameplay/application proof may not be used to close M3"));
            Assert.That(content, Does.Contain("does not by itself close M6 full-proof aggregation"));
            Assert.That(content, Does.Contain("Integration smoke may summarize the release claim"));
            Assert.That(content, Does.Contain("direct play by itself does not prove accepted live host migration"));
            Assert.That(content, Does.Contain("owns the canonical M6 gate-class bundle map"));
            Assert.That(content, Does.Contain("This catalog owns proof-family separation and current anchor identity"));
            Assert.That(content, Does.Contain("no gate-class row may replace a missing proof-family anchor"));
            Assert.That(content, Does.Contain("TC-V22-PA-12"));
        }

        [Test]
        public void V22Overview_DefinesContinuityAndAbolitionTargets()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "00_KernelV22CompletionOverviewSpec.md"));

            Assert.That(content, Does.Contain("existing command payload meaning"));
            Assert.That(content, Does.Contain("existing DynamicValue authoring surface"));
            Assert.That(content, Does.Contain("generated value-key identity continuity"));
            Assert.That(content, Does.Contain("01_KernelV22AuthorityAndServiceCensusSpec.md"));
            Assert.That(content, Does.Contain("05_KernelV22MilestoneOrderSpec.md"));
            Assert.That(content, Does.Contain("02_1_KernelV22CommandAndValueHostRemovalSpec.md"));
            Assert.That(content, Does.Contain("03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md"));
            Assert.That(content, Does.Contain("04_1_KernelV22FullProofAndReleaseHardeningSpec.md"));
            Assert.That(content, Does.Contain("KernelScopeHost"));
            Assert.That(content, Does.Contain("RuntimeResolverHub"));
            Assert.That(content, Does.Contain("CommandRunnerMB"));
            Assert.That(content, Does.Contain("BlackboardMB"));
            Assert.That(content, Does.Contain("Index/README.md"));
            Assert.That(content, Does.Contain("TC-V22-00-07"));
            Assert.That(content, Does.Contain("TC-V22-00-08"));
        }

        [Test]
        public void V22MilestoneSpec_M0_DefinesCanonicalArtifactsAndExitGates()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("### M0: Charter and Completion Package"));
            Assert.That(content, Does.Contain("Entry assumptions:"));
            Assert.That(content, Does.Contain("Required outputs:"));
            Assert.That(content, Does.Contain("Canonical V22-M0 artifact set:"));
            Assert.That(content, Does.Contain("01_KernelV22AuthorityAndServiceCensusSpec.md"));
            Assert.That(content, Does.Contain("KernelV22BaselineLedger.md"));
            Assert.That(content, Does.Contain("KernelV22ProofAnchorCatalog.md"));
            Assert.That(content, Does.Contain("five-class service-census vocabulary"));
            Assert.That(content, Does.Contain("Forbidden shortcuts:"));
            Assert.That(content, Does.Contain("direct-play-only or gameplay-only success"));
            Assert.That(content, Does.Contain("command/value host-removal proof"));
            Assert.That(content, Does.Contain("TC-V22-MO-07"));
        }

        [Test]
        public void V22AuthorityCensus_UsesFiveWayClassification()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "01_KernelV22AuthorityAndServiceCensusSpec.md"));

            Assert.That(content, Does.Contain("KernelCoreAuthority"));
            Assert.That(content, Does.Contain("KernelManagedFeatureService"));
            Assert.That(content, Does.Contain("HubOwnedRuntimeObject"));
            Assert.That(content, Does.Contain("AuthoringOnlyMonoBehaviour"));
            Assert.That(content, Does.Contain("DeleteTarget"));
            Assert.That(content, Does.Contain("TooltipChannelHubService"));
            Assert.That(content, Does.Contain("AnimationSpriteHubService"));
            Assert.That(content, Does.Contain("VerifiedCommandRuntimeBridge"));
            Assert.That(content, Does.Contain("BlackboardService"));
            Assert.That(content, Does.Contain("DynamicEvaluationRuntime"));
            Assert.That(content, Does.Contain("StatusEffectService"));
            Assert.That(content, Does.Contain("TC-V22-01-06"));
        }

        [Test]
        public void V22KernelOnlyHostSpec_DemotesLegacyRootsAndSceneHosts()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "02_KernelV22KernelOnlyHostSpec.md"));

            Assert.That(content, Does.Contain("KernelLiveBootOrchestrator"));
            Assert.That(content, Does.Contain("KernelRuntimeShell"));
            Assert.That(content, Does.Contain("AuthoringBridge"));
            Assert.That(content, Does.Contain("ProjectLifetimeScope"));
            Assert.That(content, Does.Contain("GlobalLifetimeScope"));
            Assert.That(content, Does.Contain("SceneLifetimeScope"));
            Assert.That(content, Does.Contain("KernelScopeHost"));
            Assert.That(content, Does.Contain("CommandRunnerMB"));
            Assert.That(content, Does.Contain("BlackboardMB"));
            Assert.That(content, Does.Contain("SceneService"));
            Assert.That(content, Does.Contain("LoadingScreenService"));
            Assert.That(content, Does.Contain("M1-4 Direct-Play Convergence and Mixed-Authority Rejection"));
            Assert.That(content, Does.Contain("V22-M1-HOST-005"));
            Assert.That(content, Does.Contain("TC-V22-02-08"));
        }

        [Test]
        public void V22CommandValueHostRemovalSpec_DefinesHostDemotionAndDeclarationSplit()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "02_1_KernelV22CommandAndValueHostRemovalSpec.md"));

            Assert.That(content, Does.Contain("CommandRunnerMB"));
            Assert.That(content, Does.Contain("BlackboardMB"));
            Assert.That(content, Does.Contain("CommandRunnerAuthoring"));
            Assert.That(content, Does.Contain("BlackboardAuthoring"));
            Assert.That(content, Does.Contain("VerifiedCommandRuntimeBridge"));
            Assert.That(content, Does.Contain("BlackboardService"));
            Assert.That(content, Does.Contain("DynamicEvaluationRuntime"));
            Assert.That(content, Does.Contain("kernel-owned command session"));
            Assert.That(content, Does.Contain("kernel-owned value session"));
            Assert.That(content, Does.Contain("V22-M2-CMD-001"));
            Assert.That(content, Does.Contain("V22-M2-VAL-003"));
            Assert.That(content, Does.Contain("TC-V22-021-06"));
        }

        [Test]
        public void V22MilestoneSpec_M1_DefinesHostOutputsAndConvergenceGate()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("### M1: Kernel-Only Live Host"));
            Assert.That(content, Does.Contain("02 live boot authority isolation"));
            Assert.That(content, Does.Contain("persistent-root demotion"));
            Assert.That(content, Does.Contain("runtime shell to scene handoff"));
            Assert.That(content, Does.Contain("direct-play convergence"));
            Assert.That(content, Does.Contain("live/direct paths no longer represent different host truths"));
            Assert.That(content, Does.Contain("SceneService or LoadingScreenService"));
            Assert.That(content, Does.Contain("TC-V22-MO-09"));
        }

        [Test]
        public void V22MilestoneSpec_M2_DefinesCommandValueHostRemovalGate()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("### M2: Command and Value Host Removal"));
            Assert.That(content, Does.Contain("02_1 command host demotion"));
            Assert.That(content, Does.Contain("value host demotion"));
            Assert.That(content, Does.Contain("declaration-only split"));
            Assert.That(content, Does.Contain("CommandRunnerMB or BlackboardMB participation"));
            Assert.That(content, Does.Contain("command/value sessions are still created ad hoc by scene-facing hosts"));
            Assert.That(content, Does.Contain("TC-V22-MO-11"));
        }

        [Test]
        public void V22ServiceFamilySpec_GroupsRepresentativeFamilies()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "03_KernelV22ServiceFamilyCutoverSpec.md"));

            Assert.That(content, Does.Contain("Boot and Scene Flow"));
            Assert.That(content, Does.Contain("UI and Scene Channels"));
            Assert.That(content, Does.Contain("Gameplay and Application"));
            Assert.That(content, Does.Contain("SceneFlowInstallerMB"));
            Assert.That(content, Does.Contain("LoadingScreenService"));
            Assert.That(content, Does.Contain("ModalStackChannelHubService"));
            Assert.That(content, Does.Contain("TooltipChannelHubService"));
            Assert.That(content, Does.Contain("ConversationChannelHubService"));
            Assert.That(content, Does.Contain("MeshChannelHubService"));
            Assert.That(content, Does.Contain("TraitListChannelRuntime"));
            Assert.That(content, Does.Contain("GridObjectChannelRuntime"));
            Assert.That(content, Does.Contain("GameStateMachine"));
            Assert.That(content, Does.Contain("StatusEffectService"));
            Assert.That(content, Does.Contain("M3-4 Acceptance Gate"));
            Assert.That(content, Does.Contain("gameplay/application proof may not be used to close M3"));
            Assert.That(content, Does.Contain("V22-M3-FAMILY-005"));
            Assert.That(content, Does.Contain("TC-V22-03-09"));
        }

        [Test]
        public void V22RepresentativeGameplaySpec_DefinesSlicesAndTraceableProof()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md"));

            Assert.That(content, Does.Contain("Scenes/GameScene.unity"));
            Assert.That(content, Does.Contain("Scenes/GameScene/GameScene.unity"));
            Assert.That(content, Does.Contain("GameStateMachineExecutors"));
            Assert.That(content, Does.Contain("ConversationExecutors"));
            Assert.That(content, Does.Contain("GridObjectChannelRuntime"));
            Assert.That(content, Does.Contain("TraitListChannelRuntime"));
            Assert.That(content, Does.Contain("StatusEffectService"));
            Assert.That(content, Does.Contain("TargetChannelHubService"));
            Assert.That(content, Does.Contain("AreaChannelHubService"));
            Assert.That(content, Does.Contain("CommandExecutionTrace"));
            Assert.That(content, Does.Contain("DynamicRuntimeLogUtility"));
            Assert.That(content, Does.Contain("V22-M4-PROOF-001"));
            Assert.That(content, Does.Contain("TC-V22-031-08"));
        }

        [Test]
        public void V22MilestoneSpec_M3_DefinesFamilyCutoverGate()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("### M3: Service Family Cutover A"));
            Assert.That(content, Does.Contain("Boot and Scene Flow family cutover"));
            Assert.That(content, Does.Contain("first channel family cutover"));
            Assert.That(content, Does.Contain("split-required family enforcement"));
            Assert.That(content, Does.Contain("gameplay/application proof remains deferred to M4"));
            Assert.That(content, Does.Contain("claiming M3 through GameScene-visible gameplay/application success alone"));
            Assert.That(content, Does.Contain("TC-V22-MO-13"));
        }

        [Test]
        public void V22MilestoneSpec_M4_DefinesRepresentativeGameplayGate()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("### M4: Service Family Cutover B"));
            Assert.That(content, Does.Contain("03_1 representative bundle inventory"));
            Assert.That(content, Does.Contain("Conversation and Dialogue slice"));
            Assert.That(content, Does.Contain("Grid and Trait presentation slice"));
            Assert.That(content, Does.Contain("real GameScene anchors"));
            Assert.That(content, Does.Contain("visible playability alone"));
            Assert.That(content, Does.Contain("TC-V22-MO-15"));
        }

        [Test]
        public void V22LegacyDeletionSpec_RequiresReleaseZeroAuthority()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "04_KernelV22LegacyDeletionAndHardeningSpec.md"));

            Assert.That(content, Does.Contain("Release Zero-Authority Rule"));
            Assert.That(content, Does.Contain("Allowed Temporary Residue Matrix"));
            Assert.That(content, Does.Contain("ScopeFeatureInstallerUtility"));
            Assert.That(content, Does.Contain("KernelScopeHost"));
            Assert.That(content, Does.Contain("RuntimeResolverHub"));
            Assert.That(content, Does.Contain("CommandRunnerMB"));
            Assert.That(content, Does.Contain("BlackboardMB"));
            Assert.That(content, Does.Contain("GetComponentsInChildren"));
            Assert.That(content, Does.Contain("Resources.Load"));
            Assert.That(content, Does.Contain("ancestor traversal"));
            Assert.That(content, Does.Contain("GameLib.Legacy.*"));
            Assert.That(content, Does.Contain("GameLib.Tests.*"));
            Assert.That(content, Does.Contain("M5-6 Compile Boundary and Package Quarantine"));
            Assert.That(content, Does.Contain("V22-M5-ASMDEF-001"));
            Assert.That(content, Does.Contain("V22-M5-GATE-001"));
            Assert.That(content, Does.Contain("TC-V22-04-10"));
        }

        [Test]
        public void V22MilestoneSpec_M5_DefinesDeletionAndCompileBoundaryGate()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("### M5: Compile Boundary and Legacy Deletion"));
            Assert.That(content, Does.Contain("04 residue inventory and preservation-floor outputs"));
            Assert.That(content, Does.Contain("accepted release execution no longer depends on runtime-capable legacy residue"));
            Assert.That(content, Does.Contain("current partial asmdef split"));
            Assert.That(content, Does.Contain("kernel-to-legacy and production-to-test inversion"));
            Assert.That(content, Does.Contain("representative gameplay/application success alone"));
            Assert.That(content, Does.Contain("TC-V22-MO-18"));
        }

        [Test]
        public void V22FullProofSpec_DefinesGateBundleAndFinalAggregation()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "04_1_KernelV22FullProofAndReleaseHardeningSpec.md"));

            Assert.That(content, Does.Contain("## Consumed Proof Families"));
            Assert.That(content, Does.Contain("## Required M6 Gate Bundle"));
            Assert.That(content, Does.Contain("## M6 Final Bundle Map"));
            Assert.That(content, Does.Contain("M6 is not another migration slice"));
            Assert.That(content, Does.Contain("SpecShape"));
            Assert.That(content, Does.Contain("Validation"));
            Assert.That(content, Does.Contain("Generation"));
            Assert.That(content, Does.Contain("RuntimeBehavior"));
            Assert.That(content, Does.Contain("Diagnostics"));
            Assert.That(content, Does.Contain("PerformanceRule"));
            Assert.That(content, Does.Contain("StaticRule"));
            Assert.That(content, Does.Contain("LegacyCompat"));
            Assert.That(content, Does.Contain("IntegrationSmoke"));
            Assert.That(content, Does.Contain("KernelMinimalBootPlayModeTests.cs"));
            Assert.That(content, Does.Contain("GameStateMachineMigrationTests.cs"));
            Assert.That(content, Does.Contain("ConversationDialogueMigrationTests.cs"));
            Assert.That(content, Does.Contain("StatusEffectServiceDependencyCaptureTests.cs"));
            Assert.That(content, Does.Contain("KernelDiagnosticsModelTests.cs"));
            Assert.That(content, Does.Contain("KernelPerformanceAllocationTests.cs"));
            Assert.That(content, Does.Contain("LegacyCompatBoundaryTests.cs"));
            Assert.That(content, Does.Contain("Run-M15.4Gate.ps1"));
            Assert.That(content, Does.Contain("canonical M6-1 bundle map owner"));
            Assert.That(content, Does.Contain("Current runner or lane anchor"));
            Assert.That(content, Does.Contain("Required review output"));
            Assert.That(content, Does.Contain("Failure code family"));
            Assert.That(content, Does.Contain("accepted live boot and representative GameScene evidence stays distinct"));
            Assert.That(content, Does.Contain("the final summary names gate class, profile, anchor set, and lower-layer failure reason"));
            Assert.That(content, Does.Contain("does not by itself close M6 final aggregation"));
            Assert.That(content, Does.Contain("The architecture is accepted only when it can prove that it would reject its own regressions."));
            Assert.That(content, Does.Contain("TC-V22-041-10"));
        }

        [Test]
        public void V22ImplementationPlan_DefinesExecutableMilestoneBoard()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "06_KernelV22ImplementationPlan.md"));

            Assert.That(content, Does.Contain("claimable milestones into executable implementation work packages"));
            Assert.That(content, Does.Contain("## Current Execution Constraints"));
            Assert.That(content, Does.Contain("Focused csproj builds"));
            Assert.That(content, Does.Contain("Unity batch execution in this workspace can false-green"));
            Assert.That(content, Does.Contain("partial kernel asmdef split"));
            Assert.That(content, Does.Contain("## Validation Ladder"));
            Assert.That(content, Does.Contain("## Global Milestone Execution Board"));
            Assert.That(content, Does.Contain("M0 is frozen input"));
            Assert.That(content, Does.Contain("### V22-IMP-M1-1 Live Entry Isolation"));
            Assert.That(content, Does.Contain("### V22-IMP-M2-1 Command Host Demotion"));
            Assert.That(content, Does.Contain("### V22-IMP-M3-1 Boot and Scene Flow Family Cutover"));
            Assert.That(content, Does.Contain("### V22-IMP-M4-1 GameStateMachine Representative Slice"));
            Assert.That(content, Does.Contain("### V22-IMP-M5-1 Scope and Resolver Residue Removal"));
            Assert.That(content, Does.Contain("### V22-IMP-M6-1 Final Bundle Map"));
            Assert.That(content, Does.Contain("### V22-IMP-M6-2 Accepted Live Boot and Convergence Bundle"));
            Assert.That(content, Does.Contain("### V22-IMP-M6-3 Representative GameScene Bundle"));
            Assert.That(content, Does.Contain("### V22-IMP-M6-4 Hardening, Static, Legacy, and Compile-Boundary Consolidation"));
            Assert.That(content, Does.Contain("### V22-IMP-M6-5 Performance and Report Aggregation"));
            Assert.That(content, Does.Contain("### V22-IMP-M6-6 Final Acceptance Gate"));
            Assert.That(content, Does.Not.Contain("### V22-IMP-M6-4 Final Runner and Report Shape"));
            Assert.That(content, Does.Contain("KernelLiveBootOrchestrator.cs"));
            Assert.That(content, Does.Contain("KernelV22LiveBootBundleTests.cs"));
            Assert.That(content, Does.Contain("KernelV22RepresentativeGameSceneBundleTests.cs"));
            Assert.That(content, Does.Contain("CommandRunnerMB.cs"));
            Assert.That(content, Does.Contain("BlackboardService.cs"));
            Assert.That(content, Does.Contain("SceneFlowInstallerMB.cs"));
            Assert.That(content, Does.Contain("ModalStackChannelHubService.cs"));
            Assert.That(content, Does.Contain("GameStateMachineExecutors.cs"));
            Assert.That(content, Does.Contain("ConversationExecutors.cs"));
            Assert.That(content, Does.Contain("ScopeFeatureInstallerUtility.cs"));
            Assert.That(content, Does.Contain("LegacyCompatBoundaryTests.cs"));
            Assert.That(content, Does.Contain("KernelPerformanceAllocationTests.cs"));
            Assert.That(content, Does.Contain("Run-UnityTests.ps1"));
            Assert.That(content, Does.Contain("canonical M6 bundle-map rows"));
            Assert.That(content, Does.Contain("current runner or lane anchor"));
            Assert.That(content, Does.Contain("proof-family separation and gate-class bundle ownership"));
            Assert.That(content, Does.Contain("accepted live-boot bundle separate from representative GameScene proof"));
            Assert.That(content, Does.Contain("checks the real GameScene anchors and required representative slice coverage"));
            Assert.That(content, Does.Contain("keep diagnostics snapshot, static-rule, legacy-compat, performance evidence, and final report aggregation out of this slice"));
            Assert.That(content, Does.Contain("do not start final report schema or runner aggregation in this slice"));
            Assert.That(content, Does.Contain("consume the M5 hardening bundle as explicit M6-4 intake"));
            Assert.That(content, Does.Contain("make static gates cover M5 residue anchors"));
            Assert.That(content, Does.Contain("keep legacy adapter metadata, profile bounds, removal visibility, and compile-boundary direction reviewable"));
            Assert.That(content, Does.Contain("produce one reviewable report shape that names gate class, profile, anchor, and failure reason"));
            Assert.That(content, Does.Contain("green integration smoke cannot rescue missing lower-layer evidence"));
            Assert.That(content, Does.Contain("One accepted-path milestone slice should be in progress at a time."));
            Assert.That(content, Does.Contain("## Completion Output per Milestone"));
            Assert.That(content, Does.Contain("TC-V22-06-10"));
        }

        [Test]
        public void V22MilestoneSpec_M6_DefinesFullProofAggregationGate()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("### M6: Full Proof and Release Hardening"));
            Assert.That(content, Does.Contain("04_1 proof-family intake"));
            Assert.That(content, Does.Contain("SpecShape through IntegrationSmoke"));
            Assert.That(content, Does.Contain("compile success, doc shape, or a single smoke run"));
            Assert.That(content, Does.Contain("treating M5 hardening proof as if it already aggregated M6"));
            Assert.That(content, Does.Contain("a failing lower layer invalidates the M6 claim"));
            Assert.That(content, Does.Contain("TC-V22-MO-20"));
            Assert.That(content, Does.Contain("TC-V22-MO-21"));
        }

        [Test]
        public void V22MilestoneSpec_OrdersKernelOnlyCompletionClaims()
        {
            string content = ReadDoc(Path.Combine("Docs", "v2.2", "05_KernelV22MilestoneOrderSpec.md"));

            Assert.That(content, Does.Contain("| M0 | Charter and Completion Package |"));
            Assert.That(content, Does.Contain("| M1 | Kernel-Only Live Host |"));
            Assert.That(content, Does.Contain("| M2 | Command and Value Host Removal |"));
            Assert.That(content, Does.Contain("| M3 | Service Family Cutover A |"));
            Assert.That(content, Does.Contain("| M4 | Service Family Cutover B |"));
            Assert.That(content, Does.Contain("| M5 | Compile Boundary and Legacy Deletion |"));
            Assert.That(content, Does.Contain("| M6 | Full Proof and Release Hardening |"));
            Assert.That(content, Does.Contain("M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6"));
            Assert.That(content, Does.Contain("mutually consistent"));
            Assert.That(content, Does.Contain("TC-V22-MO-05"));
        }
    }
}

