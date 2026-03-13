#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Project.Scene.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RuntimeTickHub))]
    public sealed class RuntimeManagerMB : MonoBehaviour, Game.IFeatureInstaller
    {
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

        [Tooltip("Runtime spawn parent. Nullの場合はこのGameObject直下に生成")]
        [SerializeField] Transform? root;

        [Header("Warmup")]
        [SerializeField] List<WarmupEntry> warmupEntries = new();

        [FoldoutGroup("Debug Viewer")]
        [SerializeField, InlineProperty, HideLabel]
        RuntimeManagerPoolDebugViewer debugViewer = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            // Only register this instance if the underlying Unity object is alive. When InstallFeature
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
            var buildParent = owner as LifetimeScope;

            builder.Register<RuntimeLifetimeScopePool>(Lifetime.Singleton)
                .WithParameter(buildParent)
                .WithParameter(resolvedRoot)
                .As<IRuntimeLifetimeScopePool>()
                .As<IRuntimeLifetimeScopePoolTelemetry>();

            builder.Register<RuntimeLifetimeScopeSpawnerService>(Lifetime.Singleton)
                .WithParameter(resolvedRoot)
                .WithParameter(spawnerTag)
                .AsSelf()
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

    public interface IRuntimeLifetimeScopeSpawnerService : IAsyncSpawnerService
    {
        int AllDelete(RuntimeLifetimeScopeDeleteFilter filter);
    }

    public sealed class RuntimeLifetimeScopeSpawnerService : IAsyncSpawnerService, IRuntimeLifetimeScopeSpawnerService
    {
        readonly IRuntimeLifetimeScopePool _pool;
        readonly Transform _root;
        readonly ISceneSpawnerRegistry _registry;

        public SpawnerKind Kind => SpawnerKind.RuntimeEntity;
        public string Tag { get; }

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
        }

        public async UniTask<IObjectResolver?> SpawnAsync(SpawnParams p, System.Threading.CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (p.Template == null)
                throw new ArgumentException("SpawnParams.Template is required for Runtime spawns.", nameof(p));

            if (p.Template is not BaseRuntimeTemplateSO template)
                throw new ArgumentException($"Template must be {nameof(BaseRuntimeTemplateSO)}.", nameof(p));

            Transform transformParent = p.TransformParent != null ? p.TransformParent : _root;
            var lifetimeScopeParent = p.LifetimeScopeParent;

            // UI spawn special-case:
            // If the spawned instance is a RectTransform under a non-world-space Canvas, we want to treat
            // SpawnParams.Position as an anchoredPosition (local UI space), even when SpawnParams.WorldSpace==true.
            // We can't inspect the instance before acquiring, so decide using the intended parent.
            var parentCanvas = transformParent.GetComponentInParent<Canvas>(includeInactive: true);
            bool forceAnchoredByCanvas = parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace;

            // Pooling is controlled solely by SpawnParams.AllowPooling and Template.UsePooling.
            // Parent-based reuse restrictions are enforced inside RuntimeLifetimeScopePool by a
            // (Parent Transform + Template) key, so we no longer bypass pooling for non-root parents.
            // This allows pooling under arbitrary parents while still preventing cross-parent reuse.
            bool bypassPooling = !p.AllowPooling;

            // IMPORTANT:
            // RuntimeLifetimeScopePool.ConfigureOnAcquire は常に Acquire の前に WORLD 位置/回転を設定します。
            // SpawnParams.WorldSpace が false（ローカル空間）の場合、Acquire の後に localPosition/localRotation を設定すると、
            // BulkTransform の登録がローカル変換前のワールド位置をキャプチャし、後で変換が元に戻る可能性があります。バルクトランスフォームを同期させるには、まず意図したワールド空間の姿勢を計算し、
            // それをAcquireAsyncに渡してください。
            Vector3 acquireWorldPos;
            Quaternion acquireWorldRot;
            if (forceAnchoredByCanvas)
            {
                // Interpret p.Position/p.Rotation as local UI values and convert to world for Acquire.
                acquireWorldPos = transformParent.TransformPoint(p.Position);
                acquireWorldRot = transformParent.rotation * p.Rotation;
            }
            else if (p.WorldSpace)
            {
                acquireWorldPos = p.Position;
                acquireWorldRot = p.Rotation;
            }
            else
            {
                acquireWorldPos = transformParent.TransformPoint(p.Position);
                acquireWorldRot = transformParent.rotation * p.Rotation;
            }

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
                ApplyWorldPose(t, acquireWorldPos, acquireWorldRot);

                // Build/DI parent selection: allow override, otherwise prefer nearest runtime scope, then base lifetime scope, then default.
                if (lifetimeScopeParent != null)
                {
                    scope.SetExplicitBuildParent(lifetimeScopeParent);
                }
                else
                {
                    var runtimeParent = transformParent.GetComponentInParent<RuntimeLifetimeScope>(includeInactive: true);
                    if (runtimeParent == scope)
                        runtimeParent = null;

                    if (runtimeParent != null)
                    {
                        scope.SetExplicitBuildParent(runtimeParent);
                    }
                    else
                    {
                        var baseParent = transformParent.GetComponentInParent<BaseLifetimeScope>(includeInactive: true);
                        if (baseParent != null)
                            scope.SetExplicitBuildParent((IScopeNode?)baseParent);
                        else
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
            ApplySpawnPose(scope.transform, p);

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
            if (_root == null)
                return 0;

            // NOTE: We intentionally target RuntimeLifetimeScopes under this spawner's configured root.
            // This matches the intended ownership model and avoids corrupting pooled inactive instances.
            var scopes = _root.GetComponentsInChildren<RuntimeLifetimeScope>(filter.IncludeInactive);
            if (scopes == null || scopes.Length == 0)
                return 0;

            int deleted = 0;
            for (int i = 0; i < scopes.Length; i++)
            {
                var scope = scopes[i];
                if (scope == null)
                    continue;

                if (filter.UseInclude && !MatchesIdentity(scope, filter.Include))
                    continue;

                if (filter.UseExclude && MatchesIdentity(scope, filter.Exclude))
                    continue;

                _pool.Release(scope);
                deleted++;
            }

            return deleted;
        }

        static bool MatchesIdentity(RuntimeLifetimeScope scope, CommandTargetIdentityFilter filter)
        {
            var identity = scope.Identity;
            if (identity == null)
                return false;

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

        static void ApplyWorldPose(Transform target, Vector3 worldPosition, Quaternion worldRotation)
        {
            target.SetPositionAndRotation(worldPosition, worldRotation);
            if (target is RectTransform rect)
            {
                // Keep anchoredPosition consistent with the resulting local pose.
                var localPos = rect.localPosition;
                rect.anchoredPosition3D = localPos;
                rect.anchoredPosition = new Vector2(localPos.x, localPos.y);
            }
        }

        static void ApplySpawnPose(Transform target, SpawnParams spawnParams)
        {
            var scale = spawnParams.Scale == default ? Vector3.one : spawnParams.Scale;
            if (target is RectTransform rect)
            {
                var canvas = rect.GetComponentInParent<Canvas>(includeInactive: true);
                bool forceAnchored = canvas != null && canvas.renderMode != RenderMode.WorldSpace;

                if (forceAnchored)
                {
                    // Force UI anchored positioning (treat Position as anchored space).
                    rect.anchoredPosition3D = spawnParams.Position;
                    rect.anchoredPosition = new Vector2(spawnParams.Position.x, spawnParams.Position.y);
                    rect.localRotation = spawnParams.Rotation;
                }
                else if (spawnParams.WorldSpace)
                {
                    // World-space (including WorldSpace canvas): use world pose.
                    rect.SetPositionAndRotation(spawnParams.Position, spawnParams.Rotation);

                    // Keep anchoredPosition consistent with the resulting local pose.
                    var localPos = rect.localPosition;
                    rect.anchoredPosition3D = localPos;
                    rect.anchoredPosition = new Vector2(localPos.x, localPos.y);
                }
                else
                {
                    // Local-space spawn.
                    rect.localPosition = spawnParams.Position;
                    rect.localRotation = spawnParams.Rotation;
                    rect.anchoredPosition3D = spawnParams.Position;
                    rect.anchoredPosition = new Vector2(spawnParams.Position.x, spawnParams.Position.y);
                }

                rect.localScale = scale;
            }
            else
            {
                if (spawnParams.WorldSpace)
                {
                    target.SetPositionAndRotation(spawnParams.Position, spawnParams.Rotation);
                }
                else
                {
                    target.localPosition = spawnParams.Position;
                    target.localRotation = spawnParams.Rotation;
                }

                target.localScale = scale;
            }
        }

        public UniTask WarmupAsync<T>(T template, int count, System.Threading.CancellationToken ct = default) where T : BaseRuntimeTemplateSO
            => _pool.WarmupAsync(template, count, ct);
    }
}
