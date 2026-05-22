// Game.Spawn
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;
using VContainer;
using Game.Common;

namespace Game.Spawn
{
    public struct ScopeSpawnParams
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public bool WorldSpace { get; set; }

        /// <summary>生成時の親。必須にしておいた方が事故りません。</summary>
        public Transform Parent { get; set; }

        /// <summary>
        /// true: EnsureScopeBuilt() で同期ビルド
        /// false: WhenBuiltAsync() を待つ（非同期ビルド）
        /// </summary>
        public bool BuildSynchronously { get; set; }

        public static ScopeSpawnParams World(Transform parent, Vector3 pos)
            => new ScopeSpawnParams
            {
                Parent = parent,
                Position = pos,
                Rotation = Quaternion.identity,
                WorldSpace = true,
                BuildSynchronously = true,
            };
    }
    /// <summary>
    /// Unified spawner for any scope implementing IScopeNode.
    /// </summary>
    public interface IScopeSpawner
    {
        UniTask<TScope> SpawnAsync<TScope>(
            TScope prefab,
            ScopeSpawnParams param,
            CancellationToken ct = default
        )
            where TScope : MonoBehaviour, IScopeNode;
    }

    public sealed class BaseLifetimeScopeSpawner : IScopeSpawner
    {
        public async UniTask<TScope> SpawnAsync<TScope>(
            TScope prefab,
            ScopeSpawnParams param,
            CancellationToken ct = default
        )
            where TScope : MonoBehaviour, IScopeNode
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            if (param.Parent == null)
                throw new ArgumentNullException(nameof(param.Parent));

            ct.ThrowIfCancellationRequested();

            TScope instance;
            if (param.WorldSpace)
            {
                instance = UnityEngine.Object.Instantiate(prefab, param.Position, param.Rotation, param.Parent);
            }
            else
            {
                instance = UnityEngine.Object.Instantiate(prefab, param.Parent);
                var t = instance.transform;
                t.localPosition = param.Position;
                t.localRotation = param.Rotation;
            }

            if (instance is KernelScopeHost runtime)
            {
                if (param.BuildSynchronously)
                {
                    runtime.EnsureScopeBuilt();
                }
                else
                {
                    await runtime.WhenBuiltAsync(ct);
                }
                await runtime.HandleSpawnAsync(ct);
            }
            else if (instance is KernelScopeHost scope)
            {
                if (param.BuildSynchronously)
                {
                    scope.EnsureScopeBuilt();
                }
                else
                {
                    await scope.WhenBuiltAsync(ct);
                }
            }

            return instance;
        }
    }
}


