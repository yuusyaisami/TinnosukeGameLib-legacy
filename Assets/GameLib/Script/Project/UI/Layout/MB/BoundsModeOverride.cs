using UnityEngine;
using Sirenix.OdinInspector;

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
