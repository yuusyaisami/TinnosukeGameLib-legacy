#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI
{
    internal enum SliderRangeResolveStatus
    {
        Success = 10,
        CanvasUnavailable = 20,
        AreaScopeUnavailable = 30,
        AreaHubUnavailable = 40,
        AreaPlayerUnavailable = 50,
        AreaBasePositionUnavailable = 55,
        AreaRectSnapshotUnavailable = 56,
        AreaCanvasRectSnapshotUnavailable = 57,
        RectTransformUnavailable = 60,
        UnsupportedShape = 70,
        UnsupportedWorldRectTransform = 80,
    }

    internal readonly struct SliderScreenRangeSnapshot
    {
        public readonly RectTransform CanvasRect;
        public readonly Rect LocalRect;
        public readonly Camera? UICamera;

        public SliderScreenRangeSnapshot(RectTransform canvasRect, Rect localRect, Camera? uiCamera)
        {
            CanvasRect = canvasRect;
            LocalRect = localRect;
            UICamera = uiCamera;
        }
    }

    internal readonly struct SliderResolvedEntry
    {
        public readonly int Index;
        public readonly float RawValue;
        public readonly float NormalizedValue;
        public readonly SliderSegmentEntryBase? SourceEntry;

        public SliderResolvedEntry(int index, float rawValue, float normalizedValue, SliderSegmentEntryBase? sourceEntry)
        {
            Index = index;
            RawValue = rawValue;
            NormalizedValue = normalizedValue;
            SourceEntry = sourceEntry;
        }
    }

    internal sealed class SliderResolvedSegmentLayout
    {
        public float MinValue;
        public float MaxValue;
        public readonly List<SliderResolvedEntry> Entries = new();
        public readonly List<float> Boundaries = new();
    }

    internal readonly struct SliderTransformPose
    {
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;

        public SliderTransformPose(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public SliderTransformPose(Transform transform)
        {
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
        }

        public void ApplyTo(Transform transform)
        {
            transform.localPosition = LocalPosition;
            transform.localRotation = LocalRotation;
            transform.localScale = LocalScale;
        }
    }

    internal readonly struct SliderSpriteRenderState
    {
        public readonly SpriteDrawMode DrawMode;
        public readonly Vector2 Size;

        public SliderSpriteRenderState(SpriteRenderer renderer)
        {
            DrawMode = renderer.drawMode;
            Size = renderer.size;
        }

        public void ApplyTo(SpriteRenderer renderer)
        {
            renderer.drawMode = DrawMode;
            renderer.size = Size;
        }
    }

    internal readonly struct SliderImageRenderState
    {
        public readonly Vector2 SizeDelta;

        public SliderImageRenderState(Image image)
        {
            SizeDelta = image.rectTransform != null ? image.rectTransform.sizeDelta : Vector2.zero;
        }

        public void ApplyTo(Image image)
        {
            if (image.rectTransform != null)
                image.rectTransform.sizeDelta = SizeDelta;
        }
    }

    internal enum SliderRuntimeVisualTargetKind
    {
        None = 0,
        SpriteRenderer = 10,
        Image = 20,
    }

    internal sealed class SliderSpawnedRuntimeInstance
    {
        public Transform Root = null!;
        public IScopeNode? Scope;
        public IRuntimeResolver Resolver = null!;
        public SliderTransformPose BasePose;
        public Transform? VisualTransform;
        public SliderTransformPose VisualPose;
        public SliderRuntimeVisualTargetKind VisualTargetKind;
        public SpriteRenderer? SpriteRenderer;
        public SliderSpriteRenderState SpriteState;
        public Image? Image;
        public SliderImageRenderState ImageState;
        public SliderSpawnUnitKind UnitKind;
        public int UnitIndex;
        public float StartRawValue;
        public float EndRawValue;
        public float StartNormalized;
        public float EndNormalized;
        public int EntryIndex = -1;
        public float EntryRawValue;
        public float EntryNormalized;
    }
}
