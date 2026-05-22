#nullable enable

using System;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game;
using Game.Channel;
using Game.Commands.VNext;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class AnimationSpriteMigrationTests
    {
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly System.Collections.Generic.List<GameObject> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _temporaryObjects.Count; i++)
            {
                if (_temporaryObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_temporaryObjects[i]);
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void AnimationSpriteHubAuthoring_IsDeclarationSurfaceAndAdapterBase()
        {
            Assert.That(AnimationSpriteHubAuthoring.ComponentKind, Is.EqualTo(Game.Kernel.Authoring.AuthoringComponentKind.Declaration));
            Assert.That(typeof(AnimationSpriteHubAuthoring).IsAssignableFrom(typeof(AnimationSpriteHubMB)), Is.True);
            Assert.That(typeof(IScopeInstaller).IsAssignableFrom(typeof(AnimationSpriteHubMB)), Is.True);
        }

        [Test]
        public void AnimationSpriteChannelDef_SourceNoLongerUsesHierarchyDiscoveryFallback()
        {
            string content = ReadSource(Path.Combine("GameLib", "Script", "Project", "Scene", "Channels", "SpriteAnimation", "AnimationSpriteChannelDef.cs"));

            Assert.That(content, Does.Not.Contain("GetComponentInChildren"));
        }

        [Test]
        public void AnimationSpriteChannelExecutor_SourceNoLongerUsesHierarchyDiscoveryFallback()
        {
            string content = ReadSource(Path.Combine("GameLib", "Script", "Common", "Commands", "VNext", "Executors", "Channels", "AnimationSpriteChannelExecutor.cs"));

            Assert.That(content, Does.Not.Contain("EnumerateSubtree"));
            Assert.That(content, Does.Not.Contain("EnumerateAncestors"));
        }

        [Test]
        public void AnimationSpriteChannelCommandData_DefaultTagIsNoLongerInvented()
        {
            string content = ReadSource(Path.Combine("GameLib", "Script", "Common", "Commands", "VNext", "Commands", "Channels", "AnimationSpriteChannelCommandData.cs"));

            Assert.That(content, Does.Contain("ChannelTag = string.Empty"));
            Assert.That(content, Does.Not.Contain("ChannelTag = \"default\""));
        }

        [Test]
        public void AnimationSpriteHubService_RejectsBlankHubTag()
        {
            var validChannel = CreateChannelDef("alpha", withExplicitTarget: true);

            Assert.Throws<ArgumentException>(() => new AnimationSpriteHubService(
                new[] { validChannel },
                new StubScopeNode(),
                new NoOpCommandRunner(),
                hubTag: " "));
        }

        [Test]
        public void AnimationSpriteHubService_RejectsBlankChannelTags()
        {
            var validChannel = CreateChannelDef(" ", withExplicitTarget: true);

            Assert.Throws<ArgumentException>(() => new AnimationSpriteHubService(
                new[] { validChannel },
                new StubScopeNode(),
                new NoOpCommandRunner(),
                hubTag: "alpha"));
        }

        [Test]
        public void AnimationSpriteHubService_RejectsDefaultChannelTags()
        {
            var validChannel = CreateChannelDef("default", withExplicitTarget: true);

            Assert.Throws<ArgumentException>(() => new AnimationSpriteHubService(
                new[] { validChannel },
                new StubScopeNode(),
                new NoOpCommandRunner(),
                hubTag: "alpha"));
        }

        [Test]
        public void AnimationSpriteHubService_RejectsMissingExplicitTargets()
        {
            var invalidChannel = CreateChannelDef("alpha", withExplicitTarget: false);

            Assert.Throws<InvalidOperationException>(() => new AnimationSpriteHubService(
                new[] { invalidChannel },
                new StubScopeNode(),
                new NoOpCommandRunner(),
                hubTag: "alpha"));
        }

        static string ReadSource(string relativePath)
        {
            string fullPath = Path.Combine(Application.dataPath, relativePath);
            Assert.That(File.Exists(fullPath), Is.True, "Missing source file: " + relativePath);
            return File.ReadAllText(fullPath);
        }

        AnimationSpriteChannelDef CreateChannelDef(string tag, bool withExplicitTarget)
        {
            var def = new AnimationSpriteChannelDef();
            SetBasePrivateField(def, "tag", tag);

            if (withExplicitTarget)
            {
                var targetObject = new GameObject("animation-sprite-target");
                var renderer = targetObject.AddComponent<SpriteRenderer>();
                SetPrivateField(def, "spriteRenderer", renderer);
                _temporaryObjects.Add(targetObject);
            }

            return def;
        }

        static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, PrivateInstance);
            Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
            field!.SetValue(target, value);
        }

        static void SetBasePrivateField<T>(object target, string fieldName, T value)
        {
            var field = typeof(ChannelDefBase).GetField(fieldName, PrivateInstance);
            Assert.That(field, Is.Not.Null, "Missing field: " + fieldName);
            field!.SetValue(target, value);
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
            public UniTask SetActiveAsync(bool active, bool isReset = false, System.Threading.CancellationToken ct = default) => UniTask.CompletedTask;
            public System.Collections.Generic.IReadOnlyList<IScopeNode>? GetPathFromRoot() => null;
        }

        sealed class NoOpCommandRunner : ICommandRunner
        {
            readonly IScopeNode _scope = new StubScopeNode();

            public IScopeNode Scope => _scope;

            public UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, System.Threading.CancellationToken ct, CommandRunOptions options)
            {
                return UniTask.FromResult(default(CommandRunResult));
            }

            public UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, System.Threading.CancellationToken ct, CommandRunOptions options)
            {
                return UniTask.FromResult(default(CommandRunResult));
            }

            public UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, System.Threading.CancellationToken ct, CommandRunOptions options)
            {
                return UniTask.FromResult(default(CommandRunResult));
            }
        }
    }
}
