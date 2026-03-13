#nullable enable
using UnityEngine;

namespace Game.UI
{
    public interface IScreenClampService
    {
        ScreenClampResult Evaluate(Rect screenRect, Rect tooltipRect);
    }

    public sealed class ScreenClampService : IScreenClampService
    {
        public ScreenClampResult Evaluate(Rect screenRect, Rect tooltipRect)
        {
            var width = Mathf.Max(tooltipRect.width, 0.0001f);
            var height = Mathf.Max(tooltipRect.height, 0.0001f);

            var overflowLeft = Mathf.Max(0f, screenRect.xMin - tooltipRect.xMin);
            var overflowRight = Mathf.Max(0f, tooltipRect.xMax - screenRect.xMax);
            var overflowBottom = Mathf.Max(0f, screenRect.yMin - tooltipRect.yMin);
            var overflowTop = Mathf.Max(0f, tooltipRect.yMax - screenRect.yMax);

            var leftRate = overflowLeft / width;
            var rightRate = overflowRight / width;
            var bottomRate = overflowBottom / height;
            var topRate = overflowTop / height;

            return new ScreenClampResult(screenRect, tooltipRect, leftRate, rightRate, topRate, bottomRate);
        }
    }
}
