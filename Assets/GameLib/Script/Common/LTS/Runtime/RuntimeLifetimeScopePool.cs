#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Spawn;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;

namespace Game
{
    public interface IRuntimeLifetimeScopePool : IDisposable
    {
        UniTask<RuntimeLifetimeScope> AcquireAsync(
            BaseRuntimeTemplateSO template,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            RuntimeIdentityData? identity = null,
            IScopeNode? lifetimeScopeParent = null,
            CancellationToken ct = default);

        UniTask WarmupAsync(BaseRuntimeTemplateSO template, int count, CancellationToken ct = default);

        void Release(RuntimeLifetimeScope scope);
        bool TryEnqueueOnNextAcquire(RuntimeLifetimeScope scope, CommandListData commands, CommandRunOptions options);
    }

    public interface IRuntimeLifetimeScopePoolTelemetry
    {
        int TelemetryVersion { get; }
        RuntimeLifetimeScopePoolTelemetrySnapshot GetTelemetrySnapshot();
    }

    public sealed class RuntimeLifetimeScopePool : IRuntimeLifetimeScopePool, IRuntimeLifetimeScopePoolTelemetry
    {
        const int MaxRecentTelemetryEvents = 64;
        static bool s_isApplicationQuitting;
#if UNITY_EDITOR
        static bool s_isEditorExitingPlayMode;
#endif

        static RuntimeLifetimeScopePool()
        {
            Application.quitting += OnApplicationQuitting;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShutdownFlags()
        {
            s_isApplicationQuitting = false;
#if UNITY_EDITOR
            s_isEditorExitingPlayMode = false;
#endif
        }

        static void OnApplicationQuitting()
        {
            s_isApplicationQuitting = true;
        }

#if UNITY_EDITOR
        static void OnEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            switch (state)
            {
                case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                    s_isEditorExitingPlayMode = true;
                    break;
                case UnityEditor.PlayModeStateChange.EnteredEditMode:
                case UnityEditor.PlayModeStateChange.EnteredPlayMode:
                    s_isEditorExitingPlayMode = false;
                    break;
            }
        }
#endif

        sealed class ScopeRefComparer : IEqualityComparer<RuntimeLifetimeScope>
        {
            public static readonly ScopeRefComparer Instance = new();

            public bool Equals(RuntimeLifetimeScope? x, RuntimeLifetimeScope? y) => ReferenceEquals(x, y);
            public int GetHashCode(RuntimeLifetimeScope obj) => RuntimeHelpers.GetHashCode(obj);
        }

        sealed class ScopeLeaseTelemetry
        {
            public RuntimeLifetimeScopePoolKey Key;
            public bool IsPooledAcquire;
            public bool WasReused;
            public string TemplateId = string.Empty;
            public string Category = string.Empty;
            public string ScopeName = string.Empty;
        }

        sealed class PoolTelemetryBucket
        {
            public string KeyLabel = string.Empty;
            public string PrefabName = string.Empty;
            public int PrefabInstanceId;
            public string ParentName = string.Empty;
            public string ParentPath = string.Empty;
            public int ParentInstanceId;

            public int AcquireCount;
            public int PooledAcquireCount;
            public int NonPooledAcquireCount;
            public int NewCount;
            public int ReuseCount;

            public int WarmupCallCount;
            public int WarmupRequestedCount;
            public int WarmupCreatedCount;
            public int WarmupReusedCount;

            public int ReleaseCount;
            public int ReturnedToPoolCount;
            public int DestroyedCount;

            public int CurrentPooledAliveCount;
            public int CurrentPooledActiveCount;
            public int CurrentNonPooledAliveCount;
            public int CurrentNonPooledActiveCount;
            public int PeakAliveCount;
            public int PeakActiveCount;

            public double AcquireDurationTotalMs;
            public double AcquireDurationMaxMs;
            public double ReuseDurationTotalMs;
            public double ReuseDurationMaxMs;
            public double NewDurationTotalMs;
            public double NewDurationMaxMs;
            public double WarmupDurationTotalMs;
            public double WarmupDurationMaxMs;
            public double LastAcquireDurationMs;
            public double LastReuseDurationMs;
            public double LastNewDurationMs;
            public double LastWarmupDurationMs;

            public string LastTemplateId = string.Empty;
            public string LastCategory = string.Empty;
            public string LastScopeName = string.Empty;
            public string LastOperation = string.Empty;
            public int LastFrame = -1;
            public double LastRealtime;

            public readonly Dictionary<string, int> TemplateCounts = new(StringComparer.Ordinal);
            public readonly Dictionary<string, int> CategoryCounts = new(StringComparer.Ordinal);
        }

        sealed class PoolTelemetryEventRecord
        {
            public long Sequence;
            public string Operation = string.Empty;
            public string KeyLabel = string.Empty;
            public string PrefabName = string.Empty;
            public string ParentName = string.Empty;
            public string ParentPath = string.Empty;
            public string ScopeName = string.Empty;
            public string TemplateId = string.Empty;
            public string Category = string.Empty;
            public double DurationMs;
            public int Frame = -1;
            public double Realtime;
        }

        readonly struct PendingOnAcquireCommand
        {
            public readonly CommandListData Commands;
            public readonly CommandRunOptions Options;

            public PendingOnAcquireCommand(CommandListData commands, CommandRunOptions options)
            {
                Commands = commands;
                Options = options;
            }
        }

        // Pool key is (Parent Transform, Prefab).
        // This prevents cross-parent reuse and satisfies the "same parent + same prefab only" constraint.
        readonly Dictionary<RuntimeLifetimeScopePoolKey, ObjectPool<RuntimeLifetimeScope>> _pools =
            new(new RuntimeLifetimeScopePoolKeyComparer());
        readonly Dictionary<RuntimeLifetimeScope, List<PendingOnAcquireCommand>> _pendingOnAcquireCommands =
            new(ScopeRefComparer.Instance);
        readonly HashSet<RuntimeLifetimeScope> _releasingScopes =
            new(ScopeRefComparer.Instance);
        readonly Dictionary<RuntimeLifetimeScopePoolKey, PoolTelemetryBucket> _telemetryBuckets =
            new(new RuntimeLifetimeScopePoolKeyComparer());
        readonly Dictionary<RuntimeLifetimeScope, ScopeLeaseTelemetry> _activeScopeTelemetry =
            new(ScopeRefComparer.Instance);
        readonly List<PoolTelemetryEventRecord> _recentTelemetryEvents =
            new(MaxRecentTelemetryEvents);

        readonly Transform? _poolRoot;
        readonly LifetimeScope? _buildParent;
        readonly double _telemetryStartRealtime;
        readonly int _telemetryStartFrame;

        int _telemetryVersion;
        int _currentGlobalActiveCount;
        int _peakGlobalActiveCount;
        int _currentGlobalAliveCount;
        int _peakGlobalAliveCount;
        long _telemetrySequence;

