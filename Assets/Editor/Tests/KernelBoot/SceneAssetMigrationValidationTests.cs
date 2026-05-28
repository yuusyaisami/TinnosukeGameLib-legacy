#nullable enable

using Game;
using Game.Commands;
using Game.Common;
using Game.Kernel.Authoring;
using Game.Kernel.Layers.Unity;
using NUnit.Framework;
using TinnosukeGameLib.Editor.KernelBoot;
using UnityEngine;

using Object = UnityEngine.Object;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class SceneAssetMigrationValidationTests
    {
        [Test]
        public void Validation_RejectsLegacyResidueAndMissingKernelAnchor()
        {
            GameObject root = new GameObject("SceneRoot");

            try
            {
                root.AddComponent<EntityIdentityMB>();
                root.AddComponent<CommandRunnerMB>();

                SceneAssetMigrationTarget target = new SceneAssetMigrationTarget(
                    SceneAssetMigrationAssetKind.Scene,
                    "Assets/Scenes/TestScene.unity",
                    "test-scene-guid",
                    new[]
                    {
                        typeof(EntityIdentityMB).FullName!,
                        typeof(SceneKernelHostMB).FullName!,
                    },
                    new[]
                    {
                        typeof(CommandRunnerMB).FullName!,
                    });

                SceneAssetMigrationAssetRecord record = SceneAssetMigrationReportService.ScanSceneRoots(target, new[] { root });
                SceneAssetMigrationReport report = new SceneAssetMigrationReport(new[] { record });
                AuthoringValidationReport validation = SceneAssetMigrationValidationService.Validate(report);

                Assert.That(record.HasRoots, Is.True);
                Assert.That(record.RequiredAnchors, Has.Count.EqualTo(1));
                Assert.That(record.RequiredAnchors[0].TypeName, Is.EqualTo(typeof(EntityIdentityMB).FullName));
                Assert.That(record.RequiredAnchors[0].GameObjectPath, Is.EqualTo("SceneRoot"));
                Assert.That(record.LegacyAnchors, Has.Count.EqualTo(1));
                Assert.That(record.LegacyAnchors[0].TypeName, Is.EqualTo(typeof(CommandRunnerMB).FullName));
                Assert.That(record.MissingRequiredAnchorTypeNames, Has.Count.EqualTo(1));
                Assert.That(record.MissingRequiredAnchorTypeNames[0], Is.EqualTo(typeof(SceneKernelHostMB).FullName));
                Assert.That(report.UnresolvedItemCount, Is.EqualTo(2));
                Assert.That(validation.IsValid, Is.False);
                Assert.That(ContainsIssue(validation, SceneAssetMigrationCodes.MissingRequiredAnchor), Is.True);
                Assert.That(ContainsIssue(validation, SceneAssetMigrationCodes.LegacyAnchorPresent), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Validation_AcceptsSceneShapeWithKernelAndSpawnAnchorsOnly()
        {
            GameObject root = new GameObject("GameSceneRoot");

            try
            {
                root.AddComponent<EntityIdentityMB>();
                root.AddComponent<SceneKernelHostMB>();
                root.AddComponent<SceneKernelSpawnDeclarationMB>();
                root.AddComponent<SceneKernelSpawnHostMB>();

                SceneAssetMigrationTarget target = new SceneAssetMigrationTarget(
                    SceneAssetMigrationAssetKind.Scene,
                    "Assets/Scenes/GameScene.unity",
                    "test-game-scene-guid",
                    new[]
                    {
                        typeof(EntityIdentityMB).FullName!,
                        typeof(SceneKernelHostMB).FullName!,
                        typeof(SceneKernelSpawnDeclarationMB).FullName!,
                        typeof(SceneKernelSpawnHostMB).FullName!,
                    },
                    new[]
                    {
                        typeof(RuntimeLifetimeScope).FullName!,
                        typeof(CommandRunnerMB).FullName!,
                        typeof(BlackboardMB).FullName!,
                    });

                SceneAssetMigrationAssetRecord record = SceneAssetMigrationReportService.ScanSceneRoots(target, new[] { root });
                SceneAssetMigrationReport report = new SceneAssetMigrationReport(new[] { record });
                AuthoringValidationReport validation = SceneAssetMigrationValidationService.Validate(report);

                Assert.That(record.IsValid, Is.True);
                Assert.That(record.LegacyAnchors, Is.Empty);
                Assert.That(record.MissingRequiredAnchorTypeNames, Is.Empty);
                Assert.That(report.IsValid, Is.True);
                Assert.That(validation.IsValid, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CreateDefaultTargets_DoNotTreatBlackboardMbAsLegacyAnchor()
        {
            IReadOnlyList<SceneAssetMigrationTarget> targets = SceneAssetMigrationReportService.CreateDefaultTargets();

            Assert.That(targets, Has.Count.EqualTo(2));
            Assert.That(targets[0].LegacyAnchorTypeNames, Does.Not.Contain(typeof(BlackboardMB).FullName));
            Assert.That(targets[1].LegacyAnchorTypeNames, Does.Not.Contain(typeof(BlackboardMB).FullName));
        }

        [Test]
        public void Validation_RejectsUnexpectedPrefabBaselineDrift()
        {
            SceneAssetMigrationReport report = new SceneAssetMigrationReport(
                new[]
                {
                    new SceneAssetMigrationAssetRecord(
                        new SceneAssetMigrationTarget(SceneAssetMigrationAssetKind.Scene, "Assets/Scenes/TitleScene.unity", "guid"),
                        System.Array.Empty<SceneAssetMigrationAnchorRecord>(),
                        System.Array.Empty<SceneAssetMigrationAnchorRecord>(),
                        System.Array.Empty<string>(),
                        hasRoots: true),
                },
                new[] { "Assets/Prefabs/Unexpected.prefab" });

            AuthoringValidationReport validation = SceneAssetMigrationValidationService.Validate(report);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.UnresolvedItemCount, Is.EqualTo(1));
            Assert.That(validation.IsValid, Is.False);
            Assert.That(ContainsIssue(validation, SceneAssetMigrationCodes.PrefabBaselineDrift), Is.True);
        }

        static bool ContainsIssue(AuthoringValidationReport report, string code)
        {
            for (int index = 0; index < report.Issues.Count; index++)
            {
                if (report.Issues[index].Code == code)
                    return true;
            }

            return false;
        }
    }
}