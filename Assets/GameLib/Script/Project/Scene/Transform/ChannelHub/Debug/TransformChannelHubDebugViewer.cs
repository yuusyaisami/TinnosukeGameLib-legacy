#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.TransformSystem
{
    [Serializable]
    public sealed class TransformChannelHubDebugViewer
    {
        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _hub != null;

        [FoldoutGroup("Summary")]
        [ShowInInspector, ReadOnly, LabelText("Channel Count")]
        public int ChannelCount
        {
            get
            {
                AutoRefresh();
                return _rows.Count;
            }
        }

        [FoldoutGroup("Actions")]
        [Button(ButtonSizes.Small)]
        public void RefreshNow()
        {
            Refresh();
        }

        [ShowInInspector]
        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = true,
            DraggableItems = false,
            IsReadOnly = true,
            ShowPaging = true,
            NumberOfItemsPerPage = 4,
            ListElementLabelName = nameof(ChannelRow.Header))]
        [LabelText("Runtime Channels")]
        public List<ChannelRow> Channels
        {
            get
            {
                AutoRefresh();
                return _rows;
            }
        }

        [SerializeField, LabelText("Auto Refresh Every N Frames"), MinValue(1)]
        int autoRefreshEveryNFrames = 1;

        ITransformChannelHubService? _hub;
        int _lastRefreshFrame = -1;
        readonly List<string> _tags = new();
        readonly List<ChannelRow> _rows = new();

        public void Bind(ITransformChannelHubService hub)
        {
            _hub = hub;
            _lastRefreshFrame = -1;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            _rows.Clear();
            _tags.Clear();

            if (_hub == null)
                return;

            _hub.GetTags(_tags);
            for (var i = 0; i < _tags.Count; i++)
            {
                var tag = _tags[i];
                if (!_hub.TryGetRuntime(tag, out var runtime) || runtime == null)
                {
                    _rows.Add(new ChannelRow
                    {
                        Tag = tag,
                        Status = "Runtime Missing",
                    });
                    continue;
                }

                var telemetry = runtime as ITransformChannelRuntimeDebugTelemetry;
                var target = runtime.TargetTransform;
                var baseVelocity = runtime.CurrentVelocity;
                var rigidbodyVelocity = telemetry?.RigidbodyLinearVelocity ?? Vector2.zero;
                var rigidbodyOverlayVelocity = telemetry?.RigidbodyOverlayVelocity ?? Vector2.zero;
                var globalVelocityDelta = telemetry?.LastAppliedGlobalVelocityDelta ?? Vector2.zero;
                var finalVelocity = telemetry?.LastAppliedGlobalVelocity ?? baseVelocity;

                var row = new ChannelRow
                {
                    Tag = runtime.Tag,
                    Status = telemetry != null ? "OK" : "Telemetry Missing",
                    Target = target != null ? target.name : "(null)",
                    OutputTarget = runtime.OutputTarget,
                    EnableMovement = telemetry?.EnableMovement ?? false,
                    EnableRotation = telemetry?.EnableRotation ?? false,
                    EnableScale = telemetry?.EnableScale ?? false,
                    BaseVelocity = baseVelocity,
                    RigidbodyVelocity = rigidbodyVelocity,
                    RigidbodyOverlayVelocity = rigidbodyOverlayVelocity,
                    GlobalVelocityDelta = globalVelocityDelta,
                    FinalVelocity = finalVelocity,
                    WorldPosition = target != null ? target.position : Vector3.zero,
                    WorldRotation = target != null ? target.eulerAngles : Vector3.zero,
                    LocalScale = target != null ? target.localScale : Vector3.one,
                    LastAppliedFrame = telemetry?.LastAppliedGlobalFrame ?? -1,
                };

                if (telemetry?.GlobalApplyRequests != null)
                {
                    for (var j = 0; j < telemetry.GlobalApplyRequests.Count; j++)
                        row.GlobalRequests.Add(CreateRequestRow(telemetry.GlobalApplyRequests[j]));
                }

                row.GlobalRequestCount = row.GlobalRequests.Count;
                _rows.Add(row);
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

        static RequestRow CreateRequestRow(TransformManagerChannelApplyRequest request)
        {
            return new RequestRow
            {
                HasChannelTagFilter = request.HasChannelTagFilter,
                ChannelTagFilter = request.ChannelTagFilter,
            };
        }

        static string FormatVector2(Vector2 value)
        {
            return $"({value.x:F2}, {value.y:F2})";
        }

        static string FormatVector3(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }

        [Serializable]
        public sealed class ChannelRow
        {
            [FoldoutGroup("Channel")]
            [ReadOnly]
            public string Tag = string.Empty;

            [FoldoutGroup("Channel")]
            [ReadOnly]
            public string Status = string.Empty;

            [FoldoutGroup("Channel")]
            [ReadOnly]
            public string Target = string.Empty;

            [FoldoutGroup("Channel")]
            [ReadOnly]
            public TransformChannelOutputTarget OutputTarget;

            [FoldoutGroup("Runtime")]
            [ReadOnly]
            public bool EnableMovement;

            [FoldoutGroup("Runtime")]
            [ReadOnly]
            public bool EnableRotation;

            [FoldoutGroup("Runtime")]
            [ReadOnly]
            public bool EnableScale;

            [FoldoutGroup("Runtime")]
            [ReadOnly]
            public int GlobalRequestCount;

            [FoldoutGroup("Runtime")]
            [ReadOnly]
            public int LastAppliedFrame;

            [FoldoutGroup("Movement")]
            [ReadOnly]
            public Vector2 BaseVelocity;

            [FoldoutGroup("Movement")]
            [LabelText("Rigidbody Velocity")]
            [ReadOnly]
            public Vector2 RigidbodyVelocity;

            [FoldoutGroup("Movement")]
            [LabelText("Overlay Velocity")]
            [ReadOnly]
            public Vector2 RigidbodyOverlayVelocity;

            [FoldoutGroup("Movement")]
            [LabelText("Global Delta")]
            [ReadOnly]
            public Vector2 GlobalVelocityDelta;

            [FoldoutGroup("Movement")]
            [LabelText("Final Velocity")]
            [ReadOnly]
            public Vector2 FinalVelocity;

            [FoldoutGroup("Transform")]
            [LabelText("World Position")]
            [ReadOnly]
            public Vector3 WorldPosition;

            [FoldoutGroup("Transform")]
            [LabelText("World Rotation")]
            [ReadOnly]
            public Vector3 WorldRotation;

            [FoldoutGroup("Transform")]
            [LabelText("Local Scale")]
            [ReadOnly]
            public Vector3 LocalScale;

            [FoldoutGroup("Global Requests")]
            [ListDrawerSettings(
                ShowFoldout = true,
                DefaultExpandedState = false,
                DraggableItems = false,
                IsReadOnly = true,
                ShowPaging = true,
                NumberOfItemsPerPage = 8,
                ListElementLabelName = nameof(RequestRow.Header))]
            [LabelText("Requests")]
            public List<RequestRow> GlobalRequests = new();

            public string Header => $"{Tag} [{OutputTarget}] base={FormatVector2(BaseVelocity)} rb={FormatVector2(RigidbodyVelocity)} delta={FormatVector2(GlobalVelocityDelta)}";
        }

        [Serializable]
        public sealed class RequestRow
        {
            [FoldoutGroup("Request")]
            [ReadOnly]
            public bool HasChannelTagFilter;

            [FoldoutGroup("Request")]
            [ReadOnly]
            public string ChannelTagFilter = string.Empty;

            public string Header => HasChannelTagFilter ? ChannelTagFilter : "All";
        }
    }
}