        public RuntimeLifetimeScopePool(LifetimeScope? buildParent, Transform? poolRoot = null)
        {
            _buildParent = buildParent;
            _poolRoot = poolRoot;
            _telemetryStartRealtime = Time.realtimeSinceStartupAsDouble;
            _telemetryStartFrame = Time.frameCount;
        }

        public int TelemetryVersion => _telemetryVersion;

        public async UniTask<RuntimeLifetimeScope> AcquireAsync(
            BaseRuntimeTemplateSO template,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            RuntimeIdentityData? identity = null,
            IScopeNode? lifetimeScopeParent = null,
            CancellationToken ct = default)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            ct.ThrowIfCancellationRequested();

            var key = new RuntimeLifetimeScopePoolKey(template.Prefab, parent);
            var acquireStartRealtime = Time.realtimeSinceStartupAsDouble;

            if (!template.UsePooling)
            {
                var scopeNonPooled = CreateInstance(template);
                ConfigureOnAcquire(scopeNonPooled, parent, position, rotation, lifetimeScopeParent);

                // Non-pooled instance: ensure no stale pool key is kept.
                scopeNonPooled.PoolKey = null;

                scopeNonPooled.ConfigureForAcquire(template, identity, ensureBuilt: true);
                await scopeNonPooled.SetActiveAsync(active: true, isReset: true, ct);

                if (scopeNonPooled.TryResolveLocal<IScopeLifecycleService>(out var lifecycleNonPooled) &&
                    lifecycleNonPooled != null)
                {
                    await lifecycleNonPooled.HandleSpawnAsync(ct);
                }

                await ExecutePendingOnAcquireAsync(scopeNonPooled, ct);

                var durationMs = GetElapsedMilliseconds(acquireStartRealtime);
                RecordAcquireTelemetry(
                    key,
                    template,
                    scopeNonPooled,
                    isPooledAcquire: false,
                    wasReused: false,
                    durationMs);

                return scopeNonPooled;
            }

            var pool = GetOrCreatePool(key);
            var hadInactiveBeforeGet = GetAvailableInactiveCount(key) > 0;
            var scope = pool.Get();
            var wasReused = hadInactiveBeforeGet;
            if (scope == null || !scope)
            {
                scope = CreatePooledInstance(key.PrefabKey);
                wasReused = false;
            }

            ConfigureOnAcquire(scope, parent, position, rotation, lifetimeScopeParent);

            // Record the pool key so Release can return to the exact parent+prefab pool.
            scope.PoolKey = key;

            scope.ConfigureForAcquire(template, identity, ensureBuilt: true);
            await scope.SetActiveAsync(active: true, isReset: true, ct);

            if (scope.TryResolveLocal<IScopeLifecycleService>(out var lifecycle) &&
                lifecycle != null)
            {
                await lifecycle.HandleSpawnAsync(ct);
            }

            await ExecutePendingOnAcquireAsync(scope, ct);

            var pooledDurationMs = GetElapsedMilliseconds(acquireStartRealtime);
            RecordAcquireTelemetry(
                key,
                template,
                scope,
                isPooledAcquire: true,
                wasReused,
                pooledDurationMs);

            return scope;
        }

