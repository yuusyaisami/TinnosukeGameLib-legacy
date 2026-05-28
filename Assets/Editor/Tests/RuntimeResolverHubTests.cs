#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace TinnosukeGameLib.Tests.Editor
{
    [TestFixture]
    public sealed class RuntimeResolverHubTests
    {
        [Test]
        public void TryResolve_UsesExplicitRegistration()
        {
            ExplicitService service = new ExplicitService();
            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            builder.RegisterInstance<ExplicitService>(service);

            IRuntimeResolver resolver = builder.Build();
            try
            {
                Assert.That(resolver.TryResolve<ExplicitService>(out ExplicitService resolved), Is.True);
                Assert.That(resolved, Is.SameAs(service));
                Assert.That(resolver.Resolve<ExplicitService>(), Is.SameAs(service));
            }
            finally
            {
                resolver.Dispose();
            }
        }

        [Test]
        public void TryResolve_ResolvesHostScopeOnlyByExactType()
        {
            GameObject gameObject = new GameObject("runtime-resolver-host-probe");
            try
            {
                FallbackProbeScopeNode scope = gameObject.AddComponent<FallbackProbeScopeNode>();

                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                builder.SetHostScope(scope);

                IRuntimeResolver resolver = builder.Build();
                try
                {
                    Assert.That(resolver.TryResolve<IScopeNode>(out IScopeNode resolvedScope), Is.True);
                    Assert.That(resolvedScope, Is.SameAs(scope));
                    Assert.That(resolver.TryResolve<FallbackProbeScopeNode>(out FallbackProbeScopeNode resolvedConcrete), Is.True);
                    Assert.That(resolvedConcrete, Is.SameAs(scope));
                }
                finally
                {
                    resolver.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TryResolve_DoesNotUseHostComponentFallback()
        {
            GameObject gameObject = new GameObject("runtime-resolver-fallback-probe");
            try
            {
                FallbackProbeScopeNode scope = gameObject.AddComponent<FallbackProbeScopeNode>();
                gameObject.AddComponent<ComponentProbeService>();

                RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
                builder.SetHostScope(scope);

                IRuntimeResolver resolver = builder.Build();
                try
                {
                    Assert.That(resolver.TryResolve<ComponentProbeService>(out ComponentProbeService resolved), Is.False);
                    Assert.That(resolved, Is.Null);
                    Assert.That(resolver.ResolveOrDefault(typeof(ComponentProbeService)), Is.Null);
                }
                finally
                {
                    resolver.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Resolve_FailsClosedWhenConstructorDependencyIsMissing()
        {
            RuntimeContainerBuilder builder = new RuntimeContainerBuilder();
            builder.Register<MissingDependencyConsumer>(RuntimeLifetime.Transient);

            IRuntimeResolver resolver = builder.Build();
            try
            {
                Assert.That(() => resolver.Resolve<MissingDependencyConsumer>(), Throws.InvalidOperationException);
            }
            finally
            {
                resolver.Dispose();
            }
        }

        sealed class ExplicitService
        {
        }

        sealed class MissingDependencyConsumer
        {
            public MissingDependencyConsumer(ExplicitService dependency)
            {
                Dependency = dependency;
            }

            public ExplicitService Dependency { get; }
        }

        sealed class ComponentProbeService : MonoBehaviour
        {
        }

        sealed class FallbackProbeScopeNode : MonoBehaviour, IScopeNode
        {
            public IScopeNode? Parent { get; set; }

            public ILTSIdentityService? Identity { get; set; }

            public LifetimeScopeKind Kind => LifetimeScopeKind.Runtime;

            public IRuntimeResolver? Resolver { get; set; }

            public bool IsVisible { get; private set; } = true;

            public bool IsActive { get; private set; } = true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                IsVisible = visible;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                IsActive = active;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                IsActive = active;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                return Array.Empty<IScopeNode>();
            }
        }
    }

    [TestFixture]
    public sealed class UINodeGraphRuntimeTests
    {
        [Test]
        public void UIElementStateService_AcquireRegistersHandleAndPatchesDeferredNavigationOverride()
        {
            UINodeGraphService graph = new UINodeGraphService();
            TestRuntimeResolver resolver = new TestRuntimeResolver()
                .Register<IUINodeGraphService>(graph)
                .Register<IUINodeGraphTelemetry>(graph);

            GameObject rootObject = new GameObject("ui-node-root");
            GameObject sourceObject = new GameObject("ui-node-source");
            GameObject targetObject = new GameObject("ui-node-target");

            try
            {
                UIElementStateMB sourceComponent = sourceObject.AddComponent<UIElementStateMB>();
                UIElementStateMB targetComponent = targetObject.AddComponent<UIElementStateMB>();

                TestScopeNode rootScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "root", rootObject.transform)
                };

                TestScopeNode sourceScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = rootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "source", sourceObject.transform)
                };

                TestScopeNode targetScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = rootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "target", targetObject.transform)
                };

                TestUIElementStateOptions sourceOptions = new TestUIElementStateOptions
                {
                    NavigationOverride = new NavigationOverride
                    {
                        Right = targetComponent
                    }
                };

                UIElementStateService sourceService = new UIElementStateService(
                    sourceScope,
                    sourceOptions,
                    null,
                    new TestCommandRunner(sourceScope));

                UIElementStateService targetService = new UIElementStateService(
                    targetScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(targetScope));

                sourceService.OnAcquire(sourceScope, isReset: false);

                Assert.That(sourceService.NodeHandle.IsValid, Is.True);
                Assert.That(graph.TryGetHandle(sourceScope, out UINodeHandle sourceHandle), Is.True);
                Assert.That(sourceHandle, Is.EqualTo(sourceService.NodeHandle));
                Assert.That(graph.TryGetNavigationTarget(sourceHandle, NavigateDirection.Right, out _), Is.False);

                targetService.OnAcquire(targetScope, isReset: false);

                Assert.That(targetService.NodeHandle.IsValid, Is.True);
                Assert.That(graph.NodeCount, Is.EqualTo(2));
                Assert.That(graph.TryGetHandle(targetScope, out UINodeHandle targetHandle), Is.True);
                Assert.That(graph.TryGetNavigationTarget(sourceHandle, NavigateDirection.Right, out UINodeHandle resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(targetHandle));

                targetService.OnRelease(targetScope, isDestroy: false);

                Assert.That(targetService.NodeHandle.IsValid, Is.False);
                Assert.That(graph.TryGetNavigationTarget(sourceHandle, NavigateDirection.Right, out _), Is.False);
                Assert.That(graph.TryGetHandle(targetScope, out _), Is.False);

                sourceService.OnRelease(sourceScope, isDestroy: false);

                Assert.That(sourceService.NodeHandle.IsValid, Is.False);
                Assert.That(graph.TryGetHandle(sourceScope, out _), Is.False);
                Assert.That(graph.NodeCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SelectionAndModalHub_HandleApisResolveThroughGraph()
        {
            UINodeGraphService graph = new UINodeGraphService();
            TestRuntimeResolver resolver = new TestRuntimeResolver()
                .Register<IUINodeGraphService>(graph)
                .Register<IUINodeGraphTelemetry>(graph);

            GameObject rootObject = new GameObject("ui-handle-root");
            GameObject targetObject = new GameObject("ui-handle-target");

            UISelectionService? selectionService = null;

            try
            {
                targetObject.AddComponent<UIElementStateMB>();

                TestScopeNode rootScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "root", rootObject.transform)
                };

                TestScopeNode targetScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = rootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "target", targetObject.transform)
                };

                UIElementStateService targetService = new UIElementStateService(
                    targetScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(targetScope));

                targetService.OnAcquire(targetScope, isReset: false);

                ModalStackChannelHubService modalHub = new ModalStackChannelHubService();
                modalHub.PushModal("default", targetService);

                Assert.That(modalHub.IsInAnyInputRoot(targetService.NodeHandle), Is.True);
                Assert.That(modalHub.TryGetRootState(targetService.NodeHandle, out ModalRootResolvedState rootState), Is.True);
                Assert.That(rootState.Root.OwnerHandle, Is.EqualTo(targetService.NodeHandle));

                selectionService = new UISelectionService(modalHub);
                selectionService.SetNodeGraph(graph);

                Assert.That(selectionService.Select(targetService.NodeHandle), Is.True);
                Assert.That(selectionService.CurrentHandle, Is.EqualTo(targetService.NodeHandle));
                Assert.That(selectionService.CurrentElement, Is.SameAs(targetScope));

                targetService.OnRelease(targetScope, isDestroy: false);
            }
            finally
            {
                selectionService?.Dispose();
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void UINodeGraphService_TracksParentRootAndDepthAcrossLateParentRegistration()
        {
            UINodeGraphService graph = new UINodeGraphService();
            TestRuntimeResolver resolver = new TestRuntimeResolver()
                .Register<IUINodeGraphService>(graph)
                .Register<IUINodeGraphTelemetry>(graph);

            GameObject rootObject = new GameObject("ui-lineage-root");
            GameObject parentObject = new GameObject("ui-lineage-parent");
            GameObject bridgeObject = new GameObject("ui-lineage-bridge");
            GameObject childObject = new GameObject("ui-lineage-child");

            try
            {
                parentObject.AddComponent<UIElementStateMB>();
                childObject.AddComponent<UIElementStateMB>();

                TestScopeNode rootScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "root", rootObject.transform)
                };

                TestScopeNode parentScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = rootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "parent", parentObject.transform)
                };

                TestScopeNode bridgeScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = parentScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "bridge", bridgeObject.transform)
                };

                TestScopeNode childScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = bridgeScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "child", childObject.transform)
                };

                UIElementStateService parentService = new UIElementStateService(
                    parentScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(parentScope));

                UIElementStateService childService = new UIElementStateService(
                    childScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(childScope));

                childService.OnAcquire(childScope, isReset: false);

                Assert.That(graph.TryGetRootHandle(childService.NodeHandle, out UINodeHandle childRootBeforeParent), Is.True);
                Assert.That(childRootBeforeParent, Is.EqualTo(childService.NodeHandle));
                Assert.That(graph.TryGetDepth(childService.NodeHandle, out int childDepthBeforeParent), Is.True);
                Assert.That(childDepthBeforeParent, Is.EqualTo(0));
                Assert.That(graph.TryGetParentHandle(childService.NodeHandle, out _), Is.False);

                parentService.OnAcquire(parentScope, isReset: false);

                Assert.That(graph.TryGetParentHandle(childService.NodeHandle, out UINodeHandle parentHandle), Is.True);
                Assert.That(parentHandle, Is.EqualTo(parentService.NodeHandle));
                Assert.That(graph.TryGetRootHandle(childService.NodeHandle, out UINodeHandle childRootAfterParent), Is.True);
                Assert.That(childRootAfterParent, Is.EqualTo(parentService.NodeHandle));
                Assert.That(graph.TryGetDepth(childService.NodeHandle, out int childDepthAfterParent), Is.True);
                Assert.That(childDepthAfterParent, Is.EqualTo(1));
                Assert.That(graph.IsSameOrDescendant(childService.NodeHandle, parentService.NodeHandle), Is.True);
                Assert.That(graph.IsSameOrDescendant(parentService.NodeHandle, childService.NodeHandle), Is.False);

                parentService.OnRelease(parentScope, isDestroy: false);

                Assert.That(graph.TryGetParentHandle(childService.NodeHandle, out _), Is.False);
                Assert.That(graph.TryGetRootHandle(childService.NodeHandle, out UINodeHandle childRootAfterRelease), Is.True);
                Assert.That(childRootAfterRelease, Is.EqualTo(childService.NodeHandle));
                Assert.That(graph.TryGetDepth(childService.NodeHandle, out int childDepthAfterRelease), Is.True);
                Assert.That(childDepthAfterRelease, Is.EqualTo(0));

                childService.OnRelease(childScope, isDestroy: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(childObject);
                UnityEngine.Object.DestroyImmediate(bridgeObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SelectionService_TracksHandleStateThroughNavigationPointerAndClear()
        {
            UINodeGraphService graph = new UINodeGraphService();
            TestRuntimeResolver resolver = new TestRuntimeResolver()
                .Register<IUINodeGraphService>(graph)
                .Register<IUINodeGraphTelemetry>(graph);

            GameObject rootObject = new GameObject("ui-graph-root");
            GameObject targetObject = new GameObject("ui-graph-target");

            UISelectionService? selectionService = null;

            try
            {
                targetObject.AddComponent<UIElementStateMB>();

                TestScopeNode rootScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "root", rootObject.transform)
                };

                TestScopeNode targetScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = rootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "target", targetObject.transform)
                };

                UIElementStateService targetService = new UIElementStateService(
                    targetScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(targetScope));

                targetService.OnAcquire(targetScope, isReset: false);
                Assert.That(graph.TryGetHandle(targetScope, out UINodeHandle targetHandle), Is.True);

                ModalStackChannelHubService modalHub = new ModalStackChannelHubService();
                modalHub.PushModal("default", targetService);

                selectionService = new UISelectionService(modalHub);
                selectionService.SetNodeGraph(graph);
                selectionService.SetCandidateProvider(new HandleCandidateProvider(targetHandle, targetScope));

                Assert.That(selectionService.CurrentHandle, Is.EqualTo(UINodeHandle.Invalid));

                Assert.That(selectionService.TryNavigateSelect(NavigateDirection.Right), Is.True);
                Assert.That(selectionService.CurrentHandle, Is.EqualTo(targetHandle));
                Assert.That(selectionService.CurrentElement, Is.SameAs(targetScope));
                Assert.That(selectionService.PreviousHandle, Is.EqualTo(UINodeHandle.Invalid));

                Assert.That(selectionService.TryPointerSelect(new Vector2(64f, 64f)), Is.False);
                Assert.That(selectionService.HoveredHandle, Is.EqualTo(targetHandle));
                Assert.That(selectionService.HoveredElement, Is.SameAs(targetScope));

                Assert.That(selectionService.Select(UINodeHandle.Invalid), Is.True);
                Assert.That(selectionService.CurrentHandle, Is.EqualTo(UINodeHandle.Invalid));
                Assert.That(selectionService.CurrentElement, Is.Null);
                Assert.That(selectionService.PreviousHandle, Is.EqualTo(targetHandle));
                Assert.That(selectionService.PreviousElement, Is.SameAs(targetScope));

                targetService.OnRelease(targetScope, isDestroy: false);
            }
            finally
            {
                selectionService?.Dispose();
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ModalStackChannelHubService_ResolvesDescendantRootStateFromHandleAncestry()
        {
            UINodeGraphService graph = new UINodeGraphService();
            TestRuntimeResolver resolver = new TestRuntimeResolver()
                .Register<IUINodeGraphService>(graph)
                .Register<IUINodeGraphTelemetry>(graph);

            GameObject rootObject = new GameObject("modal-descendant-root");
            GameObject modalRootObject = new GameObject("modal-root");
            GameObject bridgeObject = new GameObject("modal-bridge");
            GameObject leafObject = new GameObject("modal-leaf");

            try
            {
                modalRootObject.AddComponent<UIElementStateMB>();
                leafObject.AddComponent<UIElementStateMB>();

                TestScopeNode rootScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "root", rootObject.transform)
                };

                TestScopeNode modalRootScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = rootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "modal-root", modalRootObject.transform)
                };

                TestScopeNode bridgeScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = modalRootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "bridge", bridgeObject.transform)
                };

                TestScopeNode leafScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = bridgeScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "leaf", leafObject.transform)
                };

                UIElementStateService modalRootService = new UIElementStateService(
                    modalRootScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(modalRootScope));

                UIElementStateService leafService = new UIElementStateService(
                    leafScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(leafScope));

                modalRootService.OnAcquire(modalRootScope, isReset: false);
                leafService.OnAcquire(leafScope, isReset: false);

                ModalStackChannelHubService modalHub = new ModalStackChannelHubService(graph);
                modalHub.PushModal("default", modalRootService);

                Assert.That(modalHub.IsInAnyInputRoot(leafService.NodeHandle), Is.True);
                Assert.That(modalHub.IsInAnyInputRoot(leafScope), Is.True);
                Assert.That(modalHub.TryGetRootState(leafService.NodeHandle, out ModalRootResolvedState handleState), Is.True);
                Assert.That(handleState.Root.OwnerHandle, Is.EqualTo(modalRootService.NodeHandle));
                Assert.That(modalHub.TryGetRootState(leafScope, out ModalRootResolvedState scopeState), Is.True);
                Assert.That(scopeState.Root.OwnerHandle, Is.EqualTo(modalRootService.NodeHandle));

                leafService.OnRelease(leafScope, isDestroy: false);
                modalRootService.OnRelease(modalRootScope, isDestroy: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(leafObject);
                UnityEngine.Object.DestroyImmediate(bridgeObject);
                UnityEngine.Object.DestroyImmediate(modalRootObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SelectionService_UsesModalDefaultSelectedHandleBeforeCandidateFallback()
        {
            UINodeGraphService graph = new UINodeGraphService();
            TestRuntimeResolver resolver = new TestRuntimeResolver()
                .Register<IUINodeGraphService>(graph)
                .Register<IUINodeGraphTelemetry>(graph);

            GameObject rootObject = new GameObject("ui-modal-root");
            GameObject targetObject = new GameObject("ui-modal-target");

            UISelectionService? selectionService = null;

            try
            {
                targetObject.AddComponent<UIElementStateMB>();

                TestScopeNode rootScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "root", rootObject.transform)
                };

                TestScopeNode targetScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = rootScope,
                    Resolver = resolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "target", targetObject.transform)
                };

                UIElementStateService targetService = new UIElementStateService(
                    targetScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(targetScope));

                targetService.OnAcquire(targetScope, isReset: false);
                Assert.That(graph.TryGetHandle(targetScope, out UINodeHandle targetHandle), Is.True);

                ModalOptions modalOptions = ModalOptions.Default;
                modalOptions.DefaultSelectedHandle = targetHandle;

                ModalStackChannelHubService modalHub = new ModalStackChannelHubService();
                modalHub.PushModal("default", targetService, modalOptions);

                selectionService = new UISelectionService(modalHub);
                selectionService.SetNodeGraph(graph);
                selectionService.SetCandidateProvider(new HandleCandidateProvider(targetHandle, targetScope));

                Assert.That(selectionService.CurrentHandle, Is.EqualTo(targetHandle));
                Assert.That(selectionService.CurrentElement, Is.SameAs(targetScope));
                Assert.That(selectionService.PreviousHandle, Is.EqualTo(UINodeHandle.Invalid));
                Assert.That(selectionService.HoveredHandle, Is.EqualTo(UINodeHandle.Invalid));

                targetService.OnRelease(targetScope, isDestroy: false);
            }
            finally
            {
                selectionService?.Dispose();
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ButtonChannelHubService_UsesExplicitUISelectionSource()
        {
            UINodeGraphService graph = new UINodeGraphService();

            GameObject selectionObject = new GameObject("button-selection-root");
            GameObject ownerObject = new GameObject("button-owner");

            UISelectionService? selectionService = null;
            ButtonChannelHubService? hubService = null;

            try
            {
                UISelectionMB selectionAnchor = selectionObject.AddComponent<UISelectionMB>();
                ownerObject.AddComponent<UIElementStateMB>();
                ButtonChannelHubDeclarationMB declaration = ownerObject.AddComponent<ButtonChannelHubDeclarationMB>();

                TestRuntimeResolver selectionResolver = new TestRuntimeResolver();
                TestRuntimeResolver ownerResolver = new TestRuntimeResolver()
                    .Register<IUINodeGraphService>(graph)
                    .Register<IUINodeGraphTelemetry>(graph);

                TestScopeNode selectionScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = selectionResolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "button-selection", selectionObject.transform)
                };

                TestScopeNode ownerScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = selectionScope,
                    Resolver = ownerResolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "button-owner", ownerObject.transform)
                };

                selectionService = new UISelectionService(new ModalStackChannelHubService());
                selectionService.SetNodeGraph(graph);
                selectionResolver
                    .Register<IUISelectionState>(selectionService)
                    .Register<IUISelectionBlockService>(selectionService);

                UIInputConsumerHub consumerHub = new UIInputConsumerHub();
                UIElementStateService elementState = new UIElementStateService(
                    ownerScope,
                    new TestUIElementStateOptions(),
                    selectionService,
                    new TestCommandRunner(ownerScope));

                elementState.OnAcquire(ownerScope, isReset: false);
                ownerResolver
                    .Register<IUIElementState>(elementState)
                    .Register<IUIHandleNode>(elementState)
                    .Register<IUIInputConsumerHub>(consumerHub);

                SetButtonChannelBinding(declaration, ButtonChannelBindingMode.UI, selectionAnchor);

                hubService = new ButtonChannelHubService(
                    ownerScope,
                    declaration,
                    localElementState: elementState,
                    localConsumerHub: consumerHub,
                    localHandleNode: elementState,
                    localVarStore: NullVarStore.Instance,
                    localCommandRunner: new TestCommandRunner(ownerScope));

                hubService.OnAcquire(ownerScope, isReset: false);

                Assert.That(hubService.TryGetOutput("default", out IButtonChannelOutput? output), Is.True);
                Assert.That(output, Is.Not.Null);
                Assert.That(output!.IsEnabled, Is.True);

                hubService.OnRelease(ownerScope, isReset: false);
                hubService = null;
                elementState.OnRelease(ownerScope, isDestroy: false);
            }
            finally
            {
                selectionService?.Dispose();
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
            }
        }

        [Test]
        public void ButtonChannelHubService_FailsClosedWithoutExplicitSelectionSource()
        {
            UINodeGraphService graph = new UINodeGraphService();

            GameObject selectionObject = new GameObject("button-selection-parent");
            GameObject ownerObject = new GameObject("button-owner-no-anchor");

            UISelectionService? selectionService = null;
            ButtonChannelHubService? hubService = null;

            try
            {
                ownerObject.AddComponent<UIElementStateMB>();
                ButtonChannelHubDeclarationMB declaration = ownerObject.AddComponent<ButtonChannelHubDeclarationMB>();

                TestRuntimeResolver selectionResolver = new TestRuntimeResolver();
                TestRuntimeResolver ownerResolver = new TestRuntimeResolver()
                    .Register<IUINodeGraphService>(graph)
                    .Register<IUINodeGraphTelemetry>(graph);

                TestScopeNode selectionScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = selectionResolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "button-selection-parent", selectionObject.transform)
                };

                TestScopeNode ownerScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = selectionScope,
                    Resolver = ownerResolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "button-owner-no-anchor", ownerObject.transform)
                };

                selectionService = new UISelectionService(new ModalStackChannelHubService());
                selectionService.SetNodeGraph(graph);
                selectionResolver
                    .Register<IUISelectionState>(selectionService)
                    .Register<IUISelectionBlockService>(selectionService);

                UIInputConsumerHub consumerHub = new UIInputConsumerHub();
                UIElementStateService elementState = new UIElementStateService(
                    ownerScope,
                    new TestUIElementStateOptions(),
                    null,
                    new TestCommandRunner(ownerScope));

                elementState.OnAcquire(ownerScope, isReset: false);
                ownerResolver
                    .Register<IUIElementState>(elementState)
                    .Register<IUIHandleNode>(elementState)
                    .Register<IUIInputConsumerHub>(consumerHub);

                SetButtonChannelBinding(declaration, ButtonChannelBindingMode.UI, null);

                hubService = new ButtonChannelHubService(
                    ownerScope,
                    declaration,
                    localElementState: elementState,
                    localConsumerHub: consumerHub,
                    localHandleNode: elementState,
                    localVarStore: NullVarStore.Instance,
                    localCommandRunner: new TestCommandRunner(ownerScope));

                hubService.OnAcquire(ownerScope, isReset: false);

                Assert.That(hubService.TryGetOutput("default", out IButtonChannelOutput? output), Is.True);
                Assert.That(output, Is.Not.Null);
                Assert.That(output!.IsEnabled, Is.False);
                Assert.That(consumerHub.Count, Is.EqualTo(0));

                hubService.OnRelease(ownerScope, isReset: false);
                hubService = null;
                elementState.OnRelease(ownerScope, isDestroy: false);
            }
            finally
            {
                selectionService?.Dispose();
                UnityEngine.Object.DestroyImmediate(ownerObject);
                UnityEngine.Object.DestroyImmediate(selectionObject);
            }
        }

        [Test]
        public void UIScrollBarBindingService_UsesExplicitButtonChannelHubSource()
        {
            GameObject sourceObject = new GameObject("scrollbar-hub-source");
            GameObject scrollbarObject = new GameObject("scrollbar-owner");

            try
            {
                ButtonChannelHubDeclarationMB sourceDeclaration = sourceObject.AddComponent<ButtonChannelHubDeclarationMB>();
                UIScrollBarMB scrollbarMb = scrollbarObject.AddComponent<UIScrollBarMB>();

                FakeButtonChannelHubService fakeHub = new FakeButtonChannelHubService();
                TestRuntimeResolver sourceResolver = new TestRuntimeResolver()
                    .Register<IButtonChannelHubService>(fakeHub);

                TestScopeNode sourceScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Resolver = sourceResolver,
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "scrollbar-hub-source", sourceObject.transform)
                };

                TestScopeNode scrollbarScope = new TestScopeNode(LifetimeScopeKind.Runtime)
                {
                    Parent = sourceScope,
                    Resolver = new TestRuntimeResolver(),
                    Identity = new TestIdentityService(LifetimeScopeKind.Runtime, "scrollbar-owner", scrollbarObject.transform)
                };

                SetField(scrollbarMb, "_buttonChannelHubSource", sourceDeclaration);

                UIScrollBarBindingService bindingService = new UIScrollBarBindingService(scrollbarMb);
                bindingService.OnAcquire(scrollbarScope, isReset: false);

                Assert.That(bindingService.ButtonChannelHub, Is.SameAs(fakeHub));

                bindingService.OnRelease(scrollbarScope, isReset: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scrollbarObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
            }
        }

        [Test]
        public void UIInputRoutingHub_OnlyDispatchesPreviewObserversAlongCurrentAndHoveredPaths()
        {
            UIInputRoutingHub hub = new UIInputRoutingHub();

            TestScopeNode root = new TestScopeNode(LifetimeScopeKind.Runtime);
            TestScopeNode currentBranch = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = root };
            TestScopeNode currentLeaf = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = currentBranch };
            TestScopeNode hoveredBranch = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = root };
            TestScopeNode hoveredLeaf = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = hoveredBranch };
            TestScopeNode unrelatedBranch = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = root };

            TrackingPreviewObserver rootObserver = new TrackingPreviewObserver(priority: 0);
            TrackingPreviewObserver currentObserver = new TrackingPreviewObserver(priority: 10);
            TrackingPreviewObserver hoveredObserver = new TrackingPreviewObserver(priority: 20);
            TrackingPreviewObserver unrelatedObserver = new TrackingPreviewObserver(priority: 100);

            hub.RegisterPreview(root, rootObserver);
            hub.RegisterPreview(currentBranch, currentObserver);
            hub.RegisterPreview(hoveredBranch, hoveredObserver);
            hub.RegisterPreview(unrelatedBranch, unrelatedObserver);

            UIInputEvent inputEvent = new UIInputEvent(UIInputEventType.PointerMove, new Vector2(32f, 64f));
            hub.NotifyPreview(currentLeaf, hoveredLeaf, in inputEvent);

            Assert.That(rootObserver.CallCount, Is.EqualTo(1));
            Assert.That(currentObserver.CallCount, Is.EqualTo(1));
            Assert.That(hoveredObserver.CallCount, Is.EqualTo(1));
            Assert.That(unrelatedObserver.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void UIInputRoutingHub_OnlyDispatchesBubbleConsumersOnRelevantPath()
        {
            UIInputRoutingHub hub = new UIInputRoutingHub();

            TestScopeNode root = new TestScopeNode(LifetimeScopeKind.Runtime);
            TestScopeNode branch = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = root };
            TestScopeNode leaf = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = branch };
            TestScopeNode unrelatedBranch = new TestScopeNode(LifetimeScopeKind.Runtime) { Parent = root };

            List<string> callOrder = new List<string>();
            TrackingBubbleConsumer leafConsumer = new TrackingBubbleConsumer(priority: 20, consume: false, callOrder, "leaf");
            TrackingBubbleConsumer branchConsumer = new TrackingBubbleConsumer(priority: 10, consume: true, callOrder, "branch");
            TrackingBubbleConsumer rootConsumer = new TrackingBubbleConsumer(priority: 0, consume: true, callOrder, "root");
            TrackingBubbleConsumer unrelatedConsumer = new TrackingBubbleConsumer(priority: 100, consume: true, callOrder, "unrelated");

            hub.RegisterBubble(leaf, leafConsumer);
            hub.RegisterBubble(branch, branchConsumer);
            hub.RegisterBubble(root, rootConsumer);
            hub.RegisterBubble(unrelatedBranch, unrelatedConsumer);

            UIInputEvent inputEvent = new UIInputEvent(UIInputEventType.SubmitDown);
            bool consumed = hub.DispatchBubble(leaf, null, in inputEvent);

            Assert.That(consumed, Is.True);
            Assert.That(callOrder, Is.EqualTo(new[] { "leaf", "branch" }));
            Assert.That(rootConsumer.CallCount, Is.EqualTo(0));
            Assert.That(unrelatedConsumer.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void SelectCandidateProviderScreen_ReusesRootCacheUntilObservedStateChanges()
        {
            TestScopeNode root = new TestScopeNode(LifetimeScopeKind.Runtime)
            {
                Resolver = new TestRuntimeResolver()
            };
            TestScopeNode child = new TestScopeNode(LifetimeScopeKind.Runtime)
            {
                Parent = root
            };
            TestSelectableState childState = new TestSelectableState(child);
            child.Resolver = new TestRuntimeResolver()
                .Register<IUIElementState>(childState);

            ScopeNodeHierarchy.Register(child, root);

            try
            {
                SelectCandidateProviderScreen provider = new SelectCandidateProviderScreen(new TestCanvasService());
                List<IScopeNode> results = new List<IScopeNode>();

                provider.GetAllSelectableCandidates(root, results);
                Assert.That(results, Is.EqualTo(new[] { child }));
                Assert.That(GetPrivateIntField(provider, "_rootCacheBuildCount"), Is.EqualTo(1));

                provider.GetAllSelectableCandidates(root, results);
                Assert.That(results, Is.EqualTo(new[] { child }));
                Assert.That(GetPrivateIntField(provider, "_rootCacheBuildCount"), Is.EqualTo(1));

                childState.SetVisible(false);

                provider.GetAllSelectableCandidates(root, results);
                Assert.That(results, Is.EqualTo(new[] { child }));
                Assert.That(GetPrivateIntField(provider, "_rootCacheBuildCount"), Is.EqualTo(2));
            }
            finally
            {
                ScopeNodeHierarchy.Unregister(child);
            }
        }

        static void SetButtonChannelBinding(
            ButtonChannelHubDeclarationMB declaration,
            ButtonChannelBindingMode mode,
            UISelectionMB? selectionSource)
        {
            SetField(declaration, "bindingMode", mode);
            SetField(declaration, "uiSelectionSource", selectionSource);
        }

        static void SetField(object target, string fieldName, object? value)
        {
            FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().FullName}.");

            field.SetValue(target, value);
        }

        static int GetPrivateIntField(object target, string fieldName)
        {
            FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' was not found on {target.GetType().FullName}.");

            return (int)(field.GetValue(target) ?? 0);
        }

        sealed class TestUIElementStateOptions : IUIElementStateOptions
        {
            public IReadOnlyList<RectTransform> HitTestRects { get; set; } = Array.Empty<RectTransform>();
            public int SelectionOrder { get; set; }
            public int NavigationSelectionOrder { get; set; }
            public DynamicValue<bool> IsSelectable { get; set; } = new DynamicValue<bool>();
            public DynamicValue<bool> IsNavigationSelectable { get; set; } = new DynamicValue<bool>();
            public NavigationOverride? NavigationOverride { get; set; }
            public CommandListData OnSelectedCommands { get; set; } = new CommandListData();
            public CommandListData OnDeselectedCommands { get; set; } = new CommandListData();
        }

        sealed class TestCommandRunner : ICommandRunner
        {
            public TestCommandRunner(IScopeNode scope)
            {
                Scope = scope;
            }

            public IScopeNode Scope { get; }

            public UniTask<CommandRunResult> ExecuteSingleAsync(ICommandData data, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                return UniTask.FromResult(default(CommandRunResult));
            }

            public UniTask<CommandRunResult> ExecuteListAsync(CommandListData list, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                return UniTask.FromResult(default(CommandRunResult));
            }

            public UniTask<CommandRunResult> ExecuteWithCancelAsync(CommandListData list, CommandListData onCanceled, CommandContext ctx, CancellationToken ct, CommandRunOptions options)
            {
                return UniTask.FromResult(default(CommandRunResult));
            }
        }

        sealed class TestRuntimeResolver : IRuntimeResolver
        {
            readonly Dictionary<Type, object?> _services = new Dictionary<Type, object?>();

            public TestRuntimeResolver Register<T>(T instance) where T : class
            {
                _services[typeof(T)] = instance;
                return this;
            }

            public bool TryResolve(Type type, out object? instance)
            {
                return _services.TryGetValue(type, out instance) && instance != null;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (_services.TryGetValue(typeof(T), out object? value) && value is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default!;
                return false;
            }

            public object Resolve(Type type)
            {
                if (TryResolve(type, out object? instance) && instance != null)
                    return instance;

                throw new InvalidOperationException($"Missing service: {type.FullName}");
            }

            public T Resolve<T>()
            {
                if (TryResolve<T>(out T instance))
                    return instance;

                throw new InvalidOperationException($"Missing service: {typeof(T).FullName}");
            }

            public object? ResolveOrDefault(Type type)
            {
                TryResolve(type, out object? instance);
                return instance;
            }

            public void Inject(object instance)
            {
            }

            public void Dispose()
            {
            }
        }

        sealed class HandleCandidateProvider : ISelectCandidateProvider
        {
            readonly UINodeHandle _handle;
            readonly IScopeNode _scope;

            public HandleCandidateProvider(UINodeHandle handle, IScopeNode scope)
            {
                _handle = handle;
                _scope = scope;
            }

            public void GetNavigationCandidates(
                IScopeNode? current,
                NavigateDirection direction,
                IScopeNode rootScope,
                List<SelectCandidate> results)
            {
                _ = current;
                _ = direction;
                _ = rootScope;

                results.Add(SelectCandidate.FromExplicitLink(_handle, _scope));
            }

            public void GetPointerHitCandidates(Vector2 screenPosition, IScopeNode rootScope, List<SelectCandidate> results)
            {
                _ = screenPosition;
                _ = rootScope;

                results.Add(SelectCandidate.FromExplicitLink(_handle, _scope));
            }

            public void GetAllSelectableCandidates(IScopeNode rootScope, List<IScopeNode> results)
            {
                _ = rootScope;
                _ = results;
            }
        }

        sealed class FakeButtonChannelHubService : IButtonChannelHubService
        {
            public int ChannelCount => 0;

            public bool Contains(string tag)
            {
                _ = tag;
                return false;
            }

            public bool TryGetOutput(string tag, out IButtonChannelOutput? output)
            {
                _ = tag;
                output = null;
                return false;
            }

            public bool TryGetControl(string tag, out IButtonChannelControlService? control)
            {
                _ = tag;
                control = null;
                return false;
            }

            public bool RegisterOrReplace(string tag, ButtonChannelPreset preset)
            {
                _ = tag;
                _ = preset;
                return false;
            }

            public bool Unregister(string tag)
            {
                _ = tag;
                return false;
            }

            public void GetTags(List<string> output)
            {
                output.Clear();
            }
        }

        sealed class TrackingPreviewObserver : IUIInputPreviewObserver
        {
            public TrackingPreviewObserver(int priority)
            {
                Priority = priority;
            }

            public int Priority { get; }
            public int CallCount { get; private set; }

            public void Observe(in UIInputEvent inputEvent)
            {
                _ = inputEvent;
                CallCount++;
            }
        }

        sealed class TrackingBubbleConsumer : IUIInputBubbleConsumer
        {
            readonly bool _consume;
            readonly List<string> _callOrder;
            readonly string _label;

            public TrackingBubbleConsumer(int priority, bool consume, List<string> callOrder, string label)
            {
                Priority = priority;
                _consume = consume;
                _callOrder = callOrder;
                _label = label;
            }

            public int Priority { get; }
            public int CallCount { get; private set; }

            public bool Consume(in UIInputEvent inputEvent)
            {
                _ = inputEvent;
                CallCount++;
                _callOrder.Add(_label);
                return _consume;
            }
        }

        sealed class TestCanvasService : IUICanvasService
        {
            public Canvas? Canvas => null;
            public UICanvasType CanvasType => UICanvasType.ScreenOverlay;
            public Camera? UICamera => null;
            public ISelectCandidateProvider CandidateProvider => throw new NotSupportedException();

            public bool ScreenToLocalPoint(Vector2 screenPosition, out Vector2 localPosition)
            {
                localPosition = screenPosition;
                return true;
            }

            public Vector2 LocalToScreenPoint(Vector2 localPosition)
            {
                return localPosition;
            }

            public bool RectContainsScreenPoint(RectTransform rect, Vector2 screenPosition)
            {
                _ = rect;
                _ = screenPosition;
                return false;
            }
        }

        sealed class TestSelectableState : IUIElementState
        {
            bool _isVisible = true;

            public TestSelectableState(IScopeNode owner)
            {
                Owner = owner;
            }

            public bool IsActive => true;
            public bool IsVisible => _isVisible;
            public bool IsEffectivelyActive => true;
            public bool AcceptsInput => true;
            public IReadOnlyList<RectTransform> HitTestRects { get; } = Array.Empty<RectTransform>();
            public int SelectionOrder => 0;
            public int NavigationSelectionOrder => 0;
            public DynamicValue<bool> IsSelectable { get; } = new DynamicValue<bool>();
            public DynamicValue<bool> IsNavigationSelectable { get; } = new DynamicValue<bool>();
            public NavigationOverride? NavigationOverride => null;
            public IScopeNode? Owner { get; }
            public event Action<UIElementStateChangedArgs>? OnStateChanged;

            public bool EvaluateIsSelectable() => true;

            public bool EvaluateIsNavigationSelectable() => true;

            public void SetVisible(bool visible)
            {
                if (_isVisible == visible)
                    return;

                bool previousVisible = _isVisible;
                _isVisible = visible;
                OnStateChanged?.Invoke(new UIElementStateChangedArgs(Owner!, previousActive: true, currentActive: true, previousVisible, _isVisible));
            }
        }

        sealed class TestScopeNode : IScopeNode
        {
            readonly LifetimeScopeKind _kind;

            public TestScopeNode(LifetimeScopeKind kind)
            {
                _kind = kind;
            }

            public IScopeNode? Parent { get; set; }
            public ILTSIdentityService? Identity { get; set; }
            public LifetimeScopeKind Kind => _kind;
            public IRuntimeResolver? Resolver { get; set; }
            public bool IsVisible { get; private set; } = true;
            public bool IsActive { get; private set; } = true;

            public bool TrySetVisible(bool visible, bool isReset = false)
            {
                IsVisible = visible;
                return true;
            }

            public bool TrySetActive(bool active, bool isReset = false)
            {
                IsActive = active;
                return true;
            }

            public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
            {
                IsActive = active;
                return UniTask.CompletedTask;
            }

            public IReadOnlyList<IScopeNode>? GetPathFromRoot()
            {
                List<IScopeNode> nodes = new List<IScopeNode>();
                IScopeNode? current = this;

                while (current != null)
                {
                    nodes.Add(current);
                    current = current.Parent;
                }

                nodes.Reverse();
                return nodes;
            }
        }

        sealed class TestIdentityService : ILTSIdentityService
        {
            public TestIdentityService(LifetimeScopeKind kind, string id, Transform selfTransform)
            {
                Kind = kind;
                Id = id;
                SelfTransform = selfTransform;
            }

            public LifetimeScopeKind Kind { get; }
            public string Id { get; }
            public string Category => string.Empty;
            public bool IsActive { get; set; } = true;
            public Game.Times.TimeScaleBehavior TimeScaleBehavior => default;
            public Transform SelfTransform { get; }
            public float Radius => 0f;
        }
    }
}
