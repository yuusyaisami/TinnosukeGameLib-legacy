#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.DI;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// 繧ｨ繝溘ャ繧ｿ繝ｼ繧ｵ繝ｼ繝薙せ縺ｮ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ縲・
    /// SpawnPattern 縺ｮ螳溯｡後→邂｡逅・ｒ諡・ｽ薙・
    /// </summary>
    public interface IEmitterService
    {
        Vector3 Origin { get; }
        Quaternion Rotation { get; }

        /// <summary>繧ｨ繝溘ャ繧ｿ繝ｼ縺ｮ隕ｪ・育函謌占・ｼ峨・ Resolver</summary>
        IRuntimeResolver OwnerResolver { get; }

        /// <summary>繧ｨ繝溘ャ繧ｿ繝ｼ縺ｮ隕ｪ・育函謌占・ｼ峨・ ScopeNode</summary>
        IScopeNode OwnerNode { get; }

        /// <summary>繧ｨ繝溘ャ繧ｿ繝ｼ縺ｮ隕ｪ・育函謌占・ｼ峨・ Scope・・untimeResolver 縺ｮ蝣ｴ蜷医・ null・・/summary>
        KernelScopeHost? OwnerScope { get; }

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
        void NotifySpawnedUnit(IRuntimeResolver unitResolver, in SpawnContext context, int waveIndex);
    }
}

