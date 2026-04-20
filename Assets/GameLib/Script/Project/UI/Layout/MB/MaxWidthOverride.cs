using UnityEngine;

namespace Game.Layout
{
    /// <summary>
    /// Overrides maxWidth passed to preferred-size adapters (primarily for wrapping text).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MaxWidthOverride : MonoBehaviour
    {
        [Min(0f)]
        [SerializeField] float maxWidth;

        public float MaxWidth => maxWidth;
    }
}
