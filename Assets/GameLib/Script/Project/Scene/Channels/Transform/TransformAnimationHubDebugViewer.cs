#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class TransformAnimationHubDebugViewer
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
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
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

        ITransformAnimationHubService? _hub;
        int _lastRefreshFrame = -1;
        readonly List<ChannelRow> _rows = new();

        public void Bind(ITransformAnimationHubService hub)
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
                if (player is not ITransformAnimationChannelTelemetry telemetry)
                {
                    _rows.Add(new ChannelRow
                    {
                        Tag = player?.Tag ?? string.Empty,
                        Target = player?.TargetTransform != null ? player.TargetTransform.name : "(null)",
                        RunMode = "Unknown",
                        Operation = "-",
                    });
                    continue;
                }

                var snapshot = telemetry.GetTelemetrySnapshot();
                _rows.Add(new ChannelRow
                {
                    Tag = snapshot.Tag,
                    Target = snapshot.TargetName,
                    RunMode = snapshot.RunMode.ToString(),
                    Operation = snapshot.CurrentOperation,
                    Playing = snapshot.IsPlaying,
                    Following = snapshot.IsFollowing,
                    Shake = snapshot.IsShaking,
                    World = snapshot.WorldPosition,
                    Local = snapshot.LocalPosition,
                    Euler = snapshot.LocalEulerAngles,
                    Scale = snapshot.LocalScale,
                    Step = FormatStep(snapshot.StepIndex, snapshot.StepCount),
                    Loop = FormatLoop(snapshot.LoopIndex, snapshot.LoopCount),
                    FollowTarget = snapshot.FollowUseTransformTarget
                        ? snapshot.FollowTargetName
                        : snapshot.FollowTargetPosition.ToString("F2"),
                    FollowResolved = snapshot.FollowTargetPosition,
                    FollowSmoothTime = snapshot.FollowOptions.SmoothTime,
                    FollowMaxSpeed = snapshot.FollowOptions.MaxSpeed,
                    FollowVelocityOffset = snapshot.FollowOptions.UseVelocityOffset,
                    FollowVelSource = snapshot.FollowOptions.VelocitySourceType.ToString(),
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

        static string FormatStep(int index, int count)
        {
            if (count <= 0 || index < 0)
                return "-";

            return $"{index + 1}/{count}";
        }

        static string FormatLoop(int index, int count)
        {
            if (count < 0)
            {
                var current = index < 0 ? 0 : index + 1;
                return $"{current}/inf";
            }

            if (count == 0 || index < 0)
                return "-";

            return $"{index + 1}/{count}";
        }

        [Serializable]
        public sealed class ChannelRow
        {
            [TableColumnWidth(100)] public string Tag = string.Empty;
            [TableColumnWidth(140)] public string Target = string.Empty;
            [TableColumnWidth(110)] public string RunMode = string.Empty;
            [TableColumnWidth(130)] public string Operation = string.Empty;
            [TableColumnWidth(56)] public bool Playing;
            [TableColumnWidth(66)] public bool Following;
            [TableColumnWidth(56)] public bool Shake;
            [TableColumnWidth(140)] public Vector3 World;
            [TableColumnWidth(140)] public Vector3 Local;
            [TableColumnWidth(140)] public Vector3 Euler;
            [TableColumnWidth(110)] public Vector3 Scale;
            [TableColumnWidth(70)] public string Step = string.Empty;
            [TableColumnWidth(70)] public string Loop = string.Empty;
            [TableColumnWidth(150)] public string FollowTarget = string.Empty;
            [TableColumnWidth(140)] public Vector3 FollowResolved;
            [TableColumnWidth(80)] public float FollowSmoothTime;
            [TableColumnWidth(80)] public float FollowMaxSpeed;
            [TableColumnWidth(60)] public bool FollowVelocityOffset;
            [TableColumnWidth(100)] public string FollowVelSource = string.Empty;
        }
    }
}
