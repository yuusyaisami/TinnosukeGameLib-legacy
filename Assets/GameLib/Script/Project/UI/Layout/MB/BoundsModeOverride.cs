using UnityEngine;

namespace Game.Layout
{
    /// <summary>
    /// Overrides how bounds are computed for a contributor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoundsModeOverride : MonoBehaviour
    {
        [SerializeField] BoundsMode mode = BoundsMode.RectTransform;

        public BoundsMode Mode => mode;
    }
}
