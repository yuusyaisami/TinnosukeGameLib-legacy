#nullable enable

using AuthoringComponentKind = Game.Kernel.Authoring.AuthoringComponentKind;
using AuthoringUnityObjectLink = Game.Kernel.Authoring.UnityObjectLink;
using AuthoringUnitySourceKind = Game.Kernel.Authoring.UnityAuthoringSourceKind;
using AuthoringUnitySourceLocation = Game.Kernel.Authoring.UnitySourceLocation;
using AuthoringUnityObjectLinkKind = Game.Kernel.Authoring.UnityObjectLinkKind;
using AuthoringUnityBridge = Game.Kernel.Authoring.UnityAuthoringBridge;
using KernelSourceLocationIR = Game.Kernel.IR.SourceLocationIR;
using KernelSourceLocationKind = Game.Kernel.IR.SourceLocationKind;
using NUnit.Framework;
using RuntimeUnityObjectLink = Game.Kernel.Boot.UnityObjectLink;
using RuntimeUnityObjectLinkKind = Game.Kernel.Boot.UnityObjectLinkKind;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class UnityAuthoringModelTests
    {
        [Test]
        public void AuthoringKinds_UseExplicitStableValues()
        {
            Assert.That((int)AuthoringUnitySourceKind.SceneObject, Is.EqualTo(10));
            Assert.That((int)AuthoringUnitySourceKind.PrefabAsset, Is.EqualTo(20));
            Assert.That((int)AuthoringUnitySourceKind.PrefabInstance, Is.EqualTo(30));
            Assert.That((int)AuthoringUnitySourceKind.PrefabVariant, Is.EqualTo(40));
            Assert.That((int)AuthoringUnitySourceKind.ScriptableObjectAsset, Is.EqualTo(50));
            Assert.That((int)AuthoringUnitySourceKind.GeneratedAsset, Is.EqualTo(60));
            Assert.That((int)AuthoringUnitySourceKind.CodeDefinedModule, Is.EqualTo(70));
            Assert.That((int)AuthoringUnitySourceKind.LegacyBridge, Is.EqualTo(90));

            Assert.That((int)AuthoringUnityObjectLinkKind.Unknown, Is.EqualTo(0));
            Assert.That((int)AuthoringUnityObjectLinkKind.Asset, Is.EqualTo(10));
            Assert.That((int)AuthoringUnityObjectLinkKind.Scene, Is.EqualTo(20));
            Assert.That((int)AuthoringUnityObjectLinkKind.Runtime, Is.EqualTo(30));
            Assert.That((int)AuthoringUnityObjectLinkKind.Selection, Is.EqualTo(40));

            Assert.That((int)AuthoringComponentKind.Declaration, Is.EqualTo(10));
            Assert.That((int)AuthoringComponentKind.Link, Is.EqualTo(20));
            Assert.That((int)AuthoringComponentKind.Bridge, Is.EqualTo(30));
            Assert.That((int)AuthoringComponentKind.ViewBinding, Is.EqualTo(40));
            Assert.That((int)AuthoringComponentKind.DebugOnly, Is.EqualTo(50));
            Assert.That((int)AuthoringComponentKind.LegacyAdapter, Is.EqualTo(90));
        }

        [Test]
        public void UnitySourceLocation_TracksSourceKindAndTraceability()
        {
            AuthoringUnitySourceLocation first = CreateUnitySourceLocation();
            AuthoringUnitySourceLocation same = CreateUnitySourceLocation();
            AuthoringUnitySourceLocation different = new AuthoringUnitySourceLocation(
                AuthoringUnitySourceKind.PrefabAsset,
                "guid-2",
                "Assets/Other.prefab",
                11,
                null,
                null,
                "OtherComponent",
                "other.path");

            Assert.That(first.IsSpecified, Is.True);
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain("SceneObject"));

            KernelSourceLocationIR kernelSource = AuthoringUnityBridge.ToKernelSourceLocation(first);
            Assert.That(kernelSource.IsSpecified, Is.True);
            Assert.That(kernelSource.Kind, Is.EqualTo(KernelSourceLocationKind.Unity));
            Assert.That(kernelSource.UnitySource.HasValue, Is.True);
            Assert.That(kernelSource.UnitySource!.Value.AssetGuid, Is.EqualTo("guid-1"));
            Assert.That(kernelSource.UnitySource!.Value.GameObjectPath, Is.EqualTo("Root/Authoring"));
        }

        [Test]
        public void UnitySourceLocation_RejectsUnknownOrKindMismatchedTraceability()
        {
            Assert.That(
                () => new AuthoringUnitySourceLocation(AuthoringUnitySourceKind.Unknown, "guid-1", "Assets/Test.prefab", 10, null, null, null, null),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());

            Assert.That(
                () => new AuthoringUnitySourceLocation(AuthoringUnitySourceKind.SceneObject, null, null, 10, "Assets/Scenes/Battle.unity", "Root/Authoring", null, null),
                Throws.ArgumentException);

            Assert.That(
                () => new AuthoringUnitySourceLocation(AuthoringUnitySourceKind.PrefabInstance, null, null, 10, "Assets/Scenes/Battle.unity", "Root/Authoring", null, null),
                Throws.ArgumentException);
        }

        [Test]
        public void UnityObjectLink_TracksTraceMetadataAndBridgesToRuntime()
        {
            AuthoringUnityObjectLink first = CreateUnityObjectLink();
            AuthoringUnityObjectLink same = CreateUnityObjectLink();
            AuthoringUnityObjectLink different = new AuthoringUnityObjectLink(RuntimeUnityObjectLinkKind.Asset, "asset-guid-2", 24, 5, "Other/Link");

            Assert.That(first.IsEmpty, Is.False);
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first.ToString(), Does.Contain("Scene"));

            RuntimeUnityObjectLink runtimeLink = AuthoringUnityBridge.ToRuntimeLink(first);
            Assert.That(runtimeLink.Kind, Is.EqualTo(RuntimeUnityObjectLinkKind.Scene));
            Assert.That(runtimeLink.SourceGuid, Is.EqualTo("scene-guid-1"));
            Assert.That(runtimeLink.LocalFileId, Is.EqualTo(101L));
            Assert.That(runtimeLink.RuntimeInstanceId, Is.EqualTo(77));
            Assert.That(runtimeLink.DebugName, Is.EqualTo("Scene/Root/Authoring"));
        }

        [Test]
        public void UnityObjectLink_RejectsInvalidConstruction()
        {
            Assert.That(
                () => new AuthoringUnityObjectLink(AuthoringUnityObjectLinkKind.Unknown, "scene-guid-1", 101, 77, "Scene/Root/Authoring"),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());

            Assert.That(
                () => new AuthoringUnityObjectLink(AuthoringUnityObjectLinkKind.Scene, "   ", 101, 77, "Scene/Root/Authoring"),
                Throws.ArgumentException);

            Assert.That(
                () => new AuthoringUnityObjectLink(AuthoringUnityObjectLinkKind.Scene, "scene-guid-1", 0, 77, "Scene/Root/Authoring"),
                Throws.ArgumentException);

            Assert.That(
                () => new AuthoringUnityObjectLink(AuthoringUnityObjectLinkKind.Scene, "scene-guid-1", 101, 77, "   "),
                Throws.ArgumentException);
        }

        [Test]
        public void UnityObjectLinkAuthoring_ExposesLinkClassification()
        {
            Assert.That(Game.Kernel.Boot.UnityObjectLinkAuthoring.ComponentKind, Is.EqualTo(AuthoringComponentKind.Link));
        }

        static AuthoringUnitySourceLocation CreateUnitySourceLocation()
        {
            return new AuthoringUnitySourceLocation(
                AuthoringUnitySourceKind.SceneObject,
                "guid-1",
                "Assets/Scene.prefab",
                10,
                "Assets/Scenes/Battle.unity",
                "Root/Authoring",
                "BattleAuthoring",
                "authoring.value");
        }

        static AuthoringUnityObjectLink CreateUnityObjectLink()
        {
            return new AuthoringUnityObjectLink(
                AuthoringUnityObjectLinkKind.Scene,
                "scene-guid-1",
                101,
                77,
                "Scene/Root/Authoring");
        }
    }
}