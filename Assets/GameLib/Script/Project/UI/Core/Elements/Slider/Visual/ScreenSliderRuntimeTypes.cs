#nullable enable
using UnityEngine;

namespace Game.UI
{
    internal readonly struct SliderRectTransformState
    {
        public readonly Vector3 AnchoredPosition3D;
        public readonly Vector2 SizeDelta;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;
        public readonly Vector2 AnchorMin;
        public readonly Vector2 AnchorMax;
        public readonly Vector2 Pivot;

        public SliderRectTransformState(RectTransform rectTransform)
        {
            AnchoredPosition3D = rectTransform.anchoredPosition3D;
            SizeDelta = rectTransform.sizeDelta;
            LocalRotation = rectTransform.localRotation;
            LocalScale = rectTransform.localScale;
            AnchorMin = rectTransform.anchorMin;
            AnchorMax = rectTransform.anchorMax;
            Pivot = rectTransform.pivot;
        }

        public void ApplyTo(RectTransform rectTransform)
        {
            rectTransform.anchorMin = AnchorMin;
            rectTransform.anchorMax = AnchorMax;
            rectTransform.pivot = Pivot;
            rectTransform.anchoredPosition3D = AnchoredPosition3D;
            rectTransform.sizeDelta = SizeDelta;
            rectTransform.localRotation = LocalRotation;
            rectTransform.localScale = LocalScale;
        }
    }

    internal sealed class SliderScreenRuntimeInstance
    {
        public SliderSpawnedRuntimeInstance Runtime = null!;
        public RectTransform RootRect = null!;
        public SliderRectTransformState RootState;
        public RectTransform SizeRect = null!;
        public SliderRectTransformState SizeState;
    }
}
