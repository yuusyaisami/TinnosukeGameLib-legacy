using UnityEngine;

namespace Game.Layout
{
    [DisallowMultipleComponent]
    public sealed class LayoutElementsRootObserverMB : MonoBehaviour
    {
        void OnEnable()
        {
            Notify();
        }

        void OnTransformChildrenChanged()
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
