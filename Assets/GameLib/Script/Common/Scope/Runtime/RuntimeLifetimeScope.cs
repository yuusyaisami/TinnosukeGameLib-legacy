#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Common;
using Game.DI;
using Game.Platform;
using Game.Project;
using Game.Scene;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Pool;

namespace Game
{
    // Compatibility shell to keep existing prefab/scene MonoScript bindings alive
    // while legacy runtime-scope references are being removed from authoring assets.
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class RuntimeLifetimeScope : KernelScopeHost
    {
    }

    public readonly struct SpawnedLifetimeHandle
    {
        readonly KernelScopeHost? _runtimeScope;
        readonly KernelScopeHost? _nonRuntimeScope;

        public static SpawnedLifetimeHandle Empty => default;

        public IRuntimeResolver? Resolver { get; }
        public GameObject? Root { get; }
        public IScopeNode? ScopeNode { get; }
        public bool UsesRuntimeLifetimeScope => _runtimeScope != null;
        public bool UsesBaseLifetimeScope => _nonRuntimeScope != null;
        public bool HasLiveObject => HasLiveObjectCore(this);
        public bool IsEmpty => Resolver == null || !HasLiveObject;
        public bool HasMissingRuntimeIdentity =>
            _runtimeScope != null &&
            (_runtimeScope.Identity == null || string.IsNullOrEmpty(_runtimeScope.Identity.Id));

        SpawnedLifetimeHandle(
            IRuntimeResolver? resolver,
            GameObject? root,
            IScopeNode? scopeNode,
            KernelScopeHost? runtimeScope,
            KernelScopeHost? nonRuntimeScope)
        {
            Resolver = resolver;
            Root = root;
            ScopeNode = scopeNode;
            _runtimeScope = runtimeScope;
            _nonRuntimeScope = nonRuntimeScope;
        }

        public static SpawnedLifetimeHandle FromResolver(IRuntimeResolver? resolver)
        {
            if (resolver == null)
                return Empty;

            GameObject? root = null;
            IScopeNode? scopeNode = null;
            KernelScopeHost? runtimeScope = null;
            KernelScopeHost? nonRuntimeScope = null;

            resolver.TryResolve(out runtimeScope);

            if (runtimeScope != null)
                root = runtimeScope.gameObject;

            if (root == null)
            {
                if (resolver.TryResolve<Transform>(out var tr) && tr != null)
                    root = tr.gameObject;
                else if (resolver.TryResolve<GameObject>(out var go) && go != null)
                    root = go;
            }

            scopeNode = runtimeScope;
            if (scopeNode == null && resolver.TryResolve<IScopeNode>(out var resolved) && resolved != null)
                scopeNode = resolved;

            if (root == null && scopeNode?.Identity?.SelfTransform != null)
                root = scopeNode.Identity.SelfTransform.gameObject;

            if (root == null && scopeNode is Component scopeComponent)
                root = scopeComponent.gameObject;

            if (scopeNode == null && root != null)
            {
                var components = root.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IScopeNode node)
                    {
                        scopeNode = node;
                        break;
                    }
                }
            }

            if (runtimeScope == null && scopeNode is KernelScopeHost runtimeBase)
                nonRuntimeScope = runtimeBase;

