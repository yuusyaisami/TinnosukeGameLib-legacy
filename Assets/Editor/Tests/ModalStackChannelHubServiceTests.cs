#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Game;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class ModalStackChannelHubServiceTests
    {
        [Test]
        public void ExplicitDefaultRoot_IsUsedAsCurrentInputRoot_WithoutFallbackDiscovery()
        {
            var hub = new ModalStackChannelHubService();
            hub.RegisterLayer(ModalLayerPreset.Default("modal"));

            var owner = new TestScopeNode("Owner");
            var defaultRoot = new TestModalRoot("DefaultRoot", owner);
            var overlayRoot = new TestModalRoot("OverlayRoot", owner);

            hub.SetDefaultRoot("modal", defaultRoot);

            Assert.That(hub.CurrentInputRoot, Is.SameAs(defaultRoot));
            Assert.That(hub.TryGetRootState(owner, out var defaultState), Is.True);
            Assert.That(defaultState.Root, Is.SameAs(defaultRoot));

            hub.PushModal("modal", overlayRoot);

            Assert.That(hub.CurrentInputRoot, Is.SameAs(overlayRoot));
            Assert.That(hub.TryGetRootState(owner, out var overlayState), Is.True);
            Assert.That(overlayState.Root, Is.SameAs(overlayRoot));

            Assert.That(hub.PopModal("modal", overlayRoot), Is.True);
            Assert.That(hub.CurrentInputRoot, Is.SameAs(defaultRoot));
        }

        [Test]
        public void ClearingExplicitRoot_UnbindsCurrentInputRoot()
        {
            var hub = new ModalStackChannelHubService();
            hub.RegisterLayer(ModalLayerPreset.Default("modal"));

            var owner = new TestScopeNode("Owner");
            var root = new TestModalRoot("DefaultRoot", owner);

            hub.SetDefaultRoot("modal", root);
            Assert.That(hub.CurrentInputRoot, Is.SameAs(root));

            hub.SetDefaultRoot("modal", null);

            Assert.That(hub.CurrentInputRoot, Is.Null);
            Assert.That(hub.TryGetRootState(owner, out _), Is.False);
        }

        [Test]
        public void ModalStackChannelHubMB_SourceNoLongerContainsHierarchyDiscoveryFallback()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "GameLib",
                "Script",
                "Project",
                "UI",
                "Core",
                "ModalStackChannel",
                "ModalStackChannelHubMB.cs"));

            Assert.That(source, Does.Not.Contain("GetComponentsInChildren"));
            Assert.That(source, Does.Not.Contain("GetComponents<Component>"));
            Assert.That(source, Does.Not.Contain("ownerRoot"));
            Assert.That(source, Does.Not.Contain("SetDefaultRoot(\"default\", ownerRoot)"));
        }

        sealed class TestModalRoot : IUIModalRoot
        {
            public TestModalRoot(string modalId, IScopeNode ownerScope)
            {
                ModalId = modalId;
                OwnerScope = ownerScope;
            }

            public string ModalId { get; }
            public bool IsActive => true;
            public IScopeNode? OwnerScope { get; }

            public bool IsDescendant(IScopeNode? target)
            {
                return ReferenceEquals(target, OwnerScope);
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            public TestScopeNode(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public IScopeNode? Parent => null;
            public IScopeIdentityService? Identity => null;
            public LifetimeScopeKind Kind => LifetimeScopeKind.Scene;
            public IRuntimeResolver? Resolver => null;
            public bool IsVisible => true;
            public bool IsActive => true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                _ = visible;
                _ = isReset;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                _ = active;
                _ = isReset;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, System.Threading.CancellationToken ct = default)
            {
                _ = active;
                _ = isReset;
                _ = ct;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }
        }
    }
}
