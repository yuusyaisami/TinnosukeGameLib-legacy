#nullable enable
using System;
using System.Reflection;
using Game;
using Game.Channel;
using Game.Common;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class MeshChannelMigrationTests
    {
        [Test]
        public void MeshChannelHubAuthoring_IsDeclarationSurfaceAndAdapterBase()
        {
            Assert.That(MeshChannelHubAuthoring.ComponentKind, Is.EqualTo(Game.Kernel.Authoring.AuthoringComponentKind.Declaration));
            Assert.That(typeof(MeshChannelHubAuthoring).IsAssignableFrom(typeof(MeshChannelHubMB)), Is.True);
            Assert.That(typeof(IScopeInstaller).IsAssignableFrom(typeof(MeshChannelHubMB)), Is.True);
        }

        [Test]
        public void MeshChannelHubService_RejectsDuplicateTags()
        {
            GameObject owner = new GameObject("mesh-channel-migration-test");
            try
            {
                MeshChannelEntry[] entries =
                {
                    new MeshChannelEntry { Tag = "alpha" },
                    new MeshChannelEntry { Tag = "alpha" },
                };

                Assert.Throws<InvalidOperationException>(() =>
                    new MeshChannelHubService(entries, new StubScopeNode(), owner.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MeshRuntimeStateFactory_ResolvesMissingScopeVarsToConcreteStore()
        {
            Type factoryType = typeof(MeshChannelEntry).Assembly.GetType("Game.Channel.MeshRuntimeStateFactory", true)!;
            MethodInfo resolveVars = factoryType.GetMethod("ResolveVars", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                resolveVars.Invoke(null, new object[] { new StubScopeNode() }));

            Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException!.Message, Does.Contain("missing an IVarStore resolver"));
        }

        [Test]
        public void MeshChannelPlayerRuntime_RejectsBlankTag()
        {
            GameObject owner = new GameObject("mesh-channel-player-runtime-test");
            try
            {
                Assert.Throws<ArgumentException>(() => new MeshChannelPlayerRuntime(
                    " ",
                    MeshChannelDynamicValueFactory.FromManaged(new MeshDefinitionPreset()),
                    new StubScopeNode(),
                    owner.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MeshRuntimeStateFactory_RejectsBlankTrackKeys()
        {
            Type factoryType = typeof(MeshChannelEntry).Assembly.GetType("Game.Channel.MeshRuntimeStateFactory", true)!;
            MethodInfo resolveRegularTrack = factoryType.GetMethod("ResolveRegularTrack", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
            MeshTrackDefinition authored = new MeshTrackDefinition { Key = " ", Tag = "track" };

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                resolveRegularTrack.Invoke(null, new object[] { authored, null }));

            Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException!.Message, Does.Contain("Key"));
        }

        [Test]
        public void MeshRuntimeStateFactory_RejectsUnknownPresetTypes()
        {
            Type factoryType = typeof(MeshChannelEntry).Assembly.GetType("Game.Channel.MeshRuntimeStateFactory", true)!;
            MethodInfo createPlayerRuntime = factoryType.GetMethod("CreatePlayerRuntime", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                createPlayerRuntime.Invoke(null, new object?[] { null }));

            Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException!.Message, Does.Contain("Unsupported mesh track player preset type"));
        }

        sealed class StubScopeNode : IScopeNode
        {
            public IScopeNode? Parent => null;
            public IScopeIdentityService? Identity => null;
            public LifetimeScopeKind Kind => LifetimeScopeKind.None;
            public IRuntimeResolver? Resolver => null;
            public bool IsVisible => true;
            public bool IsActive => true;
            public bool TrySetVisible(bool visible, bool isReset = false) => false;
            public bool TrySetActive(bool active, bool isReset = false) => false;
            public Cysharp.Threading.Tasks.UniTask SetActiveAsync(bool active, bool isReset = false, System.Threading.CancellationToken ct = default) => Cysharp.Threading.Tasks.UniTask.CompletedTask;
            public System.Collections.Generic.IReadOnlyList<IScopeNode>? GetPathFromRoot() => null;
        }
    }
}
