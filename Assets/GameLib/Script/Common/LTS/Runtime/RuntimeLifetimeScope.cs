#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Common;
using Game.DI;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;

namespace Game
{
    /// <summary>
    /// RuntimeLifetimeScope: 軽量DIコンテナを使用した高パフォーマンスなスコープ
    /// 
    /// VContainerのLifetimeScopeを継承せず、独自の軽量DIシステム(RuntimeResolverHub)を使用。
    /// これによりパフォーマンスが向上し、プールでの再利用が容易になる。
    /// 
    /// サポートする機能:
    /// - Register, RegisterInstance, As, WithParameter
    /// - コンストラクタインジェクション
    /// - 親子階層
    /// - IScopeAcquireHandler/IScopeReleaseHandler
    /// - ITickable (RuntimeTickHub経由)
    /// 
    /// サポートしない機能:
    /// - [Inject]属性
    /// - VContainerのEntryPoint系 (IInitializable等はFeatureInstaller内で手動処理)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LTSIdentityMB))]
    [RequireComponent(typeof(RuntimeTickHub))]
    [RequireComponent(typeof(BlackboardMB))]
    [RequireComponent(typeof(CommandRunnerMB))]
    public sealed class RuntimeLifetimeScope : MonoBehaviour, IScopeNode
    {
        static IBaseLifetimeScopeRegistry? s_fallbackRegistry;
        static IObjectResolver? s_fallbackProjectResolver;
        static Type? s_unityCollisionManagerType;
        static bool s_unityCollisionManagerTypeResolved;

        [Header("Feature Installers")]
        [SerializeField] bool includeFeatureInstallers = true;
        [SerializeField] bool includeInactiveFeatureInstallers = true;

        // ================================================================
        // Internal State
        // ================================================================
        bool _destroyed;
        bool _built;
        bool _acquired;

        bool _isVisible = true;
        bool _isActive = true;

        // 親キャッシュ
        IScopeNode? _cachedParent;
        bool _cachedParentResolved;
        IScopeNode? _explicitBuildParent;
        bool _hierarchyRegistered;

        // Pooling control: when false, RuntimeLifetimeScopePool should NOT return this instance to its pool
        // and should instead destroy it on Release.
        public bool AllowPooling { get; set; } = true;

        // Pooling key (assigned by RuntimeLifetimeScopePool on Acquire).
        // This is required because pooled instances are now keyed by (Parent Transform, Template),
        // and Release must return the instance to the exact matching pool.
        internal RuntimeLifetimeScopePoolKey? PoolKey { get; set; }

        // Identity
        readonly RuntimeScopeIdentityService _identity = new();
        BaseRuntimeTemplateSO? _activeTemplate;
        RuntimeIdentityData _activeIdentity;

        // Registry (for ResolveOtherScope / WithActor ByIdentity)
        IBaseLifetimeScopeRegistry? _scopeRegistry;

        // DI Container
        RuntimeResolver? _resolver;
        RuntimeAcquireReleaseDispatcher? _dispatcher;

        // Tick
        IRuntimeTickHub? _tickHub;
        ITickable[]? _tickables;
        ILateTickable[]? _lateTickables;
        IFixedTickable[]? _fixedTickables;

        // Build completion
        UniTaskCompletionSource? _buildCompletionSource;

        [Header("Debug Viewer")]
        [SerializeField, ReadOnly]
        int debugAwakeFrame = -1;

        [SerializeField, ReadOnly]
        int debugBuildFrame = -1;

        [SerializeField, ReadOnly]
        int debugBuildDelayFrames = -1;

        [SerializeField, ReadOnly]
        float debugAwakeRealtime = -1f;

        [SerializeField, ReadOnly]
        float debugBuildRealtime = -1f;

        [SerializeField, ReadOnly]
        string debugParent = "null";

        [SerializeField, ReadOnly]
        bool debugParentHasResolver;

