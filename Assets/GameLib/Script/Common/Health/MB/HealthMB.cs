// Game.Health.HealthMB.cs
//
// Health 管理用の MonoBehaviour (v0.2)
// - FixedHealthModifierRegistrySO から固定 Modifier を登録
// - ローカル Modifier リストを登録

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Health
{
    /// <summary>
    /// Health 管理用の MonoBehaviour (v0.2)。
    /// Entity に配置して IHealthService を DI 登録する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthMB : MonoBehaviour, IFeatureInstaller, IDisposable
    {
        [Header("Profile")]
        [Tooltip("HealthPreset。Inline か Asset のどちらでも指定できます。")]
        [SerializeField]
        [InlineProperty, HideLabel]
        DynamicValue<HealthPreset> _preset;

        [Header("Local Modifiers")]
        [LabelText("Local Modifiers")]
        [Tooltip("この Entity にのみ適用される Modifier リスト")]
        [SerializeField]
        List<BaseHealthModifierSO> _localModifiers = new();

        [Header("Event Commands")]
        [LabelText("Event Command Hooks")]
        [SerializeField, InlineProperty]
        HealthEventCommandSettings _eventCommandSettings = new();

        [Header("Acquire")]
        [LabelText("Acquire Revive Settings")]
        [SerializeField, InlineProperty]
        HealthAcquireReviveSettings _acquireReviveSettings = new();

        [Header("Debug")]
        [SerializeField, ReadOnly]
        float _currentHP;

        [SerializeField, ReadOnly]
        float _maxHP;

        [SerializeField, ReadOnly]
        bool _isDead;

        [SerializeField, ReadOnly]
        bool _isInvincible;

        IHealthService _healthService;
        IBaseScalarService _fallbackScalarService;
        IScopeBindingRegistry _fallbackProfileRegistry;
        bool _disposed;
        bool _loggedMissingHealthService;
        bool _initialModifiersRegistered;
        float _nextDebugRefreshTime;

        public IHealthService HealthService => _healthService;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // HealthService を登録（RuntimeLTS では IEntityEventService 未登録のケースがあるためフォールバック解決）
            builder.Register<HealthService>(resolver =>
                {
                    var scalarService = ResolveScalarService(scope, resolver);
                    var blackboardService = ResolveBlackboardService(scope, resolver);
                    var profileRegistry = ResolveProfileRegistry(scope, resolver, blackboardService, scalarService);
                    var eventService = ResolveEntityEventService(resolver);
                    return new HealthService(
                        scope,
                        scalarService,
                        blackboardService,
                        eventService,
                        profileRegistry,
                        resolver.Resolve<Game.Commands.VNext.ICommandRunner>(),
                        transform);
                },
                Lifetime.Singleton)
                .As<IHealthService>()
                .As<ITickable>();

            builder.RegisterInstance(_eventCommandSettings ?? new HealthEventCommandSettings());
            builder.Register<HealthEventCommandHookService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IDisposable>();

            builder.RegisterInstance(_acquireReviveSettings ?? new HealthAcquireReviveSettings());
            builder.Register<HealthAcquireReviveOnAcquireService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Start()
        {
            TryResolveHealthService();
        }

        void Update()
        {
            if (_healthService == null && !TryResolveHealthService())
                return;

            if (Time.unscaledTime < _nextDebugRefreshTime)
                return;

            _nextDebugRefreshTime = Time.unscaledTime + 0.1f;
            _currentHP = _healthService.CurrentHP;
            _maxHP = _healthService.MaxHP;
            _isDead = _healthService.IsDead;
            _isInvincible = _healthService.IsInvincible;
        }

        bool TryResolveHealthService()
        {
            var runtimeScope = GetComponentInParent<RuntimeLifetimeScope>();
            if (runtimeScope != null)
            {
                if (runtimeScope.TryResolveLocal<IHealthService>(out var runtimeHealthService) &&
                    runtimeHealthService != null)
                {
                    _healthService = runtimeHealthService;
                    _loggedMissingHealthService = false;
                    RegisterInitialModifiersIfNeeded();
                    return true;
                }

                if (runtimeScope.IsBuilt && !_loggedMissingHealthService)
                {
                    _loggedMissingHealthService = true;
                    Debug.LogWarning($"[HealthMB] IHealthService is not available in runtime scope '{runtimeScope.gameObject.name}'. " +
                                     "Check BaseScalarMB/ScopeBindingRegistryMB registration or HealthService dependency failures.");
                }
                return false;
            }

            var baseScope = GetComponentInParent<BaseLifetimeScope>();
            var baseResolver = baseScope?.Resolver;
            if (baseResolver != null &&
                baseResolver.TryResolve<IHealthService>(out var baseHealthService) &&
                baseHealthService != null)
            {
                _healthService = baseHealthService;
                _loggedMissingHealthService = false;
                RegisterInitialModifiersIfNeeded();
                return true;
            }

            return false;
        }

        void OnDestroy() => Dispose();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            (_healthService as IDisposable)?.Dispose();
            (_fallbackScalarService as IDisposable)?.Dispose();
            _healthService = null;
            _fallbackScalarService = null;
            _fallbackProfileRegistry = null;
            _initialModifiersRegistered = false;
        }

        void RegisterInitialModifiersIfNeeded()
        {
            if (_initialModifiersRegistered || _healthService == null)
                return;

            _initialModifiersRegistered = true;

            if (TryResolveFixedModifierRegistry(out FixedHealthModifierRegistrySO registry))
            {
                foreach (var modifier in registry.Modifiers)
                {
                    if (modifier != null)
                        _healthService.RegisterModifier(modifier);
                }
            }

            foreach (var modifier in _localModifiers)
            {
                if (modifier != null)
                    _healthService.RegisterModifier(modifier);
            }
        }

        IBaseScalarService ResolveScalarService(IScopeNode scope, IObjectResolver resolver)
        {
            if (TryResolveOwnedService(scope, resolver, out IBaseScalarService scalarService))
                return scalarService;

            _fallbackScalarService ??= new BaseScalarService(scope, new NullScalarRuntimeConfigProvider());
            return _fallbackScalarService;
        }

        IBlackboardService ResolveBlackboardService(IScopeNode scope, IObjectResolver resolver)
        {
            if (TryResolveOwnedService(scope, resolver, out IBlackboardService blackboardService))
                return blackboardService;

            return new BlackboardService(scope);
        }

        IScopeBindingRegistry ResolveProfileRegistry(
            IScopeNode scope,
            IObjectResolver resolver,
            IBlackboardService blackboardService,
            IBaseScalarService scalarService)
        {
            var preset = ResolvePreset(scope);
            if (TryResolveOwnedService(scope, resolver, out IScopeBindingRegistry profileRegistry))
            {
                if (preset != null)
                    profileRegistry.SetProfileDefinition(preset);
                return profileRegistry;
            }

            if (_fallbackProfileRegistry is not ScopeBindingRegistryService fallback)
            {
                var scopeIdentity = scope.Identity?.Id ?? string.Empty;
                fallback = new ScopeBindingRegistryService(blackboardService, scalarService, scopeIdentity, scope);
                _fallbackProfileRegistry = fallback;
            }

            if (preset != null)
                fallback.SetProfileDefinition(preset);

            return fallback;
        }

        HealthPreset ResolvePreset(IScopeNode scope)
        {
            var dynamicContext = new SimpleDynamicContext(NullVarStore.Instance, scope);
            return _preset.GetOrDefault(dynamicContext, null);
        }

        static bool TryResolveOwnedService<T>(IScopeNode scope, IObjectResolver resolver, out T service) where T : class
        {
            service = null;
            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                return runtimeScope.TryResolveLocal<T>(out service) && service != null;
            }

            return resolver.TryResolve<T>(out service) && service != null;
        }

        bool TryResolveFixedModifierRegistry(out FixedHealthModifierRegistrySO registry)
        {
            registry = null;

            var runtimeScope = GetComponentInParent<RuntimeLifetimeScope>();
            var runtimeResolver = runtimeScope?.Resolver;
            if (runtimeResolver != null &&
                runtimeResolver.TryResolve<FixedHealthModifierRegistrySO>(out registry) &&
                registry != null)
            {
                return true;
            }

            var baseScope = GetComponentInParent<BaseLifetimeScope>();
            var baseResolver = baseScope?.Resolver;
            if (baseResolver != null &&
                baseResolver.TryResolve<FixedHealthModifierRegistrySO>(out registry) &&
                registry != null)
            {
                return true;
            }

            registry = null;
            return false;
        }

        static IEntityEventService ResolveEntityEventService(IObjectResolver resolver)
        {
            if (resolver.TryResolve<IEntityEventService>(out var entityEventService) && entityEventService != null)
                return entityEventService;

            if (resolver.TryResolve<IEventService>(out var eventService) &&
                eventService is IEntityEventService entityEventFromGeneric)
                return entityEventFromGeneric;

            Debug.LogWarning("[HealthMB] IEntityEventService/IEventService was not resolved. Using local EventService fallback.");
            return new EventService();
        }

