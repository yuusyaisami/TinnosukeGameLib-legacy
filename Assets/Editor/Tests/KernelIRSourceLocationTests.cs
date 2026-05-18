using System;
using Game.Kernel.IR;
using NUnit.Framework;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelIRSourceLocationTests
    {
        [Test]
        public void SourceLocationKind_UsesExplicitStableValues()
        {
            Assert.That((int)SourceLocationKind.Unknown, Is.EqualTo(0));
            Assert.That((int)SourceLocationKind.Unity, Is.EqualTo(10));
            Assert.That((int)SourceLocationKind.Legacy, Is.EqualTo(20));
            Assert.That((int)SourceLocationKind.Generated, Is.EqualTo(30));
        }

        [Test]
        public void UnitySourceLocation_PreservesNormalizedFields()
        {
            UnitySourceLocation source = new UnitySourceLocation(
                assetGuid: "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath: "Assets/Game/Kernel/BattleScope.prefab",
                localFileId: 12001,
                scenePath: "Assets/Scenes/Battle.unity",
                gameObjectPath: "BattleRoot/Scopes/BattleScope",
                componentType: "BattleScopeAuthoring",
                propertyPath: "services[0].binding");

            Assert.That(source.AssetGuid, Is.EqualTo("4f4f3b04b1e44671b9f1b6a8613bb2d6"));
            Assert.That(source.AssetPath, Is.EqualTo("Assets/Game/Kernel/BattleScope.prefab"));
            Assert.That(source.LocalFileId, Is.EqualTo(12001));
            Assert.That(source.ScenePath, Is.EqualTo("Assets/Scenes/Battle.unity"));
            Assert.That(source.GameObjectPath, Is.EqualTo("BattleRoot/Scopes/BattleScope"));
            Assert.That(source.ComponentType, Is.EqualTo("BattleScopeAuthoring"));
            Assert.That(source.PropertyPath, Is.EqualTo("services[0].binding"));
            Assert.That(source.ToString(), Does.Contain("BattleScopeAuthoring"));
        }

        [Test]
        public void UnitySourceLocation_DoesNotCollapseMissingAndEmptyTraceabilityFields()
        {
            UnitySourceLocation missingScenePath = new UnitySourceLocation(
                assetGuid: "guid-02",
                assetPath: "Assets/Game/Kernel/BattleScope.prefab",
                localFileId: 12001,
                scenePath: null,
                gameObjectPath: "BattleRoot/Scopes/BattleScope",
                componentType: "BattleScopeAuthoring",
                propertyPath: "services[0].binding");
            UnitySourceLocation emptyScenePath = new UnitySourceLocation(
                assetGuid: "guid-02",
                assetPath: "Assets/Game/Kernel/BattleScope.prefab",
                localFileId: 12001,
                scenePath: string.Empty,
                gameObjectPath: "BattleRoot/Scopes/BattleScope",
                componentType: "BattleScopeAuthoring",
                propertyPath: "services[0].binding");

            Assert.That(missingScenePath.ScenePath, Is.Null);
            Assert.That(emptyScenePath.ScenePath, Is.EqualTo(string.Empty));
            Assert.That(missingScenePath, Is.Not.EqualTo(emptyScenePath));
        }

        [Test]
        public void LegacySourceLocation_PreservesOriginFields()
        {
            LegacySourceLocation source = new LegacySourceLocation(
                legacySystemName: "RuntimeLifetimeScope.IFeatureInstaller",
                legacyOrigin: "BattleInstaller/RegisterServices",
                migrationAdapter: "LegacyScopeInstallerAdapter");

            Assert.That(source.LegacySystemName, Is.EqualTo("RuntimeLifetimeScope.IFeatureInstaller"));
            Assert.That(source.LegacyOrigin, Is.EqualTo("BattleInstaller/RegisterServices"));
            Assert.That(source.MigrationAdapter, Is.EqualTo("LegacyScopeInstallerAdapter"));
            Assert.That(source.ToString(), Does.Contain("LegacyScopeInstallerAdapter"));
        }

        [Test]
        public void GeneratedSourceLocation_PreservesGenerationFields()
        {
            GeneratedSourceLocation source = new GeneratedSourceLocation(
                generatorName: "CommandCatalogProjector",
                generatedFrom: "ModuleContribution:BattleModule",
                generationPhase: "Build");

            Assert.That(source.GeneratorName, Is.EqualTo("CommandCatalogProjector"));
            Assert.That(source.GeneratedFrom, Is.EqualTo("ModuleContribution:BattleModule"));
            Assert.That(source.GenerationPhase, Is.EqualTo("Build"));
            Assert.That(source.ToString(), Does.Contain("ModuleContribution:BattleModule"));
        }

        [Test]
        public void LegacySourceLocation_RejectsMissingRequiredOriginFields()
        {
            SourceLocationIR unityInvalid = default;

            ArgumentException legacySystemException = Assert.Throws<ArgumentException>(() => unityInvalid = new SourceLocationIR(
                new LegacySourceLocation(null, "BattleInstaller/RegisterServices", "LegacyScopeInstallerAdapter")));
            ArgumentException legacyOriginException = Assert.Throws<ArgumentException>(() => unityInvalid = new SourceLocationIR(
                new LegacySourceLocation("RuntimeLifetimeScope.IFeatureInstaller", "", "LegacyScopeInstallerAdapter")));

            Assert.That(legacySystemException!.ParamName, Is.EqualTo("LegacySystemName"));
            Assert.That(legacyOriginException!.ParamName, Is.EqualTo("LegacyOrigin"));
        }

        [Test]
        public void GeneratedSourceLocation_RejectsMissingRequiredOriginFields()
        {
            SourceLocationIR generatedInvalid = default;

            ArgumentException generatorNameException = Assert.Throws<ArgumentException>(() => generatedInvalid = new SourceLocationIR(
                new GeneratedSourceLocation(null, "ModuleContribution:BattleModule", "Build")));
            ArgumentException generatedFromException = Assert.Throws<ArgumentException>(() => generatedInvalid = new SourceLocationIR(
                new GeneratedSourceLocation("CommandCatalogProjector", "", "Build")));

            Assert.That(generatorNameException!.ParamName, Is.EqualTo("GeneratorName"));
            Assert.That(generatedFromException!.ParamName, Is.EqualTo("GeneratedFrom"));
        }

        [Test]
        public void SourceLocationIR_PreservesUnityVariantEqualityAndHashCode()
        {
            UnitySourceLocation unitySource = new UnitySourceLocation(
                assetGuid: "guid-01",
                assetPath: "Assets/Game/Kernel/Player.prefab",
                localFileId: 91,
                scenePath: string.Empty,
                gameObjectPath: "Player",
                componentType: "PlayerAuthoring",
                propertyPath: "commands[1]");

            SourceLocationIR first = new SourceLocationIR(unitySource);
            SourceLocationIR same = new SourceLocationIR(unitySource);
            SourceLocationIR different = new SourceLocationIR(new LegacySourceLocation("LegacyRunner", "PlayerInstaller", "CompatBridge"));

            Assert.That(first.Kind, Is.EqualTo(SourceLocationKind.Unity));
            Assert.That(first.UnitySource.HasValue, Is.True);
            Assert.That(first.UnitySource.Value, Is.EqualTo(unitySource));
            Assert.That(first.LegacySource.HasValue, Is.False);
            Assert.That(first.GeneratedSource.HasValue, Is.False);
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first.ToString(), Does.Contain("Unity"));
        }

        [Test]
        public void SourceLocationIR_DefaultValue_RemainsInvalidSentinel()
        {
            SourceLocationIR source = default;

            Assert.That(source.Kind, Is.EqualTo(SourceLocationKind.Unknown));
            Assert.That(source.IsSpecified, Is.False);
            Assert.That(source.UnitySource.HasValue, Is.False);
            Assert.That(source.LegacySource.HasValue, Is.False);
            Assert.That(source.GeneratedSource.HasValue, Is.False);
            Assert.That(source.ToString(), Is.EqualTo("SourceLocationIR(<invalid>)"));
        }

        [Test]
        public void SourceLocationIR_RejectsUnknownKindConstruction()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new SourceLocationIR(
                SourceLocationKind.Unknown,
                unitySource: null,
                legacySource: null,
                generatedSource: null));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("cannot be constructed"));
        }

        [Test]
        public void SourceLocationIR_RejectsMismatchedKindAndVariantPayload()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new SourceLocationIR(
                SourceLocationKind.Unity,
                unitySource: null,
                legacySource: new LegacySourceLocation("RuntimeResolverHub", "Resolve", "LegacyResolverAdapter"),
                generatedSource: null));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("Unity source locations"));
        }
    }
}