        public UniTask WarmupAsync(BaseRuntimeTemplateSO template, int count, CancellationToken ct = default)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));
            if (count <= 0)
                return UniTask.CompletedTask;

            if (!template.UsePooling)
                return UniTask.CompletedTask;

            // NOTE:
            // Pool is keyed by (Parent Transform, Prefab). Warmup does not know the intended parent,
            // so we can only warm a "default" parent pool when one is available.
            // If no suitable parent is available, warmup becomes a no-op to avoid mismatching pools.
            var warmupParent = _poolRoot != null
                ? _poolRoot
                : _buildParent != null
                    ? _buildParent.transform
                    : null;
            if (warmupParent == null)
                return UniTask.CompletedTask;

            var key = new RuntimeLifetimeScopePoolKey(template.Prefab, warmupParent);
            var pool = GetOrCreatePool(key);
            var bucket = GetOrCreateTelemetryBucket(key);
            RecordWarmupRequestTelemetry(bucket, template, count);
            var availableInactiveCount = GetAvailableInactiveCount(key);

            var tmp = ListPool<RuntimeLifetimeScope>.Get();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var warmupStartRealtime = Time.realtimeSinceStartupAsDouble;
                    var scope = pool.Get();
                    var wasReused = availableInactiveCount > 0;
                    if (scope == null || !scope)
                    {
                        scope = CreatePooledInstance(key.PrefabKey);
                        wasReused = false;
                    }

                    if (wasReused)
                        availableInactiveCount--;

                    scope.EnsureScopeBuilt();
                    RecordWarmupIterationTelemetry(
                        bucket,
                        template,
                        scope,
                        wasReused,
                        GetElapsedMilliseconds(warmupStartRealtime));
                    tmp.Add(scope);
                }
            }
            finally
            {
                for (int i = 0; i < tmp.Count; i++)
                {
                    var scope = tmp[i];
                    if (scope != null)
                        pool.Release(scope);
                }
                ListPool<RuntimeLifetimeScope>.Release(tmp);
            }

            return UniTask.CompletedTask;
        }

        public void Release(RuntimeLifetimeScope scope)
        {
            if (scope == null || !scope)
                return;
            if (!scope.IsAcquired && (!scope.gameObject.activeSelf || !scope.IsActive))
                return;
            if (!TryBeginRelease(scope))
                return;

            var template = scope.ActiveTemplate;
            if (template == null)
            {
                ClearPendingOnAcquire(scope);
                CompleteReleaseTelemetry(scope, template, fallbackKey: scope.PoolKey, returnedToPool: false, destroyed: true);
                UnityEngine.Object.Destroy(scope.gameObject);
                EndRelease(scope);
                return;
            }

            // If this scope was explicitly created to bypass pooling, always destroy it on release.
            if (!scope.AllowPooling || !template.UsePooling)
            {
                // Ensure no stale pool key remains on non-pooled instances.
                scope.PoolKey = null;
                ClearPendingOnAcquire(scope);
                if (scope.TryResolveLocal<IScopeLifecycleService>(out var lifecycleNonPooled) &&
                    lifecycleNonPooled != null)
                {
                    UniTask.Void(async () =>
                    {
                        try
                        {
                            await lifecycleNonPooled.HandleDespawnAsync(CancellationToken.None);
                        }
                        finally
                        {
                            await scope.SetActiveAsync(active: false, isReset: true, CancellationToken.None);
                            CompleteReleaseTelemetry(scope, template, fallbackKey: null, returnedToPool: false, destroyed: true);
                            UnityEngine.Object.Destroy(scope.gameObject);
                            EndRelease(scope);
                        }
                    });
                    return;
                }

                _ = scope.SetActiveAsync(active: false, isReset: true, CancellationToken.None);
                CompleteReleaseTelemetry(scope, template, fallbackKey: null, returnedToPool: false, destroyed: true);
                UnityEngine.Object.Destroy(scope.gameObject);
                EndRelease(scope);
                return;
            }

            if (ShouldDestroyInsteadOfReturnToPool(scope))
            {
                scope.PoolKey = null;
                ClearPendingOnAcquire(scope);

                if (scope.TryResolveLocal<IScopeLifecycleService>(out var shutdownLifecycle) &&
                    shutdownLifecycle != null)
                {
                    UniTask.Void(async () =>
                    {
                        try
                        {
                            await shutdownLifecycle.HandleDespawnAsync(CancellationToken.None);
                        }
                        finally
                        {
                            await scope.SetActiveAsync(active: false, isReset: true, CancellationToken.None);
                            CompleteReleaseTelemetry(scope, template, fallbackKey: null, returnedToPool: false, destroyed: true);
                            UnityEngine.Object.Destroy(scope.gameObject);
                            EndRelease(scope);
                        }
                    });
                    return;
                }

                _ = scope.SetActiveAsync(active: false, isReset: true, CancellationToken.None);
                CompleteReleaseTelemetry(scope, template, fallbackKey: null, returnedToPool: false, destroyed: true);
                UnityEngine.Object.Destroy(scope.gameObject);
                EndRelease(scope);
                return;
            }

            // Return to the exact pool used at Acquire time (parent + prefab).
            // This prevents "parent jumping" and guarantees same-parent reuse only.
            var key = scope.PoolKey;
            if (key == null || !_pools.TryGetValue(key.Value, out var pool))
            {
                ClearPendingOnAcquire(scope);
                CompleteReleaseTelemetry(scope, template, fallbackKey: key, returnedToPool: false, destroyed: true);
                UnityEngine.Object.Destroy(scope.gameObject);
                EndRelease(scope);
                return;
            }

            if (scope.TryResolveLocal<IScopeLifecycleService>(out var lifecycle) &&
                lifecycle != null)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await lifecycle.HandleDespawnAsync(CancellationToken.None);
                    }
                    finally
                    {
                        try
                        {
                            await scope.SetActiveAsync(active: false, isReset: true, CancellationToken.None);
                            pool.Release(scope);
                            CompleteReleaseTelemetry(scope, template, fallbackKey: key, returnedToPool: true, destroyed: false);
                        }
                        finally
                        {
                            EndRelease(scope);
                        }
                    }
                });
                return;
            }

            try
            {
                _ = scope.SetActiveAsync(active: false, isReset: true, CancellationToken.None);
                pool.Release(scope);
                CompleteReleaseTelemetry(scope, template, fallbackKey: key, returnedToPool: true, destroyed: false);
            }
            finally
            {
                EndRelease(scope);
            }
        }

        public bool TryEnqueueOnNextAcquire(RuntimeLifetimeScope scope, CommandListData commands, CommandRunOptions options)
        {
            if (scope == null || !scope)
                return false;
            if (commands == null || commands.Count == 0)
                return false;

            if (!_pendingOnAcquireCommands.TryGetValue(scope, out var list) || list == null)
            {
                list = new List<PendingOnAcquireCommand>(2);
                _pendingOnAcquireCommands[scope] = list;
            }

            list.Add(new PendingOnAcquireCommand(commands, options));
            return true;
        }

        public void Dispose()
        {
            foreach (var kv in _pools)
            {
                kv.Value.Dispose();
            }
            _pools.Clear();
            _pendingOnAcquireCommands.Clear();
            _releasingScopes.Clear();
            _telemetryBuckets.Clear();
            _activeScopeTelemetry.Clear();
            _recentTelemetryEvents.Clear();
        }

        public RuntimeLifetimeScopePoolTelemetrySnapshot GetTelemetrySnapshot()
        {
            CleanupDestroyedActiveScopes();

            var snapshot = new RuntimeLifetimeScopePoolTelemetrySnapshot
            {
                Version = _telemetryVersion,
                SessionStartFrame = _telemetryStartFrame,
                SessionElapsedSeconds = Math.Max(0d, Time.realtimeSinceStartupAsDouble - _telemetryStartRealtime),
                CurrentActiveCount = _currentGlobalActiveCount,
                PeakActiveCount = _peakGlobalActiveCount,
                CurrentAliveCount = _currentGlobalAliveCount,
                PeakAliveCount = _peakGlobalAliveCount,
            };

            foreach (var pair in _telemetryBuckets)
            {
                var bucket = pair.Value;
                RefreshBucketDescriptor(bucket, pair.Key);

                var currentActiveCount = GetBucketCurrentActiveCount(bucket);
                var currentAliveCount = GetBucketCurrentAliveCount(bucket);
                var currentPooledAvailableCount = Math.Max(0, bucket.CurrentPooledAliveCount - bucket.CurrentPooledActiveCount);
                var totalCreateCount = bucket.NewCount + bucket.WarmupCreatedCount;
                var totalReuseCount = bucket.ReuseCount + bucket.WarmupReusedCount;

                snapshot.TotalKeyCount++;
                snapshot.TotalAcquireCount += bucket.AcquireCount;
                snapshot.TotalPooledAcquireCount += bucket.PooledAcquireCount;
                snapshot.TotalNonPooledAcquireCount += bucket.NonPooledAcquireCount;
                snapshot.TotalNewCount += bucket.NewCount;
                snapshot.TotalReuseCount += bucket.ReuseCount;
                snapshot.TotalWarmupCallCount += bucket.WarmupCallCount;
                snapshot.TotalWarmupRequestedCount += bucket.WarmupRequestedCount;
                snapshot.TotalWarmupCreatedCount += bucket.WarmupCreatedCount;
                snapshot.TotalWarmupReusedCount += bucket.WarmupReusedCount;
                snapshot.TotalReleaseCount += bucket.ReleaseCount;
                snapshot.TotalReturnedToPoolCount += bucket.ReturnedToPoolCount;
                snapshot.TotalDestroyedCount += bucket.DestroyedCount;
                snapshot.CurrentPooledAvailableCount += currentPooledAvailableCount;

                if (bucket.AcquireDurationMaxMs > snapshot.MaxAcquireDurationMs)
                    snapshot.MaxAcquireDurationMs = bucket.AcquireDurationMaxMs;
                if (bucket.NewDurationMaxMs > snapshot.MaxNewDurationMs)
                    snapshot.MaxNewDurationMs = bucket.NewDurationMaxMs;
                if (bucket.ReuseDurationMaxMs > snapshot.MaxReuseDurationMs)
                    snapshot.MaxReuseDurationMs = bucket.ReuseDurationMaxMs;
                if (bucket.WarmupDurationMaxMs > snapshot.MaxWarmupDurationMs)
                    snapshot.MaxWarmupDurationMs = bucket.WarmupDurationMaxMs;

                snapshot.KeySnapshots.Add(new RuntimeLifetimeScopePoolKeyTelemetrySnapshot
                {
                    KeyLabel = bucket.KeyLabel,
                    PrefabName = bucket.PrefabName,
                    PrefabInstanceId = bucket.PrefabInstanceId,
                    ParentName = bucket.ParentName,
                    ParentPath = bucket.ParentPath,
                    ParentInstanceId = bucket.ParentInstanceId,
                    AcquireCount = bucket.AcquireCount,
                    PooledAcquireCount = bucket.PooledAcquireCount,
                    NonPooledAcquireCount = bucket.NonPooledAcquireCount,
                    NewCount = bucket.NewCount,
                    ReuseCount = bucket.ReuseCount,
                    WarmupCallCount = bucket.WarmupCallCount,
                    WarmupRequestedCount = bucket.WarmupRequestedCount,
                    WarmupCreatedCount = bucket.WarmupCreatedCount,
                    WarmupReusedCount = bucket.WarmupReusedCount,
                    ReleaseCount = bucket.ReleaseCount,
                    ReturnedToPoolCount = bucket.ReturnedToPoolCount,
                    DestroyedCount = bucket.DestroyedCount,
                    CurrentActiveCount = currentActiveCount,
                    PeakActiveCount = bucket.PeakActiveCount,
                    CurrentAliveCount = currentAliveCount,
                    PeakAliveCount = bucket.PeakAliveCount,
                    CurrentPooledAliveCount = bucket.CurrentPooledAliveCount,
                    CurrentPooledActiveCount = bucket.CurrentPooledActiveCount,
                    CurrentPooledAvailableCount = currentPooledAvailableCount,
                    CurrentNonPooledAliveCount = bucket.CurrentNonPooledAliveCount,
                    CurrentNonPooledActiveCount = bucket.CurrentNonPooledActiveCount,
                    TotalCreateCount = totalCreateCount,
                    TotalReuseCount = totalReuseCount,
                    AcquireDurationTotalMs = bucket.AcquireDurationTotalMs,
                    AcquireDurationMaxMs = bucket.AcquireDurationMaxMs,
                    NewDurationTotalMs = bucket.NewDurationTotalMs,
                    NewDurationMaxMs = bucket.NewDurationMaxMs,
                    ReuseDurationTotalMs = bucket.ReuseDurationTotalMs,
                    ReuseDurationMaxMs = bucket.ReuseDurationMaxMs,
                    WarmupDurationTotalMs = bucket.WarmupDurationTotalMs,
                    WarmupDurationMaxMs = bucket.WarmupDurationMaxMs,
                    LastAcquireDurationMs = bucket.LastAcquireDurationMs,
                    LastNewDurationMs = bucket.LastNewDurationMs,
                    LastReuseDurationMs = bucket.LastReuseDurationMs,
                    LastWarmupDurationMs = bucket.LastWarmupDurationMs,
                    LastTemplateId = bucket.LastTemplateId,
                    LastCategory = bucket.LastCategory,
                    LastScopeName = bucket.LastScopeName,
                    LastOperation = bucket.LastOperation,
                    LastFrame = bucket.LastFrame,
                    LastRealtime = bucket.LastRealtime,
                    TemplateSummary = FormatCountMap(bucket.TemplateCounts, maxItems: 6),
                    CategorySummary = FormatCountMap(bucket.CategoryCounts, maxItems: 4),
                });
            }

            snapshot.KeySnapshots.Sort(CompareKeySnapshots);

            for (int i = _recentTelemetryEvents.Count - 1; i >= 0; i--)
            {
                var evt = _recentTelemetryEvents[i];
                snapshot.RecentEventSnapshots.Add(new RuntimeLifetimeScopePoolEventTelemetrySnapshot
                {
                    Sequence = evt.Sequence,
                    Operation = evt.Operation,
                    KeyLabel = evt.KeyLabel,
                    PrefabName = evt.PrefabName,
                    ParentName = evt.ParentName,
                    ParentPath = evt.ParentPath,
                    ScopeName = evt.ScopeName,
                    TemplateId = evt.TemplateId,
                    Category = evt.Category,
                    DurationMs = evt.DurationMs,
                    Frame = evt.Frame,
                    Realtime = evt.Realtime,
                });
            }

            return snapshot;
        }

        internal void RecordExternalAcquire(
            BaseRuntimeTemplateSO template,
            Transform parent,
            RuntimeLifetimeScope scope,
            double durationMs)
        {
            if (template == null || parent == null || scope == null || !scope)
                return;

            var key = new RuntimeLifetimeScopePoolKey(template.Prefab, parent);
            RecordAcquireTelemetry(
                key,
                template,
                scope,
                isPooledAcquire: false,
                wasReused: false,
                durationMs);
        }

        void RecordAcquireTelemetry(
            RuntimeLifetimeScopePoolKey key,
            BaseRuntimeTemplateSO template,
            RuntimeLifetimeScope scope,
            bool isPooledAcquire,
            bool wasReused,
            double durationMs)
        {
            var bucket = GetOrCreateTelemetryBucket(key);
            ObserveTemplate(bucket, template);

            bucket.AcquireCount++;
            if (isPooledAcquire)
                bucket.PooledAcquireCount++;
            else
                bucket.NonPooledAcquireCount++;

            bucket.AcquireDurationTotalMs += durationMs;
            if (durationMs > bucket.AcquireDurationMaxMs)
                bucket.AcquireDurationMaxMs = durationMs;
            bucket.LastAcquireDurationMs = durationMs;

            if (wasReused)
            {
                bucket.ReuseCount++;
                bucket.ReuseDurationTotalMs += durationMs;
                if (durationMs > bucket.ReuseDurationMaxMs)
                    bucket.ReuseDurationMaxMs = durationMs;
                bucket.LastReuseDurationMs = durationMs;
            }
            else
            {
                bucket.NewCount++;
                bucket.NewDurationTotalMs += durationMs;
                if (durationMs > bucket.NewDurationMaxMs)
                    bucket.NewDurationMaxMs = durationMs;
                bucket.LastNewDurationMs = durationMs;

                if (isPooledAcquire)
                    bucket.CurrentPooledAliveCount++;
                else
                    bucket.CurrentNonPooledAliveCount++;

                UpdateGlobalAliveCount(1);
            }

            if (isPooledAcquire)
                bucket.CurrentPooledActiveCount++;
            else
                bucket.CurrentNonPooledActiveCount++;

            UpdateGlobalActiveCount(1);
            RefreshBucketPeaks(bucket);

            bucket.LastScopeName = GetScopeName(scope);
            bucket.LastOperation = isPooledAcquire
                ? (wasReused ? "Acquire.Reuse" : "Acquire.NewPooled")
                : "Acquire.NewNoPool";
            bucket.LastFrame = Time.frameCount;
            bucket.LastRealtime = Time.realtimeSinceStartupAsDouble;

            _activeScopeTelemetry[scope] = new ScopeLeaseTelemetry
            {
                Key = key,
                IsPooledAcquire = isPooledAcquire,
                WasReused = wasReused,
                TemplateId = bucket.LastTemplateId,
                Category = bucket.LastCategory,
                ScopeName = bucket.LastScopeName,
            };

            PushTelemetryEvent(
                bucket,
                bucket.LastOperation,
                bucket.LastScopeName,
                bucket.LastTemplateId,
                bucket.LastCategory,
                durationMs);
            TouchTelemetry();
        }

        void RecordWarmupRequestTelemetry(PoolTelemetryBucket bucket, BaseRuntimeTemplateSO template, int requestedCount)
        {
            SetLastTemplateLabels(bucket, template);
            bucket.WarmupCallCount++;
            bucket.WarmupRequestedCount += Math.Max(0, requestedCount);
            bucket.LastOperation = "Warmup.Request";
            bucket.LastFrame = Time.frameCount;
            bucket.LastRealtime = Time.realtimeSinceStartupAsDouble;
            TouchTelemetry();
        }

        void RecordWarmupIterationTelemetry(
            PoolTelemetryBucket bucket,
            BaseRuntimeTemplateSO template,
            RuntimeLifetimeScope scope,
            bool wasReused,
            double durationMs)
        {
            ObserveTemplate(bucket, template);

            if (wasReused)
            {
                bucket.WarmupReusedCount++;
            }
            else
            {
                bucket.WarmupCreatedCount++;
                bucket.CurrentPooledAliveCount++;
                UpdateGlobalAliveCount(1);
                RefreshBucketPeaks(bucket);
            }

            bucket.WarmupDurationTotalMs += durationMs;
            if (durationMs > bucket.WarmupDurationMaxMs)
                bucket.WarmupDurationMaxMs = durationMs;
            bucket.LastWarmupDurationMs = durationMs;
            bucket.LastScopeName = GetScopeName(scope);
            bucket.LastOperation = wasReused ? "Warmup.Reuse" : "Warmup.New";
            bucket.LastFrame = Time.frameCount;
            bucket.LastRealtime = Time.realtimeSinceStartupAsDouble;

            PushTelemetryEvent(
                bucket,
                bucket.LastOperation,
                bucket.LastScopeName,
                bucket.LastTemplateId,
                bucket.LastCategory,
                durationMs);
            TouchTelemetry();
        }

        void CompleteReleaseTelemetry(
            RuntimeLifetimeScope scope,
            BaseRuntimeTemplateSO? template,
            RuntimeLifetimeScopePoolKey? fallbackKey,
            bool returnedToPool,
            bool destroyed)
        {
            PoolTelemetryBucket? bucket = null;
            ScopeLeaseTelemetry? lease = null;

            if (_activeScopeTelemetry.TryGetValue(scope, out lease) && lease != null)
            {
                _activeScopeTelemetry.Remove(scope);
                bucket = GetOrCreateTelemetryBucket(lease.Key);

                if (lease.IsPooledAcquire)
                {
                    if (bucket.CurrentPooledActiveCount > 0)
                        bucket.CurrentPooledActiveCount--;
                }
                else
                {
                    if (bucket.CurrentNonPooledActiveCount > 0)
                        bucket.CurrentNonPooledActiveCount--;
                }

                UpdateGlobalActiveCount(-1);

                if (destroyed)
                {
                    if (lease.IsPooledAcquire)
                    {
                        if (bucket.CurrentPooledAliveCount > 0)
                            bucket.CurrentPooledAliveCount--;
                    }
                    else
                    {
                        if (bucket.CurrentNonPooledAliveCount > 0)
                            bucket.CurrentNonPooledAliveCount--;
                    }

                    UpdateGlobalAliveCount(-1);
                }
            }
            else if (fallbackKey != null)
            {
                bucket = GetOrCreateTelemetryBucket(fallbackKey.Value);
            }

            if (bucket == null)
                return;

            if (template != null)
                SetLastTemplateLabels(bucket, template);

            bucket.ReleaseCount++;
            if (returnedToPool)
                bucket.ReturnedToPoolCount++;
            if (destroyed)
                bucket.DestroyedCount++;

            bucket.LastScopeName = GetScopeName(scope);
            bucket.LastOperation = returnedToPool ? "Release.ToPool" : destroyed ? "Release.Destroy" : "Release";
            bucket.LastFrame = Time.frameCount;
            bucket.LastRealtime = Time.realtimeSinceStartupAsDouble;

            PushTelemetryEvent(
                bucket,
                bucket.LastOperation,
                bucket.LastScopeName,
                lease?.TemplateId ?? bucket.LastTemplateId,
                lease?.Category ?? bucket.LastCategory,
                0d);
            TouchTelemetry();
        }

        void CleanupDestroyedActiveScopes()
        {
            if (_activeScopeTelemetry.Count == 0)
                return;

            List<RuntimeLifetimeScope>? destroyedScopes = null;
            foreach (var pair in _activeScopeTelemetry)
            {
                var scope = pair.Key;
                if (scope != null && scope)
                    continue;

                destroyedScopes ??= ListPool<RuntimeLifetimeScope>.Get();
                destroyedScopes.Add(pair.Key);
            }

            if (destroyedScopes == null)
                return;

            for (int i = 0; i < destroyedScopes.Count; i++)
            {
                var scope = destroyedScopes[i];
                if (!_activeScopeTelemetry.TryGetValue(scope, out var lease) || lease == null)
                    continue;

                _activeScopeTelemetry.Remove(scope);

                var bucket = GetOrCreateTelemetryBucket(lease.Key);
                if (lease.IsPooledAcquire)
                {
                    if (bucket.CurrentPooledActiveCount > 0)
                        bucket.CurrentPooledActiveCount--;
                    if (bucket.CurrentPooledAliveCount > 0)
                        bucket.CurrentPooledAliveCount--;
                }
                else
                {
                    if (bucket.CurrentNonPooledActiveCount > 0)
                        bucket.CurrentNonPooledActiveCount--;
                    if (bucket.CurrentNonPooledAliveCount > 0)
                        bucket.CurrentNonPooledAliveCount--;
                }

                bucket.ReleaseCount++;
                bucket.DestroyedCount++;
                bucket.LastScopeName = lease.ScopeName;
                bucket.LastOperation = "Scope.Destroyed";
                bucket.LastFrame = Time.frameCount;
                bucket.LastRealtime = Time.realtimeSinceStartupAsDouble;

                UpdateGlobalActiveCount(-1);
                UpdateGlobalAliveCount(-1);

                PushTelemetryEvent(
                    bucket,
                    bucket.LastOperation,
                    lease.ScopeName,
                    lease.TemplateId,
                    lease.Category,
                    0d);
            }

            ListPool<RuntimeLifetimeScope>.Release(destroyedScopes);
            TouchTelemetry();
        }

        PoolTelemetryBucket GetOrCreateTelemetryBucket(RuntimeLifetimeScopePoolKey key)
        {
            if (!_telemetryBuckets.TryGetValue(key, out var bucket) || bucket == null)
            {
                bucket = new PoolTelemetryBucket();
                _telemetryBuckets[key] = bucket;
            }

            RefreshBucketDescriptor(bucket, key);
            return bucket;
        }

        static void RefreshBucketDescriptor(PoolTelemetryBucket bucket, RuntimeLifetimeScopePoolKey key)
        {
            bucket.PrefabName = key.PrefabKey != null ? key.PrefabKey.name : "null";
            bucket.PrefabInstanceId = key.PrefabKey != null ? key.PrefabKey.GetInstanceID() : 0;
            bucket.ParentName = key.Parent != null ? key.Parent.name : "null";
            bucket.ParentPath = GetTransformPath(key.Parent);
            bucket.ParentInstanceId = key.Parent != null ? key.Parent.GetInstanceID() : 0;
            bucket.KeyLabel = $"{bucket.PrefabName} @ {bucket.ParentPath}";
        }

        static string GetTransformPath(Transform? transform)
        {
            if (transform == null)
                return "null";

            var stack = ListPool<string>.Get();
            try
            {
                var current = transform;
                while (current != null)
                {
                    stack.Add(current.name);
                    current = current.parent;
                }

                stack.Reverse();
                return string.Join("/", stack);
            }
            finally
            {
                ListPool<string>.Release(stack);
            }
        }

        void ObserveTemplate(PoolTelemetryBucket bucket, BaseRuntimeTemplateSO template)
        {
            SetLastTemplateLabels(bucket, template);
            var templateId = bucket.LastTemplateId;
            var category = bucket.LastCategory;

            if (bucket.TemplateCounts.TryGetValue(templateId, out var currentTemplateCount))
                bucket.TemplateCounts[templateId] = currentTemplateCount + 1;
            else
                bucket.TemplateCounts[templateId] = 1;

            if (bucket.CategoryCounts.TryGetValue(category, out var currentCategoryCount))
                bucket.CategoryCounts[category] = currentCategoryCount + 1;
            else
                bucket.CategoryCounts[category] = 1;
        }

        static void SetLastTemplateLabels(PoolTelemetryBucket bucket, BaseRuntimeTemplateSO template)
        {
            bucket.LastTemplateId = string.IsNullOrEmpty(template.TemplateId) ? "(empty)" : template.TemplateId;
            bucket.LastCategory = string.IsNullOrEmpty(template.Category) ? "Runtime" : template.Category;
        }

        void PushTelemetryEvent(
            PoolTelemetryBucket bucket,
            string operation,
            string scopeName,
            string templateId,
            string category,
            double durationMs)
        {
            if (_recentTelemetryEvents.Count >= MaxRecentTelemetryEvents)
                _recentTelemetryEvents.RemoveAt(0);

            _telemetrySequence++;
            _recentTelemetryEvents.Add(new PoolTelemetryEventRecord
            {
                Sequence = _telemetrySequence,
                Operation = operation,
                KeyLabel = bucket.KeyLabel,
                PrefabName = bucket.PrefabName,
                ParentName = bucket.ParentName,
                ParentPath = bucket.ParentPath,
                ScopeName = scopeName,
                TemplateId = templateId,
                Category = category,
                DurationMs = durationMs,
                Frame = Time.frameCount,
                Realtime = Time.realtimeSinceStartupAsDouble,
            });
        }

        static string GetScopeName(RuntimeLifetimeScope? scope)
        {
            if (scope == null || !scope)
                return "Destroyed";

            return scope.gameObject != null ? scope.gameObject.name : scope.name;
        }

        static double GetElapsedMilliseconds(double startRealtime)
            => Math.Max(0d, (Time.realtimeSinceStartupAsDouble - startRealtime) * 1000d);

        int GetAvailableInactiveCount(RuntimeLifetimeScopePoolKey key)
        {
            if (!_telemetryBuckets.TryGetValue(key, out var bucket) || bucket == null)
                return 0;

            return Math.Max(0, bucket.CurrentPooledAliveCount - bucket.CurrentPooledActiveCount);
        }

        void RefreshBucketPeaks(PoolTelemetryBucket bucket)
        {
            var currentActiveCount = GetBucketCurrentActiveCount(bucket);
            var currentAliveCount = GetBucketCurrentAliveCount(bucket);

            if (currentActiveCount > bucket.PeakActiveCount)
                bucket.PeakActiveCount = currentActiveCount;
            if (currentAliveCount > bucket.PeakAliveCount)
                bucket.PeakAliveCount = currentAliveCount;
        }

        static int GetBucketCurrentActiveCount(PoolTelemetryBucket bucket)
            => bucket.CurrentPooledActiveCount + bucket.CurrentNonPooledActiveCount;

        static int GetBucketCurrentAliveCount(PoolTelemetryBucket bucket)
            => bucket.CurrentPooledAliveCount + bucket.CurrentNonPooledAliveCount;

        void UpdateGlobalActiveCount(int delta)
        {
            _currentGlobalActiveCount = Math.Max(0, _currentGlobalActiveCount + delta);
            if (_currentGlobalActiveCount > _peakGlobalActiveCount)
                _peakGlobalActiveCount = _currentGlobalActiveCount;
        }

        void UpdateGlobalAliveCount(int delta)
        {
            _currentGlobalAliveCount = Math.Max(0, _currentGlobalAliveCount + delta);
            if (_currentGlobalAliveCount > _peakGlobalAliveCount)
                _peakGlobalAliveCount = _currentGlobalAliveCount;
        }

        static int CompareKeySnapshots(
            RuntimeLifetimeScopePoolKeyTelemetrySnapshot x,
            RuntimeLifetimeScopePoolKeyTelemetrySnapshot y)
        {
            var leftScore = x.TotalCreateCount + x.TotalReuseCount + x.ReleaseCount;
            var rightScore = y.TotalCreateCount + y.TotalReuseCount + y.ReleaseCount;
            var cmp = rightScore.CompareTo(leftScore);
            if (cmp != 0)
                return cmp;

            return string.Compare(x.KeyLabel, y.KeyLabel, StringComparison.Ordinal);
        }

        static string FormatCountMap(Dictionary<string, int> source, int maxItems)
        {
            if (source.Count == 0)
                return string.Empty;

            var items = new List<KeyValuePair<string, int>>(source.Count);
            foreach (var pair in source)
            {
                items.Add(pair);
            }

            items.Sort((a, b) =>
            {
                var cmp = b.Value.CompareTo(a.Value);
                if (cmp != 0)
                    return cmp;

                return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            });

            var takeCount = Math.Min(Math.Max(1, maxItems), items.Count);
            var parts = new string[takeCount];
            for (int i = 0; i < takeCount; i++)
            {
                var pair = items[i];
                parts[i] = $"{pair.Key}({pair.Value})";
            }

            var result = string.Join(", ", parts);
            if (items.Count > takeCount)
                result = $"{result}, +{items.Count - takeCount}";

            return result;
        }

        void TouchTelemetry()
        {
            if (_telemetryVersion == int.MaxValue)
            {
                _telemetryVersion = 1;
                return;
            }

            _telemetryVersion++;
        }

        bool TryBeginRelease(RuntimeLifetimeScope scope)
        {
            if (scope == null)
                return false;

            return _releasingScopes.Add(scope);
        }

        void EndRelease(RuntimeLifetimeScope scope)
        {
            if (scope == null)
                return;

            _releasingScopes.Remove(scope);
        }

        async UniTask ExecutePendingOnAcquireAsync(RuntimeLifetimeScope scope, CancellationToken ct)
        {
            if (scope == null || !scope)
                return;
            if (!_pendingOnAcquireCommands.TryGetValue(scope, out var pending) || pending == null || pending.Count == 0)
                return;

            _pendingOnAcquireCommands.Remove(scope);

            var resolver = scope.Resolver;
            if (resolver == null || !resolver.TryResolve<ICommandRunner>(out var runner) || runner == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[RuntimeLifetimeScopePool] OnReacquire commands skipped: ICommandRunner missing.");
#endif
                return;
            }

            IVarStore vars = NullVarStore.Instance;
            if (resolver.TryResolve<IVarStore>(out var resolvedVars) && resolvedVars != null)
                vars = resolvedVars;

            for (int i = 0; i < pending.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = pending[i];
                if (entry.Commands == null || entry.Commands.Count == 0)
                    continue;

                var options = entry.Options.WithSuppressCancelLog(true);
                var context = new CommandContext(scope, vars, runner, scope, options);
                var result = await runner.ExecuteListAsync(entry.Commands, context, ct, options);
                if (result.Status == CommandRunStatus.Canceled)
                    continue;

                if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[RuntimeLifetimeScopePool] OnReacquire commands failed. FailureCount={result.FailureCount} ErrorIndex={result.ErrorIndex} Message={result.Message}");
#endif
                }
            }
        }

        void ClearPendingOnAcquire(RuntimeLifetimeScope scope)
        {
            if (scope == null)
                return;

            _pendingOnAcquireCommands.Remove(scope);
        }

        static bool ShouldDestroyInsteadOfReturnToPool(RuntimeLifetimeScope scope)
        {
            if (scope == null || !scope)
                return true;

            if (s_isApplicationQuitting)
                return true;

#if UNITY_EDITOR
            if (s_isEditorExitingPlayMode)
                return true;
#endif

            return false;
        }

        ObjectPool<RuntimeLifetimeScope> GetOrCreatePool(RuntimeLifetimeScopePoolKey poolKey)
        {
            if (_pools.TryGetValue(poolKey, out var pool))
                return pool;

            RuntimeLifetimeScope Create()
            {
                var prefab = poolKey.PrefabKey;
                var go = UnityEngine.Object.Instantiate(prefab);
                go.name = prefab.name;

                var scope = go.GetComponent<RuntimeLifetimeScope>();
                if (scope == null)
                    throw new InvalidOperationException($"Prefab {prefab.name} does not have {nameof(RuntimeLifetimeScope)}.");

                if (_buildParent != null)
                {
                    scope.SetExplicitBuildParent(_buildParent);
                }

                // newly created instances are poolable by default
                scope.AllowPooling = true;

                go.SetActive(false);
                if (_poolRoot != null)
                {
                    scope.transform.SetParent(_poolRoot, worldPositionStays: false);
                }

                return scope;
            }

            void OnGet(RuntimeLifetimeScope scope)
            {
                if (scope == null)
                    return;

                // Ensure instances obtained from the pool are considered poolable.
                scope.AllowPooling = true;
            }

            void OnRelease(RuntimeLifetimeScope scope)
            {
                if (scope == null)
                    return;

                var go = scope.gameObject;
                if (!go)
                    return;

                // If instance is marked non-poolable, destroy it instead of keeping it in the pool.
                if (!scope.AllowPooling)
                {
                    UnityEngine.Object.Destroy(go);
                    return;
                }

                if (_poolRoot != null)
                {
                    // Deactivate first to avoid Unity errors when reparenting during parent activation/deactivation.
                    go.SetActive(false);

                    var currentParent = scope.transform.parent;
                    if (currentParent != null && !currentParent.gameObject.activeInHierarchy)
                    {
                        // The current hierarchy is already shutting down, so keeping the object in place avoids
                        // Unity's reparent guard. The pool entry will be discarded naturally if the hierarchy is destroyed.
                        scope.SetExplicitBuildParent(_buildParent);
                        return;
                    }

                    scope.transform.SetParent(_poolRoot, worldPositionStays: false);
                }
                else
                {
                    go.SetActive(false);
                }

                // Reset build parent to default to avoid keeping stale runtime parent links in the pool.
                scope.SetExplicitBuildParent(_buildParent);
            }

            pool = new ObjectPool<RuntimeLifetimeScope>(
                createFunc: Create,
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: s =>
                {
                    if (s != null)
                        UnityEngine.Object.Destroy(s.gameObject);
                },
                collectionCheck: true,
                defaultCapacity: 16,
                maxSize: 1024);

            _pools.Add(poolKey, pool);
            return pool;
        }

        RuntimeLifetimeScope CreateInstance(BaseRuntimeTemplateSO template)
        {
            var prefab = template.Prefab;
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = prefab.name;

            var scope = go.GetComponent<RuntimeLifetimeScope>();
            if (scope == null)
                throw new InvalidOperationException($"Prefab {prefab.name} does not have {nameof(RuntimeLifetimeScope)}.");

            if (_buildParent != null)
            {
                scope.SetExplicitBuildParent(_buildParent);
            }

            // When created specifically as a non-pooled instance, mark accordingly
            scope.AllowPooling = false;

            return scope;
        }

        RuntimeLifetimeScope CreatePooledInstance(GameObject prefab)
        {
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = prefab.name;

            var scope = go.GetComponent<RuntimeLifetimeScope>();
            if (scope == null)
                throw new InvalidOperationException($"Prefab {prefab.name} does not have {nameof(RuntimeLifetimeScope)}.");

            if (_buildParent != null)
            {
                scope.SetExplicitBuildParent(_buildParent);
            }

            scope.AllowPooling = true;
            go.SetActive(false);
            if (_poolRoot != null)
            {
                scope.transform.SetParent(_poolRoot, worldPositionStays: false);
            }

            return scope;
        }

        void ConfigureOnAcquire(RuntimeLifetimeScope scope, Transform parent, Vector3 position, Quaternion rotation, IScopeNode? lifetimeScopeParent)
        {
            var t = scope.transform;
            t.SetParent(parent, worldPositionStays: false);
            SpawnPoseUtility.ApplySpawnPose(t, new SpawnParams
            {
                Position = position,
                Rotation = rotation,
                Scale = Vector3.one,
                WorldSpace = true,
                AllowPooling = true
            });

            // Explicit DI parent override (independent from transform hierarchy).
            if (lifetimeScopeParent != null)
            {
                scope.SetExplicitBuildParent(lifetimeScopeParent);
                scope.gameObject.SetActive(true);
                return;
            }

            // Build/DI parent should follow the nearest scope in the transform hierarchy.
            // This enables RuntimeLifetimeScope -> RuntimeLifetimeScope parenting (e.g., spawned units under an emitter runtime).
            RuntimeLifetimeScope? runtimeParent = null;
            BaseLifetimeScope? baseParent = null;
            try
            {
                runtimeParent = parent.GetComponentInParent<RuntimeLifetimeScope>(includeInactive: true);
                if (runtimeParent == scope)
                    runtimeParent = null;

                if (runtimeParent == null)
                    baseParent = parent.GetComponentInParent<BaseLifetimeScope>(includeInactive: true);
            }
            catch
            {
                // ignore
            }

            if (runtimeParent != null)
            {
                scope.SetExplicitBuildParent(runtimeParent);
            }
            else if (baseParent != null)
            {
                scope.SetExplicitBuildParent((IScopeNode?)baseParent);
            }
            else
            {
                scope.SetExplicitBuildParent(_buildParent);
            }

            scope.gameObject.SetActive(true);
        }

    }

    // ================================================================
    // Pool Key (Parent Transform + Prefab)
    // ================================================================

    internal readonly struct RuntimeLifetimeScopePoolKey
    {
        public readonly GameObject PrefabKey;
        public readonly Transform Parent;

        public RuntimeLifetimeScopePoolKey(GameObject prefabKey, Transform parent)
        {
            PrefabKey = prefabKey;
            Parent = parent;
        }
    }

    sealed class RuntimeLifetimeScopePoolKeyComparer : IEqualityComparer<RuntimeLifetimeScopePoolKey>
    {
        public bool Equals(RuntimeLifetimeScopePoolKey x, RuntimeLifetimeScopePoolKey y)
            => ReferenceEquals(x.PrefabKey, y.PrefabKey) && ReferenceEquals(x.Parent, y.Parent);

        public int GetHashCode(RuntimeLifetimeScopePoolKey obj)
        {
            unchecked
            {
                int h1 = obj.PrefabKey != null ? RuntimeHelpers.GetHashCode(obj.PrefabKey) : 0;
                int h2 = obj.Parent != null ? RuntimeHelpers.GetHashCode(obj.Parent) : 0;
                return (h1 * 397) ^ h2;
            }
        }
    }

    public sealed class RuntimeLifetimeScopePoolTelemetrySnapshot
    {
        public int Version;
        public int SessionStartFrame;
        public double SessionElapsedSeconds;

        public int TotalKeyCount;
        public int TotalAcquireCount;
        public int TotalPooledAcquireCount;
        public int TotalNonPooledAcquireCount;
        public int TotalNewCount;
        public int TotalReuseCount;
        public int TotalWarmupCallCount;
        public int TotalWarmupRequestedCount;
        public int TotalWarmupCreatedCount;
        public int TotalWarmupReusedCount;
        public int TotalReleaseCount;
        public int TotalReturnedToPoolCount;
        public int TotalDestroyedCount;

        public int CurrentActiveCount;
        public int PeakActiveCount;
        public int CurrentAliveCount;
        public int PeakAliveCount;
        public int CurrentPooledAvailableCount;

        public double MaxAcquireDurationMs;
        public double MaxNewDurationMs;
        public double MaxReuseDurationMs;
        public double MaxWarmupDurationMs;

        public readonly List<RuntimeLifetimeScopePoolKeyTelemetrySnapshot> KeySnapshots = new();
        public readonly List<RuntimeLifetimeScopePoolEventTelemetrySnapshot> RecentEventSnapshots = new();
    }

    public sealed class RuntimeLifetimeScopePoolKeyTelemetrySnapshot
    {
        public string KeyLabel = string.Empty;
        public string PrefabName = string.Empty;
        public int PrefabInstanceId;
        public string ParentName = string.Empty;
        public string ParentPath = string.Empty;
        public int ParentInstanceId;

        public int AcquireCount;
        public int PooledAcquireCount;
        public int NonPooledAcquireCount;
        public int NewCount;
        public int ReuseCount;
        public int WarmupCallCount;
        public int WarmupRequestedCount;
        public int WarmupCreatedCount;
        public int WarmupReusedCount;
        public int ReleaseCount;
        public int ReturnedToPoolCount;
        public int DestroyedCount;

        public int CurrentActiveCount;
        public int PeakActiveCount;
        public int CurrentAliveCount;
        public int PeakAliveCount;
        public int CurrentPooledAliveCount;
        public int CurrentPooledActiveCount;
        public int CurrentPooledAvailableCount;
        public int CurrentNonPooledAliveCount;
        public int CurrentNonPooledActiveCount;

        public int TotalCreateCount;
        public int TotalReuseCount;

        public double AcquireDurationTotalMs;
        public double AcquireDurationMaxMs;
        public double NewDurationTotalMs;
        public double NewDurationMaxMs;
        public double ReuseDurationTotalMs;
        public double ReuseDurationMaxMs;
        public double WarmupDurationTotalMs;
        public double WarmupDurationMaxMs;
        public double LastAcquireDurationMs;
        public double LastNewDurationMs;
        public double LastReuseDurationMs;
        public double LastWarmupDurationMs;

        public string LastTemplateId = string.Empty;
        public string LastCategory = string.Empty;
        public string LastScopeName = string.Empty;
        public string LastOperation = string.Empty;
        public int LastFrame = -1;
        public double LastRealtime;

        public string TemplateSummary = string.Empty;
        public string CategorySummary = string.Empty;
    }

    public sealed class RuntimeLifetimeScopePoolEventTelemetrySnapshot
    {
        public long Sequence;
        public string Operation = string.Empty;
        public string KeyLabel = string.Empty;
        public string PrefabName = string.Empty;
        public string ParentName = string.Empty;
        public string ParentPath = string.Empty;
        public string ScopeName = string.Empty;
        public string TemplateId = string.Empty;
        public string Category = string.Empty;
        public double DurationMs;
        public int Frame = -1;
        public double Realtime;
    }
}
