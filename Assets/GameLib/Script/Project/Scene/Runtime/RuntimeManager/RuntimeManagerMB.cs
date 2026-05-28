#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Common;
using Game.DI;
using Game.Kernel.Layers;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Project.Scene.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeTickHub))]
    public sealed class RuntimeManagerMB : MonoBehaviour
    {
        bool _runtimeInstalled;

        [Serializable]
        sealed class WarmupEntry
        {
            [SerializeField] DynamicValue<BaseRuntimeTemplatePreset> template;
            [Min(0)]
            [SerializeField] int count = 0;

            public int Count => Mathf.Max(0, count);

            public bool TryResolveTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
            {
                runtimeTemplate = null;
                if (!template.TryGet(context, out var preset) || preset == null)
                    return false;

                runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
                return runtimeTemplate != null;
            }
        }

        [Header("Spawner")]
        [SerializeField] string spawnerTag = "";

        [Tooltip("Runtime spawn parent. Null縺ｮ蝣ｴ蜷医・縺薙・GameObject逶ｴ荳九↓逕滓・")]
        [SerializeField] Transform? root;

        [Header("Warmup")]
        [SerializeField] List<WarmupEntry> warmupEntries = new();

        [FoldoutGroup("Debug Viewer")]
        [SerializeField, InlineProperty, HideLabel]
        RuntimeManagerPoolDebugViewer debugViewer = new();

        public void InstallRuntime(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (_runtimeInstalled)
                return;

            _runtimeInstalled = true;

            // Only register this instance if the underlying Unity object is alive. When runtime install
            // runs during scope build, it's possible components were destroyed or are in an invalid state.
            if (this != null)
            {
                builder.RegisterInstance(this);
            }

            var tickHub = GetComponent<RuntimeTickHub>();
            if (tickHub != null)
            {
                builder.RegisterInstance(tickHub).As<IRuntimeTickHub>();
            }
            else
            {
            }

            var resolvedRoot = root != null ? root : transform;
            var buildParent = owner;

            builder.Register<RuntimeLifetimeScopePool>(RuntimeLifetime.Singleton)
                .WithParameter(buildParent)
                .WithParameter(resolvedRoot)
                .As<IRuntimeLifetimeScopePool>()
                .As<IRuntimeLifetimeScopePoolTelemetry>();

            builder.Register<RuntimeLifetimeScopeSpawnerService>(RuntimeLifetime.Singleton)
                .WithParameter(resolvedRoot)
                .WithParameter(spawnerTag)
                .AsSelf()
                .As<IFilteredReleaseSpawnerService>()
                .As<IRuntimeLifetimeScopeSpawnerService>()
                .As<IAsyncSpawnerService>();

            builder.RegisterBuildCallback(resolver =>
            {
                if (resolver.TryResolve<IRuntimeLifetimeScopePoolTelemetry>(out var telemetry) && telemetry != null)
                {
                    debugViewer.Bind(telemetry);
                }
            });

            // Ensure the spawner service is instantiated so it can register itself into SceneSpawnerRegistry.
            builder.RegisterBuildCallback(resolver =>
            {
                try
                {
                    resolver.Resolve<RuntimeLifetimeScopeSpawnerService>();
                }
                catch (Exception)
                {
                }
            });

            builder.RegisterBuildCallback(resolver =>
            {
                if (warmupEntries == null || warmupEntries.Count == 0)
                    return;

                if (!resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) || pool == null)
                    return;

                UniTask.Void(async () =>
                {
                    try
                    {
                        var vars = resolver.TryResolve<IVarStore>(out var resolvedVars) && resolvedVars != null
                            ? resolvedVars
                            : NullVarStore.Instance;
                        var dynCtx = new SimpleDynamicContext(vars, owner);

                        for (int i = 0; i < warmupEntries.Count; i++)
                        {
                            var entry = warmupEntries[i];
                            if (entry == null)
                                continue;

                            var c = entry.Count;
                            if (c <= 0)
                                continue;

                            if (!entry.TryResolveTemplate(dynCtx, out var t) || t == null)
                                continue;

                            await pool.WarmupAsync(t, c);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RuntimeManagerMB] Warmup failed: {ex}");
                    }
                });
            });
        }
    }

    [Serializable]
    sealed class RuntimeManagerPoolDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _telemetry != null;

        [ShowInInspector, ReadOnly, LabelText("Telemetry Version")]
        public int Version
        {
            get
            {
                AutoRefresh();
                return _snapshot.Version;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Session Seconds")]
        public double SessionSeconds
        {
            get
            {
                AutoRefresh();
                return _snapshot.SessionElapsedSeconds;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Key Count")]
        public int KeyCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalKeyCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Current Active")]
        public int CurrentActiveCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.CurrentActiveCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Current Alive")]
        public int CurrentAliveCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.CurrentAliveCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Current Pooled Available")]
        public int CurrentPooledAvailableCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.CurrentPooledAvailableCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Peak Active")]
        public int PeakActiveCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.PeakActiveCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Peak Alive")]
        public int PeakAliveCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.PeakAliveCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Acquire Count")]
        public int TotalAcquireCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalAcquireCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("New Count")]
        public int TotalNewCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalNewCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Reuse Count")]
        public int TotalReuseCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalReuseCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Acquire Reuse %")]
        public float AcquireReuseRatePercent
        {
            get
            {
                AutoRefresh();
                return Percent(_snapshot.TotalReuseCount, _snapshot.TotalPooledAcquireCount);
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Warmup Requested")]
        public int TotalWarmupRequestedCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalWarmupRequestedCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Warmup New")]
        public int TotalWarmupCreatedCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalWarmupCreatedCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Warmup Reuse")]
        public int TotalWarmupReusedCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalWarmupReusedCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Warmup Reuse %")]
        public float WarmupReuseRatePercent
        {
            get
            {
                AutoRefresh();
                return Percent(_snapshot.TotalWarmupReusedCount, _snapshot.TotalWarmupRequestedCount);
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Release Count")]
        public int TotalReleaseCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalReleaseCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Returned To Pool")]
        public int TotalReturnedToPoolCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalReturnedToPoolCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Destroyed")]
        public int TotalDestroyedCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.TotalDestroyedCount;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Max Acquire ms")]
        public double MaxAcquireDurationMs
        {
            get
            {
                AutoRefresh();
                return _snapshot.MaxAcquireDurationMs;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Max Warmup ms")]
        public double MaxWarmupDurationMs
        {
            get
            {
                AutoRefresh();
                return _snapshot.MaxWarmupDurationMs;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Summary")]
        [MultiLineProperty(4)]
        public string Summary
        {
            get
            {
                AutoRefresh();
                return BuildSummary();
            }
        }

        [ShowInInspector, LabelText("Key Stats")]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        public List<KeyRow> KeyRows
        {
            get
            {
                AutoRefresh();
                return _keyRows;
            }
        }

        [ShowInInspector, LabelText("Recent Events")]
        [TableList(IsReadOnly = true, AlwaysExpanded = true)]
        public List<EventRow> RecentEvents
        {
            get
            {
                AutoRefresh();
                return _eventRows;
            }
        }

        [SerializeField, LabelText("Auto Refresh Every N Frames"), MinValue(1)]
        int autoRefreshEveryNFrames = 5;

        [SerializeField, LabelText("Recent Event Limit"), MinValue(1)]
        int recentEventLimit = 24;

        IRuntimeLifetimeScopePoolTelemetry? _telemetry;
        RuntimeLifetimeScopePoolTelemetrySnapshot _snapshot = new();
        int _lastVersion = -1;
        int _lastRefreshFrame = -1;
        readonly List<KeyRow> _keyRows = new();
        readonly List<EventRow> _eventRows = new();

        public void Bind(IRuntimeLifetimeScopePoolTelemetry telemetry)
        {
            _telemetry = telemetry;
            _lastVersion = -1;
            _lastRefreshFrame = -1;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            if (_telemetry == null)
                return;

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
        }

        void AutoRefresh()
        {
            if (_telemetry == null)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, autoRefreshEveryNFrames);
            if (_lastRefreshFrame >= 0 && frame - _lastRefreshFrame < interval)
                return;

            var currentVersion = _telemetry.TelemetryVersion;
            if (currentVersion == _lastVersion)
            {
                _lastRefreshFrame = frame;
                return;
            }

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
        }

        void ApplySnapshot(RuntimeLifetimeScopePoolTelemetrySnapshot snapshot)
        {
            _snapshot = snapshot ?? new RuntimeLifetimeScopePoolTelemetrySnapshot();
            _lastVersion = _snapshot.Version;
            _lastRefreshFrame = Time.frameCount;

            RebuildKeyRows();
            RebuildEventRows();
        }

        void RebuildKeyRows()
        {
            _keyRows.Clear();

            for (int i = 0; i < _snapshot.KeySnapshots.Count; i++)
            {
                var item = _snapshot.KeySnapshots[i];
                _keyRows.Add(new KeyRow
                {
                    Key = item.KeyLabel,
                    Prefab = item.PrefabName,
                    Parent = item.ParentName,
                    ParentPath = item.ParentPath,
                    PrefabId = item.PrefabInstanceId,
                    ParentId = item.ParentInstanceId,
                    Active = item.CurrentActiveCount,
                    Alive = item.CurrentAliveCount,
                    Available = item.CurrentPooledAvailableCount,
                    PeakActive = item.PeakActiveCount,
                    PeakAlive = item.PeakAliveCount,
                    Acquire = item.AcquireCount,
                    PooledAcquire = item.PooledAcquireCount,
                    NonPooledAcquire = item.NonPooledAcquireCount,
                    New = item.NewCount,
                    Reuse = item.ReuseCount,
                    AcquireReusePercent = Percent(item.ReuseCount, item.PooledAcquireCount),
                    WarmupCalls = item.WarmupCallCount,
                    WarmupRequested = item.WarmupRequestedCount,
                    WarmupNew = item.WarmupCreatedCount,
                    WarmupReuse = item.WarmupReusedCount,
                    WarmupReusePercent = Percent(item.WarmupReusedCount, item.WarmupRequestedCount),
                    Release = item.ReleaseCount,
                    Returned = item.ReturnedToPoolCount,
                    Destroyed = item.DestroyedCount,
                    TotalCreated = item.TotalCreateCount,
                    TotalReused = item.TotalReuseCount,
                    AvgAcquireMs = Average(item.AcquireDurationTotalMs, item.AcquireCount),
                    MaxAcquireMs = item.AcquireDurationMaxMs,
                    AvgNewMs = Average(item.NewDurationTotalMs, item.NewCount),
                    MaxNewMs = item.NewDurationMaxMs,
                    AvgReuseMs = Average(item.ReuseDurationTotalMs, item.ReuseCount),
                    MaxReuseMs = item.ReuseDurationMaxMs,
                    AvgWarmupMs = Average(item.WarmupDurationTotalMs, item.WarmupRequestedCount),
                    MaxWarmupMs = item.WarmupDurationMaxMs,
                    LastAcquireMs = item.LastAcquireDurationMs,
                    LastWarmupMs = item.LastWarmupDurationMs,
                    LastOp = item.LastOperation,
                    LastTemplate = item.LastTemplateId,
                    LastCategory = item.LastCategory,
                    LastScope = item.LastScopeName,
                    LastFrame = item.LastFrame,
                    Templates = item.TemplateSummary,
                    Categories = item.CategorySummary,
                });
            }
        }

        void RebuildEventRows()
        {
            _eventRows.Clear();

            var limit = Mathf.Max(1, recentEventLimit);
            var count = Mathf.Min(limit, _snapshot.RecentEventSnapshots.Count);
            for (int i = 0; i < count; i++)
            {
                var item = _snapshot.RecentEventSnapshots[i];
                _eventRows.Add(new EventRow
                {
                    Seq = item.Sequence,
                    Frame = item.Frame,
                    Realtime = item.Realtime,
                    Operation = item.Operation,
                    Key = item.KeyLabel,
                    Prefab = item.PrefabName,
                    Parent = item.ParentName,
                    ParentPath = item.ParentPath,
                    Scope = item.ScopeName,
                    Template = item.TemplateId,
                    Category = item.Category,
                    DurationMs = item.DurationMs,
                });
            }
        }

        string BuildSummary()
        {
            return
                $"Acquire={_snapshot.TotalAcquireCount} (new={_snapshot.TotalNewCount}, reuse={_snapshot.TotalReuseCount}, pooled={_snapshot.TotalPooledAcquireCount}, noPool={_snapshot.TotalNonPooledAcquireCount})\n" +
                $"Warmup={_snapshot.TotalWarmupRequestedCount} (new={_snapshot.TotalWarmupCreatedCount}, reuse={_snapshot.TotalWarmupReusedCount}, calls={_snapshot.TotalWarmupCallCount})\n" +
                $"Current={_snapshot.CurrentActiveCount} active / {_snapshot.CurrentAliveCount} alive / {_snapshot.CurrentPooledAvailableCount} pooled-available\n" +
                $"Release={_snapshot.TotalReleaseCount} (returned={_snapshot.TotalReturnedToPoolCount}, destroyed={_snapshot.TotalDestroyedCount})";
        }

        static double Average(double total, int count)
        {
            if (count <= 0)
                return 0d;

            return total / count;
        }

        static float Percent(int part, int whole)
        {
            if (whole <= 0)
                return 0f;

            return (float)part / whole * 100f;
        }

        [Serializable]
        public sealed class KeyRow
        {
            [TableColumnWidth(220)] public string Key = string.Empty;
            [TableColumnWidth(120)] public string Prefab = string.Empty;
            [TableColumnWidth(120)] public string Parent = string.Empty;
            [TableColumnWidth(240)] public string ParentPath = string.Empty;
            [TableColumnWidth(70)] public int PrefabId;
            [TableColumnWidth(70)] public int ParentId;
            [TableColumnWidth(56)] public int Active;
            [TableColumnWidth(56)] public int Alive;
            [TableColumnWidth(66)] public int Available;
            [TableColumnWidth(66)] public int PeakActive;
            [TableColumnWidth(66)] public int PeakAlive;
            [TableColumnWidth(66)] public int Acquire;
            [TableColumnWidth(66)] public int PooledAcquire;
            [TableColumnWidth(66)] public int NonPooledAcquire;
            [TableColumnWidth(56)] public int New;
            [TableColumnWidth(56)] public int Reuse;
            [TableColumnWidth(70)] public float AcquireReusePercent;
            [TableColumnWidth(66)] public int WarmupCalls;
            [TableColumnWidth(72)] public int WarmupRequested;
            [TableColumnWidth(66)] public int WarmupNew;
            [TableColumnWidth(66)] public int WarmupReuse;
            [TableColumnWidth(70)] public float WarmupReusePercent;
            [TableColumnWidth(56)] public int Release;
            [TableColumnWidth(56)] public int Returned;
            [TableColumnWidth(56)] public int Destroyed;
            [TableColumnWidth(66)] public int TotalCreated;
            [TableColumnWidth(66)] public int TotalReused;
            [TableColumnWidth(72)] public double AvgAcquireMs;
            [TableColumnWidth(72)] public double MaxAcquireMs;
            [TableColumnWidth(72)] public double AvgNewMs;
            [TableColumnWidth(72)] public double MaxNewMs;
            [TableColumnWidth(72)] public double AvgReuseMs;
            [TableColumnWidth(72)] public double MaxReuseMs;
            [TableColumnWidth(72)] public double AvgWarmupMs;
            [TableColumnWidth(72)] public double MaxWarmupMs;
            [TableColumnWidth(72)] public double LastAcquireMs;
            [TableColumnWidth(72)] public double LastWarmupMs;
            [TableColumnWidth(120)] public string LastOp = string.Empty;
            [TableColumnWidth(120)] public string LastTemplate = string.Empty;
            [TableColumnWidth(100)] public string LastCategory = string.Empty;
            [TableColumnWidth(120)] public string LastScope = string.Empty;
            [TableColumnWidth(72)] public int LastFrame;
            [TableColumnWidth(220)] public string Templates = string.Empty;
            [TableColumnWidth(160)] public string Categories = string.Empty;
        }

        [Serializable]
        public sealed class EventRow
        {
            [TableColumnWidth(70)] public long Seq;
            [TableColumnWidth(70)] public int Frame;
            [TableColumnWidth(84)] public double Realtime;
            [TableColumnWidth(120)] public string Operation = string.Empty;
            [TableColumnWidth(220)] public string Key = string.Empty;
            [TableColumnWidth(120)] public string Prefab = string.Empty;
            [TableColumnWidth(120)] public string Parent = string.Empty;
            [TableColumnWidth(240)] public string ParentPath = string.Empty;
            [TableColumnWidth(120)] public string Scope = string.Empty;
            [TableColumnWidth(120)] public string Template = string.Empty;
            [TableColumnWidth(100)] public string Category = string.Empty;
            [TableColumnWidth(72)] public double DurationMs;
        }
    }

    public interface IFilteredReleaseSpawnerService
    {
        int ReleaseAll(RuntimeLifetimeScopeDeleteFilter filter);
    }

    public interface IRuntimeLifetimeScopeSpawnerService : IAsyncSpawnerService, IFilteredReleaseSpawnerService
    {
        int AllDelete(RuntimeLifetimeScopeDeleteFilter filter);
    }

    public sealed class RuntimeLifetimeScopeSpawnerService : IAsyncSpawnerService, IRuntimeLifetimeScopeSpawnerService, IFilteredReleaseSpawnerService, ISceneKernelSpawnPool, ISceneKernelSpawnRouteHandler
    {
        readonly IRuntimeLifetimeScopePool _pool;
        readonly Transform _root;
        readonly ISceneSpawnerRegistry _registry;
        bool _sceneKernelRegistered;

        public SpawnerKind Kind => SpawnerKind.RuntimeEntity;
        public string Tag { get; }
        public SceneKernelSpawnRouteId RouteId => SceneKernelSpawnRouteId.FromParts(Kind.ToString(), Tag);
        public SceneKernelSpawnPoolId PoolId => SceneKernelSpawnPoolId.FromParts(Kind.ToString(), Tag);

        public RuntimeLifetimeScopeSpawnerService(
            IRuntimeLifetimeScopePool pool,
            Transform root,
            string tag,
            ISceneSpawnerRegistry registry)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            Tag = tag ?? "";
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            _registry.Register(this);
            EnsureSceneKernelBinding();
        }

        public async UniTask<IRuntimeResolver?> SpawnAsync(SpawnParams p, CancellationToken ct = default)
        {
            EnsureSceneKernelBinding();
            ct.ThrowIfCancellationRequested();

            if (p.Template == null)
                throw new ArgumentException("SpawnParams.Template is required for Runtime spawns.", nameof(p));

            if (p.Template is not BaseRuntimeTemplateSO template)
                throw new ArgumentException($"Template must be {nameof(BaseRuntimeTemplateSO)}.", nameof(p));

            Transform transformParent = p.TransformParent != null ? p.TransformParent : _root;
            var lifetimeScopeParent = p.LifetimeScopeParent;

            // Pooling is controlled solely by SpawnParams.AllowPooling and Template.UsePooling.
            // Pool identity is prefab-family based, so explicit attachment does not force a pool split.
            bool bypassPooling = !p.AllowPooling;

            RuntimeLifetimeScope scope;
            if (bypassPooling)
            {
                var directSpawnStartRealtime = Time.realtimeSinceStartupAsDouble;

                // Instantiate directly (no pooling) and configure similarly to RuntimeLifetimeScopePool.Create/ConfigureOnAcquire.
                var prefab = template.Prefab;
                var go = UnityEngine.Object.Instantiate(prefab);
                go.name = prefab.name;

                scope = go.GetComponent<RuntimeLifetimeScope>();
                if (scope == null)
                    throw new InvalidOperationException($"Prefab {prefab.name} does not have {nameof(RuntimeLifetimeScope)}.");

                // Parent in hierarchy and set pose
                var t = scope.transform;
                t.SetParent(transformParent, worldPositionStays: false);
                SpawnPoseUtility.ApplySpawnPose(t, p);

                // Build/DI parent selection: allow override, otherwise prefer nearest runtime scope, then base lifetime scope, then default.
                if (lifetimeScopeParent != null)
                {
                    scope.SetExplicitBuildParent(lifetimeScopeParent);
                }
                else
                {
                    var runtimeParent = transformParent.GetComponentInParent<RuntimeLifetimeScopeBase>(includeInactive: true);
                    if (runtimeParent == scope)
                        runtimeParent = null;

                    if (runtimeParent != null)
                    {
                        scope.SetExplicitBuildParent(runtimeParent);
                    }
                    else
                    {
                        scope.SetExplicitBuildParent((IScopeNode?)null);
                    }
                }

                // Mark instance as non-poolable so Release/Pool.Release will destroy it.
                scope.AllowPooling = false;

                scope.ConfigureForAcquire(template, p.Identity, ensureBuilt: true);
                await scope.SetActiveAsync(active: true, isReset: true, ct);

                if (scope.TryResolveLocal<IScopeLifecycleService>(out var lifecycle) &&
                    lifecycle != null)
                {
                    await lifecycle.HandleSpawnAsync(ct);
                }

                if (_pool is RuntimeLifetimeScopePool trackedPool)
                {
                    trackedPool.RecordExternalAcquire(
                        template,
                        transformParent,
                        scope,
                        (Time.realtimeSinceStartupAsDouble - directSpawnStartRealtime) * 1000d);
                }
            }
            else
            {
                if (!SpawnPoseUtility.TryResolveAcquireWorldPose(transformParent, p, out var acquireWorldPos, out var acquireWorldRot))
                {
                    acquireWorldPos = p.Position;
                    acquireWorldRot = p.Rotation;
                }

                var acquired = await _pool.AcquireAsync(
                    template,
                    transformParent,
                    acquireWorldPos,
                    acquireWorldRot,
                    identity: p.Identity,
                    lifetimeScopeParent: lifetimeScopeParent,
                    ct: ct);
                scope = acquired;
            }

            // Apply the requested pose (should be consistent with the acquire world pose above).
            SpawnPoseUtility.ApplySpawnPose(scope.transform, p);

            // Diagnostics: warn if the spawned transform doesn't match the requested position.
            // (No per-frame logging; only emits when there's a meaningful mismatch.)
            if (p.WorldSpace)
            {
                var actual = scope.transform.position;
                var expected = p.Position;
                if ((actual - expected).sqrMagnitude > 0.01f * 0.01f)
                {
                }
            }
            else
            {
                var actual = scope.transform.localPosition;
                var expected = p.Position;
                if ((actual - expected).sqrMagnitude > 0.01f * 0.01f)
                {
                }
            }

            return scope.Container;
        }

        public int AllDelete(RuntimeLifetimeScopeDeleteFilter filter)
        {
            EnsureSceneKernelBinding();
            return _pool.ReleaseMatching(scope =>
            {
                if (scope == null || !scope)
                    return false;

                if (!filter.IncludeInactive && !scope.gameObject.activeInHierarchy)
                    return false;

                if (filter.UseInclude && !MatchesIdentity(scope, filter.Include))
                    return false;

                if (filter.UseExclude && MatchesIdentity(scope, filter.Exclude))
                    return false;

                return true;
            });
        }

        public int ReleaseAll(RuntimeLifetimeScopeDeleteFilter filter)
            => AllDelete(filter);

        int ISceneKernelSpawnPool.ReleaseAll(object filter)
        {
            if (filter is RuntimeLifetimeScopeDeleteFilter typedFilter)
                return ReleaseAll(typedFilter);

            throw new ArgumentException($"{nameof(RuntimeLifetimeScopeSpawnerService)} requires {nameof(RuntimeLifetimeScopeDeleteFilter)}.", nameof(filter));
        }

        async ValueTask<object?> ISceneKernelSpawnRouteHandler.SpawnAsync(object spawnRequest, CancellationToken cancellationToken)
        {
            if (spawnRequest is not SpawnParams spawnParams)
                throw new ArgumentException($"{nameof(RuntimeLifetimeScopeSpawnerService)} requires {nameof(SpawnParams)}.", nameof(spawnRequest));

            return await SpawnAsync(spawnParams, cancellationToken);
        }

        async ValueTask ISceneKernelSpawnRouteHandler.WarmupAsync(object template, int count, CancellationToken cancellationToken)
        {
            if (template is not BaseRuntimeTemplateSO runtimeTemplate)
                throw new ArgumentException($"{nameof(RuntimeLifetimeScopeSpawnerService)} requires {nameof(BaseRuntimeTemplateSO)}.", nameof(template));

            await _pool.WarmupAsync(runtimeTemplate, count, cancellationToken);
        }

        void EnsureSceneKernelBinding()
        {
            if (_sceneKernelRegistered)
                return;

            SceneKernelSpawnBindingHub.Register(this, this);
            _sceneKernelRegistered = true;
        }

        static bool MatchesIdentity(RuntimeLifetimeScope scope, CommandTargetIdentityFilter filter)
        {
            RuntimeScopeIdentityService identity = scope.RuntimeIdentity;

            if (filter.requireActive && !identity.IsActive)
                return false;

            if (filter.kind != LifetimeScopeKind.None && filter.kind != identity.Kind)
                return false;

            if (!string.IsNullOrEmpty(filter.id))
            {
                if (string.IsNullOrEmpty(identity.Id))
                    return false;
                if (!string.Equals(filter.id, identity.Id, StringComparison.Ordinal))
                    return false;
            }

            if (!string.IsNullOrEmpty(filter.category))
            {
                if (string.IsNullOrEmpty(identity.Category))
                    return false;
                if (!string.Equals(filter.category, identity.Category, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        public UniTask WarmupAsync<T>(T template, int count, CancellationToken ct = default) where T : BaseRuntimeTemplateSO
            => _pool.WarmupAsync(template, count, ct);
    }
}
