using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;
using UnityEngine;
using VContainer.Unity;
using VContainer;
namespace Game.Scalar
{
    /// <summary>
    /// ScalarKey ごとにランタイムを管琁E��、Mod パイプラインを通じて値を扱ぁEScalar サービス、E
    /// ローカルのみ(Local*) / 親フォールバック(Global*) を�E示皁E��使ぁE�Eける、E
    /// </summary>
    public class BaseScalarService :
        IBaseScalarService,
        IScalarTelemetry,
        IScopeTickHandler,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IProjectScalarService,
        IPlatformScalarService,
        IGlobalScalarService,
        ISceneScalarService,
        IFieldScalarService,
        IEntityScalarService,
        IUIScalarService,
        IUIElementScalarService,
        IRuntimeScalarService,
        IDisposable
    {
        // ================================================================
        // Subscription Types
        // ================================================================

        sealed class Subscription : IDisposable
        {
            readonly BaseScalarService _owner;
            readonly int _keyId; // 0 = all keys
            readonly ScalarValueChangedHandler _handler;
            bool _disposed;

            public int KeyId => _keyId;
            public ScalarValueChangedHandler Handler => _handler;
            public bool IsDisposed => _disposed;

            public Subscription(BaseScalarService owner, int keyId, ScalarValueChangedHandler handler)
            {
                _owner = owner;
                _keyId = keyId;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner?.RemoveSubscription(this);
            }
        }

        // ================================================================
        // Fields
        // ================================================================

        readonly LifetimeScopeKind _space;
        readonly IScopeNode _scope;
        readonly SimpleDynamicContext _dynamicContext;
        readonly IScalarRuntimeConfigProvider _configProvider;
        readonly Dictionary<int, ScalarKeyRuntime> _runtimes = new();

        // Subscriptions
        readonly List<Subscription> _subscriptions = new();
        readonly Dictionary<int, List<Subscription>> _keySubscriptions = new();
        readonly List<Subscription> _allSubscriptions = new();

        // Value cache for change detection
        readonly Dictionary<int, float> _lastValues = new();

        BaseScalarService _nearestAncestorScalarServiceCache;
        bool _hasNearestAncestorScalarServiceCache;

        public BaseScalarService(
            IScopeNode scope,
            IScalarRuntimeConfigProvider configProvider)
        {
            _scope = scope;
            _dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, scope);
            _configProvider = configProvider;
            _space = scope != null ? scope.Kind : LifetimeScopeKind.None;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!isReset)
                return;

            InvalidateAncestorScalarCache();

            // Runtime scope は pool 再利用される前提なので、Acquire 時�E reset では
            // まぁEscalar の local runtime を完�Eに破棁E��る、E
            // ここで古ぁEbaseline / modifier / subscription が残ると、profile の再適用結果と
            // 実際に参�Eされる値が食い違うため、いったん完�E初期化する、E
            ResetForScopeReuse();

