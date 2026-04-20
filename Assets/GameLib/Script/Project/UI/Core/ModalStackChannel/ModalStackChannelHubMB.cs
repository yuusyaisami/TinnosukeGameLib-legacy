#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [Serializable]
    public sealed class ModalLayerPresetEntry
    {
        [BoxGroup("Layer")]
        [LabelText("Layer Key")]
        public string LayerKey = "default";

        [BoxGroup("Layer")]
        [LabelText("Order")]
        public int Order = 0;

        [BoxGroup("Layer")]
        [LabelText("Tie Policy")]
        public ModalLayerTiePolicy TiePolicy = ModalLayerTiePolicy.FirstCome;

        [BoxGroup("Layer")]
        [LabelText("Allow Simultaneous Input")]
        public bool AllowSimultaneousInputInSameOrder = false;

        [BoxGroup("Top Order")]
        [LabelText("Top Order Effect")]
        public ModalLayerTopOrderEffect TopOrderEffect = ModalLayerTopOrderEffect.None;

        [BoxGroup("Layer")]
        [LabelText("Keep Non Active Visible")]
        public bool KeepNonActiveInLayerVisible = false;

        [BoxGroup("Layer")]
        [LabelText("Keep Non Active InputActive")]
        public bool KeepNonActiveInLayerInputActive = false;

        public ModalLayerPreset ToPreset()
        {
            return new ModalLayerPreset
            {
                LayerKey = string.IsNullOrWhiteSpace(LayerKey) ? "default" : LayerKey.Trim(),
                Order = Order,
                TiePolicy = TiePolicy,
                AllowSimultaneousInputInSameOrder = AllowSimultaneousInputInSameOrder,
                TopOrderEffect = TopOrderEffect,
                KeepNonActiveInLayerVisible = KeepNonActiveInLayerVisible,
                KeepNonActiveInLayerInputActive = KeepNonActiveInLayerInputActive,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class ModalStackChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Initial Root")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        UIElementLifetimeScope? _initialRoot;

        [Header("Debug")]
        [SerializeField]
        ModalStackChannelHubDebugView _debugView = new();

        [BoxGroup("Layers")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true)]
        [SerializeField]
        List<ModalLayerPresetEntry> _layers = new() { new ModalLayerPresetEntry() };

        IScopeNode? _ownerScope;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ownerScope = scope;

            builder.Register<ModalStackChannelHubService>(RuntimeLifetime.Singleton)
                .As<IModalStackChannelHubService>()
                .As<IModalStackChannelTelemetry>();

            builder.RegisterInstance(_debugView);

            builder.RegisterBuildCallback(container =>
            {
                if (!container.TryResolve<IModalStackChannelHubService>(out var hub) || hub == null)
                    return;

                for (var i = 0; i < _layers.Count; i++)
                {
                    var layer = _layers[i];
                    if (layer == null)
                        continue;

                    hub.RegisterLayer(layer.ToPreset());
                }

                if (container.TryResolve<IModalStackChannelTelemetry>(out var telemetry))
                    _debugView.Bind(telemetry);

                SetupInitialRoot(hub);
            });
        }

        void SetupInitialRoot(IModalStackChannelHubService hub)
        {
            if (_ownerScope == null)
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    if (_initialRoot != null)
                    {
                        await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(_initialRoot, CancellationToken.None);
                        var initResolver = _initialRoot.Resolver;
                        if (initResolver != null && initResolver.TryResolve<IUIModalRoot>(out var initRoot) && initRoot != null)
                        {
                            hub.SetDefaultRoot("default", initRoot);
                            return;
                        }
                    }

                    await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(_ownerScope, CancellationToken.None);
                    if (_ownerScope.Resolver != null && _ownerScope.Resolver.TryResolve<IUIModalRoot>(out var ownerRoot) && ownerRoot != null)
                    {
                        hub.SetDefaultRoot("default", ownerRoot);
                        return;
                    }

                    var searchRoot = transform != null ? transform : _ownerScope.Identity?.SelfTransform;
                    if (searchRoot != null)
                    {
                        var lifetimeScopes = searchRoot.GetComponentsInChildren<VContainer.Unity.LifetimeScope>(includeInactive: true);
                        for (var i = 0; i < lifetimeScopes.Length; i++)
                        {
                            var lifetimeScope = lifetimeScopes[i];

                            IScopeNode? nodeFound = null;
                            if (lifetimeScope is Component compRoot)
                            {
                                var components = compRoot.GetComponents<Component>();
                                for (var ci = 0; ci < components.Length; ci++)
                                {
                                    if (components[ci] is IScopeNode scopeNode)
                                    {
                                        nodeFound = scopeNode;
                                        break;
                                    }
                                }
                            }

                            if (nodeFound == null)
                                continue;

                            await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(nodeFound, CancellationToken.None);
                            var resolver = nodeFound.Resolver;
                            if (resolver != null && resolver.TryResolve<IUIModalRoot>(out var root) && root != null)
                            {
                                hub.SetDefaultRoot("default", root);
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            });
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _layers ??= new List<ModalLayerPresetEntry>();
            if (_layers.Count == 0)
                _layers.Add(new ModalLayerPresetEntry());
        }
#endif
    }
}
