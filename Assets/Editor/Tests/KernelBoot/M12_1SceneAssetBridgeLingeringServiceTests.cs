#nullable enable

using Game.Commands;
using Game.Common;
using Game.Kernel.Authoring;
using Game.Project.Scene.Runtime;
using NUnit.Framework;
using TinnosukeGameLib.Editor.KernelBoot;
using UnityEngine;

using Object = UnityEngine.Object;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class M12_1SceneAssetBridgeLingeringServiceTests
    {
        [Test]
        public void CreateAuditTargets_AugmentsDefaultLegacyAnchorsWithM12_1BridgeTypes()
        {
            IReadOnlyList<SceneAssetMigrationTarget> targets = M12_1SceneAssetBridgeLingeringService.CreateAuditTargets();

            Assert.That(targets, Has.Count.EqualTo(2));
            Assert.That(targets[0].LegacyAnchorTypeNames, Does.Contain(typeof(CommandRunnerMB).FullName));
            Assert.That(targets[0].LegacyAnchorTypeNames, Does.Contain(typeof(BlackboardMB).FullName));
            Assert.That(targets[1].LegacyAnchorTypeNames, Does.Contain(typeof(RuntimeManagerMB).FullName));
            Assert.That(targets[1].LegacyAnchorTypeNames, Does.Contain(typeof(RuntimeLifetimeScope).FullName));
        }

        [Test]
        public void Validate_FlagsBlackboardResidueAsM12_1BridgeLingering()
        {
            GameObject root = new GameObject("SceneRoot");

            try
            {
                root.AddComponent<EntityIdentityMB>();
                root.AddComponent<BlackboardMB>();

                SceneAssetMigrationTarget target = new SceneAssetMigrationTarget(
                    SceneAssetMigrationAssetKind.Scene,
                    "Assets/Scenes/TestScene.unity",
                    "test-scene-guid",
                    new[] { typeof(EntityIdentityMB).FullName! },
                    new[] { typeof(BlackboardMB).FullName! });

                SceneAssetMigrationAssetRecord record = SceneAssetMigrationReportService.ScanSceneRoots(target, new[] { root });
                SceneAssetMigrationReport report = new SceneAssetMigrationReport(new[] { record });
                AuthoringValidationReport validation = M12_1SceneAssetBridgeLingeringService.Validate(report);

                Assert.That(record.LegacyAnchors, Has.Count.EqualTo(1));
                Assert.That(record.LegacyAnchors[0].TypeName, Is.EqualTo(typeof(BlackboardMB).FullName));
                Assert.That(ContainsIssue(validation, M12_1AuditCodes.AssetBridgeLingering), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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