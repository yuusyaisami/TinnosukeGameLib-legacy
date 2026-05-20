#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Pool;

namespace Game
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LTSIdentityMB))]
    [RequireComponent(typeof(RuntimeTickHub))]
    public abstract class RuntimeLifetimeScopeBase : MonoBehaviour, IScopeNode, ICoordinatedBuildScope
    {
        static IBaseLifetimeScopeRegistry? s_cachedRegistry;

        [Header("Feature Installers")]
        [SerializeField] bool includeFeatureInstallers = true;
        [SerializeField] bool includeInactiveFeatureInstallers = true;

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
        List<IFeatureInstaller>? _ownedFeatureInstallers;
        bool _ownedFeatureInstallersCached;
        bool _ownedFeatureInstallersIncludeInactive;

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
        public ILTSIdentityService? Identity => _identity;
        public LifetimeScopeKind Kind => _identity.Kind;
        public IRuntimeResolver? Resolver => _resolver;
        public bool IsVisible => _isVisible;
        public bool IsActive => _isActive;

        protected virtual bool UseBuildCoordinator => true;
        protected virtual bool IsBuildRoot => false;
        protected virtual bool AutoBuildOnAwake => false;
        protected virtual LifetimeScopeKind RequiredParentKind => LifetimeScopeKind.None;
        protected virtual bool RequiresParentScope => RequiredParentKind != LifetimeScopeKind.None;

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
            if (parent is RuntimeLifetimeScopeBase runtimeParent && !ReferenceEquals(runtimeParent, this))
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
        }

        public void ReleaseIfNeeded()
        {
            if (!_acquired)
                return;

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

            var builder = new RuntimeContainerBuilder();
            builder.SetHostScope(this);

            ConfigureCore(builder);
            AwakeConfigure(builder);

            if (includeFeatureInstallers)
                InstallFeatures(builder);

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

            RefreshScopeRegistryRegistrationIfPossible();
            RefreshDebugViewer();
            _buildCompletionSource?.TrySetResult();
            ScopeBuildCoordinator.NotifyBuilt(this);

            if (_isActive)
                AcquireIfNeeded();
        }

        void ConfigureCore(IRuntimeContainerBuilder builder)
        {
            builder.Register<ScopeMultiRegistry>(RuntimeLifetime.Singleton)
                .As<IScopeMultiRegistry>();

            builder.Register<ScopeAcquireReleaseDispatcher>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireReleaseDispatcher>();

            builder.RegisterInstance(_identity)
                .As<ILTSIdentityService>()
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

        void InstallFeatures(IRuntimeContainerBuilder builder)
        {
            CacheOwnedFeatureInstallers(includeInactiveFeatureInstallers);
            if (_ownedFeatureInstallers == null)
                return;

            for (int i = 0; i < _ownedFeatureInstallers.Count; i++)
                _ownedFeatureInstallers[i].InstallFeature(builder, this);
        }

        void CacheOwnedFeatureInstallers(bool includeInactive)
        {
            if (_ownedFeatureInstallersCached && _ownedFeatureInstallersIncludeInactive == includeInactive)
                return;

            _ownedFeatureInstallersIncludeInactive = includeInactive;
            _ownedFeatureInstallers ??= new List<IFeatureInstaller>(16);
            _ownedFeatureInstallers.Clear();

            var installers = ListPool<IFeatureInstaller>.Get();
            try
            {
                GetComponentsInChildren(includeInactive, installers);
                for (int i = 0; i < installers.Count; i++)
                {
                    var installer = installers[i];
                    if (installer is not Component component)
                        continue;

                    if (!ScopeFeatureInstallerUtility.TryGetNearestScopeNode(component, includeInactive, out var owner) || !ReferenceEquals(owner, this))
                        continue;

                    _ownedFeatureInstallers.Add(installer);
                }
            }
            finally
            {
                ListPool<IFeatureInstaller>.Release(installers);
            }

            _ownedFeatureInstallersCached = true;
        }

        void ApplyIdentityFromComponent()
        {
            var identityMb = GetComponent<LTSIdentityMB>();
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
            _cachedParent = _explicitBuildParent ?? ResolveParentCore();
            return _cachedParent;
        }

        IScopeNode? ResolveParentCore()
        {
            var current = transform.parent;
            while (current != null)
            {
                var scope = current.GetComponent<RuntimeLifetimeScopeBase>();
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
            public static async UniTask DespawnAsync(RuntimeLifetimeScopeBase scope, CancellationToken ct)
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

    [DisallowMultipleComponent]
    [RequireComponent(typeof(LTSIdentityMB))]
    [RequireComponent(typeof(RuntimeTickHub))]
    [RequireComponent(typeof(BlackboardMB))]
    [RequireComponent(typeof(CommandRunnerMB))]
    public class RuntimeLifetimeScope : RuntimeLifetimeScopeBase
    {
    }
}
