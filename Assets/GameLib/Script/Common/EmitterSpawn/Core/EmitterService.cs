#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.DI;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// 繧ｨ繝溘ャ繧ｿ繝ｼ繧ｵ繝ｼ繝薙せ縺ｮ螳溯｣・・
    /// EntityLifetimeScope 縺ｾ縺溘・ RuntimeResolverMB 蜀・・繧ｵ繝ｼ繝薙せ縺ｨ縺励※蜍穂ｽ懊・
    /// </summary>
    public sealed class EmitterService : IEmitterService, IDisposable, IScopeReleaseHandler
    {
        readonly Transform _transform;
        readonly IRuntimeResolver _ownerResolver;
        readonly IScopeNode _ownerNode;
        readonly KernelScopeHost? _ownerScope;
        readonly ISceneSpawnerRegistry _spawnerRegistry;


        readonly Dictionary<IScopeNode, List<ISpawnContextConsumer>> _consumerMap =
            new(ReferenceEqualityComparer<IScopeNode>.Instance);
        readonly List<ISpawnContextConsumer> _notifyBuffer = new(8);
        readonly List<ISpawnContextConsumer> _transientRegistered = new(8);

        readonly List<CancellationTokenSource> _activePatterns = new();

        private BaseRuntimeTemplateSO? _overrideTemplate;
        private GameObject? _overridePrefab;

        public Vector3 Origin => _transform.position;
        public Quaternion Rotation => _transform.rotation;
        public IRuntimeResolver OwnerResolver => _ownerResolver;
        public IScopeNode OwnerNode => _ownerNode;
        public KernelScopeHost? OwnerScope => _ownerScope;
        public ISceneSpawnerRegistry SpawnerRegistry => _spawnerRegistry;

        public EmitterService(
            Transform transform,
            IRuntimeResolver ownerResolver,
            ISceneSpawnerRegistry spawnerRegistry,
            IScopeNode ownerNode,
            KernelScopeHost? ownerScope = null)
        {
            _transform = transform ? transform : throw new ArgumentNullException(nameof(transform));
            _ownerResolver = ownerResolver ?? throw new ArgumentNullException(nameof(ownerResolver));
            _spawnerRegistry = spawnerRegistry ?? throw new ArgumentNullException(nameof(spawnerRegistry));
            _ownerNode = ownerNode ?? throw new ArgumentNullException(nameof(ownerNode));
            _ownerScope = ownerScope;
        }
        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _overridePrefab = null;
            _overrideTemplate = null;
        }

        public bool RegisterSpawnContextConsumer(IScopeNode unitScope, ISpawnContextConsumer consumer)
        {
            if (unitScope == null || consumer == null)
                return false;

            if (!_consumerMap.TryGetValue(unitScope, out var list) || list == null)
            {
                list = new List<ISpawnContextConsumer>(4);
                _consumerMap[unitScope] = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], consumer))
                    return false;
            }
            list.Add(consumer);
            return true;
        }

        public bool UnregisterSpawnContextConsumer(IScopeNode unitScope, ISpawnContextConsumer consumer)
        {
            if (unitScope == null || consumer == null)
                return false;

            if (!_consumerMap.TryGetValue(unitScope, out var list) || list == null)
                return false;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(list[i], consumer))
                    continue;

                list.RemoveAt(i);
                if (list.Count == 0)
                    _consumerMap.Remove(unitScope);
                return true;
            }

            return false;
        }

        public async UniTask ExecutePatternAsync(ISpawnPattern pattern, BaseRuntimeTemplateSO? overrideTemplate = null, GameObject? overridePrefab = null, CancellationToken ct = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _activePatterns.Add(linkedCts);

            _overrideTemplate = overrideTemplate;
            _overridePrefab = overridePrefab;

            try
            {
                if (pattern == null)
                    throw new ArgumentNullException(nameof(pattern));

                var contexts = await pattern.EvaluateAsync(this, linkedCts.Token);

                Array.Sort(contexts, (a, b) =>
                {
                    int cmp = a.Data.DelayTime.CompareTo(b.Data.DelayTime);
                    return cmp != 0 ? cmp : a.DistanceFromEmitter.CompareTo(b.DistanceFromEmitter);
                });

                await SpawnUnitsAsync(pattern, contexts, linkedCts.Token);
            }
            finally
            {
                _activePatterns.Remove(linkedCts);
            }
        }

        public async UniTask SpawnUnitsAsync(ISpawnPattern pattern, SpawnContext[] contexts, CancellationToken ct = default)
        {
            var spawner = _spawnerRegistry.Get<IAsyncSpawnerService>(pattern.SpawnerKind, pattern.SpawnerTag);
            var spawnedResolvers = pattern.AutoDespawnSpawnedUnitsAfterComplete
                ? new List<IRuntimeResolver>(Mathf.Max(4, contexts.Length))
                : null;

            float startTime = Time.time;
            int waveIndex = 0;

            const float delayGroupEpsilon = 0.0001f;
            int cursor = 0;
            while (cursor < contexts.Length)
            {
                ct.ThrowIfCancellationRequested();

                var groupDelay = contexts[cursor].Data.DelayTime;
                float elapsed = Time.time - startTime;
                float waitTime = groupDelay - elapsed;
                if (waitTime > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: ct);

                var taskBuffer = new List<UniTask<IRuntimeResolver?>>(8);

                while (cursor < contexts.Length)
                {
                    var ctx = contexts[cursor];
                    if (Mathf.Abs(ctx.Data.DelayTime - groupDelay) > delayGroupEpsilon)
                        break;

                    var spawnCount = Mathf.Max(1, ctx.SpawnCount);
                    for (int spawnIdx = 0; spawnIdx < spawnCount; spawnIdx++)
                    {
                        var assignedWaveIndex = waveIndex++;
                        taskBuffer.Add(SpawnSingleUnit(spawner, ctx, assignedWaveIndex, ct));
                    }

                    cursor++;
                }

                var resolved = await UniTask.WhenAll(taskBuffer);
                if (spawnedResolvers != null)
                {
                    for (int i = 0; i < resolved.Length; i++)
                    {
                        var spawnedResolver = resolved[i];
                        if (spawnedResolver != null)
                            spawnedResolvers.Add(spawnedResolver);
                    }
                }
            }

            if (spawnedResolvers == null || spawnedResolvers.Count == 0)
                return;

            for (int i = 0; i < spawnedResolvers.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                await DespawnLikeSelfDespawnAsync(spawnedResolvers[i], ct);
            }
        }

        public void NotifySpawnedUnit(IRuntimeResolver unitResolver, in SpawnContext context, int waveIndex)
        {
            if (unitResolver == null)
                return;

            if (!TryGetUnitScopeNode(unitResolver, out var unitScope) || unitScope == null)
                return;

            RegisterTransientConsumersFromUnitResolver(unitResolver, unitScope);

            var unitCtx = new UnitSpawnContext(context, unitResolver, Time.time, waveIndex);
            NotifyConsumers(unitResolver, in unitCtx);

            UnregisterTransientConsumers(unitScope);
        }

        async UniTask<IRuntimeResolver?> SpawnSingleUnit(IAsyncSpawnerService spawner, SpawnContext ctx, int waveIndex, CancellationToken ct)
        {
            Vector3 finalPosition = ctx.Data.ApplyRandomOffset(ctx.Position, ctx.TangentDirection);

            var spawnParams = ctx.SpawnParams;
            spawnParams.Position = finalPosition;

            // Apply per-pattern overrides set via ExecutePatternAsync().
            // NOTE: OverrideTemplate is intended for Runtime spawners; OverridePrefab is for LTS spawners.
            if (_overridePrefab != null)
            {
                spawnParams.Prefab = _overridePrefab;
                spawnParams.Template = null;
            }
            else if (_overrideTemplate != null)
            {
                spawnParams.Template = _overrideTemplate;
                spawnParams.Prefab = null;
            }

            var unitResolver = await spawner.SpawnAsync(spawnParams, ct);
            if (unitResolver == null) return null;

            try
            {
                var tplName = _overrideTemplate != null ? _overrideTemplate.name : (ctx.SpawnParams.Template != null ? ctx.SpawnParams.Template.TemplateId : "(none)");

            }
            catch { }

            var unitCtx = new UnitSpawnContext(ctx, unitResolver, Time.time, waveIndex);

            // Register consumers from the spawned unit resolver (registration-based, no GetComponents scanning).
            // This avoids relying on scope hierarchy containing the emitter.
            TryGetUnitScopeNode(unitResolver, out var unitScope);
            RegisterTransientConsumersFromUnitResolver(unitResolver, unitScope);

            NotifyConsumers(unitResolver, in unitCtx);

            UnregisterTransientConsumers(unitScope);
            return unitResolver;
        }

        static async UniTask DespawnLikeSelfDespawnAsync(IRuntimeResolver resolver, CancellationToken ct)
        {
            if (resolver == null)
                return;

            await WaitUntilScopeCommandsIdleAsync(resolver, ct);

            if (resolver.TryResolve<KernelScopeHost>(out var runtimeScope) &&
                runtimeScope != null &&
                !IsDestroyed(runtimeScope) &&
                resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                pool != null)
            {
                pool.Release(runtimeScope);
                return;
            }

            if (resolver.TryResolve<KernelScopeHost>(out var runtimeBaseScope) &&
                runtimeBaseScope != null &&
                !IsDestroyed(runtimeBaseScope))
            {
                await runtimeBaseScope.DespawnAsync(ct);
                return;
            }

            if (resolver.TryResolve<Component>(out var component) &&
                component != null &&
                !IsDestroyed(component))
            {
                var go = component.gameObject;
                if (go != null && !IsDestroyed(go))
                    UnityEngine.Object.Destroy(go);
                return;
            }

            if (resolver.TryResolve<GameObject>(out var gameObject) &&
                gameObject != null &&
                !IsDestroyed(gameObject))
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        static async UniTask WaitUntilScopeCommandsIdleAsync(IRuntimeResolver resolver, CancellationToken ct)
        {
            if (resolver == null)
                return;

            if (!resolver.TryResolve<ICommandRunnerActivity>(out var activity) || activity == null)
                return;

            if (resolver.TryResolve<IScopeNode>(out var scope) && scope != null)
            {
                await activity.WaitUntilScopeIdleAsync(scope, ct);
                return;
            }

            await activity.WaitUntilIdleAsync(ct);
        }

        static bool IsDestroyed(object obj)
        {
            if (obj is not UnityEngine.Object unityObj)
                return false;

            return !unityObj;
        }

        void RegisterTransientConsumersFromUnitResolver(IRuntimeResolver unitResolver, IScopeNode? unitScope)
        {
            if (unitScope == null)
                return;

            if (!unitResolver.TryResolve<IReadOnlyList<ISpawnContextConsumer>>(out var consumers) || consumers == null || consumers.Count == 0)
            {
                if (unitResolver.TryResolve<ISpawnContextConsumer>(out var singleConsumer) && singleConsumer != null)
                {
                    if (RegisterSpawnContextConsumer(unitScope, singleConsumer))
                        _transientRegistered.Add(singleConsumer);
                }
                return;
            }

            // Track only those we actually added, so we can unregister after notification.
            _transientRegistered.Clear();
            for (int i = 0; i < consumers.Count; i++)
            {
                var c = consumers[i];
                if (c == null)
                    continue;

                if (RegisterSpawnContextConsumer(unitScope, c))
                    _transientRegistered.Add(c);
            }
        }

        void UnregisterTransientConsumers(IScopeNode? unitScope)
        {
            if (unitScope == null)
                return;

            if (_transientRegistered.Count == 0)
                return;

            for (int i = 0; i < _transientRegistered.Count; i++)
            {
                var c = _transientRegistered[i];
                if (c != null)
                    UnregisterSpawnContextConsumer(unitScope, c);
            }

            _transientRegistered.Clear();
        }

        void NotifyConsumers(IRuntimeResolver resolver, in UnitSpawnContext unitCtx)
        {

            if (!TryGetUnitScopeNode(resolver, out var unitScope) || unitScope == null)
                return;
            if (!_consumerMap.TryGetValue(unitScope, out var list) || list == null || list.Count == 0)
            {
                return;
            }

            _notifyBuffer.Clear();
            for (int i = 0; i < list.Count; i++)
                _notifyBuffer.Add(list[i]);
            for (int i = 0; i < _notifyBuffer.Count; i++)
            {
                try
                {
                    _notifyBuffer[i]?.OnSpawnContextReceived(in unitCtx);
                }
                catch
                {
                    // swallow: spawn notifications should not break spawning
                }
            }

            _notifyBuffer.Clear();
        }

        static bool TryGetUnitScopeNode(IRuntimeResolver resolver, out IScopeNode? node)
        {
            node = null;
            if (resolver == null) return false;

            // RuntimeResolver: Template 蛛ｴ縺ｧ RuntimeResolverMB 繧堤匳骭ｲ縺励※縺・ｋ蝣ｴ蜷医′縺ゅｋ
            if (resolver.TryResolve<KernelScopeHost>(out var runtimeScope) && runtimeScope != null)
            {
                node = runtimeScope;
                return true;
            }

            // LTS: Scope 繧堤匳骭ｲ縺励※縺・ｋ蝣ｴ蜷医′縺ゅｋ
            if (resolver.TryResolve<KernelScopeHost>(out var scope) && scope != null)
            {
                node = scope;
                return true;
            }

            return false;
        }

        sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new();

            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        public void Dispose()
        {
            for (int i = 0; i < _activePatterns.Count; i++)
            {
                try { _activePatterns[i].Cancel(); }
                catch { }
                try { _activePatterns[i].Dispose(); }
                catch { }
            }
            _activePatterns.Clear();

            _consumerMap.Clear();
            _notifyBuffer.Clear();
            _transientRegistered.Clear();
        }
    }
}