            return new SpawnedLifetimeHandle(resolver, root, scopeNode, runtimeScope, nonRuntimeScope);
        }

        public UniTask ReleaseAsync(CancellationToken ct, Action<Exception>? onPoolReleaseError = null)
            => ReleaseAsyncCore(this, ct, onPoolReleaseError);

        static bool HasLiveObjectCore(SpawnedLifetimeHandle handle)
        {
            if (handle.Root != null)
                return true;

            if (handle._runtimeScope != null)
                return true;

            if (handle._nonRuntimeScope != null)
                return true;

            if (handle.ScopeNode == null)
                return false;

            if (handle.ScopeNode is UnityEngine.Object unityObject)
                return unityObject != null;

            return true;
        }

        static async UniTask ReleaseAsyncCore(
            SpawnedLifetimeHandle handle,
            CancellationToken ct,
            Action<Exception>? onPoolReleaseError)
        {
            if (handle.Resolver == null)
                return;

            await UniTask.SwitchToMainThread();

            if (handle._runtimeScope != null)
            {
                try
                {
                    if (handle._runtimeScope.Resolver != null &&
                        handle._runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                        pool != null)
                    {
                        pool.Release(handle._runtimeScope);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    onPoolReleaseError?.Invoke(ex);
                }

                if (handle.Root != null)
                {
                    UnityEngine.Object.Destroy(handle.Root);
                }
                else
                {
                    UnityEngine.Object.Destroy(handle._runtimeScope.gameObject);
                }

                return;
            }

            if (handle._nonRuntimeScope != null)
            {
                await KernelScopeHost.DespawnCompatAsync(handle._nonRuntimeScope, ct);
                return;
            }

            if (handle.Root != null)
                UnityEngine.Object.Destroy(handle.Root);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScopeIdentityMB))]
    [RequireComponent(typeof(RuntimeTickHub))]
    public abstract class KernelScopeHost : MonoBehaviour, IScopeNode, IScopeGraphHost, ICoordinatedBuildScope
    {
        const int VerifiedScopeStateBuilding = 20;
        const int VerifiedScopeStateBuilt = 30;
        const int VerifiedScopeStateAcquiring = 40;
        const int VerifiedScopeStateActive = 50;
        const int VerifiedScopeStateReleasing = 60;
        const int VerifiedScopeStateInactive = 70;
        const int VerifiedScopeStateFailed = 100;

        static IBaseLifetimeScopeRegistry? s_cachedRegistry;

        [Header("Feature Installers")]
        [SerializeField] bool includeFeatureInstallers = true;

        [Header("Scope State")]
        [SerializeField] bool initiallyVisible = true;

        [Header("Game Logic Root")]
        [ToggleLeft]
        [SerializeField] bool useAsGameLogicRoot = false;

        bool _destroyed;
        bool _built;
        bool _acquired;
        bool _hierarchyRegistered;
        bool _cachedParentResolved;
        bool _suppressGameObjectActiveSync;

        bool _isVisible = true;
        bool _isActive = true;

        IScopeNode? _cachedParent;
        IScopeNode? _explicitBuildParent;
        IBaseLifetimeScopeRegistry? _scopeRegistry;

        readonly RuntimeScopeIdentityService _identity = new();
        RuntimeIdentityData _activeIdentity;
        BaseRuntimeTemplateSO? _activeTemplate;

        RuntimeResolver? _resolver;
        RuntimeAcquireReleaseDispatcher? _dispatcher;
        IRuntimeTickHub? _tickHub;
        IScopeTickHandler[]? _tickHandlers;
        IScopeLateTickHandler[]? _lateTickHandlers;
        IScopeFixedTickHandler[]? _fixedTickHandlers;
        UniTaskCompletionSource? _buildCompletionSource;

        [Header("Debug Viewer")]
        [SerializeField, ReadOnly] int debugAwakeFrame = -1;
        [SerializeField, ReadOnly] int debugBuildFrame = -1;
        [SerializeField, ReadOnly] int debugBuildDelayFrames = -1;
        [SerializeField, ReadOnly] float debugAwakeRealtime = -1f;
        [SerializeField, ReadOnly] float debugBuildRealtime = -1f;
        [SerializeField, ReadOnly] string debugParent = "null";
        [SerializeField, ReadOnly] bool debugParentHasResolver;
        [SerializeField, ReadOnly] string debugParentBuildStatus = "Unknown";
        [SerializeField, ReadOnly] string debugPath = string.Empty;
        [SerializeField, ReadOnly] string debugBuildStatus = "NotBuilt";

        public bool AllowPooling { get; set; } = true;
        internal RuntimeLifetimeScopePoolKey? PoolKey { get; set; }

        public RuntimeScopeIdentityService RuntimeIdentity => _identity;
        public BaseRuntimeTemplateSO? ActiveTemplate => _activeTemplate;
        public bool IsBuilt => _built;
        public bool IsBuildCompleted => _built;
        public bool IsAcquired => _acquired;
        public string DebugBuildStatus => debugBuildStatus;
        public bool UseAsGameLogicRoot => useAsGameLogicRoot;
        public IRuntimeResolver? Container => _resolver;

        public IScopeNode? Parent => GetParentCached();
        public IScopeIdentityService? Identity => _identity;
        public LifetimeScopeKind Kind => _identity.Kind;
        public IRuntimeResolver? Resolver => _resolver;
        public bool IsVisible => _isVisible;
        public bool IsActive => _isActive;
        public Component HostComponent => this;
        public GameObject HostGameObject => gameObject;
        public Transform HostTransform => transform;

        protected virtual bool UseBuildCoordinator => true;
        protected virtual bool IsBuildRoot => false;
        protected virtual bool AutoBuildOnAwake => false;
        protected virtual LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.None;
        protected virtual bool RequiresParentScope => RequiredParentKind != LifetimeScopeKind.None;
        protected virtual bool AllowsLegacyFeatureInstallerProjectionWhenVerifiedComposition => false;

        bool ICoordinatedBuildScope.UseBuildCoordinator => UseBuildCoordinator;
        bool ICoordinatedBuildScope.IsBuildCompleted => _built;

        protected virtual void Awake()
        {
            debugAwakeFrame = Time.frameCount;
            debugAwakeRealtime = Time.realtimeSinceStartup;

            _isVisible = initiallyVisible;
            ApplyIdentityFromComponent();
            _tickHub = GetComponent<IRuntimeTickHub>();

            if (UseBuildCoordinator)
                ScopeBuildCoordinator.Register(this, IsBuildRoot && AutoBuildOnAwake);

            RegisterInHierarchy();
            RefreshDebugViewer();
        }

        protected virtual void Start()
        {
            if (!_acquired)
            {
                UniTask.Void(async () =>
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    if (_destroyed || _acquired)
                        return;

                    if (!_hierarchyRegistered)
                        RegisterInHierarchy();

                    await AcquireAsync(template: null, identity: null);
                });
            }
        }

        protected virtual void OnEnable()
        {
            if (_destroyed || _suppressGameObjectActiveSync)
                return;

            if (_built && _isActive)
                AcquireIfNeeded();
        }

        protected virtual void OnDisable()
        {
            if (_destroyed || _suppressGameObjectActiveSync)
                return;

            ReleaseIfNeeded();
        }

        protected virtual void OnTransformParentChanged()
        {
            if (_destroyed || _explicitBuildParent != null)
                return;

            _cachedParentResolved = false;
            _cachedParent = null;
            RegisterInHierarchy();
            RefreshDebugViewer();
        }

        protected virtual void OnDestroy()
        {
            _destroyed = true;
            RefreshDebugViewer();

            if (UseBuildCoordinator)
                ScopeBuildCoordinator.Unregister(this);

            ReleaseIfNeeded();
            UnregisterFromScopeRegistryIfNeeded();
            UnregisterFromHierarchy();
            VerifiedCompositionRuntime.ReleaseRuntimeScope(this);

            _resolver?.Dispose();
            _resolver = null;
            _dispatcher = null;
        }

        public bool TrySetVisible(bool visible, bool isReset = false)
        {
            _isVisible = visible;
            return true;
        }

        public bool TrySetActive(bool active, bool isReset = false)
        {
            if (_destroyed)
                return false;

            UniTask.Void(async () => await SetActiveAsync(active, isReset, CancellationToken.None));
            return true;
        }

        public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
        {
            if (_destroyed || ct.IsCancellationRequested)
                return UniTask.CompletedTask;

            if (active)
                EnsureScopeBuilt();

            _isActive = active;
            _identity.IsActive = active;
            RefreshScopeRegistryRegistrationIfPossible();

            if (active)
            {
                if (!gameObject.activeSelf)
                {
                    _suppressGameObjectActiveSync = true;
                    gameObject.SetActive(true);
                    _suppressGameObjectActiveSync = false;
                }

                AcquireIfNeeded();
            }
            else
            {
                ReleaseIfNeeded();

                if (gameObject.activeSelf)
                {
                    _suppressGameObjectActiveSync = true;
                    gameObject.SetActive(false);
                    _suppressGameObjectActiveSync = false;
                }
            }

            RefreshDebugViewer();
            return UniTask.CompletedTask;
        }

        public IReadOnlyList<IScopeNode>? GetPathFromRoot()
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
                list.Add(stack.Pop());
            return list;
        }

        public void EnsureScopeBuilt()
        {
            if (_built || _destroyed)
                return;

            var parent = GetParentCached();
            if (parent is KernelScopeHost runtimeParent && !ReferenceEquals(runtimeParent, this))
            {
                runtimeParent.EnsureScopeBuilt();
            }
            else if (parent is ICoordinatedBuildScope coordinatedParent && coordinatedParent.Resolver == null && !coordinatedParent.IsBuildCompleted)
            {
                coordinatedParent.ExecuteBuildForCoordinator();
            }

            if (RequiresParentScope && parent == null)
                throw new InvalidOperationException($"[{GetType().Name}] Required parent scope '{RequiredParentKind}' was not found for '{name}'.");

            if (parent != null && parent.Resolver == null)
                throw new InvalidOperationException($"[{GetType().Name}] Parent scope '{parent.GetType().Name}' has no resolver when building '{name}'.");

            Build();
        }

        public UniTask WhenBuiltAsync(CancellationToken ct = default)
        {
            if (_built || _destroyed)
                return UniTask.CompletedTask;

            _buildCompletionSource ??= new UniTaskCompletionSource();
            EnsureScopeBuilt();
            return ct.CanBeCanceled
                ? _buildCompletionSource.Task.AttachExternalCancellation(ct)
                : _buildCompletionSource.Task;
        }

        public static bool TryResolveProjectHostService<T>(out T? service)
            where T : class
        {
            if (TryResolveProjectHostResolver(out var resolver) &&
                resolver != null &&
                resolver.TryResolve<T>(out var resolved) &&
                resolved != null)
            {
                service = resolved;
                return true;
            }

            service = null;
            return false;
        }

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

        static bool TryResolveProjectHostResolver(out IRuntimeResolver? resolver)
        {
            if (TryResolveProjectHostScope(out var scope) && scope != null)
            {
                scope.EnsureScopeBuilt();
                resolver = scope.Resolver;
                return resolver != null;
            }

            resolver = null;
            return false;
        }

        static bool TryResolveProjectHostScope(out KernelScopeHost? scope)
        {
            if (s_cachedRegistry != null)
            {
                var filter = new CommandTargetIdentityFilter
                {
                    kind = LifetimeScopeKind.Project,
                    requireActive = false,
                    searchScope = CommandTargetSearchScope.All,
                };

                if (s_cachedRegistry.Resolve(filter) is KernelScopeHost cachedProjectScope)
                {
                    scope = cachedProjectScope;
                    return true;
                }
            }

            var scopes = UnityEngine.Object.FindObjectsByType<KernelScopeHost>(
                FindObjectsInactive.Include);

            for (int i = 0; i < scopes.Length; i++)
            {
                var candidate = scopes[i];
                if (candidate == null)
                    continue;

                LifetimeScopeKind resolvedKind = ScopeIdentityMB.PredictKindFromComponent(candidate, candidate.Kind);
                if (resolvedKind != LifetimeScopeKind.Project)
                    continue;

                scope = candidate;
                return true;
            }

            scope = null;
            return false;
        }

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
            _isActive = data.InitiallyActive;
            _identity.IsActive = _isActive;

            if (ensureBuilt)
                EnsureScopeBuilt();
        }

        public async UniTask AcquireAsync(BaseRuntimeTemplateSO? template, RuntimeIdentityData? identity, CancellationToken ct = default)
        {
            if (_destroyed || ct.IsCancellationRequested)
                return;

            ConfigureForAcquire(template, identity, ensureBuilt: false);
            EnsureScopeBuilt();

            var desiredActive = identity?.InitiallyActive ?? _activeIdentity.InitiallyActive;
            await SetActiveAsync(desiredActive, isReset: false, ct);
            if (!desiredActive)
                return;

            if (TryResolveLocal<IScopeLifecycleService>(out var lifecycle) && lifecycle != null)
                await lifecycle.HandleSpawnAsync(ct);
        }

        public async UniTask HandleSpawnAsync(CancellationToken ct = default)
        {
            if (_destroyed || ct.IsCancellationRequested)
                return;

            EnsureScopeBuilt();
            await SetActiveAsync(true, isReset: false, ct);

            if (TryResolveLocal<IScopeLifecycleService>(out var lifecycle) && lifecycle != null)
                await lifecycle.HandleSpawnAsync(ct);
        }

        public void AcquireIfNeeded()
        {
            if (_destroyed || _acquired || !_isActive)
                return;

            if (_resolver == null)
                EnsureScopeBuilt();

            if (_resolver == null)
                return;

            _dispatcher ??= new RuntimeAcquireReleaseDispatcher(_resolver);
            TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateAcquiring);
            try
            {
                _dispatcher.Acquire(this, isReset: false);
                _acquired = true;

                if (_tickHub != null)
                {
                    if (_tickHandlers != null)
                        _tickHub.RegisterRange(_tickHandlers);
                    if (_lateTickHandlers != null)
                        _tickHub.RegisterLateRange(_lateTickHandlers);
                    if (_fixedTickHandlers != null)
                        _tickHub.RegisterFixedRange(_fixedTickHandlers);
                }

                _activeTemplate?.OnAcquire(this, _activeIdentity);
                TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateActive);
                TryRefreshVerifiedRuntimeScopeUnityLink();
            }
            catch
            {
                TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateFailed);
                throw;
            }
        }

        public void ReleaseIfNeeded()
        {
            if (!_acquired)
                return;

            TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateReleasing);
            try
            {

                if (_tickHub != null)
                {
                    if (_tickHandlers != null)
                        _tickHub.UnregisterRange(_tickHandlers);
                    if (_lateTickHandlers != null)
                        _tickHub.UnregisterLateRange(_lateTickHandlers);
                    if (_fixedTickHandlers != null)
                        _tickHub.UnregisterFixedRange(_fixedTickHandlers);
                }

                _activeTemplate?.OnRelease(this);
                _dispatcher?.Release(this, isReset: false);
                _acquired = false;
                TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateInactive);
            }
            catch
            {
                TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateFailed);
                throw;
            }
        }

        public void SetExplicitBuildParent(IScopeNode? parent)
        {
            if (ReferenceEquals(_explicitBuildParent, parent))
                return;

            _explicitBuildParent = parent;
            _cachedParentResolved = false;
            _cachedParent = null;
            RegisterInHierarchy();
            RefreshDebugViewer();
        }

        public UniTask DespawnAsync(CancellationToken ct = default)
            => ScopeDespawnCoordinator.DespawnAsync(this, ct);

        internal static UniTask DespawnCompatAsync(KernelScopeHost scope, CancellationToken ct = default)
            => ScopeDespawnCoordinator.DespawnAsync(scope, ct);

        protected virtual void AwakeConfigure(IRuntimeContainerBuilder builder)
        {
        }

        protected virtual void ConfigureBase(IRuntimeContainerBuilder builder)
        {
        }

        void Build()
        {
            if (_built || _destroyed)
                return;

            TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateBuilding);
            try
            {
                var builder = new RuntimeContainerBuilder();
                builder.SetHostScope(this);

                if (ShouldUseVerifiedCompositionRuntime())
                    builder.DisableHandlerCollectionResolution();

                ConfigureCore(builder);
                AwakeConfigure(builder);
                RuntimeScopeContributionBridge.InstallHostContributions(builder, this);

                if (includeFeatureInstallers)
                {
                    RuntimeScopeContributionBridge.InstallAcceptedAuthoringContributions(builder, this);

                    if (ShouldUseVerifiedCompositionRuntime())
                        RuntimeScopeContributionBridge.InstallVerifiedCompositionContributions(builder, this);

                    if (ShouldRejectLegacyFeatureInstallerProjection())
                        RuntimeScopeContributionBridge.ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection(this);
                    else
                        InstallLocalFeatures(builder);
                }

                ConfigureBase(builder);

                _resolver = (RuntimeResolver)builder.Build();
                _dispatcher = new RuntimeAcquireReleaseDispatcher(_resolver);
                _tickHandlers = _resolver.GetTickHandlers();
                _lateTickHandlers = _resolver.GetLateTickHandlers();
                _fixedTickHandlers = _resolver.GetFixedTickHandlers();

                _built = true;
                debugBuildFrame = Time.frameCount;
                debugBuildRealtime = Time.realtimeSinceStartup;
                debugBuildDelayFrames = debugAwakeFrame >= 0 ? debugBuildFrame - debugAwakeFrame : -1;
                debugBuildStatus = "Built";

                TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateBuilt);
                TryRefreshVerifiedRuntimeScopeUnityLink();
                RefreshScopeRegistryRegistrationIfPossible();
                RefreshDebugViewer();
                _buildCompletionSource?.TrySetResult();
                ScopeBuildCoordinator.NotifyBuilt(this);

                if (_isActive)
                    AcquireIfNeeded();
            }
            catch
            {
                TryUpdateVerifiedRuntimeScopeState(VerifiedScopeStateFailed);
                throw;
            }
        }

        void ConfigureCore(IRuntimeContainerBuilder builder)
        {
            builder.Register<ScopeMultiRegistry>(RuntimeLifetime.Singleton)
                .As<IScopeMultiRegistry>();

            builder.Register<ScopeAcquireReleaseDispatcher>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireReleaseDispatcher>();

            builder.RegisterInstance(_identity)
                .As<IScopeIdentityService>()
                .AsSelf();

            builder.RegisterInstance(this)
                .As<IScopeNode>()
                .AsSelf();

            if (_tickHub == null)
                _tickHub = GetComponent<IRuntimeTickHub>();
            if (_tickHub != null)
                builder.RegisterInstance(_tickHub).As<IRuntimeTickHub>();

            builder.RegisterInstance(new Game.Common.RandomVarianceControllerOptions(
                    512,
                    Game.Common.VarianceSettings.Default))
                .AsSelf();

            builder.Register<Game.Common.RandomVarianceController>(RuntimeLifetime.Singleton)
                .As<Game.Common.IRandomVarianceController>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<Game.Common.DynamicCounterController>(RuntimeLifetime.Singleton)
                .As<Game.Common.IDynamicCounterController>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void InstallLocalFeatures(IRuntimeContainerBuilder builder)
        {
            var components = ListPool<Component>.Get();
            try
            {
                GetComponents(components);
                for (int i = 0; i < components.Count; i++)
                {
                    if (components[i] is not IScopeInstaller installer)
                        continue;

                    installer.InstallScopeServices(builder, this);
                }
            }
            finally
            {
                ListPool<Component>.Release(components);
            }
        }

        bool ShouldUseVerifiedCompositionRuntime()
        {
            return VerifiedCompositionRuntime.IsActive;
        }

        bool ShouldRejectLegacyFeatureInstallerProjection()
        {
            return ShouldUseVerifiedCompositionRuntime() && !AllowsLegacyFeatureInstallerProjectionWhenVerifiedComposition;
        }

        void TryUpdateVerifiedRuntimeScopeState(int nextState)
        {
            if (!ShouldUseVerifiedCompositionRuntime())
                return;

            VerifiedCompositionRuntime.TryUpdateRuntimeScopeState(this, nextState);
        }

        void TryRefreshVerifiedRuntimeScopeUnityLink()
        {
            if (!ShouldUseVerifiedCompositionRuntime())
                return;

            VerifiedCompositionRuntime.TryRefreshRuntimeScopeUnityLink(this);
        }

        void ApplyIdentityFromComponent()
        {
            var identityMb = GetComponent<ScopeIdentityMB>();
            if (identityMb != null)
            {
                _activeIdentity = new RuntimeIdentityData
                {
                    Id = identityMb.id,
                    Category = identityMb.category,
                    Kind = identityMb.kind == LifetimeScopeKind.None ? LifetimeScopeKind.Runtime : identityMb.kind,
                    TimeScaleBehavior = identityMb.timeScaleBehavior,
                    InitiallyActive = identityMb.initiallyActive,
                    SelfTransform = transform,
                    Radius = identityMb.Radius,
                };
            }
            else
            {
                _activeIdentity = RuntimeIdentityData.CreateDefault(transform);
            }

            _isActive = _activeIdentity.InitiallyActive;
            _identity.Apply(_activeIdentity);
            _identity.IsActive = _isActive;
        }

        IScopeNode? GetParentCached()
        {
            if (_cachedParentResolved)
                return _cachedParent;

            _cachedParentResolved = true;
            _cachedParent = _explicitBuildParent;
            if (_cachedParent == null && !ShouldUseVerifiedCompositionRuntime())
                _cachedParent = ResolveParentCore();
            return _cachedParent;
        }

        IScopeNode? ResolveParentCore()
        {
            var current = transform.parent;
            while (current != null)
            {
                var scope = current.GetComponent<KernelScopeHost>();
                if (scope != null && scope != this && MatchesRequiredParent(scope))
                    return scope;

                current = current.parent;
            }

            return null;
        }

        bool MatchesRequiredParent(IScopeNode node)
        {
            if (RequiredParentKind == LifetimeScopeKind.None)
                return true;
            return node.Kind == RequiredParentKind;
        }

        void RegisterInHierarchy()
        {
            var parent = GetParentCached();
            ScopeNodeHierarchy.Register(this, parent);
            _hierarchyRegistered = true;
        }

        void UnregisterFromHierarchy()
        {
            if (!_hierarchyRegistered)
                return;

            ScopeNodeHierarchy.Unregister(this);
            _hierarchyRegistered = false;
        }

        void RefreshScopeRegistryRegistrationIfPossible()
        {
            if (_destroyed)
                return;

            if (!TryEnsureScopeRegistryResolved(out var registry) || registry == null)
                return;

            if (_isActive)
                registry.RegisterScope(this, _identity);
            else
                registry.UnregisterScope(this);
        }

        bool TryEnsureScopeRegistryResolved(out IBaseLifetimeScopeRegistry? registry)
        {
            if (_scopeRegistry != null)
            {
                registry = _scopeRegistry;
                return true;
            }

            var current = (IScopeNode?)this;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
                {
                    _scopeRegistry = resolved;
                    s_cachedRegistry = resolved;
                    registry = resolved;
                    return true;
                }
                current = current.Parent;
            }

            if (s_cachedRegistry != null)
            {
                _scopeRegistry = s_cachedRegistry;
                registry = s_cachedRegistry;
                return true;
            }

            registry = null;
            return false;
        }

        void UnregisterFromScopeRegistryIfNeeded()
        {
            if (_scopeRegistry != null)
            {
                _scopeRegistry.UnregisterScope(this);
                return;
            }

            if (s_cachedRegistry != null)
                s_cachedRegistry.UnregisterScope(this);
        }

        async UniTask ICoordinatedBuildScope.WaitForParentForBuildAsync()
        {
            var parent = GetParentCached();
            if (parent is ICoordinatedBuildScope coordinated && !ReferenceEquals(parent, this))
                await ScopeBuildCoordinator.WaitUntilBuiltAsync(coordinated, CancellationToken.None);
        }

        void ICoordinatedBuildScope.ExecuteBuildForCoordinator()
        {
            EnsureScopeBuilt();
        }

        void RefreshDebugViewer()
        {
            if (!this)
                return;

            var parent = _cachedParentResolved ? _cachedParent : null;
            debugParent = ToScopeDebugName(parent);
            debugParentHasResolver = parent?.Resolver != null;
            debugParentBuildStatus = GetParentBuildStatus(parent);
            debugPath = GetPathStringFromRoot(this);

            if (_destroyed)
                debugBuildStatus = "Destroyed";
            else if (_built)
                debugBuildStatus = "Built";
            else
                debugBuildStatus = "NotBuilt";
        }

        static string ToScopeDebugName(IScopeNode? node)
        {
            if (node == null)
                return "null";

            if (node is Component component)
                return component ? $"{node.GetType().Name}({component.gameObject.name})" : $"{node.GetType().Name}(Destroyed)";

            return node.GetType().Name;
        }

        static string GetParentBuildStatus(IScopeNode? node)
        {
            if (node == null)
                return "None";
            if (node is ICoordinatedBuildScope coordinated)
                return coordinated.IsBuildCompleted ? "Built" : "NotBuilt";
            return node.Resolver != null ? "Built" : "Unknown";
        }

        static string GetPathStringFromRoot(IScopeNode node)
        {
            var path = node.GetPathFromRoot();
            if (path == null || path.Count == 0)
                return string.Empty;

            var names = new List<string>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i] is Component component)
                    names.Add(component ? component.gameObject.name : "Destroyed");
                else
                    names.Add(path[i].GetType().Name);
            }
            return string.Join(" / ", names);
        }

        static class ScopeDespawnCoordinator
        {
            public static async UniTask DespawnAsync(KernelScopeHost scope, CancellationToken ct)
            {
                if (!scope)
                    return;

                if (scope.TryResolveLocal<IScopeLifecycleService>(out var lifecycle) && lifecycle != null)
                {
                    await lifecycle.HandleDespawnAsync(ct);
                    return;
                }

                scope.ReleaseIfNeeded();
                if (scope && scope.gameObject)
                    Destroy(scope.gameObject);
            }
        }
    }

    internal interface IVerifiedInstallerContributionHost
    {
        void InstallVerifiedInstallerContributions(IRuntimeContainerBuilder builder, IScopeNode owner);

        bool AcceptsVerifiedInstallerComponent(Component component);
    }

    internal static class RuntimeScopeContributionBridge
    {
        public static void InstallHostContributions(IRuntimeContainerBuilder builder, KernelScopeHost scope)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            if (scope.TryGetComponent<ProjectRootScopeServicesMB>(out var projectRoot))
                projectRoot.InstallProjectRootRuntime(builder, scope);

            if (scope.TryGetComponent<PlatformRootScopeServicesMB>(out var platformRoot))
                platformRoot.InstallPlatformRootRuntime(builder, scope);

            if (scope.TryGetComponent<SceneRootScopeServicesMB>(out var sceneRoot))
                sceneRoot.InstallSceneRootRuntime(builder, scope);
        }

        public static void InstallAcceptedAuthoringContributions(IRuntimeContainerBuilder builder, KernelScopeHost scope)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            CommandRuntimeInstaller.Install(builder, scope, scope.GetComponent<CommandRunnerAuthoring>());
            BlackboardRuntimeInstaller.Install(builder, scope, scope.GetComponent<BlackboardAuthoring>());

            if (scope.TryGetComponent<SceneFlowInstallerMB>(out var sceneFlow))
                sceneFlow.InstallSceneFlowRuntime(builder, scope);
        }

        public static void InstallVerifiedCompositionContributions(IRuntimeContainerBuilder builder, KernelScopeHost scope)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            InstallExplicitInstallerContribution(builder, scope, scope.GetComponent<ScopeIdentityMB>());
            InstallVerifiedInstallerContributionHosts(builder, scope);
        }

        public static void ThrowIfVerifiedRuntimeWouldUseLegacyInstallerProjection(KernelScopeHost scope)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            var components = ListPool<Component>.Get();
            try
            {
                scope.GetComponents(components);

                List<string>? rejectedComponents = null;
                for (int i = 0; i < components.Count; i++)
                {
                    if (components[i] is not IScopeInstaller)
                        continue;

                    if (IsAcceptedVerifiedInstallerProjection(scope, components[i]))
                        continue;

                    rejectedComponents ??= new List<string>();
                    rejectedComponents.Add(components[i].GetType().FullName ?? components[i].GetType().Name);
                }

                if (rejectedComponents == null || rejectedComponents.Count == 0)
                    return;

                throw new InvalidOperationException(
                    $"Verified composition runtime does not accept local IScopeInstaller projection on {scope.GetType().Name}. " +
                    $"Move registrations to an explicit contribution bridge. Rejected components: {string.Join(", ", rejectedComponents)}");
            }
            finally
            {
                ListPool<Component>.Release(components);
            }
        }

        internal static void InstallExplicitInstallerContribution<TInstaller>(IRuntimeContainerBuilder builder, IScopeNode owner, TInstaller? installer)
            where TInstaller : Component, IScopeInstaller
        {
            if (installer == null)
                return;

            installer.InstallScopeServices(builder, owner);
        }

        static bool IsAcceptedVerifiedInstallerProjection(KernelScopeHost scope, Component component)
        {
            if (component is ScopeIdentityMB)
                return true;

            var hosts = ListPool<MonoBehaviour>.Get();
            try
            {
                scope.GetComponents(hosts);
                for (int index = 0; index < hosts.Count; index++)
                {
                    if (hosts[index] is not IVerifiedInstallerContributionHost host)
                        continue;

                    if (host.AcceptsVerifiedInstallerComponent(component))
                        return true;
                }

                return false;
            }
            finally
            {
                ListPool<MonoBehaviour>.Release(hosts);
            }
        }

        static void InstallVerifiedInstallerContributionHosts(IRuntimeContainerBuilder builder, KernelScopeHost scope)
        {
            var hosts = ListPool<MonoBehaviour>.Get();
            try
            {
                scope.GetComponents(hosts);
                for (int index = 0; index < hosts.Count; index++)
                {
                    if (hosts[index] is not IVerifiedInstallerContributionHost host)
                        continue;

                    host.InstallVerifiedInstallerContributions(builder, scope);
                }
            }
            finally
            {
                ListPool<MonoBehaviour>.Release(hosts);
            }
        }
    }
}