            // そ�E直後に profile binding を�E適用する、E
            // これは「profile が存在してぁE��のに watch では 0/null になる」問題を防ぐためで、E
            // Acquire/Install の頁E��差めE�E生�Eタイミングの差で baseline が抜け落ちても、E
            // 最終的に profile 定義の値が忁E�� local runtime に戻るよぁE��する、E
            ReapplyScopeBindingsIfAvailable();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!isReset)
                return;

            InvalidateAncestorScalarCache();
            ResetForScopeReuse();
        }

        public LifetimeScopeKind Space => _space;
        internal IDynamicContext DynamicContext => _dynamicContext;

        static bool IsValidKey(ScalarKey key) => key.Id > 0;

        void ReportInvalidKey(string operation, ScalarKey key)
        {
            Debug.LogError($"[Scalar] SCALAR_KEY_UNRESOLVED operation={operation} id={key.Id} name={key.Name ?? string.Empty}");
        }

        void ReportRequiredValueMissing(string operation, ScalarKey key)
        {
            Debug.LogError($"[Scalar] SCALAR_REQUIRED_VALUE_MISSING operation={operation} id={key.Id} name={key.Name ?? string.Empty}");
        }

        public ScalarKeyRuntime GetOrCreateRuntime(ScalarKey key)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(GetOrCreateRuntime), key);
                return null;
            }

            if (_runtimes.TryGetValue(key.Id, out var rt))
                return rt;

            ScalarRuntimeConfig cfg = null;
            if (_configProvider != null && _configProvider.TryGetConfig(key, out var c))
            {
                cfg = c; // 定義があるときだぁEconfig を持つ
            }

            rt = new ScalarKeyRuntime(key, cfg, () => MarkRuntimeDirty(key));
            _runtimes[key.Id] = rt;
            return rt;
        }

        bool TryGetLocalInternal(ScalarKey key, bool includeAllLayers, string layer, out float value)
        {
            if (!IsValidKey(key))
            {
                value = 0f;
                return false;
            }

            var rt = GetOrCreateRuntime(key);
            if (rt == null)
            {
                value = 0f;
                return false;
            }

            if (!rt.HasLocalData)
            {
                value = 0f;
                return false;
            }

            value = rt.Get(this, includeAllLayers, layer);
            return true;
        }

        public bool LocalTryGet(ScalarKey key, out float value)
            => TryGetLocalInternal(key, includeAllLayers: true, layer: null, out value);

        public float LocalGet(ScalarKey key)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(LocalGet), key);
                return 0f;
            }

            if (!TryGetLocalInternal(key, includeAllLayers: true, layer: null, out var v))
                ReportRequiredValueMissing(nameof(LocalGet), key);

            return v;
        }

        public float LocalGet(ScalarKey key, bool includeAllLayers, string layer = null)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(LocalGet), key);
                return 0f;
            }

            if (!TryGetLocalInternal(key, includeAllLayers, layer, out var v))
                ReportRequiredValueMissing(nameof(LocalGet), key);

            return v;
        }

        public bool GlobalTryGet(ScalarKey key, out float value)
            => TryGetGlobalInternal(key, includeAllLayers: true, layer: null, out value);

        public float GlobalGet(ScalarKey key)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(GlobalGet), key);
                return 0f;
            }

            if (!TryGetGlobalInternal(key, includeAllLayers: true, layer: null, out var v))
                ReportRequiredValueMissing(nameof(GlobalGet), key);

            return v;
        }

        public float GlobalGet(ScalarKey key, bool includeAllLayers, string layer = null)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(GlobalGet), key);
                return 0f;
            }

            if (!TryGetGlobalInternal(key, includeAllLayers, layer, out var v))
                ReportRequiredValueMissing(nameof(GlobalGet), key);

            return v;
        }

        bool TryGetGlobalInternal(ScalarKey key, bool includeAllLayers, string layer, out float value)
        {
            if (!IsValidKey(key))
            {
                value = 0f;
                return false;
            }

            if (TryGetLocalInternal(key, includeAllLayers, layer, out value))
                return true;

            var parentService = ResolveParentScalarService();
            if (parentService == null || ReferenceEquals(parentService, this))
            {
                value = 0f;
                return false;
            }

            return parentService.TryGetLocalInternal(key, includeAllLayers, layer, out value);
        }

        BaseScalarService ResolveParentScalarService()
        {
            if (_hasNearestAncestorScalarServiceCache)
                return _nearestAncestorScalarServiceCache;

            var parentScope = _scope?.Parent;
            if (parentScope?.Resolver == null)
            {
                _nearestAncestorScalarServiceCache = null;
                _hasNearestAncestorScalarServiceCache = true;
                return null;
            }

            if (parentScope.Resolver.TryResolve<IBaseScalarService>(out var svc) && svc is BaseScalarService baseSvc)
            {
                _nearestAncestorScalarServiceCache = baseSvc;
                _hasNearestAncestorScalarServiceCache = true;
                return baseSvc;
            }

            _nearestAncestorScalarServiceCache = null;
            _hasNearestAncestorScalarServiceCache = true;
            return null;
        }

        void InvalidateAncestorScalarCache()
        {
            _nearestAncestorScalarServiceCache = null;
            _hasNearestAncestorScalarServiceCache = false;
        }

        BaseScalarService ResolveServiceForGlobalKey(ScalarKey key, bool includeAllLayers, string layer)
        {
            if (TryGetLocalInternal(key, includeAllLayers, layer, out _))
                return this;

            var parentService = ResolveParentScalarService();
            if (parentService == null || ReferenceEquals(parentService, this))
                return this;

            return parentService;
        }

        bool HasLocalOwnership(ScalarKey key)
        {
            if (key.Id == 0)
                return true;

            if (_runtimes.TryGetValue(key.Id, out var rt))
                return rt != null && rt.HasLocalData;

            // Runtime hasn't been created yet, but a local config definition implies local ownership.
            if (_configProvider != null && _configProvider.TryGetConfig(key, out var cfg) && cfg != null)
                return true;

            return false;
        }

        BaseScalarService ResolveServiceForGlobalWrite(ScalarKey key)
        {
            if (key.Id != 0 && _runtimes.TryGetValue(key.Id, out var rt) && rt != null && rt.HasLocalOverride)
                return this;

            var parentService = ResolveParentScalarService();
            if (parentService == null || ReferenceEquals(parentService, this))
                return this;

            return parentService;
        }

        public float AddLocalBase(ScalarKey key, string layer, float delta)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(AddLocalBase), key);
                return 0f;
            }

            var rt = GetOrCreateRuntime(key);
            if (rt == null)
                return 0f;

            return rt.AddLocalBase(this, layer, delta);
        }

        public ScalarHandle LocalAdd(
            ScalarKey key,
            string layer,
            float delta,
            float duration = -1f,
            object source = null,
            string tag = null)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(LocalAdd), key);
                return null;
            }

            var rt = GetOrCreateRuntime(key);
            if (rt == null)
                return null;

            return rt.Add(this, layer, delta, duration, source, tag);
        }

        public ScalarHandle GlobalAdd(
            ScalarKey key,
            string layer,
            float delta,
            float duration = -1f,
            object source = null,
            string tag = null)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(GlobalAdd), key);
                return null;
            }

            var target = ResolveServiceForGlobalWrite(key);
            return target.LocalAdd(key, layer, delta, duration, source, tag);
        }

        public ScalarHandle LocalMul(
            ScalarKey key,
            string layer,
            float factor,
            ScalarMulPhase phase,
            float duration = -1f,
            object source = null,
            string tag = null)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(LocalMul), key);
                return null;
            }

            var rt = GetOrCreateRuntime(key);
            if (rt == null)
                return null;

            return rt.Mul(this, layer, factor, phase, duration, source, tag);
        }

        public ScalarHandle GlobalMul(
            ScalarKey key,
            string layer,
            float factor,
            ScalarMulPhase phase,
            float duration = -1f,
            object source = null,
            string tag = null)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(GlobalMul), key);
                return null;
            }

            var target = ResolveServiceForGlobalWrite(key);
            return target.LocalMul(key, layer, factor, phase, duration, source, tag);
        }

        public TMod ResolveMod<TMod>(ScalarKey key) where TMod : class, IScalarModifier
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(ResolveMod), key);
                return null;
            }

            var rt = GetOrCreateRuntime(key);
            if (rt == null)
                return null;

            return rt.ResolveModifier<TMod>();
        }

        public void SetLocalBase(ScalarKey key, float value)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(SetLocalBase), key);
                return;
            }

            var rt = GetOrCreateRuntime(key);
            if (rt == null)
                return;

            rt.SetLocalBase(value);
        }

        public void SetGlobalBase(ScalarKey key, float value)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(SetGlobalBase), key);
                return;
            }

            var target = ResolveServiceForGlobalWrite(key);
            target.SetLocalBase(key, value);
        }

        public void ClearAll(ScalarKey? key = null)
        {
            if (key.HasValue)
            {
                if (!IsValidKey(key.Value))
                {
                    ReportInvalidKey(nameof(ClearAll), key.Value);
                    return;
                }

                if (_runtimes.TryGetValue(key.Value.Id, out var runtime) && runtime != null)
                    runtime.InvalidateAllHandles();

                _runtimes.Remove(key.Value.Id);
                _lastValues.Remove(key.Value.Id);
                return;
            }

            foreach (var runtime in _runtimes.Values)
            {
                runtime?.InvalidateAllHandles();
            }

            _runtimes.Clear();
            _lastValues.Clear();
        }

        /// <summary>
        /// Ensure a runtime entry exists for the given key using the specified runtime config.
        /// If a runtime already exists, it will be replaced with a new instance backed by <paramref name="config"/>.
        /// </summary>
        public void EnsureRuntime(ScalarKey key, ScalarRuntimeConfig config)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(EnsureRuntime), key);
                return;
            }

            if (_runtimes.TryGetValue(key.Id, out var existingRuntime) && existingRuntime != null)
                existingRuntime.InvalidateAllHandles();

            var rt = new ScalarKeyRuntime(key, config, () => MarkRuntimeDirty(key));
            _runtimes[key.Id] = rt;
        }

        /// <summary>
        /// Ensure a runtime exists with the config and return the runtime for direct modification.
        /// </summary>
        public ScalarKeyRuntime EnsureAndGetRuntime(ScalarKey key, ScalarRuntimeConfig config)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(EnsureAndGetRuntime), key);
                return null;
            }

            if (_runtimes.TryGetValue(key.Id, out var existingRuntime) && existingRuntime != null)
                existingRuntime.InvalidateAllHandles();

            var rt = new ScalarKeyRuntime(key, config, () => MarkRuntimeDirty(key));
            _runtimes[key.Id] = rt;
            return rt;
        }

        /// <summary>
        /// Try resolve an existing runtime. Does not create one.
        /// </summary>
        public bool TryGetRuntime(ScalarKey key, out ScalarKeyRuntime runtime)
        {
            if (!IsValidKey(key))
            {
                runtime = null;
                return false;
            }
            return _runtimes.TryGetValue(key.Id, out runtime);
        }

        /// <summary>
        /// Convenience API: Set the baseline for an existing runtime or create one with the baseline.
        /// This ensures the runtime exists and sets the baseline.
        /// </summary>
        public void SetRuntimeBaseline(ScalarKey key, float baseline)
        {
            if (!IsValidKey(key))
            {
                ReportInvalidKey(nameof(SetRuntimeBaseline), key);
                return;
            }

            if (!_runtimes.TryGetValue(key.Id, out var rt))
            {
                // Create a minimal runtime to hold the baseline
                rt = new ScalarKeyRuntime(key, null, () => MarkRuntimeDirty(key));
                _runtimes[key.Id] = rt;
            }

            rt.SetBaseline(baseline, markAsFromConfig: false);
        }

        void MarkRuntimeDirty(ScalarKey key)
        {
            if (_runtimes.TryGetValue(key.Id, out var rt))
            {
                rt.ForceInvalidate();
            }
        }

        public void Tick()
        {
            if (_runtimes.Count == 0)
                return;

            float dt = Time.deltaTime;

            if (_subscriptions.Count == 0)
            {
                bool hasTimedEntries = false;
                foreach (var rt in _runtimes.Values)
                {
                    if (!rt.HasTimedEntries)
                        continue;

                    hasTimedEntries = true;
                    rt.Tick(dt);
                }

                if (!hasTimedEntries)
                    return;

                return;
            }

            foreach (var rt in _runtimes.Values)
            {
                rt.Tick(dt);
            }

            // Check for value changes and fire events
            CheckAndFireValueChangedEvents();
        }

        // ================================================================
        // Subscription API
        // ================================================================

        public IDisposable LocalSubscribe(ScalarKey key, ScalarValueChangedHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var sub = new Subscription(this, key.Id, handler);
            _subscriptions.Add(sub);

            if (!_keySubscriptions.TryGetValue(key.Id, out var list))
            {
                list = new List<Subscription>();
                _keySubscriptions[key.Id] = list;
            }
            list.Add(sub);

            // Initialize last value cache if not present
            if (!_lastValues.ContainsKey(key.Id))
            {
                _lastValues[key.Id] = LocalGet(key);
            }

            return sub;
        }

        public IDisposable GlobalSubscribe(ScalarKey key, ScalarValueChangedHandler handler)
        {
            var target = ResolveServiceForGlobalKey(key, includeAllLayers: true, layer: null);
            return target.LocalSubscribe(key, handler);
        }

        public IDisposable LocalSubscribeAll(ScalarValueChangedHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var sub = new Subscription(this, 0, handler); // keyId=0 means all keys
            _subscriptions.Add(sub);
            _allSubscriptions.Add(sub);

            return sub;
        }

        void RemoveSubscription(Subscription sub)
        {
            _subscriptions.Remove(sub);

            if (sub.KeyId == 0)
            {
                _allSubscriptions.Remove(sub);
            }
            else if (_keySubscriptions.TryGetValue(sub.KeyId, out var list))
            {
                list.Remove(sub);
                if (list.Count == 0)
                {
                    _keySubscriptions.Remove(sub.KeyId);
                }
            }
        }

        void ResetForScopeReuse()
        {
            InvalidateAncestorScalarCache();
            ClearAll();
            _subscriptions.Clear();
            _keySubscriptions.Clear();
            _allSubscriptions.Clear();
        }

        void ReapplyScopeBindingsIfAvailable()
        {
            if (_scope?.Resolver == null)
                return;

            // ScopeBindingRegistry は profile 定義の実値めEscalar/blackboard に流し込む責務を持つ、E
            // scalar 側は reset で runtime を消すため、registry を�E実行しなぁE��
            // ProfileFloatValue の Default Value / UpdateBaseline が反映されなぁE��E
            if (_scope.Resolver.TryResolve<IScopeBindingRegistry>(out var registry) && registry is ScopeBindingRegistryService scopeRegistry)
            {
                scopeRegistry.ReapplyAllBindings();
            }
        }

        void CheckAndFireValueChangedEvents()
        {
            if (_subscriptions.Count == 0) return;

            // Check all monitored keys
            foreach (var kvp in _keySubscriptions)
            {
                int keyId = kvp.Key;
                if (!_runtimes.TryGetValue(keyId, out var rt)) continue;

                var key = rt.Key;
                float currentValue = LocalGet(key);

                if (!_lastValues.TryGetValue(keyId, out float lastValue))
                {
                    lastValue = 0f;
                }

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (currentValue != lastValue)
                {
                    _lastValues[keyId] = currentValue;
                    FireValueChanged(key, lastValue, currentValue);
                }
            }
        }

        void FireValueChanged(ScalarKey key, float oldValue, float newValue)
        {
            var args = new ScalarValueChangedArgs(key, oldValue, newValue);

            // Fire key-specific handlers
            if (_keySubscriptions.TryGetValue(key.Id, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var sub = list[i];
                    if (sub.IsDisposed) continue;

                    try { sub.Handler(args); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }

            }

            // Fire all-keys handlers
            for (int i = _allSubscriptions.Count - 1; i >= 0; i--)
            {
                var sub = _allSubscriptions[i];
                if (sub.IsDisposed) continue;

                try { sub.Handler(args); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public IEnumerable<ScalarSnapshot> Enumerate(ScalarKey key)
        {
            if (_runtimes.TryGetValue(key.Id, out var rt))
            {
                foreach (var s in rt.EnumerateSnapshots())
                    yield return s;
            }
        }

        public IEnumerable<ScalarKey> EnumerateKeys()
        {
            if (_runtimes.Count == 0)
                return Array.Empty<ScalarKey>();

            var keys = new List<ScalarKey>(_runtimes.Count);
            foreach (var runtime in _runtimes.Values)
            {
                if (runtime == null)
                    continue;

                var key = runtime.Key;
                if (key.Id == 0 && string.IsNullOrWhiteSpace(key.Name))
                    continue;

                keys.Add(key);
            }

            keys.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return keys;
        }

        public void Dispose()
        {
            ClearAll();
            _subscriptions.Clear();
            _keySubscriptions.Clear();
            _allSubscriptions.Clear();
            _lastValues.Clear();
        }
    }
}
