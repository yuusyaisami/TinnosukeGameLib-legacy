#nullable enable

using System;
using UnityEngine;

namespace Game.Kernel.Layers.Unity
{
    [DisallowMultipleComponent]
    public sealed class SceneKernelEntityInstanceMB : MonoBehaviour
    {
        bool hasLease;
        SceneKernelEntityLeaseHandle currentLease;
        SceneKernelSpawnRouteId currentRouteId;
        string currentRouteKey = string.Empty;

        public bool HasLease => hasLease;

        public string CurrentRouteKey => currentRouteKey;

        public bool TryGetLease(out SceneKernelEntityLeaseHandle lease)
        {
            lease = currentLease;
            return hasLease;
        }

        public bool TryGetRouteId(out SceneKernelSpawnRouteId routeId)
        {
            routeId = currentRouteId;
            return !currentRouteId.IsEmpty;
        }

        public void BindLease(SceneKernelEntityLeaseHandle lease, SceneKernelSpawnRouteId routeId)
        {
            if (routeId.IsEmpty)
                throw new ArgumentException("SceneKernelEntityInstanceMB requires a non-empty spawn route id.", nameof(routeId));

            currentLease = lease;
            currentRouteId = routeId;
            currentRouteKey = routeId.Value;
            hasLease = true;
        }

        public void ClearLease()
        {
            currentLease = default;
            currentRouteId = default;
            currentRouteKey = string.Empty;
            hasLease = false;
        }
    }
}