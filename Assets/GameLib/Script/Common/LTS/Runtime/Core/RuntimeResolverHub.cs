#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using VContainer.Diagnostics;

namespace Game
{
    // ================================================================
    // RuntimeResolverHub: 軽量DIコンテナ
    // VContainerのIContainerBuilder/IObjectResolverインターフェースを実装した
    // 最小限の機能を持つDIシステム
    // 
    // サポートする機能:
    // - Register<T>, RegisterInstance<T>
    // - As<T>, WithParameter
    // - コンストラクタインジェクション
    // - 親子階層
    // - IScopeAcquireHandler/IScopeReleaseHandler
    //
    // サポートしない機能:
    // - [Inject]属性
    // - IInitializable/IStartable/ITickable/IDisposable の自動解決
    // - RegisterEntryPoint
    // ================================================================

    /// <summary>
    /// 登録情報
    /// </summary>
    public sealed class RuntimeRegistration
    {
        public Type ImplementationType { get; set; } = null!;
        public Type[] InterfaceTypes { get; set; } = Array.Empty<Type>();
        public object? Instance { get; set; }
        public bool IsInstance { get; set; }
        public Lifetime Lifetime { get; set; } = Lifetime.Transient;
        public Dictionary<Type, object>? Parameters { get; set; }
        public Func<IRuntimeResolver, object>? Factory { get; set; }
    }

    /// <summary>
    /// RuntimeResolver用のインターフェース（IObjectResolverを継承しない独自版）
    /// </summary>
    public interface IRuntimeResolver : IDisposable
    {
        bool TryResolve(Type type, out object? instance);
        T Resolve<T>();
        bool TryResolve<T>(out T instance);
        object Resolve(Type type);
        void Inject(object instance);
        object? ResolveOrDefault(Type type);
        IObjectResolver AsVContainerResolver();
    }

    /// <summary>
    /// RuntimeContainerBuilder: IContainerBuilder互換の軽量ビルダー
    /// VContainerのIContainerBuilderを継承し、同等のAPIを提供
    /// </summary>
    public sealed class RuntimeContainerBuilder : IContainerBuilder
    {
        readonly List<RuntimeRegistration> _registrations = new(64);
        readonly List<RegistrationBuilder> _deferredVContainerRegistrations = new(32);
        readonly List<Action<IRuntimeResolver>> _buildCallbacks = new(8);
        IRuntimeResolver? _parentResolver;
        IObjectResolver? _parentVContainerResolver;
        RuntimeLifetimeScope? _hostScope;

        public RuntimeContainerBuilder()
        {
        }

        public RuntimeContainerBuilder(IRuntimeResolver? parentResolver)
        {
            _parentResolver = parentResolver;
        }

        public IReadOnlyList<RuntimeRegistration> Registrations => _registrations;
        public IReadOnlyList<Action<IRuntimeResolver>> BuildCallbacks => _buildCallbacks;
        public IRuntimeResolver? ParentResolver => _parentResolver;
        public IObjectResolver? ParentVContainerResolver => _parentVContainerResolver;

        public void SetParentResolver(IRuntimeResolver? parent)
        {
            _parentResolver = parent;
        }

        public void SetParentVContainerResolver(IObjectResolver? parent)
        {
            _parentVContainerResolver = parent;
        }

        public void SetHostScope(RuntimeLifetimeScope? host)
        {
            _hostScope = host;
        }

        // ================================================================
        // 主要API
        // ================================================================

        public RuntimeRegistrationBuilder Register<T>(Lifetime lifetime)
        {
            return Register(typeof(T), lifetime);
        }

        public RuntimeRegistrationBuilder Register(Type type, Lifetime lifetime)
        {
            var reg = new RuntimeRegistration
            {
                ImplementationType = type,
                Lifetime = lifetime,
                IsInstance = false,
            };
            _registrations.Add(reg);
            return new RuntimeRegistrationBuilder(reg);
        }

        public RuntimeRegistrationBuilder RegisterInstance<T>(T instance)
        {
            return RegisterInstance(typeof(T), instance!);
        }