        [SerializeField, ReadOnly]
        string debugParentBuildStatus = "Unknown";

        [SerializeField, ReadOnly]
        string debugPath = string.Empty;

        [SerializeField, ReadOnly]
        string debugBuildStatus = "NotBuilt";

        // ================================================================
        // Public Properties
        // ================================================================

        public RuntimeScopeIdentityService RuntimeIdentity => _identity;
        public BaseRuntimeTemplateSO? ActiveTemplate => _activeTemplate;
        public bool IsBuilt => _built;
        public bool IsAcquired => _acquired;

        /// <summary>VContainer互換のContainer プロパティ (IObjectResolver)</summary>
        public IObjectResolver? Container => _resolver?.AsVContainerResolver();

        public bool TryResolveLocal<T>(out T instance)
        {
            if (_resolver != null && _resolver.TryResolveLocal(typeof(T), out var obj) && obj is T typed)
            {
                instance = typed;
                return true;
            }

            instance = default!;
            return false;
        }

        public bool TryResolveLocal(Type type, out object? instance)
        {
            if (_resolver == null)
            {
                instance = null;
                return false;
            }

            return _resolver.TryResolveLocal(type, out instance);
        }

        // ================================================================
        // IScopeNode Implementation
        // ================================================================

        public IScopeNode? Parent => GetParentCached();
        public ILTSIdentityService? Identity => _identity;
        public LifetimeScopeKind Kind => _identity.Kind;
        public IObjectResolver? Resolver => _resolver?.AsVContainerResolver();

        public bool IsVisible => _isVisible;

        public bool IsActive => _isActive;

        public bool TrySetVisible(bool visible, bool isReset = false)
        {
            _isVisible = visible;
            return true;
        }

        public bool TrySetActive(bool active, bool isReset = false)
        {
            _isActive = active;
            _identity.IsActive = active;

            UniTask.Void(async () => await SetActiveAsync(active, isReset, CancellationToken.None));
            return true;
        }

        public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
        {
            if (_destroyed)
                return UniTask.CompletedTask;

            if (ct.IsCancellationRequested)
                return UniTask.CompletedTask;

            if (active && !_built)
            {
                EnsureScopeBuilt();
            }

            // Apply active state
            _isActive = active;
            _identity.IsActive = active;

            RefreshScopeRegistryRegistrationIfPossible();

            if (active)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }

                if (!_acquired)
                {
                    AcquireInternal(isReset);
                }
            }
            else
            {
                if (_acquired)
                {
                    ReleaseInternal(isReset);
                }

                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
            }

