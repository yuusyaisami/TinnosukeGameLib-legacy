#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class ParallaxChannelHubDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _hub != null;

        [ShowInInspector, ReadOnly, LabelText("Channels")]
        public int ChannelCount
        {
            get
            {
                AutoRefresh();
                return _rows.Count;
            }
        }

        [ShowInInspector]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = false, HideAddButton = true, HideRemoveButton = true, ShowPaging = false)]
        public List<ChannelRow> ChannelRows
        {
            get
            {
                AutoRefresh();
                return _rows;
            }
        }

        [SerializeField, LabelText("Auto Refresh Every N Frames"), MinValue(1)]
        int autoRefreshEveryNFrames = 1;

        IParallaxChannelHubService? _hub;
        int _lastRefreshFrame = -1;
        readonly List<ChannelRow> _rows = new();

        public void Bind(IParallaxChannelHubService hub)
        {
            _hub = hub;
            _lastRefreshFrame = -1;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            _rows.Clear();

            if (_hub == null)
                return;

            var players = _hub.Players;
            if (players == null)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player is not IParallaxChannelTelemetry telemetry)
                {
                    _rows.Add(new ChannelRow
                    {
                        Tag = player?.Tag ?? string.Empty,
                        Enabled = player?.Enabled ?? false,
                    });
                    continue;
                }

                var snapshot = telemetry.GetTelemetrySnapshot();
                _rows.Add(new ChannelRow
                {
                    Tag = snapshot.Tag,
                    Enabled = snapshot.Enabled,
                    DriverMode = snapshot.DriverMode.ToString(),
                    CameraBind = snapshot.CameraBindMode.ToString(),
                    WriteMode = snapshot.WriteMode.ToString(),
                    Target = snapshot.TargetName,
                    Camera = snapshot.CameraName,
                    Offset = snapshot.LastOffset,
                    BaseWorld = snapshot.BaseWorldPosition,
                    Factor = snapshot.Factor,
                    ExtraOffset = snapshot.ExtraOffset,
                    UseSmoothing = snapshot.UseSmoothing,
                    SmoothTime = snapshot.SmoothTime,
                    UpdateEvery = snapshot.UpdateEveryNFrames,
                    UnsafeRb = snapshot.AllowUnsafeRigidbody2DWrite,
                    LastTickFrame = snapshot.LastTickFrame,
                    LastWriteApplied = snapshot.LastWriteApplied,
                    LastAppliedWorld = snapshot.LastAppliedWorldPosition,
                });
            }

            _lastRefreshFrame = Time.frameCount;
        }

        void AutoRefresh()
        {
            if (_hub == null)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, autoRefreshEveryNFrames);
            if (_lastRefreshFrame >= 0 && frame - _lastRefreshFrame < interval)
                return;

            Refresh();
        }

        [Serializable]
        [InlineProperty]
        [HideLabel]
        public sealed class ChannelRow
        {
            [FoldoutGroup("Channel")]
            [LabelText("Tag")]
            [ReadOnly]
            public string Tag = string.Empty;

            [FoldoutGroup("Channel")]
            [ReadOnly]
            public bool Enabled;

            [FoldoutGroup("Channel")]
            [LabelText("Driver")]
            [ReadOnly]
            public string DriverMode = string.Empty;

            [FoldoutGroup("Channel")]
            [LabelText("Camera Bind")]
            [ReadOnly]
            public string CameraBind = string.Empty;

            [FoldoutGroup("Channel")]
            [ReadOnly]
            public string WriteMode = string.Empty;

            [FoldoutGroup("Bind")]
            [ReadOnly]
            public string Target = string.Empty;

            [FoldoutGroup("Bind")]
            [ReadOnly]
            public string Camera = string.Empty;

            [FoldoutGroup("Offset")]
            [ReadOnly]
            public Vector3 Offset;

            [FoldoutGroup("Offset")]
            [LabelText("Base World")]
            [ReadOnly]
            public Vector3 BaseWorld;

            [FoldoutGroup("Offset")]
            [ReadOnly]
            public Vector3 Factor;

            [FoldoutGroup("Offset")]
            [LabelText("Extra")]
            [ReadOnly]
            public Vector3 ExtraOffset;

            [FoldoutGroup("Runtime")]
            [ReadOnly]
            public bool UseSmoothing;

            [FoldoutGroup("Runtime")]
            [ReadOnly]
            public float SmoothTime;

            [FoldoutGroup("Runtime")]
            [LabelText("Update Every")]
            [ReadOnly]
            public int UpdateEvery;

            [FoldoutGroup("Runtime")]
            [LabelText("Unsafe RB")]
            [ReadOnly]
            public bool UnsafeRb;

            [FoldoutGroup("Runtime")]
            [LabelText("Last Tick Frame")]
            [ReadOnly]
            public int LastTickFrame;

            [FoldoutGroup("Runtime")]
            [LabelText("Last Write Applied")]
            [ReadOnly]
            public bool LastWriteApplied;

            [FoldoutGroup("Runtime")]
            [LabelText("Last Applied World")]
            [ReadOnly]
            public Vector3 LastAppliedWorld;
        }
    }
}
