#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Search;
using Game.Spawn;
using Game.TransformSystem;
using UnityEngine;
using VContainer;

namespace Game.Fire
{
    public sealed class FirePatternService : IFirePatternService
    {
        bool EnableDebugLogs = false;
        readonly ISceneSpawnerRegistry _spawnerRegistry;

        public FirePatternService(ISceneSpawnerRegistry spawnerRegistry)
        {
            _spawnerRegistry = spawnerRegistry;
        }

        public async UniTask ExecuteAsync(
            BaseFirePattern[] patterns,
            UnitSpawnContext inputContext,
            IReadOnlyList<DynamicSearchHit> targetHits,
            CancellationToken ct = default)
        {
            if (patterns == null || patterns.Length == 0)
                return;

            var scheduled = new List<(BaseFirePattern Pattern, FireContext Context, int PatternIndex, int ContextIndex)>(64);
            float startTime = Time.time;
            var spawnedResolvers = new List<IObjectResolver>(32);

            for (int p = 0; p < patterns.Length; p++)
            {
                ct.ThrowIfCancellationRequested();

                var pattern = patterns[p];
                if (pattern == null)
                    continue;

                int emitCount = (pattern is BaseFirePattern b) ? b.EmitRepeatCount : 1;
                for (int emitIndex = 0; emitIndex < emitCount; emitIndex++)
                {
                    var contexts = await pattern.EvaluateAsync(this, inputContext, targetHits, ct);
                    if (contexts == null || contexts.Length == 0)
                        continue;

                    for (int i = 0; i < contexts.Length; i++)
                        scheduled.Add((pattern, contexts[i], p, i));
                }
            }

            scheduled.Sort((a, b) =>
            {
                int cmp = a.Context.Data.DelayTime.CompareTo(b.Context.Data.DelayTime);
                if (cmp != 0) return cmp;
                cmp = a.PatternIndex.CompareTo(b.PatternIndex);
                if (cmp != 0) return cmp;
                return a.ContextIndex.CompareTo(b.ContextIndex);
            });
            if (scheduled.Count > 0)
            {
                if (EnableDebugLogs)
                {
                    var sb = new System.Text.StringBuilder();
                    int max = Math.Min(16, scheduled.Count);
                    for (int j = 0; j < max; j++)
                    {
                        sb.Append(scheduled[j].Context.Data.DelayTime);
                        if (j + 1 < max) sb.Append(", ");
                    }
                }
            }
            const float delayGroupEpsilon = 0.0001f;
            int cursor = 0;
            while (cursor < scheduled.Count)
            {
                ct.ThrowIfCancellationRequested();

                var groupDelay = scheduled[cursor].Context.Data.DelayTime;
                float elapsed = Time.time - startTime;
                float waitTime = groupDelay - elapsed;
                if (waitTime > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: ct);

                var taskBuffer = new List<UniTask<IObjectResolver?>>(8);
                var autoDespawnFlags = new List<bool>(8);

                while (cursor < scheduled.Count)
                {
                    var item = scheduled[cursor];
                    if (Mathf.Abs(item.Context.Data.DelayTime - groupDelay) > delayGroupEpsilon)
                        break;

                    taskBuffer.Add(SpawnAndDeliverAsync(item.Pattern, item.Context, ct));
                    autoDespawnFlags.Add(item.Pattern.AutoDespawnSpawnedUnitsAfterComplete);
                    cursor++;
                }

                var resolved = await UniTask.WhenAll(taskBuffer);
                for (int i = 0; i < resolved.Length; i++)
                {
                    var resolver = resolved[i];
                    if (resolver != null && autoDespawnFlags[i])
                        spawnedResolvers.Add(resolver);
                }
            }

            if (spawnedResolvers.Count == 0)
                return;

            for (int i = 0; i < spawnedResolvers.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                await DespawnLikeSelfDespawnAsync(spawnedResolvers[i], ct);
            }
        }


