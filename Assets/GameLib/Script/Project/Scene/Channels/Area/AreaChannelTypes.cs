#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum AreaPlane
    {
        XY = 0,
        XZ = 1,
    }

    public enum AreaShapeLayer
    {
        Area = 0,
        Line = 1,
    }

    public enum AreaSampleMode
    {
        InteriorRandom = 0,
        LineRandom = 1,
        LineSequential = 2,
    }

    public enum AreaTagSelectionMode
    {
        RandomOne = 0,
    }

    [Serializable]
    public sealed class AreaSampleSettings
    {
        [LabelText("Sequence Seed (0 = Random)")]
        public int SequenceSeed = 0;

        [LabelText("Jitter Rate"), MinValue(0f)]
        public float JitterRate = 0f;

        [LabelText("Min Distance"), MinValue(0f)]
        public float MinDistance = 0f;

        [LabelText("Max Retry"), MinValue(1)]
        public int MaxRetry = 1;

        public void EnsureIntegrity()
        {
            if (JitterRate < 0f)
                JitterRate = 0f;
            if (JitterRate > 1f)
                JitterRate = 1f;
            if (MinDistance < 0f)
                MinDistance = 0f;
            if (MaxRetry < 1)
                MaxRetry = 1;
        }
    }

    public readonly struct AreaSampleRequest
    {
        public readonly AreaSampleMode Mode;
        public readonly string LayerKey;

        public AreaSampleRequest(AreaSampleMode mode, string layerKey = "")
        {
            Mode = mode;
            LayerKey = layerKey ?? string.Empty;
        }

        public static AreaSampleRequest InteriorRandom => new(AreaSampleMode.InteriorRandom);
    }

    public readonly struct AreaShapeSampleContext
    {
        public readonly AreaSampleMode Mode;
        public readonly string LayerKey;

        public AreaShapeSampleContext(AreaSampleMode mode, string layerKey)
        {
            Mode = mode;
            LayerKey = layerKey ?? string.Empty;
        }
    }

    public interface IAreaShape
    {
        AreaShapeLayer Layer { get; }
        bool TrySample(in AreaShapeSampleContext context, Vector2 uv01, out Vector2 localPosition);
        void DrawGizmo(Vector3 center, AreaPlane plane);
    }

    [Serializable]
    public sealed class CircleAreaShape : IAreaShape
    {
        [LabelText("Radius"), MinValue(0f)]
        public float Radius = 5f;

        [LabelText("Inner Radius"), MinValue(0f)]
        public float InnerRadius = 0f;

        public AreaShapeLayer Layer => AreaShapeLayer.Area;

        public bool TrySample(in AreaShapeSampleContext context, Vector2 uv01, out Vector2 localPosition)
        {
            if (context.Mode != AreaSampleMode.InteriorRandom)
            {
                localPosition = Vector2.zero;
                return false;
            }

            var outer = Mathf.Max(0f, Radius);
            var inner = Mathf.Clamp(InnerRadius, 0f, outer);
            var r = Mathf.Sqrt(Mathf.Clamp01(uv01.x)) * (outer - inner) + inner;
            var angle = Mathf.Repeat(uv01.y, 1f) * Mathf.PI * 2f;
            localPosition = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            return true;
        }

        public void DrawGizmo(Vector3 center, AreaPlane plane)
        {
            DrawCircle(center, Mathf.Max(0f, Radius), plane);
            var inner = Mathf.Clamp(InnerRadius, 0f, Mathf.Max(0f, Radius));
            if (inner > 0f)
                DrawCircle(center, inner, plane);
        }

        static void DrawCircle(Vector3 center, float radiusValue, AreaPlane plane)
        {
            if (radiusValue <= 0f)
                return;

            const int segments = 48;
            Vector3 first = GetPlanePoint(center, radiusValue, 0f, plane);
            Vector3 prev = first;
            for (int i = 1; i <= segments; i++)
            {
                var t = (i / (float)segments) * Mathf.PI * 2f;
                var p = GetPlanePoint(center, radiusValue, t, plane);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        static Vector3 GetPlanePoint(Vector3 center, float radiusValue, float rad, AreaPlane plane)
        {
            var x = Mathf.Cos(rad) * radiusValue;
            var y = Mathf.Sin(rad) * radiusValue;
            return plane == AreaPlane.XZ
                ? new Vector3(center.x + x, center.y, center.z + y)
                : new Vector3(center.x + x, center.y + y, center.z);
        }
    }

    [Serializable]
    public sealed class RectAreaShape : IAreaShape
    {
        [LabelText("Size")]
        public Vector2 Size = new(5f, 5f);

        public AreaShapeLayer Layer => AreaShapeLayer.Area;

        public bool TrySample(in AreaShapeSampleContext context, Vector2 uv01, out Vector2 localPosition)
        {
            if (context.Mode != AreaSampleMode.InteriorRandom)
            {
                localPosition = Vector2.zero;
                return false;
            }

            var sx = Mathf.Max(0f, Size.x);
            var sy = Mathf.Max(0f, Size.y);
            localPosition = new Vector2((uv01.x - 0.5f) * sx, (uv01.y - 0.5f) * sy);
            return true;
        }

        public void DrawGizmo(Vector3 center, AreaPlane plane)
        {
            var size = new Vector2(Mathf.Max(0f, Size.x), Mathf.Max(0f, Size.y));
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;

            var p0 = OffsetPoint(center, -hx, -hy, plane);
            var p1 = OffsetPoint(center, -hx, hy, plane);
            var p2 = OffsetPoint(center, hx, hy, plane);
            var p3 = OffsetPoint(center, hx, -hy, plane);

            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
        }

        static Vector3 OffsetPoint(Vector3 center, float x, float y, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector3(center.x + x, center.y, center.z + y)
                : new Vector3(center.x + x, center.y + y, center.z);
        }
    }

    [Serializable]
    public sealed class AreaChannelDefinition : ChannelDefBase
    {
        [LabelText("Enabled")]
        public bool Enabled = true;

        [LabelText("Anchor")]
        public Transform? Anchor;

        [LabelText("Center Offset")]
        public Vector3 CenterOffset = Vector3.zero;

        [LabelText("Plane")]
        public AreaPlane Plane = AreaPlane.XY;

        [LabelText("Sample")]
        [InlineProperty]
        public AreaSampleSettings Sample = new();

        [SerializeReference, InlineProperty, HideLabel]
        [LabelText("Shape")]
        public IAreaShape Shape = new CircleAreaShape();

        public override void EnsureIntegrity(Component owner)
        {
            base.EnsureIntegrity(owner);

            if (Shape == null)
                Shape = new CircleAreaShape();

            Sample ??= new AreaSampleSettings();
            Sample.EnsureIntegrity();
        }
    }

    public interface IAreaChannelHubService : IChannelHubService
    {
        bool TryGetPlayer(string tag, out IAreaChannelPlayer player);
        bool TrySamplePosition(string tag, in AreaSampleRequest request, out Vector3 position);
        bool TrySamplePosition(IReadOnlyList<string> tags, AreaTagSelectionMode selectionMode, in AreaSampleRequest request, out Vector3 position, out string selectedTag);
    }

    public interface IAreaChannelPlayer
    {
        AreaChannelDefinition Definition { get; }
        bool TrySamplePosition(Vector3 basePosition, in AreaSampleRequest request, out Vector3 position);
    }
}
