#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Layers;

namespace Game.Spawn
{
    public static class SceneKernelSpawnBindingHub
    {
        static readonly Dictionary<SceneKernelSpawnRouteId, ISceneKernelSpawnRouteHandler> handlersByRouteId = new Dictionary<SceneKernelSpawnRouteId, ISceneKernelSpawnRouteHandler>();
        static readonly Dictionary<SceneKernelSpawnPoolId, ISceneKernelSpawnPool> poolsById = new Dictionary<SceneKernelSpawnPoolId, ISceneKernelSpawnPool>();

        public static void Register(ISceneKernelSpawnRouteHandler handler, ISceneKernelSpawnPool pool)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            handlersByRouteId[handler.RouteId] = handler;
            poolsById[pool.PoolId] = pool;
        }

        public static bool TryResolveSpawnRouteHandler(SpawnerKind kind, string tag, out ISceneKernelSpawnRouteHandler handler)
        {
            return handlersByRouteId.TryGetValue(ToRouteId(kind, tag), out handler!);
        }

        public static bool TryReleaseAll(SpawnerKind kind, string tag, object filter, out int releasedCount)
        {
            SceneKernelSpawnPoolId poolId = SceneKernelSpawnPoolId.FromParts(kind.ToString(), tag);
            if (!poolsById.TryGetValue(poolId, out ISceneKernelSpawnPool? pool) || pool == null)
            {
                releasedCount = 0;
                return false;
            }

            releasedCount = pool.ReleaseAll(filter);
            return true;
        }

        static SceneKernelSpawnRouteId ToRouteId(SpawnerKind kind, string tag)
        {
            return SceneKernelSpawnRouteId.FromParts(kind.ToString(), tag);
        }

    }
}