        public async UniTask<IObjectResolver?> SpawnAndDeliverAsync(
            BaseFirePattern pattern,
            FireContext context,
            CancellationToken ct = default)
        {
            var spawner = _spawnerRegistry.Get<IAsyncSpawnerService>(pattern.SpawnerKind, pattern.SpawnerTag);
            if (spawner == null)
            {
                Debug.LogWarning($"[FirePatternService] No spawner found for Pattern={pattern.DebugName} Kind={pattern.SpawnerKind} Tag={pattern.SpawnerTag}");
                return null;
            }

            var spawnParams = pattern.BuildSpawnParams(in context);
            if (EnableDebugLogs)
                Debug.Log($"[FirePatternService] SpawnAndDeliverAsync: Pattern={pattern.DebugName} Pos={spawnParams.Position} Rot={spawnParams.Rotation.eulerAngles} Prefab={(spawnParams.Prefab != null ? spawnParams.Prefab.name : "null")} Template={(spawnParams.Template != null ? spawnParams.Template.name : "null")} WorldSpace={spawnParams.WorldSpace}");
            var outputResolver = await spawner.SpawnAsync(spawnParams, ct);
            if (outputResolver == null)
            {
                Debug.LogWarning($"[FirePatternService] SpawnAsync returned null for Pattern={pattern.DebugName}");
                return null;
            }

            try
            {
                // Apply initial rotation via TransformChannel runtime if available.
                if (!TrySetInitialRotation(outputResolver, spawnParams.Rotation) && EnableDebugLogs)
                    Debug.LogWarning($"[FirePatternService] Initial rotation was skipped because no TransformChannel runtime was resolved. Pattern={pattern.DebugName}");

                if (outputResolver.TryResolve<IOutputFirePattern>(out var output) && output != null)
                {
                    output.OnFireContextReceived(in context);
                }
                else
                {
                    if (EnableDebugLogs)
                        Debug.LogWarning($"[FirePatternService] Spawned resolver does not implement IOutputFirePattern: Pattern={pattern.DebugName}");
                }
                // Additional debug: try to resolve a GameObject or LifetimeScope from the resolver to inspect transform
                if (outputResolver.TryResolve<GameObject>(out var go) && go != null)
                {

                }
                else if (outputResolver.TryResolve<ILTSIdentityService>(out var lts) && lts != null)
                {
                    var go2 = lts.SelfTransform;
                }
            }
            catch (Exception ex)
            {
                if (EnableDebugLogs)
                    Debug.LogWarning($"[FirePatternService] Exception delivering FireContext to spawned output: {ex}");
            }

            return outputResolver;
        }

        static bool TrySetInitialRotation(IObjectResolver resolver, Quaternion rotation)
        {
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<ITransformChannelHubService>(out var hub) || hub == null)
                return false;

            if (hub.TryGetRuntime(TransformChannelTagUtility.DefaultTag, out var runtime) && runtime != null)
            {
                runtime.SetInitialRotation(rotation);
                return true;
            }

            var tags = new List<string>(4);
            hub.GetTags(tags);
            for (var i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                if (hub.TryGetRuntime(tag, out runtime) && runtime != null)
                {
                    runtime.SetInitialRotation(rotation);
                    return true;
                }
            }

            return false;
        }

        static async UniTask DespawnLikeSelfDespawnAsync(IObjectResolver resolver, CancellationToken ct)
        {
            if (resolver == null)
                return;

            await WaitUntilScopeCommandsIdleAsync(resolver, ct);

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) &&
                runtimeScope != null &&
                !IsDestroyed(runtimeScope) &&
                resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                pool != null)
            {
                pool.Release(runtimeScope);
                return;
            }

            if (resolver.TryResolve<BaseLifetimeScope>(out var baseScope) &&
                baseScope != null &&
                !IsDestroyed(baseScope))
            {
                await baseScope.DespawnAsync(ct);
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

        static async UniTask WaitUntilScopeCommandsIdleAsync(IObjectResolver resolver, CancellationToken ct)
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
    }
}
