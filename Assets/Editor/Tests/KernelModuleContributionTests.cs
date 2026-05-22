using System;
using System.IO;
using Game.Kernel.Contributions;
using Game.Kernel.IR;
using NUnit.Framework;
using UnityEngine;

using KernelModuleKind = Game.Kernel.IR.ModuleKind;
using KernelModuleVersion = Game.Kernel.IR.ModuleVersion;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class KernelModuleContributionTests
    {
        [Test]
        public void ContributionContracts_UseExplicitStableEnumValues()
        {
            Assert.That((int)ContributionKind.ServiceContribution, Is.EqualTo(10));
            Assert.That((int)ContributionKind.CommandContribution, Is.EqualTo(20));
            Assert.That((int)ContributionKind.CodeGenerationContribution, Is.EqualTo(120));
            Assert.That((int)ContributionSource.SceneObject, Is.EqualTo(10));
            Assert.That((int)ContributionSource.LegacyBridge, Is.EqualTo(80));
            Assert.That((int)ContributionConflictPolicy.ValidationError, Is.EqualTo(10));
            Assert.That((int)KernelModuleKind.Feature, Is.EqualTo(10));
            Assert.That((int)KernelModuleKind.MigrationAdapter, Is.EqualTo(50));
        }

        [Test]
        public void ContributionAvailability_RejectsWhitespaceOnlyDeclarativeValues()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new ContributionAvailability(" ", null, null));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.ParamName, Is.EqualTo("profileId"));
        }

        [Test]
        public void ContributionItem_PreservesMetadataWithExplicitValidationErrorConflictPolicy()
        {
            ContributionDependencyDeclaration dependency = new ContributionDependencyDeclaration(
                ContributionKind.ServiceContribution,
                new ModuleId(9),
                "inventory-service",
                true);
            ContributionItem item = new ContributionItem(
                ContributionKind.CommandContribution,
                new ModuleId(3),
                ContributionSource.PrefabAsset,
                CreateUnitySourceLocation("Assets/Game/Kernel/BattleScope.prefab", "BattleRoot/Commands", "BattleCommandAuthoring", "commands[0]"),
                "battle-command",
                new ContributionAvailability("Battle", "Windows", "Desktop", ContributionEnvironment.Release),
                new[] { dependency },
                ContributionConflictPolicy.ValidationError,
                "Battle Command");

            Assert.That(item.Kind, Is.EqualTo(ContributionKind.CommandContribution));
            Assert.That(item.OwnerModuleId, Is.EqualTo(new ModuleId(3)));
            Assert.That(item.Source, Is.EqualTo(ContributionSource.PrefabAsset));
            Assert.That(item.StableId, Is.EqualTo("battle-command"));
            Assert.That(item.ConflictPolicy, Is.EqualTo(ContributionConflictPolicy.ValidationError));
            Assert.That(item.DebugName, Is.EqualTo("Battle Command"));
            Assert.That(item.Dependencies.Length, Is.EqualTo(1));
            Assert.That(item.Dependencies[0], Is.EqualTo(dependency));
        }

        [Test]
        public void ContributionItem_RejectsUnknownConflictPolicy()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => new ContributionItem(
                ContributionKind.CommandContribution,
                new ModuleId(3),
                ContributionSource.PrefabAsset,
                CreateUnitySourceLocation("Assets/Game/Kernel/BattleScope.prefab", "BattleRoot/Commands", "BattleCommandAuthoring", "commands[0]"),
                "battle-command",
                default,
                conflictPolicy: ContributionConflictPolicy.Unknown));

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.ParamName, Is.EqualTo("conflictPolicy"));
        }

        [Test]
        public void ContributionItem_RejectsMissingOwnerStableIdAndSpecifiedSourceLocation()
        {
            ArgumentException ownerException = Assert.Throws<ArgumentException>(() => new ContributionItem(
                ContributionKind.ServiceContribution,
                default,
                ContributionSource.CodeDefinedModule,
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                "battle-service",
                default));
            ArgumentException stableIdException = Assert.Throws<ArgumentException>(() => new ContributionItem(
                ContributionKind.ServiceContribution,
                new ModuleId(3),
                ContributionSource.CodeDefinedModule,
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                string.Empty,
                default));
            ArgumentException sourceLocationException = Assert.Throws<ArgumentException>(() => new ContributionItem(
                ContributionKind.ServiceContribution,
                new ModuleId(3),
                ContributionSource.CodeDefinedModule,
                default,
                "battle-service",
                default));

            Assert.That(ownerException!.ParamName, Is.EqualTo("ownerModuleId"));
            Assert.That(stableIdException!.ParamName, Is.EqualTo("stableId"));
            Assert.That(sourceLocationException!.ParamName, Is.EqualTo("sourceLocation"));
        }

        [Test]
        public void ModuleContributionData_SortsDeterministicallyAndRejectsDuplicateContributionKeys()
        {
            ContributionItem later = new ContributionItem(
                ContributionKind.ServiceContribution,
                new ModuleId(8),
                ContributionSource.CodeDefinedModule,
                CreateGeneratedSourceLocation("ServiceProjector", "BattleModule"),
                "zeta-service",
                default);
            ContributionItem earlier = new ContributionItem(
                ContributionKind.CommandContribution,
                new ModuleId(8),
                ContributionSource.CodeDefinedModule,
                CreateGeneratedSourceLocation("CommandProjector", "BattleModule"),
                "alpha-command",
                default);

            ModuleContributionData data = new ModuleContributionData(
                new ModuleId(8),
                "BattleModule",
                KernelModuleKind.Feature,
                new KernelModuleVersion(1),
                default,
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                new[] { ContributionKind.CommandContribution, ContributionKind.ServiceContribution },
                Array.Empty<ModuleId>(),
                Array.Empty<ModuleId>(),
                new[] { later, earlier });

            Assert.That(data.Items.Length, Is.EqualTo(2));
            Assert.That(data.Items[0].StableId, Is.EqualTo("zeta-service"));
            Assert.That(data.Items[1].StableId, Is.EqualTo("alpha-command"));

            ArgumentException duplicateException = Assert.Throws<ArgumentException>(() => new ModuleContributionData(
                new ModuleId(8),
                "BattleModule",
                KernelModuleKind.Feature,
                new KernelModuleVersion(1),
                default,
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                new[] { ContributionKind.ServiceContribution },
                Array.Empty<ModuleId>(),
                Array.Empty<ModuleId>(),
                new[]
                {
                    later,
                    new ContributionItem(
                        ContributionKind.ServiceContribution,
                        new ModuleId(8),
                        ContributionSource.CodeDefinedModule,
                        CreateGeneratedSourceLocation("ServiceProjector", "BattleModuleSecondary"),
                        "zeta-service",
                        default),
                }));

            Assert.That(duplicateException, Is.Not.Null);
            Assert.That(duplicateException!.Message, Does.Contain("Duplicate contribution identity"));
        }

        [Test]
        public void ModuleContributionData_RejectsDuplicateAndOverlappingModuleDependencies()
        {
            ContributionItem item = new ContributionItem(
                ContributionKind.ServiceContribution,
                new ModuleId(8),
                ContributionSource.CodeDefinedModule,
                CreateGeneratedSourceLocation("ServiceProjector", "BattleModule"),
                "zeta-service",
                default);

            ArgumentException duplicateRequiredException = Assert.Throws<ArgumentException>(() => new ModuleContributionData(
                new ModuleId(8),
                "BattleModule",
                KernelModuleKind.Feature,
                new KernelModuleVersion(1),
                default,
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                new[] { ContributionKind.ServiceContribution },
                new[] { new ModuleId(5), new ModuleId(5) },
                Array.Empty<ModuleId>(),
                new[] { item }));
            ArgumentException overlapException = Assert.Throws<ArgumentException>(() => new ModuleContributionData(
                new ModuleId(8),
                "BattleModule",
                KernelModuleKind.Feature,
                new KernelModuleVersion(1),
                default,
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                new[] { ContributionKind.ServiceContribution },
                new[] { new ModuleId(5) },
                new[] { new ModuleId(5) },
                new[] { item }));

            Assert.That(duplicateRequiredException, Is.Not.Null);
            Assert.That(duplicateRequiredException!.ParamName, Is.EqualTo("requiredModuleIds"));
            Assert.That(overlapException, Is.Not.Null);
            Assert.That(overlapException!.Message, Does.Contain("must not overlap"));
        }

        [Test]
        public void ModuleDefinition_CollectContributions_PreservesModuleMetadataAndEnforcesOwnedKinds()
        {
            TestModuleDefinition definition = new TestModuleDefinition(
                new ModuleId(12),
                "BattleModule",
                KernelModuleKind.Feature,
                new KernelModuleVersion(2),
                new ContributionAvailability("Battle", "Windows", "Desktop", ContributionEnvironment.Release),
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                new[] { ContributionKind.CommandContribution, ContributionKind.ServiceContribution },
                new[]
                {
                    new ContributionItem(
                        ContributionKind.ServiceContribution,
                        new ModuleId(12),
                        ContributionSource.CodeDefinedModule,
                        CreateGeneratedSourceLocation("ServiceProjector", "BattleModule"),
                        "battle-service",
                        default),
                    new ContributionItem(
                        ContributionKind.CommandContribution,
                        new ModuleId(12),
                        ContributionSource.CodeDefinedModule,
                        CreateGeneratedSourceLocation("CommandProjector", "BattleModule"),
                        "battle-command",
                        default),
                });

            ModuleContributionData data = definition.CollectContributions();

            Assert.That(data.ModuleId, Is.EqualTo(new ModuleId(12)));
            Assert.That(data.ModuleName, Is.EqualTo("BattleModule"));
            Assert.That(data.ModuleKind, Is.EqualTo(KernelModuleKind.Feature));
            Assert.That(data.ModuleVersion, Is.EqualTo(new KernelModuleVersion(2)));
            Assert.That(data.Items.Length, Is.EqualTo(2));
            Assert.That(data.Items[0].StableId, Is.EqualTo("battle-service"));

            TestModuleDefinition invalidDefinition = new TestModuleDefinition(
                new ModuleId(12),
                "BattleModule",
                KernelModuleKind.Feature,
                new KernelModuleVersion(2),
                default,
                CreateGeneratedSourceLocation("ModuleProjector", "BattleModule"),
                new[] { ContributionKind.CommandContribution },
                new[]
                {
                    new ContributionItem(
                        ContributionKind.ServiceContribution,
                        new ModuleId(12),
                        ContributionSource.CodeDefinedModule,
                        CreateGeneratedSourceLocation("ServiceProjector", "BattleModule"),
                        "battle-service",
                        default),
                });

            ArgumentException exception = Assert.Throws<ArgumentException>(() => invalidDefinition.CollectContributions());
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.Message, Does.Contain("owned contribution kinds"));
        }

        [Test]
        public void ContributionContracts_DoNotReferenceRuntimeBuildersOrHierarchyDiscoveryApis()
        {
            string contributionsPath = Path.Combine(Application.dataPath, "GameLib", "Script", "Kernel", "Contributions");
            string[] files = Directory.GetFiles(contributionsPath, "*.cs", SearchOption.TopDirectoryOnly);
            string[] forbiddenTokens =
            {
                "IRuntimeContainerBuilder",
                "IRuntimeResolver",
                "GetComponentsInChildren",
                "FindObjectsByType",
                "Transform.parent",
                "InstallScopeServices(",
            };

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string content = File.ReadAllText(files[fileIndex]);
                for (int tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(content, Does.Not.Contain(forbiddenTokens[tokenIndex]), "Forbidden token found in contribution contract file: " + Path.GetFileName(files[fileIndex]));
                }
            }
        }

        static SourceLocationIR CreateUnitySourceLocation(string assetPath, string gameObjectPath, string componentType, string propertyPath)
        {
            return new SourceLocationIR(new UnitySourceLocation(
                assetGuid: "4f4f3b04b1e44671b9f1b6a8613bb2d6",
                assetPath: assetPath,
                localFileId: 12001,
                scenePath: "Assets/Scenes/Battle.unity",
                gameObjectPath: gameObjectPath,
                componentType: componentType,
                propertyPath: propertyPath));
        }

        static SourceLocationIR CreateGeneratedSourceLocation(string generatorName, string generatedFrom)
        {
            return new SourceLocationIR(new GeneratedSourceLocation(generatorName, generatedFrom, "Build"));
        }

        sealed class TestModuleDefinition : ModuleDefinition
        {
            readonly ContributionItem[] items;

            public TestModuleDefinition(ModuleId id, string name, KernelModuleKind kind, KernelModuleVersion version, ContributionAvailability availability, SourceLocationIR sourceLocation, ContributionKind[] ownedContributionKinds, ContributionItem[] items)
                : base(id, name, kind, version, availability, sourceLocation, ownedContributionKinds)
            {
                this.items = items;
            }

            protected override ContributionItem[] CollectContributionItemsCore()
            {
                ContributionItem[] clone = new ContributionItem[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    clone[i] = items[i];
                }

                return clone;
            }
        }
    }
}

