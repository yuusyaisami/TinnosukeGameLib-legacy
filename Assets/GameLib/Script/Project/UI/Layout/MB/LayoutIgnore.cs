using UnityEngine;

namespace Game.Layout
{
    [DisallowMultipleComponent]
    public sealed class LayoutIgnore : MonoBehaviour
    {
        void OnEnable()
        {
            Notify();
        }

        void OnDisable()
        {
            Notify();
        }

        void Notify()
        {
            var system = GetComponentInParent<LayoutSystemMB>();
            system?.MarkMembershipDirty();
        }
    }
}
