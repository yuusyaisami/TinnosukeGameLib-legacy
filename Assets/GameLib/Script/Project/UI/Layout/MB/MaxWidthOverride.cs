using UnityEngine;
using Sirenix.OdinInspector;

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

        void OnEnable()
        {
            NotifyMembershipDirty();
        }

        void OnDisable()
        {
            NotifyMembershipDirty();
        }

        void NotifyMembershipDirty()
        {
            var system = GetComponentInParent<LayoutSystemMB>();
            system?.MarkMembershipDirty();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying)
            {
                var system = GetComponentInParent<LayoutSystemMB>();
                system?.MarkContentDirty();
            }
        }
#endif
    }
}
