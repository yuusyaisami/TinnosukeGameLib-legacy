#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public static class UISliderUnityGeometry
    {
        public static RectTransform ResolveContainerRect(Slider slider)
        {
            if (slider.handleRect != null && slider.handleRect.parent is RectTransform hp)
                return hp;

            if (slider.fillRect != null && slider.fillRect.parent is RectTransform fp)
                return fp;

            return slider.transform as RectTransform
                   ?? throw new System.InvalidOperationException("Slider has no RectTransform.");
        }

        public static bool IsHorizontal(Slider.Direction dir)
            => dir == Slider.Direction.LeftToRight || dir == Slider.Direction.RightToLeft;

        public static bool IsReversed(Slider.Direction dir)
            => dir == Slider.Direction.RightToLeft || dir == Slider.Direction.TopToBottom;

        /// <summary>
        /// Unity標準Sliderのドラッグ計算の核心を再現する。
        /// </summary>
        public static bool TryScreenToNormalized(
            Slider slider,
            Vector2 screenPos,
            Camera? cam,
            Vector2? handleOffsetLocalInHandle,
            out float normalized)
        {
            normalized = 0f;

            var container = ResolveContainerRect(slider);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPos, cam, out var localCursor))
                return false;

            if (handleOffsetLocalInHandle.HasValue)
                localCursor -= handleOffsetLocalInHandle.Value;

            bool horizontal = IsHorizontal(slider.direction);
            bool reversed = IsReversed(slider.direction);

            float min = horizontal ? container.rect.xMin : container.rect.yMin;
            float max = horizontal ? container.rect.xMax : container.rect.yMax;
            float pos = horizontal ? localCursor.x : localCursor.y;

            float t = Mathf.InverseLerp(min, max, pos);
            t = Mathf.Clamp01(t);

            if (reversed) t = 1f - t;

            normalized = t;
            return true;
        }

        public static Vector2 ComputeHandleOffsetLocalInHandle(Slider slider, Vector2 screenPos, Camera? cam)
        {
            if (slider.handleRect == null)
                return Vector2.zero;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(slider.handleRect, screenPos, cam, out var localInHandle))
                return Vector2.zero;

            return localInHandle - slider.handleRect.rect.center;
        }
    }
}
