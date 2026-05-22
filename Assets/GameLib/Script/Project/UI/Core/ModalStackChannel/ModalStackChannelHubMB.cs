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
    public sealed class ModalStackChannelHubMB : MonoBehaviour, IScopeInstaller
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

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
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

                SetupInitialRoot(hub, scope);
            });
        }

        void SetupInitialRoot(IModalStackChannelHubService hub, IScopeNode scope)
        {
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

                    Debug.LogWarning($"[ModalStackChannelHub] Explicit modal root binding was not found for '{DescribeOwnerScope(scope)}'. The modal stack hub will stay unbound until a root is provided explicitly.", this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            });
        }

        static string DescribeOwnerScope(IScopeNode scope)
        {
            return scope.Identity?.Id ?? scope.Identity?.SelfTransform?.name ?? scope.GetType().Name;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _layers ??= new List<ModalLayerPresetEntry>();
            if (_layers.Count == 0)
                _layers.Add(new ModalLayerPresetEntry());

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < _layers.Count; i++)
            {
                var entry = _layers[i];
                if (entry == null)
                    continue;

                var normalizedKey = string.IsNullOrWhiteSpace(entry.LayerKey) ? "default" : entry.LayerKey.Trim();
                if (!seenKeys.Add(normalizedKey))
                {
                    Debug.LogWarning($"[ModalStackChannelHub] Duplicate layer key '{normalizedKey}' found in {name}. The hub will keep the last registered preset for that key.", this);
                }

                entry.LayerKey = normalizedKey;
            }
        }
#endif
    }
}

