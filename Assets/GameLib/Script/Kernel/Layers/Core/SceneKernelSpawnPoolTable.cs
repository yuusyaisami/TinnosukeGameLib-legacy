#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Kernel.Layers
{
    internal sealed class SceneKernelSpawnPoolTable
    {
        readonly Dictionary<SceneKernelSpawnPoolId, ISceneKernelSpawnPool> poolsById = new Dictionary<SceneKernelSpawnPoolId, ISceneKernelSpawnPool>();

        public int Count => poolsById.Count;

        public void Clear()
        {
            poolsById.Clear();
        }

        public bool TryBind(ISceneKernelSpawnPool pool)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            if (pool.PoolId.IsEmpty || poolsById.ContainsKey(pool.PoolId))
                return false;

            poolsById.Add(pool.PoolId, pool);
            return true;
        }

        public bool TryGet(SceneKernelSpawnPoolId poolId, out ISceneKernelSpawnPool pool)
        {
            if (poolId.IsEmpty)
            {
                pool = null!;
                return false;
            }

            return poolsById.TryGetValue(poolId, out pool!);
        }
    }
}