#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Kernel.Layers
{
    internal sealed class SceneKernelPrefabPoolTable
    {
        readonly Dictionary<SceneKernelSpawnPoolId, ISceneKernelPrefabPool> poolsById = new Dictionary<SceneKernelSpawnPoolId, ISceneKernelPrefabPool>();

        public int Count => poolsById.Count;

        public void Clear()
        {
            poolsById.Clear();
        }

        public bool TryBind(ISceneKernelPrefabPool pool)
        {
            if (pool == null)
                throw new ArgumentNullException(nameof(pool));

            if (pool.PoolId.IsEmpty || poolsById.ContainsKey(pool.PoolId))
                return false;

            poolsById.Add(pool.PoolId, pool);
            return true;
        }

        public bool TryGet(SceneKernelSpawnPoolId poolId, out ISceneKernelPrefabPool pool)
        {
            if (poolId.IsEmpty)
            {
                pool = null!;
                return false;
            }

            return poolsById.TryGetValue(poolId, out pool!);
        }

        public IEnumerable<ISceneKernelPrefabPool> Pools => poolsById.Values;

        public void ForEach(Action<ISceneKernelPrefabPool> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            foreach (ISceneKernelPrefabPool pool in poolsById.Values)
                action(pool);
        }
    }
}