using UnityEngine;

namespace Game.Layout
{
    /// <summary>
    /// Generic size adapter for UI elements. If present, LayoutSystem should prefer this adapter's preferred size
    /// over raw RectTransform-derived sizes.
    /// </summary>
    public interface IRectTransformSizeAdapter
    {
        RectTransform Target { get; }

        /// <summary>
        /// Returns the preferred size under the given maxWidth constraint. For non-wrapping elements the maxWidth may be ignored.
        /// </summary>
        Vector2 GetPreferredSize(float maxWidth);
    }
}
