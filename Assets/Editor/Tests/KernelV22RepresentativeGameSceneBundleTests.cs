#nullable enable
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelV22RepresentativeGameSceneBundleTests
    {
        [Test]
        public void RepresentativeGameSceneAssets_ContainCoreRepresentativeAnchors()
        {
            string sceneOne = ReadProjectFile(Path.Combine("Assets", "Scenes", "GameScene.unity"));
            string sceneTwo = ReadProjectFile(Path.Combine("Assets", "Scenes", "GameScene", "GameScene.unity"));

            AssertRepresentativeSceneAnchors(sceneOne);
            AssertRepresentativeSceneAnchors(sceneTwo);
        }

        [Test]
        public async System.Threading.Tasks.Task RepresentativeGameplaySliceTests_ExposeCoreDiagnostics()
        {
            GameStateMachineMigrationTests gameStateMachine = new GameStateMachineMigrationTests();
            gameStateMachine.ResolveServiceOrThrow_RejectsAncestorFallback();
            gameStateMachine.ResolveServiceOrThrow_RejectsNullOrigin();
            await gameStateMachine.ChangeGameStateExecutor_RejectsUnresolvedGameLogicRoot();

            ConversationDialogueMigrationTests conversation = new ConversationDialogueMigrationTests();
            await conversation.ConversationFlowExecutor_StrictRunFailsWhenHubIsMissing();
            await conversation.ConversationFlowExecutor_StrictRunFailsWhenTargetScopeResolutionFails();

            GridObjectAuthorityMigrationTests grid = new GridObjectAuthorityMigrationTests();
            await grid.BindGridObjectChannelExecutor_FailsWithV22DiagnosticWhenHubIsMissing();

            TraitListAuthorityMigrationTests trait = new TraitListAuthorityMigrationTests();
            trait.ApplyPayloadToBlackboard_DoesNotMergeBlackboardBackIntoCommandVars();

            StatusEffectServiceDependencyCaptureTests statusEffect = new StatusEffectServiceDependencyCaptureTests();
            await statusEffect.StatusEffectExecutor_ThrowsWhenServiceIsMissingOnResolvedScope();
            await statusEffect.WriteStatusEffectDataExecutor_ThrowsWhenTargetScopeCannotBeResolved();
            await statusEffect.WriteStatusEffectDataExecutor_ThrowsWhenRuntimeServiceScopeCannotBeResolved();

            GameplayAuthorityRegressionTests regression = new GameplayAuthorityRegressionTests();
            regression.ConversationResolveTargetScopeAsync_DoesNotFallbackToCurrentScope();
            regression.GridObjectResolveTargetScopeAsync_DoesNotFallbackToCurrentScope();
            regression.TraitListResolveTargetScopeAsync_DoesNotFallbackToCurrentScope();
        }

        [Test]
        public void RepresentativeGameplayBundle_RetainsTraceabilitySurfaces()
        {
            string representativeSpec = ReadProjectFile(Path.Combine("Assets", "Docs", "v2.2", "03_1_KernelV22RepresentativeGameplayAndApplicationCutoverSpec.md"));
            string traceabilityCatalog = ReadProjectFile(Path.Combine("Assets", "Docs", "v2", "Index", "DiagnosticCodeTraceabilityCatalog.md"));

            Assert.That(representativeSpec, Does.Contain("Scenes/GameScene.unity"));
            Assert.That(representativeSpec, Does.Contain("Scenes/GameScene/GameScene.unity"));
            Assert.That(representativeSpec, Does.Contain("CommandExecutionTrace.cs"));
            Assert.That(representativeSpec, Does.Contain("DynamicRuntimeLogUtility.cs"));
            Assert.That(representativeSpec, Does.Contain("TargetChannelHubService.cs"));
            Assert.That(representativeSpec, Does.Contain("AreaChannelHubService.cs"));

            Assert.That(traceabilityCatalog, Does.Contain("V22-M4-GSM-001"));
            Assert.That(traceabilityCatalog, Does.Contain("V22-M4-CONV-001"));
            Assert.That(traceabilityCatalog, Does.Contain("V22-M4-GRID-001"));
            Assert.That(traceabilityCatalog, Does.Contain("V22-M4-TRAIT-001"));
            Assert.That(traceabilityCatalog, Does.Contain("V22-M4-STATUS-001"));
            Assert.That(traceabilityCatalog, Does.Contain("V22-M4-SCALAR-001"));
        }

        static void AssertRepresentativeSceneAnchors(string sceneText)
        {
            Assert.That(sceneText, Does.Contain("m_EditorClassIdentifier: Assembly-CSharp::Game.Actions.GameStateMachineMB"));
            Assert.That(sceneText, Does.Contain("m_EditorClassIdentifier: Assembly-CSharp::Game.UI.TraitListChannelHubMB"));
            Assert.That(sceneText, Does.Contain("m_EditorClassIdentifier: Assembly-CSharp::Game.StatusEffect.StatusEffectMB"));
            Assert.That(sceneText, Does.Contain("type: {class: ConversationFlowCommandData, ns: Game.Commands.VNext, asm: Assembly-CSharp}"));
            Assert.That(sceneText, Does.Contain("type: {class: GridObjectChannelLayoutPreset, ns: Game.Channel, asm: Assembly-CSharp}"));
        }

        static string ReadProjectFile(string relativePath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.That(File.Exists(fullPath), Is.True, "Missing file: " + relativePath);
            return File.ReadAllText(fullPath);
        }
    }
}