            return UniTask.CompletedTask;
        }

        IReadOnlyList<IScopeNode>? IScopeNode.GetPathFromRoot()
        {
            var stack = new Stack<IScopeNode>();
            IScopeNode? current = this;
            while (current != null)
            {
                stack.Push(current);
                current = current.Parent;
            }

            if (stack.Count == 0)
                return null;

            var list = new List<IScopeNode>(stack.Count);
            while (stack.Count > 0)
            {
                list.Add(stack.Pop());
            }
            return list;
        }

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        void Awake()
        {
            debugAwakeFrame = Time.frameCount;
            debugAwakeRealtime = Time.realtimeSinceStartup;

            // Identity初期化
            // Default: newly created runtime scopes are active.
            _isVisible = true;
            _isActive = true;


            var identityMb = GetComponent<LTSIdentityMB>();
            if (identityMb != null)
            {
                _activeIdentity = new RuntimeIdentityData
                {
                    Id = identityMb.id,
                    Category = identityMb.category,
                    Kind = identityMb.kind,
                    TimeScaleBehavior = identityMb.timeScaleBehavior,
                    InitiallyActive = identityMb.initiallyActive,
                    SelfTransform = transform,
                };
                _identity.Apply(_activeIdentity);

                // Keep identity active in sync with scope active.
                _identity.IsActive = _isActive;
            }

            // TickHub取得
            _tickHub = GetComponent<IRuntimeTickHub>();

            // 親子関係登録
            RegisterInHierarchy();

            RefreshScopeRegistryRegistrationIfPossible();
            RefreshDebugViewer();
        }

        void Start()
        {
            // シーンに配置されたRuntimeLifetimeScopeは自動的にAcquireを行う
            // Spawnerから生成された場合はAcquireAsyncが外部から呼ばれるため、ここでは何もしない
            if (!_acquired)
            {
                UniTask.Void(async () =>
                {
                    if (_destroyed)
                        return;

                    // 親スコープのビルドを待つために1フレーム待機
                    await UniTask.Yield(PlayerLoopTiming.Update);

                    if (_destroyed || _acquired)
                        return;

                    // 親が後から確定するケースの再登録
                    if (!_hierarchyRegistered)
                    {
                        RegisterInHierarchy();
                    }

                    await AcquireAsync(template: null, identity: null);
                });
            }

            // Runtime scopes can be instantiated outside any BaseLifetimeScope hierarchy.
            // Retry registry registration after startup when ProjectLifetimeScope is expected to exist and be built.
            UniTask.Void(async () =>
            {
                if (_destroyed)
                    return;

                // A few frames are enough for Project/Scene scopes to build and expose IBaseLifetimeScopeRegistry.
                for (int i = 0; i < 5; i++)
                    await UniTask.Yield(PlayerLoopTiming.Update);

                if (_destroyed || !this)
                    return;

                RefreshScopeRegistryRegistrationIfPossible();
                RefreshDebugViewer();
            });
        }

        void OnTransformParentChanged()
        {
            if (_destroyed)
                return;

            // 親が付け替えられた場合にHierarchy登録を更新
            var parent = GetParentCached();
            if (parent == null)
            {
                if (_hierarchyRegistered)
                {
                    ScopeNodeHierarchy.Unregister(this);
                    _hierarchyRegistered = false;
                }
                RefreshDebugViewer();
                return;
            }

            _hierarchyRegistered = true;
            ScopeNodeHierarchy.Register(this, parent);
            RefreshDebugViewer();
        }

        void OnDestroy()
        {
            _destroyed = true;
            RefreshDebugViewer();

            // Release if acquired
            if (_acquired)
            {
                ReleaseToPool();
            }

            // Hierarchy登録解除
            UnregisterFromHierarchy();

            // Registry 登録解除
            UnregisterFromScopeRegistryIfNeeded();

            // Resolver破棄
            _resolver?.Dispose();
            _resolver = null;
            _dispatcher = null;
        }

        // ================================================================
        // Build
        // ================================================================

        /// <summary>
        /// スコープをビルドする。親が未ビルドなら先にビルドする。
        /// </summary>
        public void EnsureScopeBuilt()
        {
            if (_built || _destroyed) return;

            // 親を先にビルド
            var parent = GetParentCached();
            if (parent is RuntimeLifetimeScope parentRuntime && parentRuntime != this)
            {
                parentRuntime.EnsureScopeBuilt();
            }
            else if (parent is BaseLifetimeScope parentBase && parentBase != this)
            {
                parentBase.EnsureScopeBuilt();
            }

            Build();
        }

        public UniTask WhenBuiltAsync(CancellationToken ct = default)
        {
            if (_built || _destroyed)
                return UniTask.CompletedTask;

            _buildCompletionSource ??= new UniTaskCompletionSource();

            EnsureScopeBuilt();

            if (ct.IsCancellationRequested)
                return UniTask.CompletedTask;

            return ct.CanBeCanceled
                ? _buildCompletionSource.Task.AttachExternalCancellation(ct)
                : _buildCompletionSource.Task;
        }

        void Build()
        {
            if (_built || _destroyed) return;

            var builder = new RuntimeContainerBuilder();

            // 親のResolverを設定
            var parent = GetParentCached();
            if (parent is RuntimeLifetimeScope parentRuntime && parentRuntime._resolver != null)
            {
                builder.SetParentResolver(parentRuntime._resolver);
                builder.SetParentVContainerResolver(parentRuntime._resolver.AsVContainerResolver());
            }
            else if (parent?.Resolver is IRuntimeResolver parentResolver)
            {
                builder.SetParentResolver(parentResolver);
                builder.SetParentVContainerResolver(parentResolver.AsVContainerResolver());
            }
            else if (parent?.Resolver is IObjectResolver parentContainer)
            {
                builder.SetParentVContainerResolver(parentContainer);
            }

            // Fallback: if no parent VContainer resolver is available, try ProjectLifetimeScope.
            if (builder.ParentVContainerResolver == null &&
                TryResolveProjectResolver(out var projectResolver) &&
                projectResolver != null)
            {
                builder.SetParentVContainerResolver(projectResolver);
            }

            // Host scope を設定（Unity コンポーネント解決に利用）
            builder.SetHostScope(this);

            LogBuildContext(builder, parent);

            // Core registrations
            ConfigureCore(builder);

            // Feature Installers
            if (includeFeatureInstallers)
            {
                InstallFeatures(builder);
            }

            // Build
            try
            {
                _resolver = (RuntimeResolver)builder.Build();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeLifetimeScope] Build: Failed to build resolver for {gameObject.name}: {ex.Message}");
                Debug.LogException(ex);
                debugBuildStatus = "BuildFailed";
                RefreshDebugViewer();
                return;
            }

            try
            {
                _dispatcher = new RuntimeAcquireReleaseDispatcher(_resolver);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeLifetimeScope] Build: Failed to create dispatcher for {gameObject.name}: {ex.Message}");
                Debug.LogException(ex);
            }

            // Tickables キャッシュ
            try
            {
                _tickables = _resolver.GetTickables();
                _lateTickables = _resolver.GetLateTickables();
                _fixedTickables = _resolver.GetFixedTickables();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeLifetimeScope] Build: Failed to get tickables for {gameObject.name}: {ex.Message}");
                Debug.LogException(ex);
            }

            _built = true;
            debugBuildFrame = Time.frameCount;
            debugBuildRealtime = Time.realtimeSinceStartup;
            debugBuildDelayFrames = debugAwakeFrame >= 0 ? debugBuildFrame - debugAwakeFrame : -1;
            debugBuildStatus = "Built";
            RefreshDebugViewer();
            _buildCompletionSource?.TrySetResult();
        }

        void ConfigureCore(RuntimeContainerBuilder builder)
        {
            if (builder == null)
                return;

            // Scope-local multi registry
            builder.Register<ScopeMultiRegistry>(Lifetime.Singleton)
                .As<IScopeMultiRegistry>();

            // Identity service for runtime scopes
            builder.RegisterInstance(_identity)
                .As<ILTSIdentityService>()
                .AsSelf();

            // Scope instance
            builder.RegisterInstance(this)
                .As<IScopeNode>()
                .AsSelf();

            // Tick hub (required component)
            if (_tickHub == null)
                _tickHub = GetComponent<IRuntimeTickHub>();
            if (_tickHub != null)
            {
                builder.RegisterInstance(_tickHub)
                    .As<IRuntimeTickHub>();
            }

            // Common controllers (matching BaseLifetimeScope)
            builder.RegisterInstance(new Game.Common.RandomVarianceControllerOptions(
                    512,
                    Game.Common.VarianceSettings.Default))
                .AsSelf();

            builder.Register<Game.Common.RandomVarianceController>(Lifetime.Singleton)
                .As<Game.Common.IRandomVarianceController>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<Game.Common.DynamicCounterController>(Lifetime.Singleton)
                .As<Game.Common.IDynamicCounterController>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void InstallFeatures(RuntimeContainerBuilder builder)
        {
            if (builder == null)
                return;

            var installers = ListPool<IFeatureInstaller>.Get();
            try
            {
                GetComponentsInChildren(includeInactiveFeatureInstallers, installers);
                for (int i = 0; i < installers.Count; i++)
                {
                    var installer = installers[i];
                    if (installer is not Component component)
                        continue;

                    if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(component, includeInactiveFeatureInstallers, out var owner) ||
                        owner == null ||
                        !ReferenceEquals(owner, this))
                    {
                        continue;
                    }
                    installer.InstallFeature(builder, this);
                }
            }
            finally
            {
                ListPool<IFeatureInstaller>.Release(installers);
            }
        }

        bool TryResolveProjectResolver(out IObjectResolver? resolver)
        {
            if (s_fallbackProjectResolver != null)
            {
                resolver = s_fallbackProjectResolver;
                return true;
            }

            var projectScopes = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (projectScopes == null || projectScopes.Length == 0)
            {
                LTSLog.LogWarning("[RuntimeLifetimeScope] ProjectLifetimeScope not found while resolving parent resolver.", this);
                resolver = null;
                return false;
            }

            var project = projectScopes[0];
            if (project == null)
            {
                LTSLog.LogWarning("[RuntimeLifetimeScope] ProjectLifetimeScope was found but is null.", this);
                resolver = null;
                return false;
            }

            if (project.Container == null)
            {
                try
                {
                    project.EnsureScopeBuilt();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            resolver = project.Resolver;
            if (resolver != null)
            {
                s_fallbackProjectResolver = resolver;
                return true;
            }

            LTSLog.LogWarning("[RuntimeLifetimeScope] ProjectLifetimeScope resolver is still null after EnsureScopeBuilt.", project);
            return false;
        }

        void RefreshScopeRegistryRegistrationIfPossible()
        {
            if (_destroyed) return;

            if (!TryEnsureScopeRegistryResolved(out var registry) || registry == null)
                return;

            if (_isActive)
            {
                TryRegisterInScopeRegistry(registry);
            }
            else
            {
                TryUnregisterFromScopeRegistry(registry);
            }
        }

        bool TryEnsureScopeRegistryResolved(out IBaseLifetimeScopeRegistry? registry)
        {
            if (_scopeRegistry != null)
            {
                registry = _scopeRegistry;
                return true;
            }

            // Fast-path: cached project/global registry.
            if (s_fallbackRegistry != null)
            {
                _scopeRegistry = s_fallbackRegistry;
                registry = s_fallbackRegistry;
                return true;
            }

            // Prefer own resolver (after Build) because it should chain to parent resolver.
            var localResolver = Resolver;
            if (localResolver != null && localResolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
            {
                _scopeRegistry = resolved;
                registry = resolved;
                return true;
            }

            // Fallback: walk up the scope parent chain.
            var current = GetParentCached();
            while (current != null)
            {
                var r = current.Resolver;
                if (r != null && r.TryResolve<IBaseLifetimeScopeRegistry>(out resolved) && resolved != null)
                {
                    _scopeRegistry = resolved;
                    registry = resolved;
                    return true;
                }
                current = current.Parent;
            }

            // Last-resort fallback: resolve from ProjectLifetimeScope even when this runtime scope is not under any scope hierarchy.
            // Cache the result to avoid repeated scene searches.
            var projectScopes = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (projectScopes != null && projectScopes.Length > 0)
            {
                var project = projectScopes[0];
                var pr = project != null ? project.Resolver : null;
                if (pr != null && pr.TryResolve<IBaseLifetimeScopeRegistry>(out resolved) && resolved != null)
                {
                    s_fallbackRegistry = resolved;
                    _scopeRegistry = resolved;
                    registry = resolved;
                    return true;
                }
            }

            registry = null;
            return false;
        }

        bool TryRegisterInScopeRegistry(IBaseLifetimeScopeRegistry registry)
        {
            if (registry == null)
                return false;

            var identity = Identity;
            if (identity == null)
                return false;

            registry.RegisterScope(this, identity);
            return true;
        }

        bool TryUnregisterFromScopeRegistry(IBaseLifetimeScopeRegistry registry)
        {
            if (registry == null)
                return false;

            registry.UnregisterScope(this);
            return true;
        }

        void UnregisterFromScopeRegistryIfNeeded()
        {
            if (_scopeRegistry != null)
            {
                _scopeRegistry.UnregisterScope(this);
                return;
            }

            if (s_fallbackRegistry != null)
            {
                s_fallbackRegistry.UnregisterScope(this);
                return;
            }

            if (TryEnsureScopeRegistryResolved(out var registry) && registry != null)
            {
                registry.UnregisterScope(this);
            }
        }

        // ================================================================
        // Acquire / Release
        // ================================================================

        public void ConfigureForAcquire(BaseRuntimeTemplateSO? template, RuntimeIdentityData? identity, bool ensureBuilt)
        {
            _activeTemplate = template;

            var data = identity ?? _activeIdentity;
            data.SelfTransform = transform;
            if (data.Kind == LifetimeScopeKind.None)
                data.Kind = LifetimeScopeKind.Runtime;

            if (template != null)
            {
                if (string.IsNullOrEmpty(data.Category))
                    data.Category = template.Category;
                if (string.IsNullOrEmpty(data.Id))
                    data.Id = template.TemplateId;
            }

            if (string.IsNullOrEmpty(data.Category))
                data.Category = "Runtime";

            _activeIdentity = data;
            _identity.Apply(_activeIdentity);
            _identity.IsActive = _isActive;

            if (ensureBuilt)
            {
                EnsureScopeBuilt();
            }
        }

        public async UniTask AcquireAsync(BaseRuntimeTemplateSO? template, RuntimeIdentityData? identity, CancellationToken ct = default)
        {
            if (_destroyed)
                return;

            if (ct.IsCancellationRequested)
                return;

            ConfigureForAcquire(template, identity, ensureBuilt: false);

            EnsureScopeBuilt();

            // Determine initial active state
            var desiredActive = identity?.InitiallyActive ?? _activeIdentity.InitiallyActive;

            await SetActiveAsync(desiredActive, isReset: false, ct);

            if (!desiredActive)
                return;

            if (TryResolveLocal<IScopeLifecycleService>(out var lifecycle) && lifecycle != null)
            {
                try
                {
                    await lifecycle.HandleSpawnAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public async UniTask HandleSpawnAsync(CancellationToken ct = default)
        {
            if (_destroyed)
                return;

            if (ct.IsCancellationRequested)
                return;

            EnsureScopeBuilt();

            await SetActiveAsync(active: true, isReset: false, ct);

            if (TryResolveLocal<IScopeLifecycleService>(out var lifecycle) && lifecycle != null)
            {
                try
                {
                    await lifecycle.HandleSpawnAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        void ReleaseToPool()
        {
            _isActive = false;
            _identity.IsActive = false;
            ReleaseInternal(isReset: true);
        }

        void AcquireInternal(bool isReset)
        {
            if (_resolver == null)
            {
                _acquired = false;
                return;
            }

            if (_dispatcher == null)
            {
                try
                {
                    _dispatcher = new RuntimeAcquireReleaseDispatcher(_resolver);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    _acquired = false;
                    return;
                }
            }

            _acquired = true;

            _dispatcher?.Acquire(this, isReset);

            if (_tickables != null && _tickHub != null)
            {
                _tickHub.RegisterRange(_tickables);
            }

            if (_lateTickables != null && _tickHub != null)
            {
                _tickHub.RegisterLateRange(_lateTickables);
            }

            if (_fixedTickables != null && _tickHub != null)
            {
                _tickHub.RegisterFixedRange(_fixedTickables);
            }

            if (_activeTemplate != null)
            {
                try
                {
                    _activeTemplate.OnAcquire(this, _activeIdentity);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        void ReleaseInternal(bool isReset)
        {
            if (_resolver == null)
            {
                _acquired = false;
                return;
            }

            if (_activeTemplate != null)
            {
                try
                {
                    _activeTemplate.OnRelease(this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (_tickables != null && _tickHub != null)
            {
                _tickHub.UnregisterRange(_tickables);
            }

            if (_lateTickables != null && _tickHub != null)
            {
                _tickHub.UnregisterLateRange(_lateTickables);
            }

            if (_fixedTickables != null && _tickHub != null)
            {
                _tickHub.UnregisterFixedRange(_fixedTickables);
            }

            _dispatcher?.Release(this, isReset);
            _acquired = false;
        }

        void LogBuildContext(RuntimeContainerBuilder builder, IScopeNode? parent)
        {
            if (!LTSLog.Enabled)
                return;

            string parentInfo = parent is Component pc
                ? $"{parent.GetType().Name}({pc.gameObject.name})"
                : parent != null
                    ? parent.GetType().Name
                    : "null";

            string parentVContainerInfo = builder.ParentVContainerResolver != null
                ? builder.ParentVContainerResolver.GetType().Name
                : "null";

            LTSLog.Log(
                $"[RuntimeLifetimeScope] Build scope='{gameObject.name}' parent={parentInfo} parentVContainer={parentVContainerInfo}",
                this);

            var collisionType = ResolveUnityCollisionManagerType();
            if (collisionType == null)
                return;

            if (builder.ParentVContainerResolver == null)
            {
                LTSLog.LogWarning(
                    $"[RuntimeLifetimeScope] Parent VContainer resolver is null. {collisionType.Name} cannot be resolved from parent.",
                    this);
                return;
            }

            var ok = builder.ParentVContainerResolver.TryResolve(collisionType, out var _);
            LTSLog.Log(
                $"[RuntimeLifetimeScope] Parent VContainer resolve {collisionType.Name} => {ok}",
                this);
        }

        void RefreshDebugViewer()
        {
            if (!this)
                return;

            var parent = GetParentCached();
            debugParent = ToScopeDebugName(parent);
            debugParentHasResolver = parent?.Resolver != null;
            debugParentBuildStatus = GetParentBuildStatus(parent);
            debugPath = GetPathStringFromRoot(this);

            if (_destroyed)
                debugBuildStatus = "Destroyed";
            else if (_built)
                debugBuildStatus = "Built";
            else if (string.Equals(debugBuildStatus, "BuildFailed", StringComparison.Ordinal))
                debugBuildStatus = "BuildFailed";
            else
                debugBuildStatus = "NotBuilt";
        }

        static string ToScopeDebugName(IScopeNode? node)
        {
            if (node == null)
                return "null";

            if (node is Component component)
            {
                if (!component)
                    return $"{node.GetType().Name}(Destroyed)";

                var go = component.gameObject;
                return go != null
                    ? $"{node.GetType().Name}({go.name})"
                    : $"{node.GetType().Name}(Destroyed)";
            }

            return node.GetType().Name;
        }

        static string GetPathStringFromRoot(IScopeNode? node)
        {
            if (node is Component owner && !owner)
                return "Destroyed";

            var path = node?.GetPathFromRoot();
            if (path == null || path.Count == 0)
                return string.Empty;

            var names = new List<string>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                var p = path[i];
                if (p is Component c)
                {
                    if (!c)
                    {
                        names.Add("Destroyed");
                        continue;
                    }

                    var go = c.gameObject;
                    names.Add(go != null ? go.name : "Destroyed");
                }
                else
                    names.Add(p.GetType().Name);
            }

            return string.Join(" / ", names);
        }

        static string GetParentBuildStatus(IScopeNode? node)
        {
            if (node == null)
                return "None";
            if (node is BaseLifetimeScope baseScope)
                return baseScope.IsBuildCompleted ? "Built" : "NotBuilt";
            if (node is RuntimeLifetimeScope runtimeScope)
                return runtimeScope.IsBuilt ? "Built" : "NotBuilt";
            return "Unknown";
        }

        static Type? ResolveUnityCollisionManagerType()
        {
            if (s_unityCollisionManagerTypeResolved)
                return s_unityCollisionManagerType;

            s_unityCollisionManagerTypeResolved = true;
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var type = assemblies[i].GetType("Game.Collision.IUnityCollisionManager", throwOnError: false);
                    if (type != null)
                    {
                        s_unityCollisionManagerType = type;
                        break;
                    }
                }
            }
            catch
            {
            }

            return s_unityCollisionManagerType;
        }

        // ================================================================
        // Parent Resolution
        // ================================================================

        IScopeNode? GetParentCached()
        {
            if (_cachedParentResolved)
            {
                // 生成直後に親が未確定だった場合の再評価
                if (_cachedParent == null && _explicitBuildParent == null && transform.parent != null)
                {
                    var resolved = ResolveParentCore();
                    if (resolved != null)
                        _cachedParent = resolved;
                }
                return _cachedParent;
            }

            _cachedParentResolved = true;
            _cachedParent = _explicitBuildParent ?? ResolveParentCore();
            return _cachedParent;
        }

        IScopeNode? ResolveParentCore()
        {
            // Transformヒエラルキーを辿って最も近いスコープを探す
            var current = transform.parent;
            while (current != null)
            {
                // RuntimeLifetimeScope を優先
                var runtimeScope = current.GetComponent<RuntimeLifetimeScope>();
                if (runtimeScope != null && runtimeScope != this)
                    return runtimeScope;

                // BaseLifetimeScope もサポート
                var baseScope = current.GetComponent<BaseLifetimeScope>();
                if (baseScope != null)
                    return baseScope;

                current = current.parent;
            }

            // Fallback: hierarchyにスコープが無い場合はProjectを親にする
            var projectScopes = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (projectScopes != null && projectScopes.Length > 0)
                return projectScopes[0];

            return null;
        }

        /// <summary>
        /// 親スコープを明示的に設定する（プール用）
        /// </summary>
        public void SetExplicitBuildParent(IScopeNode? parent)
        {
            if (ReferenceEquals(_explicitBuildParent, parent))
                return; // ここ超重要。プールで無駄撃ちしてるなら即効く

            _explicitBuildParent = parent;
            _cachedParentResolved = false;
            _cachedParent = null;

            if (parent == null)
            {
                if (_hierarchyRegistered)
                {
                    ScopeNodeHierarchy.Unregister(this);
                    _hierarchyRegistered = false;
                }
                RefreshDebugViewer();
                return;
            }

            // Unregister→Register をやめる。Register が移動/デタッチまで処理する。
            ScopeNodeHierarchy.Register(this, parent);
            _hierarchyRegistered = true;
            RefreshDebugViewer();
        }


        /// <summary>
        /// VContainer互換: LifetimeScopeを親として設定
        /// </summary>
        public void SetExplicitBuildParent(LifetimeScope? parent)
        {
            SetExplicitBuildParent(parent as IScopeNode);
        }

        void RegisterInHierarchy()
        {
            var parent = GetParentCached();
            if (parent == null)
            {
                _hierarchyRegistered = false;
                RefreshDebugViewer();
                return;
            }

            _hierarchyRegistered = true;
            ScopeNodeHierarchy.Register(this, parent);
            RefreshDebugViewer();
        }


        void UnregisterFromHierarchy()
        {
            if (!_hierarchyRegistered)
                return;

            ScopeNodeHierarchy.Unregister(this);
            _hierarchyRegistered = false;
            RefreshDebugViewer();
        }
    }
}
