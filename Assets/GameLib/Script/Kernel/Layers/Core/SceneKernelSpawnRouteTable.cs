#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Kernel.Layers
{
    internal sealed class SceneKernelSpawnRouteTable
    {
        readonly Dictionary<SceneKernelSpawnRouteId, SceneKernelSpawnPoolId> poolIdsByRouteId = new Dictionary<SceneKernelSpawnRouteId, SceneKernelSpawnPoolId>();

        public int Count => poolIdsByRouteId.Count;

        public void Clear()
        {
            poolIdsByRouteId.Clear();
        }

        public bool TryBind(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId)
        {
            if (routeId.IsEmpty || poolId.IsEmpty || poolIdsByRouteId.ContainsKey(routeId))
                return false;

            poolIdsByRouteId.Add(routeId, poolId);
            return true;
        }

        public bool TryResolve(SceneKernelSpawnRouteId routeId, out SceneKernelSpawnPoolId poolId)
        {
            if (routeId.IsEmpty)
            {
                poolId = default;
                return false;
            }

            return poolIdsByRouteId.TryGetValue(routeId, out poolId);
        }
    }
}