#if UNITY_EDITOR
        [Button("Apply 10 Damage")]
        void DebugApplyDamage()
        {
            if (_healthService == null)
                return;

            var source = new VarStore();
            if (VarIdResolver.TryResolve("Debug", out var varId) && varId != 0)
                source.TrySetVariant(varId, DynamicVariant.FromBool(true));
            if (VarIdResolver.TryResolve("Button", out varId) && varId != 0)
                source.TrySetVariant(varId, DynamicVariant.FromString("Apply10Damage"));

            var ctx = new DamageContext
            {
                BaseDamage = 10f,
                DamageType = DamageType.Physical,
                Source = source
            };
            _healthService.ApplyDamage(ref ctx);
        }

        [Button("Heal 20")]
        void DebugHeal()
        {
            if (_healthService == null)
                return;
            var ctx = HealContext.Create(20f, HealType.Normal);
            _healthService.ApplyHeal(ref ctx);
        }

        [Button("Kill")]
        void DebugKill()
        {
            _healthService?.Kill();
        }

        [Button("Revive")]
        void DebugRevive()
        {
            _healthService?.Revive(1f);
        }

        [Button("Toggle Invincible")]
        void DebugToggleInvincible()
        {
            if (_healthService != null)
            {
                bool current = _healthService.InvincibleLayer.Value;
                _healthService.InvincibleLayer.Set("Debug", !current);
            }
        }
#endif
    }
}
