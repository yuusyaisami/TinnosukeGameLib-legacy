#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DI;
using UnityEngine;
using VContainer;

namespace Game.Spawn
{
    /// <summary>
    /// エミッターサービスのインターフェース。
    /// SpawnPattern の実行と管理を担当。
    /// </summary>
    public interface IEmitterService
    {
        Vector3 Origin { get; }
        Quaternion Rotation { get; }

        /// <summary>エミッターの親（生成者）の Resolver</summary>
        IObjectResolver OwnerResolver { get; }

        /// <summary>エミッターの親（生成者）の ScopeNode</summary>
        IScopeNode OwnerNode { get; }

        /// <summary>エミッターの親（生成者）の Scope（RuntimeResolver の場合は null）</summary>
        BaseLifetimeScope? OwnerScope { get; }

        /// <summary>SceneSpawnerRegistry</summary>
        ISceneSpawnerRegistry SpawnerRegistry { get; }

        /// <summary>
        /// Register a spawn-context consumer for a specific unit scope.
        /// The consumer will be notified when that scope is spawned via this emitter.
        /// </summary>
        bool RegisterSpawnContextConsumer(IScopeNode unitScope, ISpawnContextConsumer consumer);

        /// <summary>
        /// Unregister a previously registered consumer.
        /// </summary>
        bool UnregisterSpawnContextConsumer(IScopeNode unitScope, ISpawnContextConsumer consumer);

        UniTask ExecutePatternAsync(ISpawnPattern pattern, BaseRuntimeTemplateSO? overrideTemplate = null, GameObject? overridePrefab = null, CancellationToken ct = default);
        UniTask SpawnUnitsAsync(ISpawnPattern pattern, SpawnContext[] contexts, CancellationToken ct = default);

        /// <summary>
        /// Notify spawn-context consumers for a directly spawned unit.
        /// Intended for non-pattern spawns that still need FirePattern execution.
        /// </summary>
        void NotifySpawnedUnit(IObjectResolver unitResolver, in SpawnContext context, int waveIndex);
    }
}
