#nullable enable

using Game.Kernel.Authoring;
using Game.Kernel.Boot;
using Game.Kernel.IR;
using NUnit.Framework;
using TinnosukeGameLib.Editor.KernelBoot;
using UnityEngine;

using AuthoringUnitySourceLocation = Game.Kernel.Authoring.UnitySourceLocation;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ScopeAuthoringLinkTests
    {
        [Test]
        public void ScopeAuthoringLink_UsesExplicitDeclarationClassification()
        {
            GameObject gameObject = new GameObject("ScopeAuthoringLinkTests");

            try
            {
                ScopeAuthoringLink link = gameObject.AddComponent<ScopeAuthoringLink>();

                Assert.That(ScopeAuthoringLink.ComponentKind, Is.EqualTo(AuthoringComponentKind.Declaration));
                Assert.That(link.HasScopeAuthoringId, Is.False);
                Assert.That(link.TryValidate(out string failureReason), Is.False);
                Assert.That(failureReason, Does.Contain("ScopeAuthoringId"));

                link.SetAuthoringId(new ScopeAuthoringId(101));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    null,
                    null,
                    7,
                    "Assets/Scenes/Battle.unity",
                    "Root/Scope",
                    "ScopeAuthoringLink",
                    "scope.id");

                Assert.That(link.HasScopeAuthoringId, Is.True);
                Assert.That(link.ScopeAuthoringId, Is.EqualTo(new ScopeAuthoringId(101)));
                Assert.That(link.TryValidate(out failureReason), Is.True, failureReason);

                AuthoringUnitySourceLocation sourceLocation = link.CreateSourceLocation();
                Assert.That(sourceLocation.Kind, Is.EqualTo(UnityAuthoringSourceKind.SceneObject));
                Assert.That(sourceLocation.ScenePath, Is.EqualTo("Assets/Scenes/Battle.unity"));
                Assert.That(sourceLocation.GameObjectPath, Is.EqualTo("Root/Scope"));
                Assert.That(sourceLocation.ComponentType, Is.EqualTo("ScopeAuthoringLink"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ScopeAuthoringLink_ValidatesPrefabVariantBaseTrace()
        {
            GameObject gameObject = new GameObject("ScopeAuthoringLinkVariantTests");

            try
            {
                ScopeAuthoringLink link = gameObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(202));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.PrefabVariant,
                    "variant-guid-1",
                    "Assets/Scopes/Variant.prefab",
                    17,
                    null,
                    null,
                    "ScopeAuthoringLink",
                    null);

                Assert.That(link.TryValidate(out string failureReason), Is.False);
                Assert.That(failureReason, Does.Contain("base source trace"));

                link.SetBaseSourceLocation(
                    UnityAuthoringSourceKind.PrefabAsset,
                    "base-guid-1",
                    "Assets/Scopes/Base.prefab",
                    11,
                    null,
                    null,
                    "ScopeAuthoringLink",
                    null);

                Assert.That(link.HasBaseSourceLocation, Is.True);
                Assert.That(link.TryGetBaseSourceLocation(out AuthoringUnitySourceLocation baseLocation), Is.True);
                Assert.That(baseLocation.Kind, Is.EqualTo(UnityAuthoringSourceKind.PrefabAsset));
                Assert.That(baseLocation.AssetGuid, Is.EqualTo("base-guid-1"));
                Assert.That(link.TryValidate(out failureReason), Is.True, failureReason);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ScopeAuthoringLink_DetectsDuplicateIds()
        {
            GameObject firstObject = new GameObject("ScopeAuthoringLinkDuplicateA");
            GameObject secondObject = new GameObject("ScopeAuthoringLinkDuplicateB");
            GameObject thirdObject = new GameObject("ScopeAuthoringLinkDuplicateC");

            try
            {
                ScopeAuthoringLink first = firstObject.AddComponent<ScopeAuthoringLink>();
                ScopeAuthoringLink second = secondObject.AddComponent<ScopeAuthoringLink>();
                ScopeAuthoringLink third = thirdObject.AddComponent<ScopeAuthoringLink>();

                first.SetAuthoringId(new ScopeAuthoringId(301));
                second.SetAuthoringId(new ScopeAuthoringId(301));
                third.SetAuthoringId(new ScopeAuthoringId(302));

                first.SetSourceLocation(UnityAuthoringSourceKind.SceneObject, null, null, 1, "Assets/Scenes/One.unity", "Root/One", "ScopeAuthoringLink", null);
                second.SetSourceLocation(UnityAuthoringSourceKind.SceneObject, null, null, 2, "Assets/Scenes/Two.unity", "Root/Two", "ScopeAuthoringLink", null);
                third.SetSourceLocation(UnityAuthoringSourceKind.SceneObject, null, null, 3, "Assets/Scenes/Three.unity", "Root/Three", "ScopeAuthoringLink", null);

                ScopeAuthoringLinkValidationReport report = ScopeAuthoringLinkValidationUtility.Validate(new[] { first, second, third });

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Issues, Has.Count.EqualTo(1));
                Assert.That(report.Issues[0].Code, Is.EqualTo("UNITY_SCOPE_AUTHORING_DUPLICATE_ID"));
                Assert.That(report.Issues[0].AuthoringId, Is.EqualTo(new ScopeAuthoringId(301)));
                Assert.That(report.Issues[0].Primary.name, Is.EqualTo("ScopeAuthoringLinkDuplicateA"));
                Assert.That(report.Issues[0].Secondary != null, Is.True);
                Assert.That(report.Issues[0].Secondary!.name, Is.EqualTo("ScopeAuthoringLinkDuplicateB"));
                Assert.That(report.Issues[0].HasSecondarySourceLocation, Is.True);
                Assert.That(report.Issues[0].SecondarySourceLocation.ScenePath, Is.EqualTo("Assets/Scenes/Two.unity"));

                second.ClearAuthoringId();
                second.SetAuthoringId(new ScopeAuthoringId(401));

                report = ScopeAuthoringLinkValidationUtility.Validate(new[] { first, second, third });

                Assert.That(report.IsValid, Is.True);
                Assert.That(report.Issues, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(thirdObject);
            }
        }

        [Test]
        public void ScopeAuthoringLink_RootValidationReportsInvalidTrace()
        {
            GameObject root = new GameObject("ScopeAuthoringLinkRoot");
            GameObject child = new GameObject("ScopeAuthoringLinkInvalidChild");

            try
            {
                child.transform.SetParent(root.transform, false);

                ScopeAuthoringLink invalid = child.AddComponent<ScopeAuthoringLink>();
                invalid.SetAuthoringId(new ScopeAuthoringId(601));
                invalid.SetSourceLocation(
                    UnityAuthoringSourceKind.PrefabVariant,
                    "variant-guid-2",
                    "Assets/Scopes/VariantTwo.prefab",
                    19,
                    null,
                    null,
                    "ScopeAuthoringLink",
                    null);

                ScopeAuthoringLinkValidationReport report = ScopeAuthoringLinkValidationUtility.Validate(root);

                Assert.That(report.IsValid, Is.False);
                Assert.That(report.Issues, Has.Count.EqualTo(1));
                Assert.That(report.Issues[0].Code, Is.EqualTo("UNITY_SCOPE_AUTHORING_INVALID"));
                Assert.That(report.Issues[0].Message, Does.Contain("base source trace"));
                Assert.That(report.Issues[0].Primary, Is.EqualTo(invalid));
                Assert.That(report.Issues[0].Secondary, Is.Null);
                Assert.That(report.Issues[0].HasSecondarySourceLocation, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ScopeAuthoringLink_ExplicitRegenerationDoesNotChangeTrace()
        {
            GameObject gameObject = new GameObject("ScopeAuthoringLinkRegenerationTests");

            try
            {
                ScopeAuthoringLink link = gameObject.AddComponent<ScopeAuthoringLink>();
                link.SetAuthoringId(new ScopeAuthoringId(501));
                link.SetSourceLocation(
                    UnityAuthoringSourceKind.SceneObject,
                    null,
                    null,
                    5,
                    "Assets/Scenes/Trace.unity",
                    "Root/Trace",
                    "ScopeAuthoringLink",
                    "scope.id");

                AuthoringUnitySourceLocation before = link.CreateSourceLocation();

                link.ClearAuthoringId();
                Assert.That(link.HasScopeAuthoringId, Is.False);
                Assert.That(link.TryValidate(out string failureReason), Is.False);
                Assert.That(failureReason, Does.Contain("ScopeAuthoringId"));

                link.SetAuthoringId(new ScopeAuthoringId(502));
                AuthoringUnitySourceLocation after = link.CreateSourceLocation();

                Assert.That(before, Is.EqualTo(after));
                Assert.That(link.ScopeAuthoringId, Is.EqualTo(new ScopeAuthoringId(502)));
                Assert.That(link.TryValidate(out failureReason), Is.True, failureReason);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ScopeAuthoringIdentityPolicy_AllocatesAndResolvesIds()
        {
            GameObject rootObject = new GameObject("ScopeAuthoringPolicyRoot");
            GameObject firstObject = new GameObject("ScopeAuthoringPolicyFirst");
            GameObject secondObject = new GameObject("ScopeAuthoringPolicySecond");
            GameObject thirdObject = new GameObject("ScopeAuthoringPolicyThird");

            try
            {
                ScopeAuthoringRoot root = rootObject.AddComponent<ScopeAuthoringRoot>();
                ScopeAuthoringLink first = firstObject.AddComponent<ScopeAuthoringLink>();
                ScopeAuthoringLink second = secondObject.AddComponent<ScopeAuthoringLink>();
                ScopeAuthoringLink third = thirdObject.AddComponent<ScopeAuthoringLink>();

                first.SetAuthoringId(new ScopeAuthoringId(700));
                second.SetAuthoringId(new ScopeAuthoringId(700));
                root.RegisterExistingScopeAuthoringId(first.ScopeAuthoringId);
                root.RegisterExistingScopeAuthoringId(second.ScopeAuthoringId);

                ScopeAuthoringId allocated = ScopeAuthoringIdentityPolicy.AllocateNextAuthoringId(root);
                Assert.That(allocated, Is.EqualTo(new ScopeAuthoringId(701)));

                Assert.That(ScopeAuthoringIdentityPolicy.TryAssignMissingAuthoringId(root, third, out ScopeAuthoringId assignedId, out string failureReason), Is.True, failureReason);
                Assert.That(assignedId, Is.EqualTo(new ScopeAuthoringId(701)));
                Assert.That(third.ScopeAuthoringId, Is.EqualTo(new ScopeAuthoringId(701)));

                Assert.That(ScopeAuthoringIdentityPolicy.TryResolveDuplicateAuthoringId(root, second, out ScopeAuthoringId resolvedId, out failureReason), Is.True, failureReason);
                Assert.That(resolvedId.Value, Is.GreaterThan(701));
                Assert.That(second.ScopeAuthoringId, Is.EqualTo(resolvedId));

                ScopeAuthoringLinkValidationReport report = ScopeAuthoringLinkValidationUtility.Validate(new[] { first, second, third });
                Assert.That(report.IsValid, Is.True, "Duplicate resolution should leave a clean validation report.");
            }
            finally
            {
                Object.DestroyImmediate(rootObject);
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(thirdObject);
            }
        }
    }
}