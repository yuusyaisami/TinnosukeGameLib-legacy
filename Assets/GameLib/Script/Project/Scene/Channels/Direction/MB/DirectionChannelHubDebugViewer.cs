#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Direction
{
    [Serializable]
    public sealed class DirectionChannelHubDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _telemetry != null;

        [ShowInInspector, ReadOnly, LabelText("Telemetry Version")]
        public int TelemetryVersion
        {
            get
            {
                AutoRefresh();
                return _snapshot.Version;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Target")]
        public Vector2 Target
        {
            get
            {
                AutoRefresh();
                return _snapshot.Target;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Output")]
        public Vector2 Output
        {
            get
            {
                AutoRefresh();
                return _snapshot.Output;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Target Magnitude")]
        public float TargetMagnitude
        {
            get
            {
                AutoRefresh();
                return _snapshot.Target.magnitude;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Output Magnitude")]
        public float OutputMagnitude
        {
            get
            {
                AutoRefresh();
                return _snapshot.Output.magnitude;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Dirty")]
        public bool IsDirty
        {
            get
            {
                AutoRefresh();
                return _snapshot.IsDirty;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Sort Dirty")]
        public bool IsSortDirty
        {
            get
            {
                AutoRefresh();
                return _snapshot.IsSortDirty;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Transition Override")]
        public float TransitionSpeedOverride
        {
            get
            {
                AutoRefresh();
                return _snapshot.TransitionSpeedOverride;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Default Rise Speed")]
        public float DefaultRiseSpeed
        {
            get
            {
                AutoRefresh();
                return _snapshot.DefaultRiseSpeed;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Default Fall Speed")]
        public float DefaultFallSpeed
        {
            get
            {
                AutoRefresh();
                return _snapshot.DefaultFallSpeed;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Opposite Dot Threshold")]
        public float OppositeDotThreshold
        {
            get
            {
                AutoRefresh();
                return _snapshot.OppositeDotThreshold;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Downward Bias")]
        public float DownwardBiasStrength
        {
            get
            {
                AutoRefresh();
                return _snapshot.DownwardBiasStrength;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Layer Count")]
        public int LayerCount
        {
            get
            {
                AutoRefresh();
                return _rows.Count;
            }
        }

        [ShowInInspector]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = false, ShowPaging = false)]
        public List<LayerRow> Layers
        {
            get
            {
                AutoRefresh();
                return _rows;
            }
        }

        [SerializeField, LabelText("Auto Refresh Every N Frames"), MinValue(1)]
        int autoRefreshEveryNFrames = 1;

        IDirectionChannelHubTelemetry? _telemetry;
        DirectionChannelHubTelemetrySnapshot _snapshot;
        int _lastVersion = -1;
        int _lastRefreshFrame = -1;
        readonly List<LayerRow> _rows = new();

        public void Bind(IDirectionChannelHubTelemetry telemetry)
        {
            _telemetry = telemetry;
            _lastVersion = -1;
            _lastRefreshFrame = -1;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            if (_telemetry == null)
                return;

            var snapshot = _telemetry.GetTelemetrySnapshot();
            ApplySnapshot(snapshot);
        }

        void AutoRefresh()
        {
            if (_telemetry == null)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, autoRefreshEveryNFrames);
            if (_lastRefreshFrame >= 0 && frame - _lastRefreshFrame < interval)
                return;

            var telemetryVersion = _telemetry.TelemetryVersion;
            if (telemetryVersion == _lastVersion)
            {
                _lastRefreshFrame = frame;
                return;
            }

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
        }

        void ApplySnapshot(in DirectionChannelHubTelemetrySnapshot snapshot)
        {
            _snapshot = snapshot;
            _lastVersion = snapshot.Version;
            _lastRefreshFrame = Time.frameCount;

            _rows.Clear();
            var layers = snapshot.Layers;
            if (layers == null)
                return;

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                _rows.Add(new LayerRow
                {
                    Tag = layer.Tag,
                    Enabled = layer.Enabled,
                    Direction = layer.Direction,
                    Magnitude = layer.Direction.magnitude,
                    Priority = layer.Priority,
                    BlendMode = layer.BlendMode.ToString(),
                    Influence = layer.Influence,
                    TransitionSpeedOverride = layer.TransitionSpeedOverride,
                });
            }
        }

        [Serializable]
        public sealed class LayerRow
        {
            [LabelText("Tag")] public string Tag = string.Empty;
            [LabelText("Enabled")] public bool Enabled;
            [LabelText("Direction")] public Vector2 Direction;
            [LabelText("Magnitude")] public float Magnitude;
            [LabelText("Priority")] public int Priority;
            [LabelText("Blend Mode")] public string BlendMode = string.Empty;
            [LabelText("Influence")] public float Influence;
            [LabelText("Transition Speed Override")] public float TransitionSpeedOverride;
        }
    }
}
