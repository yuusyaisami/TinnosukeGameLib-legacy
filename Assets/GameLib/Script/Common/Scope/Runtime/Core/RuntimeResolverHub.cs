#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Game
{
    public enum RuntimeLifetime
    {
        Transient = 10,
        Scoped = 20,
        Singleton = 30,
    }

    public sealed class RuntimeRegistration
    {
        public Type ImplementationType { get; set; } = null!;
        public Type[] InterfaceTypes { get; set; } = Array.Empty<Type>();
        public object? Instance { get; set; }
        public bool IsInstance { get; set; }
        public RuntimeLifetime Lifetime { get; set; } = RuntimeLifetime.Transient;
        public Dictionary<Type, object?>? Parameters { get; set; }
        public Dictionary<string, object?>? NamedParameters { get; set; }
        public Func<IRuntimeResolver, object?>? Factory { get; set; }
    }

    public interface IRuntimeResolver : IDisposable
    {
        bool TryResolve(Type type, out object? instance);
        bool TryResolve<T>(out T instance);
        object Resolve(Type type);
        T Resolve<T>();
        object? ResolveOrDefault(Type type);
        void Inject(object instance);
    }

    public interface IRuntimeContainerBuilder
    {
        IRuntimeRegistrationBuilder Register<T>(RuntimeLifetime lifetime) where T : class;
        IRuntimeRegistrationBuilder Register(Type type, RuntimeLifetime lifetime);
        IRuntimeRegistrationBuilder Register<TService, TImpl>(RuntimeLifetime lifetime)
            where TService : class
            where TImpl : class, TService;
        IRuntimeRegistrationBuilder Register<TService>(Func<IRuntimeResolver, TService> factory, RuntimeLifetime lifetime)
            where TService : class;
        IRuntimeRegistrationBuilder RegisterInstance<T>(T instance);
        IRuntimeRegistrationBuilder RegisterInstance<TService>(TService instance, bool asSelf) where TService : class;
        IRuntimeRegistrationBuilder RegisterInstance(Type type, object instance);
        IRuntimeRegistrationBuilder RegisterComponent<T>(T component) where T : Component;
        void RegisterBuildCallback(Action<IRuntimeResolver> callback);
        bool Exists(Type type, bool includeInterfaceTypes = true, bool includeInheritedTypes = false);
    }

    public interface IRuntimeRegistrationBuilder
    {
        IRuntimeRegistrationBuilder As<T>() where T : class;
        IRuntimeRegistrationBuilder As(Type serviceType);
        IRuntimeRegistrationBuilder AsSelf();
        IRuntimeRegistrationBuilder AsImplementedInterfaces();
        IRuntimeRegistrationBuilder WithParameter<T>(T value);
        IRuntimeRegistrationBuilder WithParameter(Type type, object? value);
        IRuntimeRegistrationBuilder WithParameter(Type type, Func<IRuntimeResolver, object?> valueFactory);
        IRuntimeRegistrationBuilder WithParameter(string name, object? value);
    }

    public sealed class RuntimeContainerBuilder : IRuntimeContainerBuilder
    {
        readonly List<RuntimeRegistration> _registrations = new(64);
        readonly List<Action<IRuntimeResolver>> _buildCallbacks = new(8);
        IScopeNode? _hostScope;
        bool _handlerCollectionResolutionEnabled = true;

        public IReadOnlyList<RuntimeRegistration> Registrations => _registrations;
        public IScopeNode? HostScope => _hostScope;

        public void DisableHandlerCollectionResolution()
        {
            _handlerCollectionResolutionEnabled = false;
        }

        public void SetHostScope(IScopeNode? host)
        {
            _hostScope = host;
        }

        public IRuntimeRegistrationBuilder Register<T>(RuntimeLifetime lifetime) where T : class
            => Register(typeof(T), lifetime);

        public IRuntimeRegistrationBuilder Register(Type type, RuntimeLifetime lifetime)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var reg = new RuntimeRegistration
            {
                ImplementationType = type,
                Lifetime = lifetime,
                IsInstance = false,
            };
            _registrations.Add(reg);
            return new RuntimeRegistrationBuilder(reg);
        }

        public IRuntimeRegistrationBuilder Register<TService, TImpl>(RuntimeLifetime lifetime)
            where TService : class
            where TImpl : class, TService
        {
            var builder = Register(typeof(TImpl), lifetime);
            builder.As<TService>();
            return builder;
        }

        public IRuntimeRegistrationBuilder Register<TService>(Func<IRuntimeResolver, TService> factory, RuntimeLifetime lifetime)
            where TService : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var reg = new RuntimeRegistration
            {
                ImplementationType = typeof(TService),
                Lifetime = lifetime,
                Factory = resolver => factory(resolver),
                IsInstance = false,
            };
            _registrations.Add(reg);
            return new RuntimeRegistrationBuilder(reg).As<TService>();
        }

        public IRuntimeRegistrationBuilder RegisterInstance<T>(T instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            return RegisterInstance(typeof(T), instance!);
        }

        public IRuntimeRegistrationBuilder RegisterInstance<TService>(TService instance, bool asSelf) where TService : class
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var builder = RegisterInstance(typeof(TService), instance);
            if (asSelf)
                builder.As(instance.GetType());
            return builder;
        }

        public IRuntimeRegistrationBuilder RegisterInstance(Type type, object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var reg = new RuntimeRegistration
            {
                ImplementationType = type,
                InterfaceTypes = new[] { type },
                Instance = instance,
                IsInstance = true,
                Lifetime = RuntimeLifetime.Singleton,
            };
            _registrations.Add(reg);
            return new RuntimeRegistrationBuilder(reg);
        }

        public IRuntimeRegistrationBuilder RegisterComponent<T>(T component) where T : Component
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            return RegisterInstance(component.GetType(), component).AsSelf();
        }

        public void RegisterBuildCallback(Action<IRuntimeResolver> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            _buildCallbacks.Add(callback);
        }

        public bool Exists(Type type, bool includeInterfaceTypes = true, bool includeInheritedTypes = false)
        {
            if (type == null)
                return false;

            for (int i = 0; i < _registrations.Count; i++)
            {
                var reg = _registrations[i];
                if (Matches(reg.ImplementationType, type, includeInheritedTypes))
                    return true;

                if (!includeInterfaceTypes)
                    continue;

                var interfaces = reg.InterfaceTypes;
                for (int j = 0; j < interfaces.Length; j++)
                {
                    if (Matches(interfaces[j], type, includeInheritedTypes))
                        return true;
                }
            }
            return false;
        }

        public IRuntimeResolver Build()
        {
            var resolver = new RuntimeResolver(
                _registrations,
                _hostScope,
                _handlerCollectionResolutionEnabled,
                BuildHandlerCatalog<IScopeAcquireHandler>(),
                BuildHandlerCatalog<IScopeReleaseHandler>(),
                BuildHandlerCatalog<IScopeTickHandler>(),
                BuildHandlerCatalog<IScopeLateTickHandler>(),
                BuildHandlerCatalog<IScopeFixedTickHandler>());
            for (int i = 0; i < _buildCallbacks.Count; i++)
            {
                _buildCallbacks[i](resolver);
            }
            return resolver;
        }

        RuntimeRegistration[] BuildHandlerCatalog<THandler>() where THandler : class
        {
            Type handlerType = typeof(THandler);
            List<RuntimeRegistration> matches = new List<RuntimeRegistration>(8);
            HashSet<RuntimeRegistration> seen = new HashSet<RuntimeRegistration>(ReferenceEqualityComparer<RuntimeRegistration>.Instance);

            for (int index = 0; index < _registrations.Count; index++)
            {
                RuntimeRegistration registration = _registrations[index];
                if (!RegistrationMatchesHandler(registration, handlerType))
                    continue;

                if (seen.Add(registration))
                    matches.Add(registration);
            }

            return matches.Count == 0 ? Array.Empty<RuntimeRegistration>() : matches.ToArray();
        }

        static bool RegistrationMatchesHandler(RuntimeRegistration registration, Type handlerType)
        {
            if (handlerType.IsAssignableFrom(registration.ImplementationType))
                return true;

            Type[] interfaces = registration.InterfaceTypes;
            for (int index = 0; index < interfaces.Length; index++)
            {
                if (interfaces[index] == handlerType)
                    return true;
            }

            return false;
        }

        static bool Matches(Type registered, Type requested, bool includeInheritedTypes)
        {
            if (registered == requested)
                return true;
            return includeInheritedTypes && requested.IsAssignableFrom(registered);
        }
    }

    public sealed class RuntimeRegistrationBuilder : IRuntimeRegistrationBuilder
    {
        readonly RuntimeRegistration _registration;

        public RuntimeRegistrationBuilder(RuntimeRegistration registration)
        {
            _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        }

        public IRuntimeRegistrationBuilder As<T>() where T : class
            => As(typeof(T));

        public IRuntimeRegistrationBuilder As(Type serviceType)
        {
            if (serviceType == null)
                throw new ArgumentNullException(nameof(serviceType));

            var current = _registration.InterfaceTypes;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == serviceType)
                    return this;
            }

            var next = new Type[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = serviceType;
            _registration.InterfaceTypes = next;
            return this;
        }

        public IRuntimeRegistrationBuilder AsSelf()
            => As(_registration.ImplementationType);

        public IRuntimeRegistrationBuilder AsImplementedInterfaces()
        {
            var interfaces = _registration.ImplementationType.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                As(interfaces[i]);
            }
            return this;
        }

        public IRuntimeRegistrationBuilder WithParameter<T>(T value)
            => WithParameter(typeof(T), value);

        public IRuntimeRegistrationBuilder WithParameter(Type type, object? value)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _registration.Parameters ??= new Dictionary<Type, object?>();
            _registration.Parameters[type] = value;
            return this;
        }

        public IRuntimeRegistrationBuilder WithParameter(Type type, Func<IRuntimeResolver, object?> valueFactory)
        {
            if (valueFactory == null)
                throw new ArgumentNullException(nameof(valueFactory));
            return WithParameter(type, (object)valueFactory);
        }

        public IRuntimeRegistrationBuilder WithParameter(string name, object? value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Parameter name is required.", nameof(name));

            _registration.NamedParameters ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            _registration.NamedParameters[name] = value;
            return this;
        }
    }

    public sealed class RuntimeResolver : IRuntimeResolver
    {
        readonly RuntimeRegistration[] _registrations;
        readonly RuntimeRegistration[] _acquireHandlerRegistrations;
        readonly RuntimeRegistration[] _releaseHandlerRegistrations;
        readonly RuntimeRegistration[] _tickHandlerRegistrations;
        readonly RuntimeRegistration[] _lateTickHandlerRegistrations;
        readonly RuntimeRegistration[] _fixedTickHandlerRegistrations;
        readonly Dictionary<Type, List<RuntimeRegistration>> _registrationsByType = new(128);
        readonly Dictionary<RuntimeRegistration, object?> _singletonCache = new(ReferenceEqualityComparer<RuntimeRegistration>.Instance);
        readonly Dictionary<RuntimeRegistration, object?> _scopedCache = new(ReferenceEqualityComparer<RuntimeRegistration>.Instance);
        readonly Dictionary<Type, object?> _resolvedTypeCache = new(128);
        readonly IScopeNode? _hostScope;
        readonly bool _handlerCollectionResolutionEnabled;
        bool _disposed;

        IScopeAcquireHandler[]? _acquireHandlers;
        IScopeReleaseHandler[]? _releaseHandlers;
        IScopeTickHandler[]? _tickHandlers;
        IScopeLateTickHandler[]? _lateTickHandlers;
        IScopeFixedTickHandler[]? _fixedTickHandlers;

        public RuntimeResolver(
            IReadOnlyList<RuntimeRegistration> registrations,
            IScopeNode? hostScope,
            bool handlerCollectionResolutionEnabled,
            IReadOnlyList<RuntimeRegistration>? acquireHandlerRegistrations = null,
            IReadOnlyList<RuntimeRegistration>? releaseHandlerRegistrations = null,
            IReadOnlyList<RuntimeRegistration>? tickHandlerRegistrations = null,
            IReadOnlyList<RuntimeRegistration>? lateTickHandlerRegistrations = null,
            IReadOnlyList<RuntimeRegistration>? fixedTickHandlerRegistrations = null)
        {
            if (registrations == null)
                throw new ArgumentNullException(nameof(registrations));

            _registrations = new RuntimeRegistration[registrations.Count];
            for (int i = 0; i < registrations.Count; i++)
            {
                var reg = registrations[i] ?? throw new ArgumentException("Registration contains null.", nameof(registrations));
                _registrations[i] = reg;
                Index(reg.ImplementationType, reg);

                var services = reg.InterfaceTypes;
                for (int j = 0; j < services.Length; j++)
                {
                    Index(services[j], reg);
                }

                if (reg.IsInstance)
                    _singletonCache[reg] = reg.Instance;
            }

            _hostScope = hostScope;
            _handlerCollectionResolutionEnabled = handlerCollectionResolutionEnabled;
            _acquireHandlerRegistrations = CloneRegistrations(acquireHandlerRegistrations);
            _releaseHandlerRegistrations = CloneRegistrations(releaseHandlerRegistrations);
            _tickHandlerRegistrations = CloneRegistrations(tickHandlerRegistrations);
            _lateTickHandlerRegistrations = CloneRegistrations(lateTickHandlerRegistrations);
            _fixedTickHandlerRegistrations = CloneRegistrations(fixedTickHandlerRegistrations);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeInstances(_scopedCache.Values);
            DisposeInstances(_singletonCache.Values);
            _scopedCache.Clear();
            _singletonCache.Clear();
            _resolvedTypeCache.Clear();
        }

        public T Resolve<T>()
        {
            if (TryResolve<T>(out var instance))
                return instance;
            throw new InvalidOperationException($"[RuntimeResolver] Failed to resolve {typeof(T).FullName}.");
        }

        public object Resolve(Type type)
        {
            if (TryResolve(type, out var instance) && instance != null)
                return instance;
            throw new InvalidOperationException($"[RuntimeResolver] Failed to resolve {type.FullName}.");
        }

        public object? ResolveOrDefault(Type type)
        {
            TryResolve(type, out var instance);
            return instance;
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

        public bool TryResolve(Type type, out object? instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (_disposed)
                throw new ObjectDisposedException(nameof(RuntimeResolver));

            if (type == typeof(IRuntimeResolver) || type == typeof(RuntimeResolver))
            {
                instance = this;
                return true;
            }

            if (_hostScope != null && (type == typeof(IScopeNode) || type == _hostScope.GetType()))
            {
                instance = _hostScope;
                return true;
            }

            if (_resolvedTypeCache.TryGetValue(type, out instance))
                return instance != null;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                if (!_handlerCollectionResolutionEnabled && IsHandlerCollectionType(type.GetGenericArguments()[0]))
                {
                    instance = null;
                    return false;
                }

                instance = CollectAll(type.GetGenericArguments()[0]);
                _resolvedTypeCache[type] = instance;
                return true;
            }

            if (_registrationsByType.TryGetValue(type, out var registrations) && registrations.Count > 0)
            {
                var reg = registrations[registrations.Count - 1];
                instance = CreateInstance(reg, type);
                return instance != null;
            }

            instance = null;
            return false;
        }

        public bool TryResolveLocal(Type type, out object? instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (_disposed)
                throw new ObjectDisposedException(nameof(RuntimeResolver));

            if (type == typeof(IRuntimeResolver) || type == typeof(RuntimeResolver))
            {
                instance = this;
                return true;
            }

            if (_hostScope != null && (type == typeof(IScopeNode) || type == _hostScope.GetType()))
            {
                instance = _hostScope;
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                if (!_handlerCollectionResolutionEnabled && IsHandlerCollectionType(type.GetGenericArguments()[0]))
                {
                    instance = null;
                    return false;
                }

                instance = CollectAll(type.GetGenericArguments()[0]);
                return true;
            }

            if (_registrationsByType.TryGetValue(type, out var registrations) && registrations.Count > 0)
            {
                var reg = registrations[registrations.Count - 1];
                instance = CreateInstance(reg, type);
                return instance != null;
            }

            instance = null;
            return false;
        }

        public void Inject(object instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            // Runtime DI intentionally supports constructor injection only.
        }

        public IScopeAcquireHandler[] GetAcquireHandlers()
            => _acquireHandlers ??= ResolveRegisteredHandlers<IScopeAcquireHandler>(_acquireHandlerRegistrations);

        public IScopeReleaseHandler[] GetReleaseHandlers()
            => _releaseHandlers ??= ResolveRegisteredHandlers<IScopeReleaseHandler>(_releaseHandlerRegistrations);

        public IScopeTickHandler[] GetTickHandlers()
            => _tickHandlers ??= ResolveRegisteredHandlers<IScopeTickHandler>(_tickHandlerRegistrations);

        public IScopeLateTickHandler[] GetLateTickHandlers()
            => _lateTickHandlers ??= ResolveRegisteredHandlers<IScopeLateTickHandler>(_lateTickHandlerRegistrations);

        public IScopeFixedTickHandler[] GetFixedTickHandlers()
            => _fixedTickHandlers ??= ResolveRegisteredHandlers<IScopeFixedTickHandler>(_fixedTickHandlerRegistrations);

        public IScopeTickHandler[] GetTickables() => GetTickHandlers();
        public IScopeLateTickHandler[] GetLateTickables() => GetLateTickHandlers();
        public IScopeFixedTickHandler[] GetFixedTickables() => GetFixedTickHandlers();

        void Index(Type type, RuntimeRegistration reg)
        {
            if (type == null)
                return;

            if (!_registrationsByType.TryGetValue(type, out var list))
            {
                list = new List<RuntimeRegistration>(1);
                _registrationsByType.Add(type, list);
            }

            if (!list.Contains(reg))
                list.Add(reg);
        }

        object? CreateInstance(RuntimeRegistration reg, Type requestedType)
        {
            if (reg.Lifetime == RuntimeLifetime.Singleton && _singletonCache.TryGetValue(reg, out var singleton))
                return singleton;
            if (reg.Lifetime == RuntimeLifetime.Scoped && _scopedCache.TryGetValue(reg, out var scoped))
                return scoped;

            object? instance;
            if (reg.Factory != null)
            {
                instance = reg.Factory(this);
            }
            else if (reg.IsInstance)
            {
                instance = reg.Instance;
            }
            else
            {
                instance = Construct(reg);
            }

            Cache(reg, requestedType, instance);
            return instance;
        }

        object? Construct(RuntimeRegistration reg)
        {
            var type = reg.ImplementationType;
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
                throw new InvalidOperationException($"[RuntimeResolver] {type.FullName} must expose a public constructor for explicit runtime registration.");

            Array.Sort(ctors, static (a, b) => b.GetParameters().Length.CompareTo(a.GetParameters().Length));

            Exception? lastFailure = null;
            for (int i = 0; i < ctors.Length; i++)
            {
                var ctor = ctors[i];
                var parameters = ctor.GetParameters();
                var args = new object?[parameters.Length];
                var failed = false;

                for (int p = 0; p < parameters.Length; p++)
                {
                    if (!TryResolveParameter(reg, parameters[p], out args[p]))
                    {
                        failed = true;
                        lastFailure = new InvalidOperationException(
                            $"[RuntimeResolver] Cannot resolve parameter '{parameters[p].Name}' ({parameters[p].ParameterType.FullName}) for {type.FullName}.");
                        break;
                    }
                }

                if (failed)
                    continue;

                return ctor.Invoke(args);
            }

            throw lastFailure ?? new InvalidOperationException($"[RuntimeResolver] No usable public constructor found for {type.FullName}.");
        }

        bool TryResolveParameter(RuntimeRegistration reg, ParameterInfo parameter, out object? value)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (reg.NamedParameters != null &&
                !string.IsNullOrEmpty(parameter.Name) &&
                reg.NamedParameters.TryGetValue(parameter.Name, out value))
            {
                return true;
            }

            if (reg.Parameters != null)
            {
                if (reg.Parameters.TryGetValue(parameter.ParameterType, out value))
                {
                    if (value is Func<IRuntimeResolver, object?> factory)
                        value = factory(this);
                    return true;
                }

                foreach (var kv in reg.Parameters)
                {
                    if (parameter.ParameterType.IsAssignableFrom(kv.Key))
                    {
                        value = kv.Value is Func<IRuntimeResolver, object?> factory ? factory(this) : kv.Value;
                        return true;
                    }
                }
            }

            if (TryResolve(parameter.ParameterType, out value))
                return true;

            value = null;
            return false;
        }

        void Cache(RuntimeRegistration reg, Type requestedType, object? instance)
        {
            if (instance == null)
                return;

            switch (reg.Lifetime)
            {
                case RuntimeLifetime.Singleton:
                    _singletonCache[reg] = instance;
                    break;
                case RuntimeLifetime.Scoped:
                    _scopedCache[reg] = instance;
                    break;
                case RuntimeLifetime.Transient:
                    return;
            }

            _resolvedTypeCache[requestedType] = instance;
            _resolvedTypeCache[reg.ImplementationType] = instance;
            var services = reg.InterfaceTypes;
            for (int i = 0; i < services.Length; i++)
            {
                _resolvedTypeCache[services[i]] = instance;
            }
        }

        object CollectAll(Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            ConstructorInfo? constructor = listType.GetConstructor(Type.EmptyTypes);
            var list = constructor?.Invoke(Array.Empty<object>()) as IList;
            if (list == null)
                throw new InvalidOperationException($"[RuntimeResolver] Failed to create list for {elementType.FullName}.");

            var seen = new HashSet<object>(ReferenceEqualityComparer<object>.Instance);
            for (int i = 0; i < _registrations.Length; i++)
            {
                var reg = _registrations[i];
                if (!RegistrationMatchesService(reg, elementType))
                    continue;

                var instance = CreateInstance(reg, reg.ImplementationType);
                if (instance == null || !seen.Add(instance))
                    continue;

                list.Add(instance);
            }

            return list;
        }

        THandler[] CollectHandlers<THandler>() where THandler : class
        {
            var list = new List<THandler>(8);
            var seen = new HashSet<object>(ReferenceEqualityComparer<object>.Instance);
            var handlerType = typeof(THandler);

            for (int i = 0; i < _registrations.Length; i++)
            {
                var reg = _registrations[i];
                if (!RegistrationMatchesService(reg, handlerType) && !handlerType.IsAssignableFrom(reg.ImplementationType))
                    continue;

                var instance = CreateInstance(reg, reg.ImplementationType);
                if (instance is not THandler handler || !seen.Add(handler))
                    continue;

                list.Add(handler);
            }

            return list.ToArray();
        }

        THandler[] ResolveRegisteredHandlers<THandler>(IReadOnlyList<RuntimeRegistration> registrations) where THandler : class
        {
            if (registrations == null || registrations.Count == 0)
                return Array.Empty<THandler>();

            List<THandler> handlers = new List<THandler>(registrations.Count);
            HashSet<object> seen = new HashSet<object>(ReferenceEqualityComparer<object>.Instance);

            for (int index = 0; index < registrations.Count; index++)
            {
                RuntimeRegistration registration = registrations[index];
                object? instance = CreateInstance(registration, registration.ImplementationType);
                if (instance is not THandler handler || !seen.Add(handler))
                    continue;

                handlers.Add(handler);
            }

            return handlers.Count == 0 ? Array.Empty<THandler>() : handlers.ToArray();
        }

        static RuntimeRegistration[] CloneRegistrations(IReadOnlyList<RuntimeRegistration>? registrations)
        {
            if (registrations == null || registrations.Count == 0)
                return Array.Empty<RuntimeRegistration>();

            RuntimeRegistration[] snapshot = new RuntimeRegistration[registrations.Count];
            for (int index = 0; index < registrations.Count; index++)
            {
                snapshot[index] = registrations[index] ?? throw new ArgumentException("Handler registration catalogs must not contain null registrations.", nameof(registrations));
            }

            return snapshot;
        }

        static bool IsHandlerCollectionType(Type elementType)
        {
            return elementType == typeof(IScopeAcquireHandler)
                || elementType == typeof(IScopeReleaseHandler)
                || elementType == typeof(IScopeTickHandler)
                || elementType == typeof(IScopeLateTickHandler)
                || elementType == typeof(IScopeFixedTickHandler);
        }

        static bool RegistrationMatchesService(RuntimeRegistration reg, Type serviceType)
        {
            if (reg.ImplementationType == serviceType)
                return true;

            var services = reg.InterfaceTypes;
            for (int i = 0; i < services.Length; i++)
            {
                if (services[i] == serviceType)
                    return true;
            }

            return false;
        }

        static void DisposeInstances(IEnumerable<object?> instances)
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer<object>.Instance);
            foreach (var instance in instances)
            {
                if (instance == null || !seen.Add(instance))
                    continue;

                if (instance is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }

    public sealed class RuntimeAcquireReleaseDispatcher : IScopeAcquireReleaseDispatcher
    {
        readonly IScopeAcquireHandler[] _acquireHandlers;
        readonly IScopeReleaseHandler[] _releaseHandlers;

        public RuntimeAcquireReleaseDispatcher(IRuntimeResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            if (resolver is RuntimeResolver runtimeResolver)
            {
                _acquireHandlers = runtimeResolver.GetAcquireHandlers();
                _releaseHandlers = runtimeResolver.GetReleaseHandlers();
                return;
            }

            _acquireHandlers = ResolveHandlers<IScopeAcquireHandler>(resolver);
            _releaseHandlers = ResolveHandlers<IScopeReleaseHandler>(resolver);
        }

        public void Acquire(IScopeNode scope, bool isReset)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            for (int i = 0; i < _acquireHandlers.Length; i++)
            {
                var handler = _acquireHandlers[i];
                if (ScopeHandlerOwnershipUtility.ShouldInvokeHandler(scope, handler))
                    handler.OnAcquire(scope, isReset);
            }
        }

        public void Release(IScopeNode scope, bool isReset)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            for (int i = 0; i < _releaseHandlers.Length; i++)
            {
                var handler = _releaseHandlers[i];
                if (ScopeHandlerOwnershipUtility.ShouldInvokeHandler(scope, handler))
                    handler.OnRelease(scope, isReset);
            }
        }

        static THandler[] ResolveHandlers<THandler>(IRuntimeResolver resolver) where THandler : class
        {
            if (resolver is RuntimeResolver runtimeResolver)
            {
                return typeof(THandler) == typeof(IScopeAcquireHandler)
                    ? (THandler[])(object)runtimeResolver.GetAcquireHandlers()
                    : typeof(THandler) == typeof(IScopeReleaseHandler)
                        ? (THandler[])(object)runtimeResolver.GetReleaseHandlers()
                        : Array.Empty<THandler>();
            }

            if (resolver.TryResolve<IReadOnlyList<THandler>>(out var list) && list != null && list.Count > 0)
            {
                var result = new List<THandler>(list.Count);
                var seen = new HashSet<object>(ReferenceEqualityComparer<object>.Instance);
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (item != null && seen.Add(item))
                        result.Add(item);
                }
                return result.ToArray();
            }

            return Array.Empty<THandler>();
        }
    }
}
