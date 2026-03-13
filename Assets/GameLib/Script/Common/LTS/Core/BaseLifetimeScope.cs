#nullable enable annotations
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using Game.Common;
namespace Game
{
    public interface IFeatureInstaller
    {
        void InstallFeature(IContainerBuilder builder, IScopeNode scope);
    }
    // 親の型を TParent に固定したバージョン
    public abstract class BaseLifetimeScope<TParent> : BaseLifetimeScope
        where TParent : LifetimeScope
    {
        protected override LifetimeScope ResolveParentForBuildCore()
        {
            // 1) Prefer nearest parent in Transform hierarchy.
            var current = transform.parent;
            while (current != null)
            {
                var scope = current.GetComponent<TParent>();
                if (scope != null && scope != this)
                    return scope;
                current = current.parent;
            }

            // FindObjectsByType はシーン上の実体を拾いやすく、アセット混入が起きにくい
            var all = UnityEngine.Object.FindObjectsByType<TParent>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var parent in all)
            {
                if (parent != null && parent != this)
                    return parent;
            }

            // 見つからなければ通常の親解決
            return base.ResolveParentForBuildCore();
        }
    }
    /// <summary>
    /// すべての LifetimeScope の共通基底。
    /// - FeatureInstallerLifetimeScope の機能を継承
    /// - 親→子の協調ビルド
    /// - LifetimeScope 階層のパス計算
    /// </summary>
    [RequireComponent(typeof(LTSIdentityMB))]
    public abstract class BaseLifetimeScope : LifetimeScope, IScopeNode, ICoordinatedBuildScope
    {
        [Header("Build Coordination")]
        [Tooltip("協調ビルドシステムに参加するか。false にすると通常の LifetimeScope として動く。")]
        // 基本すべての LTS は協調ビルドに参加する想定
        bool useBuildCoordinator = true;

        [Tooltip("このスコープを協調ビルドの Root とみなすか")]
        [ShowIf(nameof(useBuildCoordinator))]
        bool isBuildRoot = false;

        [Tooltip("Root の場合、Awake 時に自動でビルドをスケジュールするか。")]
        [ShowIf(nameof(ShowAutoBuildOnAwake))]
        bool autoBuildOnAwake = true;

        [Header("Game Logic Root")]
        [Tooltip("ゲームロジック上の Root として扱うか。ActorSource で GameLogicRoot を選ぶとこれが探索対象になる。")]
        [ToggleLeft]
        [SerializeField]
        bool useAsGameLogicRoot = false;

        [Header("Parent Resolution Override")]
        [ShowIf(nameof(overrideParentByType))]
        bool overrideParentByType = false;

        // Odin の TypeFilter で LifetimeScope 派生型だけ選択可能
        [ShowIf(nameof(overrideParentByType)), TypeFilter(nameof(LifetimeScopeTypes))]
        Type? parentScopeType = null;

        bool _destroyed;
        bool _acquired;
        IScopeAcquireReleaseDispatcher? _acquireRelease;
        internal bool IsBuildCompleted { get; private set; }

        // ---- 親キャッシュ（超重要：一度だけ解決してキャッシュ） ----
        LifetimeScope _cachedBuildParent;
        bool _cachedBuildParentResolved;

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

        internal string DebugBuildStatus => debugBuildStatus;

        /// <summary>親を一度だけ解決してキャッシュする。全ての親参照はここを経由する。</summary>
        protected LifetimeScope GetBuildParentCached()
        {
            if (_cachedBuildParentResolved) return _cachedBuildParent;
            _cachedBuildParentResolved = true;
            _cachedBuildParent = ResolveParentForBuildCore();
            return _cachedBuildParent;
        }

        /// <summary>インスペクタの値を上書きしたい場合は派生クラスでオーバーライド。</summary>
        protected virtual bool UseBuildCoordinator => useBuildCoordinator;
        protected virtual bool IsBuildRoot => isBuildRoot;

        protected virtual bool AutoBuildOnAwake => autoBuildOnAwake;
        public bool UseAsGameLogicRoot => useAsGameLogicRoot;

        // autoBuildOnAwake を表示する条件
        bool ShowAutoBuildOnAwake => useBuildCoordinator && isBuildRoot;
#if UNITY_EDITOR
        // TypeFilter で使う：LifetimeScope を継承した型だけ候補に出す
        private static IEnumerable<Type> LifetimeScopeTypes()
        {
            foreach (var t in typeof(LifetimeScope).Assembly.GetTypes())
            {
                if (t.IsAbstract)
                    continue;
                if (!typeof(LifetimeScope).IsAssignableFrom(t))
                    continue;

                yield return t;
            }
        }
#else
        private static IEnumerable<Type> LifetimeScopeTypes()
        {
            yield break;
        }
#endif
        [Header("Feature Installers")]
        [SerializeField] bool includeFeatureInstallers = true;
        [SerializeField] bool includeInactiveFeatureInstallers = true;

        [Header("Scope State")]
        [LabelText("Initially Visible")]
        [SerializeField]
        bool initiallyVisible = true;

        bool _isVisible = true;
        bool _isActive = true;
        bool _suppressGameObjectActiveSync;


        public LifetimeScopeKind GetLifetimeScopeKind()
        {
            var identity = GetComponent<LTSIdentityMB>();
            if (identity != null)
            {
                return identity.kind;
            }
            return LifetimeScopeKind.None;
        }

        /// <summary>
        /// 派生クラスで親解決ロジックをカスタマイズするためのメソッド。
        /// GetBuildParentCached() から一度だけ呼ばれる。
        /// </summary>
        protected virtual LifetimeScope ResolveParentForBuildCore()
        {
            // 1) 型指定の親を優先
            var typeParent = ResolveParentByType();
            if (typeParent != null)
                return typeParent;

            // 2) ヒエラルキーを遡って最も近い BaseLifetimeScope を探す
            var hierarchyParent = FindParentInHierarchy();
            if (hierarchyParent != null)
                return hierarchyParent;

            // 3) 何も見つからなければ従来どおり Transform/ParentReference を使う
            return FindParent();
        }

        // ================================================================================
        // IScopeNode Implementation (Phase0)
        // ================================================================================

        public new IScopeNode? Parent => GetParentScope();

        public ILTSIdentityService? Identity
        {
            get
            {
                var container = Container;
                if (container != null && container.TryResolve<ILTSIdentityService>(out var identity))
                {
                    return identity;
                }
                return null;
            }
        }

        public LifetimeScopeKind Kind => Identity?.Kind ?? GetLifetimeScopeKind();

        public IObjectResolver? Resolver => Container;

        public bool IsVisible => _isVisible;

        public bool IsActive => _isActive;

        public bool TrySetVisible(bool visible, bool isReset = false)
        {
            _isVisible = visible;
            return true;
        }

        public bool TrySetActive(bool active, bool isReset = false)
        {
            // Sync API: best-effort immediate change.
            // If callers need cancellation or ordering, use SetActiveAsync.
            if (_destroyed)
                return false;

            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }

            SyncActiveFromGameObject(isReset);
            return true;
        }

        public UniTask SetActiveAsync(bool active, bool isReset = false, CancellationToken ct = default)
        {
            if (_destroyed)
                return UniTask.CompletedTask;

            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }

            SyncActiveFromGameObject(isReset);

            return UniTask.CompletedTask;
        }

        IReadOnlyList<IScopeNode>? IScopeNode.GetPathFromRoot()
        {
            var path = GetPathFromRoot();
            if (path == null || path.Count == 0)
                return null;

            var nodes = new List<IScopeNode>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                nodes.Add(path[i]);
            }
            return nodes;
        }

        /// <summary>
        /// Transformヒエラルキーを遡って最も近いBaseLifetimeScopeを探す
        /// </summary>
        BaseLifetimeScope FindParentInHierarchy()
        {
            var current = transform.parent;
            while (current != null)
            {
                var scope = current.GetComponent<BaseLifetimeScope>();
                if (scope != null && scope != this)
                    return scope;
                current = current.parent;
            }
            return null;
        }

        LifetimeScope ResolveParentByType()
        {
            if (!overrideParentByType)
                return null;

            var t = parentScopeType;
            if (t == null)
                return null;

            if (!typeof(LifetimeScope).IsAssignableFrom(t))
                return null;

            // FindObjectsByType はシーン上の実体を拾い、アセット混入が起きにくい
            var all = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in all)
            {
                if (obj is LifetimeScope scope && scope != this)
                    return scope;
            }

            return null;
        }

        protected override void Awake()
        {
            debugAwakeFrame = Time.frameCount;
            debugAwakeRealtime = Time.realtimeSinceStartup;

            // Initialize scope state before Build.
            var identityMb = GetComponent<LTSIdentityMB>();
            _isActive = identityMb != null ? identityMb.initiallyActive : true;
            _isVisible = initiallyVisible;

            SyncActiveFromGameObject(isReset: true);

            // 協調ビルドに参加するスコープは autoRun を殺して、必ず Coordinator 経由で Build する
            if (UseBuildCoordinator)
            {
                autoRun = false;
            }

            // 親子関係の登録は base.Awake() (Build) の前に行う
            RegisterInHierarchy();
            RefreshDebugViewer();

            base.Awake();

            if (UseBuildCoordinator)
            {
                ScopeBuildCoordinator.Register(this, IsBuildRoot && AutoBuildOnAwake);
            }
        }

        void OnEnable()
        {
            SyncActiveFromGameObject(isReset: false);
            RefreshDebugViewer();
        }

        void OnDisable()
        {
            SyncActiveFromGameObject(isReset: false);
            RefreshDebugViewer();
        }

        protected override void OnDestroy()
        {
            _destroyed = true;
            RefreshDebugViewer();

            if (UseBuildCoordinator)
            {
                ScopeBuildCoordinator.Unregister(this);
            }

            UnregisterFromHierarchy();

            ReleaseIfNeeded();
            base.OnDestroy();
        }

        public bool IsAcquired => _acquired;

        public void AcquireIfNeeded()
        {
            if (_destroyed || _acquired || !_isActive)
                return;

            var container = Container;
            if (container == null)
                return;

            if (_acquireRelease == null &&
                container.TryResolve<IScopeAcquireReleaseDispatcher>(out var ar) &&
                ar != null)
            {
                _acquireRelease = ar;
            }

            _acquired = true;
            _acquireRelease?.Acquire(this, isReset: false);
        }

        public void ReleaseIfNeeded()
        {
            if (!_acquired)
                return;

            _acquired = false;

            var container = Container;
            if (container == null)
                return;

            if (_acquireRelease == null &&
                container.TryResolve<IScopeAcquireReleaseDispatcher>(out var ar) &&
                ar != null)
            {
                _acquireRelease = ar;
            }

            _acquireRelease?.Release(this, isReset: false);
        }

        protected virtual bool GetActiveStateFromGameObject()
        {
            return gameObject.activeInHierarchy;
        }

        void SyncActiveFromGameObject(bool isReset)
        {
            if (_destroyed || _suppressGameObjectActiveSync)
                return;

            var active = GetActiveStateFromGameObject();
            ApplyActiveState(active, isReset);
        }

        void ApplyActiveState(bool active, bool isReset)
        {
            _ = isReset;
            if (_isActive == active)
            {
                if (active && IsBuildCompleted)
                    AcquireIfNeeded();
                return;
            }

            _isActive = active;

            var identity = Identity;
            if (identity != null)
                identity.IsActive = active;

            if (active)
            {
                if (IsBuildCompleted)
                    AcquireIfNeeded();
            }
            else
            {
                ReleaseIfNeeded();
            }
        }

        // ---- DI登録入口 ----

        /// <summary>
        /// VContainer の Configure を封じて、派生クラスには ConfigureBase だけ書かせる。
        /// FeatureInstaller の Install もここで行う。
        /// </summary>
        protected sealed override void Configure(IContainerBuilder builder)
        {


            // IScopeMultiRegistry は全スコープで必ずローカル登録
            // これにより親探索を防ぎ、各スコープで独立したレジストリを持つ
            builder.Register<ScopeMultiRegistry>(Lifetime.Singleton)
                   .As<IScopeMultiRegistry>();

            builder.Register<ScopeAcquireReleaseDispatcher>(Lifetime.Singleton)
                .As<IScopeAcquireReleaseDispatcher>();

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

            // Awake 後の初期化処理をここで行う またはFeatureInstaller の前に行いたい処理
            AwakeConfigure(builder);

            // まずこのスコープが所有する IFeatureInstaller をインストール
            if (includeFeatureInstallers)
            {
                ScopeFeatureInstallerUtility.InstallOwnedFeatureInstallers(
                    this,
                    includeInactiveFeatureInstallers,
                    builder,
                    this);
            }

            // 派生ごとのサービス登録
            ConfigureBase(builder);

            // Build 完了フラグ＋Coordinator 通知（次フレームで実行して子の登録を待つ）
            builder.RegisterBuildCallback(resolver =>
            {
                IsBuildCompleted = true;
                debugBuildFrame = Time.frameCount;
                debugBuildRealtime = Time.realtimeSinceStartup;
                debugBuildDelayFrames = debugAwakeFrame >= 0 ? debugBuildFrame - debugAwakeFrame : -1;
                debugBuildStatus = "Built";

                _acquireRelease = resolver.TryResolve<IScopeAcquireReleaseDispatcher>(out var ar) ? ar : null;

                // Keep identity state in sync with this scope state.
                if (resolver.TryResolve<ILTSIdentityService>(out var idSvc) && idSvc != null)
                    idSvc.IsActive = _isActive;

                // If a lifecycle service is registered *locally*, run spawn handling.
                // IMPORTANT: Do NOT resolve lifecycle from parent scopes. Children must not start parent's lifecycle.
                // Use IScopeMultiRegistry to enforce local-only resolution.
                IScopeLifecycleService lifecycle = null;
                if (resolver.TryResolve<IScopeMultiRegistry>(out var registry) && registry != null)
                {
                    registry.TryGetSingle<IScopeLifecycleService>(out lifecycle);
                }

                if (lifecycle != null)
                {
                    if (_isActive)
                    {
                        // Match spawn path: acquire then run lifecycle spawn asynchronously.
                        UniTask.Void(async () =>
                        {
                            try
                            {
                                AcquireIfNeeded();
                                await lifecycle.HandleSpawnAsync(System.Threading.CancellationToken.None);
                            }
                            catch (Exception)
                            {
                            }
                        });
                    }
                }
                else
                {
                    if (_isActive)
                        AcquireIfNeeded();
                }

                // UseBuildCoordinator でなくても子への通知は行う
                if (!_destroyed)
                {
                    // 同じフレーム内で他のスコープの Awake/RegisterInHierarchy が完了するのを待つ
                    UniTask.Void(async () =>
                    {
                        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                        if (!_destroyed)
                        {
                            ScopeBuildCoordinator.NotifyBuilt(this);
                        }
                    });
                }

                RefreshDebugViewer();
            });
        }

        protected virtual void AwakeConfigure(IContainerBuilder builder)
        {
            // 何もしない。派生クラスで必要ならオーバーライド。
        }

        /// <summary>
        /// 派生クラスはここに通常の Configure 処理を書く。
        /// </summary>
        protected abstract void ConfigureBase(IContainerBuilder builder);

        // ---- 親解決 / 階層登録 ----

        void RegisterInHierarchy()
        {
            var parentNode = GetBuildParentCached() as IScopeNode;
            ScopeNodeHierarchy.Register(this, parentNode);

            // 親が既にビルド済みの場合、後から登録された子は自動でスケジュールされないため
            // ここで改めて子自身のビルドをスケジュールする。
            if (UseBuildCoordinator && (Resolver == null && !IsBuildCompleted) && parentNode is ICoordinatedBuildScope parentCoord)
            {
                var parentBuilt = parentCoord.IsBuildCompleted || parentNode.Resolver != null;
                if (parentBuilt)
                {
                    ScopeBuildCoordinator.Register(this, autoBuild: false);
                    UniTask.Void(async () =>
                    {
                        await ScopeBuildCoordinator.WaitUntilBuiltAsync(this, CancellationToken.None);
                    });
                }
            }
            RefreshDebugViewer();
        }

        void UnregisterFromHierarchy()
        {
            ScopeNodeHierarchy.Unregister(this);
            RefreshDebugViewer();
        }

        BaseLifetimeScope GetParentScope()
        {
            return GetBuildParentCached() as BaseLifetimeScope;
        }

        void RefreshDebugViewer()
        {
            if (!this)
                return;

            var parentNode = Parent;
            debugParent = ToScopeDebugName(parentNode);
            debugParentHasResolver = parentNode?.Resolver != null;
            debugParentBuildStatus = GetParentBuildStatus(parentNode);
            debugPath = GetPathStringFromRoot(this);

            if (_destroyed)
                debugBuildStatus = "Destroyed";
            else if (IsBuildCompleted)
                debugBuildStatus = "Built";
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

        // ---- ビルド API ----

        /// <summary>
        /// このスコープがビルド完了するまで待つ（協調ビルド参加時のみ意味がある）。
        /// </summary>
        public UniTask WhenBuiltAsync(CancellationToken token = default)
        {
            if (!UseBuildCoordinator)
                return UniTask.CompletedTask;

            return ScopeBuildCoordinator.WaitUntilBuiltAsync(this, token);
        }

        /// <summary>
        /// 親を含め、このスコープを同期的にビルドする。
        /// Root から叩けば親→子の順でビルドされる。
        /// </summary>
        public void EnsureScopeBuilt()
        {
            if (!UseBuildCoordinator)
            {
                if (Container == null)
                {
                    Build();
                }
                return;
            }

            BuildSynchronously(this);
        }

        async UniTask WaitForParentAsync()
        {
            var parent = GetBuildParentCached();

            if (parent is BaseLifetimeScope parentBase &&
                parentBase.UseBuildCoordinator &&
                parentBase != this)
            {
                // 親も協調ビルド参加なら、親の Build 完了を待つ
                await ScopeBuildCoordinator.WaitUntilBuiltAsync(parentBase, CancellationToken.None);
                return;
            }

            if (parent != null && parent != this)
            {
                // 通常 LifetimeScope の親なら、Container が立つまで待つ
                while (parent && parent.Container == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
        }

        bool ICoordinatedBuildScope.UseBuildCoordinator => UseBuildCoordinator;
        bool ICoordinatedBuildScope.IsBuildCompleted => IsBuildCompleted;
        UniTask ICoordinatedBuildScope.WaitForParentForBuildAsync() => WaitForParentAsync();
        void ICoordinatedBuildScope.ExecuteBuildForCoordinator() => ExecuteBuild(this);

        static void ExecuteBuild(BaseLifetimeScope scope)
        {
            if (!scope)
                return;

            var go = scope.gameObject;
            var wasActiveSelf = go.activeSelf;
            var wasActiveInHierarchy = go.activeInHierarchy;

            if (!wasActiveInHierarchy)
            {
                scope._suppressGameObjectActiveSync = true;
                go.SetActive(true);

                if (scope.Container != null)
                {
                    if (!wasActiveSelf)
                    {
                        go.SetActive(false);
                    }
                    scope._suppressGameObjectActiveSync = false;
                    scope.SyncActiveFromGameObject(isReset: true);
                    return;
                }
            }

            // VContainer の親を明示的に設定（EnqueueParent を使用）
            var parent = scope.GetBuildParentCached();
            if (parent != null && parent != scope)
            {
                using (EnqueueParent(parent))
                {
                    scope.Build();
                }
            }
            else
            {
                scope.Build();
            }

            if (!wasActiveSelf && scope)
            {
                go.SetActive(false);
            }

            if (scope)
            {
                scope._suppressGameObjectActiveSync = false;
                scope.SyncActiveFromGameObject(isReset: true);
            }
        }

        static void BuildSynchronously(BaseLifetimeScope scope)
        {
            if (!scope || scope.Container != null)
                return;

            var parent = scope.GetBuildParentCached();

            if (parent is BaseLifetimeScope parentBase &&
                parentBase.UseBuildCoordinator &&
                parentBase != scope)
            {
                // 親も協調ビルド参加 → 親を先に Ensure
                parentBase.EnsureScopeBuilt();
            }
            else if (parent != null && parent != scope && parent.Container == null)
            {
                // 通常 LifetimeScope の親なら単純に Build
                parent.Build();
            }

            if (scope.Container == null)
            {
                ExecuteBuild(scope);
            }
        }

        // ---- 階層パス API ----

        /// <summary>
        /// this から target までのパスを計算する。
        /// - target が祖先なら this→…→ancestor
        /// - target が子孫なら this→…→descendant
        /// 双方向 BFS ではなく、まず上方向、ダメなら子方向に DFS/BFS。
        /// </summary>
        public bool TryGetPathTo(BaseLifetimeScope target,
                                 out List<BaseLifetimeScope> path,
                                 bool includeSelf = true)
        {
            path = null;
            if (!target)
                return false;

            if (ReferenceEquals(this, target))
            {
                path = includeSelf
                    ? new List<BaseLifetimeScope> { this }
                    : new List<BaseLifetimeScope>();
                return true;
            }

            // 1) 祖先方向 this → parent → parent...
            {
                var current = includeSelf ? this : GetParentScope();
                var list = new List<BaseLifetimeScope>();

                while (current != null)
                {
                    list.Add(current);

                    if (ReferenceEquals(current, target))
                    {
                        // this から target まで： [this, ..., target]
                        path = list;
                        return true;
                    }

                    current = current.GetParentScope();
                }
            }

            // 2) 子孫方向 this から BFS
            {
                var visited = new HashSet<BaseLifetimeScope>();
                var queue = new Queue<BaseLifetimeScope>();
                var parentMap = new Dictionary<BaseLifetimeScope, BaseLifetimeScope>();

                queue.Enqueue(this);
                visited.Add(this);
                parentMap[this] = null;

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();

                    if (ReferenceEquals(node, target))
                    {
                        // parentMap から this→...→target を復元
                        var stack = new Stack<BaseLifetimeScope>();
                        var n = node;
                        while (n != null)
                        {
                            stack.Push(n);
                            parentMap.TryGetValue(n, out n);
                        }

                        path = new List<BaseLifetimeScope>();

                        if (!includeSelf && stack.Count > 0 && ReferenceEquals(stack.Peek(), this))
                        {
                            stack.Pop(); // 先頭の this を落とす
                        }

                        while (stack.Count > 0)
                        {
                            path.Add(stack.Pop());
                        }

                        return true;
                    }

                    var children = ScopeNodeHierarchy.GetChildrenOrEmpty(node);
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i] is not BaseLifetimeScope child)
                            continue;
                        if (!child || visited.Contains(child))
                            continue;

                        visited.Add(child);
                        queue.Enqueue(child);
                        parentMap[child] = node;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// this から target までのパスを "C2 - B2 - A1" のような文字列にする。
        /// 見た目に使う用。
        /// </summary>
        public string BuildPathString(BaseLifetimeScope target,
                                      string separator = " - ")
        {
            if (!TryGetPathTo(target, out var scopes, includeSelf: true) ||
                scopes == null || scopes.Count == 0)
            {
                return string.Empty;
            }

            // デフォルトは GameObject 名を使う。必要なら override して別の名前を返すメソッドを用意してもいい。
            var names = new List<string>(scopes.Count);
            for (int i = 0; i < scopes.Count; i++)
            {
                names.Add(scopes[i].gameObject.name);
            }

            return string.Join(separator, names);
        }

        /// <summary>
        /// ツリーの最上位 BaseLifetimeScope から this までのパスを取得する。
        /// </summary>
        public IReadOnlyList<BaseLifetimeScope> GetPathFromRoot()
        {
            var stack = new Stack<BaseLifetimeScope>();
            var current = this;

            while (current != null)
            {
                stack.Push(current);
                current = current.GetParentScope();
            }

            var list = new List<BaseLifetimeScope>(stack.Count);
            while (stack.Count > 0)
            {
                list.Add(stack.Pop());
            }

            return list;
        }

        public string GetPathFromRootString(string separator = " / ")
        {
            var scopes = GetPathFromRoot();
            if (scopes == null || scopes.Count == 0)
                return string.Empty;

            var names = new List<string>(scopes.Count);
            for (int i = 0; i < scopes.Count; i++)
            {
                names.Add(scopes[i].gameObject.name);
            }

            return string.Join(separator, names);
        }
        // BaseLifetimeScope 
        public UniTask DespawnAsync(CancellationToken ct = default)
        {
            return ScopeDespawnCoordinator.DespawnAsync(this, ct);
        }

        static class ScopeDespawnCoordinator
        {
            public static async UniTask DespawnAsync(BaseLifetimeScope scope, CancellationToken ct)
            {
                if (!scope)
                    return;

                var container = scope.Container;
                if (container != null &&
                    container.TryResolve<IScopeMultiRegistry>(out var registry) &&
                    registry != null &&
                    registry.TryGetSingle<IScopeLifecycleService>(out var lifecycle) &&
                    lifecycle != null)
                {
                    await lifecycle.HandleDespawnAsync(ct);
                }
                else
                {
                    // Lifecycle の設定がない Scope は即時 Destroy
                    if (scope && scope.gameObject)
                    {
                        scope.ReleaseIfNeeded();
                        UnityEngine.Object.Destroy(scope.gameObject);
                    }
                }
            }
        }
        // 協調ビルド実装は ScopeBuildCoordinator へ移動。
    }
}
