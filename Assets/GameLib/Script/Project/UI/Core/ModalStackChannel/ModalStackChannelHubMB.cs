#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

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
        [BoxGroup("Layers")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true)]
        [SerializeField]
        List<ModalLayerPresetEntry> _layers = new() { new ModalLayerPresetEntry() };

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<ModalStackChannelHubService>(Lifetime.Singleton)
                .As<IModalStackChannelHubService>()
                .As<IModalStackChannelTelemetry>();

            builder.RegisterBuildCallback(container =>
            {
                if (!container.TryResolve<IModalStackChannelHubService>(out var hub) || hub == null)
                    return;

                for (int i = 0; i < _layers.Count; i++)
                {
                    var layer = _layers[i];
                    if (layer == null)
                        continue;

                    hub.RegisterLayer(layer.ToPreset());
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
