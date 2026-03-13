#nullable enable
using UnityEngine;

namespace Game.UI
{
    public interface IVisualBoundsOutput
    {
        Transform? LocalSpaceRoot { get; }
        bool HasBounds { get; }
        Rect LocalRect { get; }
        Vector2 LocalCenter { get; }
        Vector2 LocalSize { get; }
        Bounds WorldBounds { get; }
        Vector3 WorldCenter { get; }
        Vector3 WorldSize { get; }
        ScreenClampResult LastClamp { get; }
    }

    public interface IVisualBoundsService
    {
        void MarkDirty();
        void RebuildNow();
        bool TryGetLastOutput(out VisualBoundsOutput output);
        void SetClampResult(in ScreenClampResult clamp);
    }

    public readonly struct VisualBoundsOutput
    {
        public bool HasBounds { get; }
        public Rect LocalRect { get; }
        public Vector2 LocalCenter { get; }
        public Vector2 LocalSize { get; }
        public Bounds WorldBounds { get; }
        public Vector3 WorldCenter { get; }
        public Vector3 WorldSize { get; }
        public ScreenClampResult LastClamp { get; }

        public VisualBoundsOutput(
            bool hasBounds,
            Rect localRect,
            Bounds worldBounds,
            in ScreenClampResult lastClamp)
        {
            HasBounds = hasBounds;
            LocalRect = localRect;
            LocalCenter = localRect.center;
            LocalSize = localRect.size;
            WorldBounds = worldBounds;
            WorldCenter = worldBounds.center;
            WorldSize = worldBounds.size;
            LastClamp = lastClamp;
        }
    }

    public readonly struct ScreenClampResult
    {
        public Rect ScreenRect { get; }
        public Rect TooltipRect { get; }
        public float LeftRate { get; }
        public float RightRate { get; }
        public float TopRate { get; }
        public float BottomRate { get; }
        public bool HasValue { get; }

        public float MaxRate
        {
            get
            {
                var lr = Mathf.Max(LeftRate, RightRate);
                var tb = Mathf.Max(TopRate, BottomRate);
                return Mathf.Max(lr, tb);
            }
        }

        public ScreenClampResult(Rect screenRect, Rect tooltipRect, float left, float right, float top, float bottom)
        {
            ScreenRect = screenRect;
            TooltipRect = tooltipRect;
            LeftRate = left;
            RightRate = right;
            TopRate = top;
            BottomRate = bottom;
            HasValue = true;
        }

        public static ScreenClampResult Empty => default;
    }
}
