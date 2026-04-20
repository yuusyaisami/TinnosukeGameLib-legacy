#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Health
{
    [Serializable]
    public sealed class HealthAcquireReviveSettings
    {
        [LabelText("Revive If Dead On Acquire")]
        [Tooltip("Inspector setting.")]
        public bool ReviveIfDeadOnAcquire = true;

        [LabelText("Only On Reset Acquire")]
        [ShowIf(nameof(ReviveIfDeadOnAcquire))]
        [Tooltip("Inspector setting.")]
        public bool OnlyOnResetAcquire = true;

        [LabelText("Revive HP Ratio")]
        [ShowIf(nameof(ReviveIfDeadOnAcquire))]
        [Range(0f, 1f)]
        [Tooltip("Inspector setting.")]
        public float ReviveHPRatio = 1f;
    }

    public sealed class HealthAcquireReviveOnAcquireService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IScopeNode _ownerScope;
        readonly HealthAcquireReviveSettings _settings;

        IHealthService? _healthService;

        public HealthAcquireReviveOnAcquireService(IScopeNode ownerScope, HealthAcquireReviveSettings settings)
        {
            _ownerScope = ownerScope;
            _settings = settings ?? new HealthAcquireReviveSettings();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;

            if (!_settings.ReviveIfDeadOnAcquire)
                return;

            if (_settings.OnlyOnResetAcquire && !isReset)
                return;

            var health = ResolveHealthService();
            if (health == null || !health.IsDead)
                return;

            health.Revive(_settings.ReviveHPRatio);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _healthService = null;
        }

        IHealthService? ResolveHealthService()
        {
            if (_healthService != null)
                return _healthService;

            var resolver = _ownerScope.Resolver;
            if (resolver == null)
                return null;

            if (resolver.TryResolve<IHealthService>(out var health) && health != null)
            {
                _healthService = health;
                return health;
            }

            return null;
        }
    }
}
