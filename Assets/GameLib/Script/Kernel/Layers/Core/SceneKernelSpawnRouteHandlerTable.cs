#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Kernel.Layers
{
    internal sealed class SceneKernelSpawnRouteHandlerTable
    {
        readonly Dictionary<SceneKernelSpawnRouteId, ISceneKernelSpawnRouteHandler> handlersByRouteId = new Dictionary<SceneKernelSpawnRouteId, ISceneKernelSpawnRouteHandler>();

        public int Count => handlersByRouteId.Count;

        public void Clear()
        {
            handlersByRouteId.Clear();
        }

        public bool TryBind(ISceneKernelSpawnRouteHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (handler.RouteId.IsEmpty || handlersByRouteId.ContainsKey(handler.RouteId))
                return false;

            handlersByRouteId.Add(handler.RouteId, handler);
            return true;
        }

        public bool TryGet(SceneKernelSpawnRouteId routeId, out ISceneKernelSpawnRouteHandler handler)
        {
            if (routeId.IsEmpty)
            {
                handler = null!;
                return false;
            }

            return handlersByRouteId.TryGetValue(routeId, out handler!);
        }
    }
}