using System;
using UnityEngine;

namespace Game.Layout
{
    public enum BoundsMode
    {
        RectTransform,
        PreferredLayout,
        GlyphBounds,
    }

    public interface ILayoutElementsOutput
    {
        Rect LocalRect { get; }
        Vector2 LocalCenter { get; }
        Vector2 LocalSize { get; }
    }

    [Serializable]
    public struct LayoutBackgroundOptions
    {
        public float ExtendLeft;
        public float ExtendRight;
        public float ExtendTop;
        public float ExtendBottom;

        public Vector2 Offset;
        public Vector2 MinSize;

        public static LayoutBackgroundOptions Default => new LayoutBackgroundOptions
        {
            ExtendLeft = 0f,
            ExtendRight = 0f,
            ExtendTop = 0f,
            ExtendBottom = 0f,
            Offset = Vector2.zero,
            MinSize = Vector2.zero,
        };
    }

    public readonly struct LayoutOutput : ILayoutElementsOutput
    {
        public Rect LocalRect { get; }
        public Vector2 LocalCenter { get; }
        public Vector2 LocalSize { get; }

        public LayoutOutput(Rect localRect)
        {
            LocalRect = localRect;
            LocalCenter = localRect.center;
            LocalSize = localRect.size;
        }
    }
}