        public RuntimeRegistrationBuilder RegisterInstance(Type type, object instance)
        {
            var reg = new RuntimeRegistration
            {
                ImplementationType = type,
                Instance = instance,
                IsInstance = true,
                Lifetime = Lifetime.Singleton,
            };
            _registrations.Add(reg);
            return new RuntimeRegistrationBuilder(reg);
        }

        public RuntimeRegistrationBuilder RegisterFactory<T>(Func<IRuntimeResolver, T> factory, Lifetime lifetime) where T : class
        {
            var reg = new RuntimeRegistration
            {
                ImplementationType = typeof(T),
                Factory = r => factory(r)!,
                IsInstance = false,
                Lifetime = lifetime,
            };
            _registrations.Add(reg);
            return new RuntimeRegistrationBuilder(reg);
        }

        public void RegisterRuntimeBuildCallback(Action<IRuntimeResolver> callback)
        {
            _buildCallbacks.Add(callback);
        }

        public IRuntimeResolver Build()
        {
            var allRegistrations = new List<RuntimeRegistration>(_registrations.Count + _deferredVContainerRegistrations.Count);
            allRegistrations.AddRange(_registrations);

            for (int i = 0; i < _deferredVContainerRegistrations.Count; i++)
            {
                var rb = _deferredVContainerRegistrations[i];
                if (rb == null) continue;

                try
                {
                    var vreg = rb.Build();
                    allRegistrations.Add(new RuntimeRegistration
                    {
                        ImplementationType = vreg.ImplementationType,
                        InterfaceTypes = vreg.InterfaceTypes != null
                            ? new List<Type>(vreg.InterfaceTypes).ToArray()
                            : Array.Empty<Type>(),
                        Lifetime = vreg.Lifetime,
                        IsInstance = false,
                        Factory = r =>
                        {
                            var vresolver = (r as RuntimeResolver)?.AsVContainerResolver();
                            return vreg.Provider.SpawnInstance(vresolver!);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            var resolver = new RuntimeResolver(allRegistrations, _parentResolver, _parentVContainerResolver, hostScope: _hostScope);

            // Debug: list registrations for diagnostics
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Uncomment locally when you need to inspect registrations.
            // try
            // {
            //     foreach (var reg in allRegistrations)
            //     {
            //         Debug.Log($"[RuntimeContainerBuilder] Registered: {reg.ImplementationType.FullName}");
            //     }
            // }
            // catch { }
#endif

            // Build callbacks を実行
            foreach (var callback in _buildCallbacks)
            {
                try
                {
                    callback(resolver);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            return resolver;
        }

        // ================================================================
        // IContainerBuilder 実装 (VContainer互換)
        // ================================================================

        object IContainerBuilder.ApplicationOrigin { get; set; } = null!;

        int IContainerBuilder.Count => _deferredVContainerRegistrations.Count;

        RegistrationBuilder IContainerBuilder.this[int index]
        {
            get => _deferredVContainerRegistrations[index];
            set => _deferredVContainerRegistrations[index] = value;
        }

        DiagnosticsCollector? IContainerBuilder.Diagnostics
        {
            get => null;
            set { }
        }

        T IContainerBuilder.Register<T>(T registrationBuilder)
        {
            if (registrationBuilder != null)
                _deferredVContainerRegistrations.Add(registrationBuilder);
            return registrationBuilder;
        }

        bool IContainerBuilder.Exists(Type type, bool includeInterfaceTypes, bool includeInteritedTypes)
        {
            foreach (var reg in _registrations)
            {
                if (reg.ImplementationType == type)
                    return true;
                if (includeInterfaceTypes && reg.InterfaceTypes != null)
                {
                    foreach (var iface in reg.InterfaceTypes)
                    {
                        if (iface == type)
                            return true;
                    }
                }
            }
            return false;
        }

        void IContainerBuilder.RegisterBuildCallback(Action<IObjectResolver> callback)
        {
            // VContainer互換: IObjectResolverを受け取るコールバック
            // RuntimeResolverはIObjectResolverを実装しないため、ラッパーを使用
            _buildCallbacks.Add(r =>
            {
                if (r is RuntimeResolver rr)
                    callback(rr.AsVContainerResolver());
            });
        }
    }

    /// <summary>
    /// RuntimeRegistrationBuilder: Fluent APIサポート
    /// </summary>
    public sealed class RuntimeRegistrationBuilder
    {
        readonly RuntimeRegistration _registration;

        public RuntimeRegistrationBuilder(RuntimeRegistration registration)
        {
            _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        }

        public RuntimeRegistrationBuilder As<T>()
        {
            return As(typeof(T));
        }

        public RuntimeRegistrationBuilder As(Type interfaceType)
        {
            var current = _registration.InterfaceTypes;
            var newArray = new Type[current.Length + 1];
            Array.Copy(current, newArray, current.Length);
            newArray[current.Length] = interfaceType;
            _registration.InterfaceTypes = newArray;
            return this;
        }

        public RuntimeRegistrationBuilder AsSelf()
        {
            return As(_registration.ImplementationType);
        }

        public RuntimeRegistrationBuilder WithParameter<T>(T value)
        {
            _registration.Parameters ??= new Dictionary<Type, object>();
            _registration.Parameters[typeof(T)] = value!;
            return this;
        }

        public RuntimeRegistrationBuilder WithParameter(Type type, object value)
        {
            _registration.Parameters ??= new Dictionary<Type, object>();
            _registration.Parameters[type] = value;
            return this;
        }

        public RuntimeRegistrationBuilder AsImplementedInterfaces()
        {
            var implType = _registration.ImplementationType;
            foreach (var iface in implType.GetInterfaces())
            {
                As(iface);
            }
            return this;
        }
    }

    /// <summary>
    /// RuntimeResolver: 軽量Resolver実装
    /// </summary>
    public sealed class RuntimeResolver : IRuntimeResolver
    {
        readonly RuntimeRegistration[] _allRegistrations;
        readonly Dictionary<Type, RuntimeRegistration> _typeToRegistration = new(128);
        readonly Dictionary<Type, object> _singletonCache = new(64);
        readonly Dictionary<Type, object> _scopedCache = new(64);
        readonly IRuntimeResolver? _parentRuntime;
        readonly IObjectResolver? _parentVContainer;
        readonly RuntimeLifetimeScope? _hostScope;
        bool _disposed;

        // Handler caches
        IScopeAcquireHandler[]? _acquireHandlers;
        IScopeReleaseHandler[]? _releaseHandlers;
        ITickable[]? _tickables;
        ILateTickable[]? _lateTickables;
        IFixedTickable[]? _fixedTickables;

        // VContainer互換ラッパー
        VContainerResolverWrapper? _vcontainerWrapper;

        public RuntimeResolver(
            IReadOnlyList<RuntimeRegistration> registrations,
            IRuntimeResolver? parentRuntime,
            IObjectResolver? parentVContainer,
            RuntimeLifetimeScope? hostScope = null)
        {
            _parentRuntime = parentRuntime;
            _parentVContainer = parentVContainer;
            _hostScope = hostScope;

            // Keep the original ordered list for multi-resolve (IReadOnlyList<T>)
            _allRegistrations = new RuntimeRegistration[registrations.Count];
            for (int i = 0; i < registrations.Count; i++)
                _allRegistrations[i] = registrations[i];

            // 登録情報をインデックス化
            foreach (var reg in registrations)
            {
                // ImplementationType をキーとして登録
                _typeToRegistration[reg.ImplementationType] = reg;

                // InterfaceTypes もキーとして登録
                foreach (var iface in reg.InterfaceTypes)
                {
                    _typeToRegistration[iface] = reg;
                }

                // Instanceの場合、即座にキャッシュ
                if (reg.IsInstance && reg.Instance != null)
                {
                    _singletonCache[reg.ImplementationType] = reg.Instance;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Debug: list registered keys for diagnostics (very noisy; keep disabled by default)
            // try
            // {
            //     var keys = new List<string>(_typeToRegistration.Count);
            //     foreach (var k in _typeToRegistration.Keys)
            //         keys.Add(k.Name);
            //     Debug.Log($"[RuntimeResolver] Registered keys: {string.Join(", ", keys)}");
            // }
            // catch { }
#endif

        }

        public IObjectResolver AsVContainerResolver()
        {
            _vcontainerWrapper ??= new VContainerResolverWrapper(this);
            return _vcontainerWrapper;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Dispose all scoped instances that implement IDisposable
            foreach (var instance in _scopedCache.Values)
            {
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            _scopedCache.Clear();
        }

        // ================================================================
        // IRuntimeResolver 実装
        // ================================================================

        public T Resolve<T>()
        {
            if (TryResolve<T>(out var instance))
                return instance;
            throw new InvalidOperationException($"[RuntimeResolver] Unable to resolve type: {typeof(T).Name}");
        }

        public bool TryResolve<T>(out T instance)
        {
            if (TryResolve(typeof(T), out var obj) && obj is T typed)
            {
                instance = typed;
                return true;
            }
            instance = default!;
            return false;
        }

        public object Resolve(Type type)
        {
            if (TryResolve(type, out var instance) && instance != null)
                return instance;
            throw new InvalidOperationException($"[RuntimeResolver] Unable to resolve type: {type.Name}");
        }

        public object? ResolveOrDefault(Type type)
        {
            TryResolve(type, out var instance);
            return instance;
        }

        public bool TryResolve(Type type, out object? instance)
        {
            if (_disposed)
            {
                instance = null;
                return false;
            }

            // VContainer互換: IObjectResolver 自体を注入可能にする
            if (type == typeof(IObjectResolver))
            {
                instance = AsVContainerResolver();
                return true;
            }

            // 1. Singleton/Scoped キャッシュをチェック
            if (_singletonCache.TryGetValue(type, out instance))
                return true;
            if (_scopedCache.TryGetValue(type, out instance))
                return true;

            // 2. 登録をチェック
            if (_typeToRegistration.TryGetValue(type, out var reg))
            {
                try
                {
                    instance = CreateInstance(reg, requestedType: type);
                    return instance != null;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    instance = null;
                    return false;
                }
            }

            // 3. IReadOnlyList<T> の特殊処理
            // IMPORTANT:
            // VContainer's TryResolve(IReadOnlyList<T>) can succeed with an empty list even when
            // the current RuntimeResolver has explicit registrations. If we consult parent resolvers
            // first, we may incorrectly return that empty list and miss local multi-bindings.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                var elementType = type.GetGenericArguments()[0];
                instance = CollectAll(elementType);
                return instance != null;
            }

            // 4. Unity Component のフォールバック (HostScope を参照)
            if (_hostScope != null && typeof(Component).IsAssignableFrom(type))
            {
                var go = _hostScope.gameObject;
                // まずルートにあるコンポーネントを探す
                var comp = (Component?)go.GetComponent(type);
                if (comp == null)
                {
                    // 子も含めて探す（非アクティブも含む）
                    comp = (Component?)go.GetComponentInChildren(type, includeInactive: true);
                }

                if (comp != null)
                {
                    instance = comp;
                    return true;
                }
            }

            // 5. 親RuntimeResolverから解決
            if (_parentRuntime != null && _parentRuntime.TryResolve(type, out instance))
                return true;

            // 6. 親VContainerResolverから解決
            if (_parentVContainer != null && _parentVContainer.TryResolve(type, out var vcInstance))
            {
                instance = vcInstance;
                return true;
            }

            instance = null;

            if (type != null && string.Equals(type.FullName, "Game.Collision.IUnityCollisionManager", StringComparison.Ordinal))
            {
                var scopeName = _hostScope != null ? _hostScope.gameObject.name : "(no-scope)";
                LTSLog.LogWarning(
                    $"[RuntimeResolver] Failed to resolve {type.FullName} scope='{scopeName}' parentRuntime={_parentRuntime != null} parentVContainer={_parentVContainer != null}",
                    _hostScope);
            }

            return false;
        }

        /// <summary>
        /// Resolve only from this RuntimeResolver (no parent resolvers, no Unity Component fallback).
        /// This is important for per-scope services like IScopeLifecycleService where resolving a
        /// parent's instance would cause cross-scope side effects.
        /// </summary>
        public bool TryResolveLocal(Type type, out object? instance)
        {
            if (_disposed)
            {
                instance = null;
                return false;
            }

            if (type == typeof(IObjectResolver))
            {
                instance = AsVContainerResolver();
                return true;
            }

            if (_singletonCache.TryGetValue(type, out instance))
                return true;
            if (_scopedCache.TryGetValue(type, out instance))
                return true;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                var elementType = type.GetGenericArguments()[0];
                instance = CollectLocalAll(elementType);
                return instance != null;
            }

            if (_typeToRegistration.TryGetValue(type, out var reg))
            {
                try
                {
                    instance = CreateInstance(reg, requestedType: type);
                    return instance != null;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    instance = null;
                    return false;
                }
            }

            instance = null;
            return false;
        }

        object? CollectLocalAll(Type elementType)
        {
            // Collect only local registrations (preserve registration order).
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList?)Activator.CreateInstance(listType);
            if (list == null)
                return null;

            for (int i = 0; i < _allRegistrations.Length; i++)
            {
                var reg = _allRegistrations[i];
                if (reg == null)
                    continue;

                if (elementType.IsAssignableFrom(reg.ImplementationType))
                {
                    try
                    {
                        var obj = CreateInstance(reg, requestedType: reg.ImplementationType);
                        if (obj != null)
                            list.Add(obj);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }

            // Return as IReadOnlyList<T>
            return list;
        }

        object? CreateInstance(RuntimeRegistration reg, Type requestedType)
        {
            // IMPORTANT:
            // A single registration can be resolved via multiple service types (interfaces).
            // Acquire/Release handler collection creates instances using requestedType=ImplementationType,
            // so if we only cache by requestedType we can accidentally create duplicates when later
            // resolving the same registration via an interface. Always reuse the cache keyed by
            // ImplementationType for Singleton/Scoped lifetimes.
            if (reg.Lifetime == Lifetime.Singleton && _singletonCache.TryGetValue(reg.ImplementationType, out var cachedSingleton))
            {
                if (requestedType != reg.ImplementationType)
                    _singletonCache[requestedType] = cachedSingleton;
                return cachedSingleton;
            }
            if (reg.Lifetime == Lifetime.Scoped && _scopedCache.TryGetValue(reg.ImplementationType, out var cachedScoped))
            {
                if (requestedType != reg.ImplementationType)
                    _scopedCache[requestedType] = cachedScoped;
                return cachedScoped;
            }

            // Factory がある場合
            if (reg.Factory != null)
            {
                var factoryInstance = reg.Factory(this);
                CacheIfNeeded(reg, requestedType, factoryInstance);
                return factoryInstance;
            }

            // Instance の場合
            if (reg.IsInstance)
            {
                return reg.Instance;
            }

            // コンストラクタインジェクション
            var type = reg.ImplementationType;
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
            {
                // パラメータなしコンストラクタを試す
                var instance = Activator.CreateInstance(type);
                CacheIfNeeded(reg, requestedType, instance);
                return instance;
            }

            // 最も多くのパラメータを持つコンストラクタを使用
            ConstructorInfo? bestCtor = null;
            int maxParams = -1;
            foreach (var ctor in ctors)
            {
                var paramCount = ctor.GetParameters().Length;
                if (paramCount > maxParams)
                {
                    maxParams = paramCount;
                    bestCtor = ctor;
                }
            }

            if (bestCtor == null)
            {
                var instance = Activator.CreateInstance(type);
                CacheIfNeeded(reg, requestedType, instance);
                return instance;
            }

            // パラメータを解決
            var parameters = bestCtor.GetParameters();
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;

                // WithParameter で指定されたパラメータを優先
                if (reg.Parameters != null && reg.Parameters.TryGetValue(paramType, out var paramValue))
                {
                    args[i] = paramValue;
                    continue;
                }

                // DIから解決
                if (TryResolve(paramType, out var resolved))
                {
                    args[i] = resolved;
                    continue;
                }

                // オプショナルパラメータ
                if (parameters[i].HasDefaultValue)
                {
                    args[i] = parameters[i].DefaultValue;
                    continue;
                }

                // nullable型
                if (Nullable.GetUnderlyingType(paramType) != null || !paramType.IsValueType)
                {
                    args[i] = null;
                    continue;
                }

                throw new InvalidOperationException(
                    $"[RuntimeResolver] Cannot resolve parameter '{parameters[i].Name}' of type '{paramType.Name}' for '{type.Name}'");
            }

            var createdInstance = bestCtor.Invoke(args);
            CacheIfNeeded(reg, requestedType, createdInstance);
            return createdInstance;
        }

        void CacheIfNeeded(RuntimeRegistration reg, Type requestedType, object? instance)
        {
            if (instance == null) return;

            switch (reg.Lifetime)
            {
                case Lifetime.Singleton:
                    _singletonCache[reg.ImplementationType] = instance;
                    if (requestedType != reg.ImplementationType)
                        _singletonCache[requestedType] = instance;
                    break;
                case Lifetime.Scoped:
                    _scopedCache[reg.ImplementationType] = instance;
                    if (requestedType != reg.ImplementationType)
                        _scopedCache[requestedType] = instance;
                    break;
            }
        }

        object? CollectAll(Type elementType)
        {
            var list = new List<object>();

            // 現在のスコープから収集
            for (int i = 0; i < _allRegistrations.Length; i++)
            {
                var reg = _allRegistrations[i];
                // IMPORTANT:
                // VContainer's multi-resolve returns only services explicitly registered as T
                // (or AsSelf for T itself). Do NOT treat any implementor as a multi-binding,
                // otherwise we end up instantiating unrelated services and causing hitches.
                if (reg.ImplementationType == elementType ||
                    Array.Exists(reg.InterfaceTypes, t => t == elementType))
                {
                    // Use implementation type as the requested type to avoid poisoning interface caches.
                    var instance = CreateInstance(reg, requestedType: reg.ImplementationType);
                    if (instance != null && !list.Contains(instance))
                        list.Add(instance);
                }
            }

            // 親から収集
            if (_parentRuntime != null && _parentRuntime.TryResolve(typeof(IReadOnlyList<>).MakeGenericType(elementType), out var parentList) && parentList != null)
            {
                foreach (var item in (System.Collections.IEnumerable)parentList)
                {
                    if (item != null && !list.Contains(item))
                        list.Add(item);
                }
            }

            // 型付きリストに変換
            var typedList = Array.CreateInstance(elementType, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                typedList.SetValue(list[i], i);
            }

            return typedList;
        }

        // ================================================================
        // Handler キャッシュ
        // ================================================================

        /// <summary>
        /// 指定のインターフェース型を実装する登録を収集する共通メソッド。
        /// 同じ ImplementationType は1回だけ登録される（重複防止）。
        /// </summary>
        THandler[] CollectHandlers<THandler>() where THandler : class
        {
            var list = new List<THandler>();
            var seen = new HashSet<Type>();

            for (int i = 0; i < _allRegistrations.Length; i++)
            {
                var reg = _allRegistrations[i];
                if (!typeof(THandler).IsAssignableFrom(reg.ImplementationType))
                    continue;

                // 同じ ImplementationType は1回だけ登録
                if (!seen.Add(reg.ImplementationType))
                    continue;

                try
                {
                    var instance = CreateInstance(reg, requestedType: reg.ImplementationType);
                    if (instance is THandler handler)
                        list.Add(handler);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[RuntimeResolver] CollectHandlers<{typeof(THandler).Name}>: Failed to create instance of {reg.ImplementationType.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }

            return list.ToArray();
        }

        public IScopeAcquireHandler[] GetAcquireHandlers()
        {
            return _acquireHandlers ??= CollectHandlers<IScopeAcquireHandler>();
        }

        public IScopeReleaseHandler[] GetReleaseHandlers()
        {
            return _releaseHandlers ??= CollectHandlers<IScopeReleaseHandler>();
        }

        public ITickable[] GetTickables()
        {
            return _tickables ??= CollectHandlers<ITickable>();
        }

        public ILateTickable[] GetLateTickables()
        {
            return _lateTickables ??= CollectHandlers<ILateTickable>();
        }

        public IFixedTickable[] GetFixedTickables()
        {
            return _fixedTickables ??= CollectHandlers<IFixedTickable>();
        }

        public void Inject(object instance)
        {
            // [Inject]属性はサポートしない
            // コンストラクタインジェクションのみ
        }
    }

    /// <summary>
    /// VContainer IObjectResolver互換ラッパー
    /// </summary>
    internal sealed class VContainerResolverWrapper : IObjectResolver
    {
        readonly RuntimeResolver _resolver;

        public VContainerResolverWrapper(RuntimeResolver resolver)
        {
            _resolver = resolver;
        }

        public object ApplicationOrigin { get; set; } = null!;
        public DiagnosticsCollector? Diagnostics { get => null; set { } }

        public object Resolve(Type type, object? tag = null)
        {
            try
            {
                var result = _resolver.Resolve(type);
                return result;
            }
            catch (Exception ex)
            {
                // IVisualSystem is optional in some runtime scopes; if it's not registered, return null instead
                if (ex is InvalidOperationException && type == typeof(Game.Visual.IVisualSystem))
                {
                    Debug.LogWarning($"[VContainerResolverWrapper] Optional service {type.FullName} not registered in this scope: {ex.Message}");
                    return null!;
                }

                Debug.LogError($"[VContainerResolverWrapper] Resolve failed for {type.FullName}: {ex.Message}");
                throw;
            }
        }

        public object ResolveOrDefault(Type type, object? tag = null)
        {
            return _resolver.ResolveOrDefault(type)!;
        }

        public bool TryResolve(Type type, out object instance, object? tag = null)
        {
            var result = _resolver.TryResolve(type, out var obj);
            instance = obj!;
            return result;
        }

        public bool TryGetRegistration(Type type, out Registration registration, object? tag = null)
        {
            registration = default!;
            return false;
        }

        public object Resolve(Registration registration)
        {
            return _resolver.Resolve(registration.ImplementationType);
        }

        public void Inject(object instance)
        {
            _resolver.Inject(instance);
        }

        public IScopedObjectResolver CreateScope(Action<IContainerBuilder>? installation = null)
        {
            throw new NotSupportedException("CreateScope is not supported in RuntimeResolver");
        }

        public void Dispose()
        {
            _resolver.Dispose();
        }
    }

    /// <summary>
    /// RuntimeScope用のAcquire/Release ディスパッチャー
    /// </summary>
    public sealed class RuntimeAcquireReleaseDispatcher
    {
        readonly IScopeAcquireHandler[] _acquireHandlers;
        readonly IScopeReleaseHandler[] _releaseHandlers;

        public RuntimeAcquireReleaseDispatcher(RuntimeResolver resolver)
        {
            _acquireHandlers = resolver.GetAcquireHandlers();
            _releaseHandlers = resolver.GetReleaseHandlers();
        }

        public void Acquire(IScopeNode scope, bool isReset)
        {
            for (int i = 0; i < _acquireHandlers.Length; i++)
            {
                try
                {
                    _acquireHandlers[i]?.OnAcquire(scope, isReset);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void Release(IScopeNode scope, bool isReset)
        {
            for (int i = 0; i < _releaseHandlers.Length; i++)
            {
                try
                {
                    _releaseHandlers[i]?.OnRelease(scope, isReset);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
