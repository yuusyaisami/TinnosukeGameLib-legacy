#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Game.UI
{
    [Serializable]
    public sealed class ModalStackChannelHubDebugView : IDisposable
    {
        [Serializable]
        public struct LayerRow
        {
            public string LayerKey;
            public int Order;
            public bool HasAnyUI;
            public string ActiveRoot;
            public bool Visible;
            public bool InputActive;
            public bool IsTopOrderGroup;
            public bool IsPrimaryInOrder;
            public string SuppressedByLayerKey;
        }

        [Serializable]
        public struct RootRow
        {
            public string LayerKey;
            public string Root;
            public bool IsActiveInLayer;
            public bool Visible;
            public bool InputActive;
            public string InactiveReason;
        }

        [ShowInInspector, ReadOnly]
        public string CurrentInputRoot = "(none)";

        [ShowInInspector, ReadOnly]
        public string LastChange = "(none)";

        [ShowInInspector, ReadOnly]
        public int LayerCount;

        [ShowInInspector, ReadOnly]
        public int RootCount;

        [ShowInInspector, ReadOnly, TableList]
        public List<LayerRow> Layers = new();

        [ShowInInspector, ReadOnly, TableList]
        public List<RootRow> Roots = new();

        IModalStackChannelTelemetry? _telemetry;

        public void Bind(IModalStackChannelTelemetry telemetry)
        {
            Unbind();
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _telemetry.OnLayerStatesChanged += HandleLayerStatesChanged;
            Refresh();
        }

        public void Unbind()
        {
            if (_telemetry == null)
                return;

            _telemetry.OnLayerStatesChanged -= HandleLayerStatesChanged;
            _telemetry = null;
        }

        public void Dispose()
        {
            Unbind();
        }

        void HandleLayerStatesChanged(ModalLayerStatesChangedContext context)
        {
            LastChange = $"{context.ChangeKind} / {context.CauseLayerKey} / {context.ChangeType}";
            Refresh();
        }

        void Refresh()
        {
            if (_telemetry == null)
                return;

            CurrentInputRoot = ModalStackChannelDebugLabelUtility.DescribeRoot(_telemetry.CurrentInputRoot);

            Layers.Clear();
            var layerStates = _telemetry.LayerStates;
            LayerCount = layerStates.Count;
            for (var i = 0; i < layerStates.Count; i++)
            {
                var state = layerStates[i];
                Layers.Add(new LayerRow
                {
                    LayerKey = state.LayerKey,
                    Order = state.Order,
                    HasAnyUI = state.HasAnyUI,
                    ActiveRoot = ModalStackChannelDebugLabelUtility.DescribeRoot(state.ActiveRoot),
                    Visible = state.Visible,
                    InputActive = state.InputActive,
                    IsTopOrderGroup = state.IsTopOrderGroup,
                    IsPrimaryInOrder = state.IsPrimaryInOrder,
                    SuppressedByLayerKey = string.IsNullOrWhiteSpace(state.SuppressedByLayerKey) ? "(none)" : state.SuppressedByLayerKey,
                });
            }

            Roots.Clear();
            var rootStates = _telemetry.RootStates;
            RootCount = rootStates.Count;
            for (var i = 0; i < rootStates.Count; i++)
            {
                var state = rootStates[i];
                Roots.Add(new RootRow
                {
                    LayerKey = state.LayerKey,
                    Root = ModalStackChannelDebugLabelUtility.DescribeRoot(state.Root),
                    IsActiveInLayer = state.IsActiveInLayer,
                    Visible = state.Visible,
                    InputActive = state.InputActive,
                    InactiveReason = state.InactiveReason.ToString(),
                });
            }
        }
    }
}