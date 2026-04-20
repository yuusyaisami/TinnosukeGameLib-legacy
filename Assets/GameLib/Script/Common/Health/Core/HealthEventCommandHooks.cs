#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.Events.Generated;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using VContainer;
using UnityEngine;

namespace Game.Health
{
    [Serializable]
    public sealed class HealthEventCommandBinding
    {
        [LabelText("Condition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [LabelText("Commands")]
        [VNext.CommandListFunctionName("Health.EventHook.Commands")]
        public VNext.CommandListData Commands = new();
    }

    [Serializable]
    public sealed class HealthEventCommandSettings
    {
        [Title("Health Event Commands")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [LabelText("On Damaged")]
        [Tooltip("Inspector setting.")]
        public List<HealthEventCommandBinding> OnDamaged = new();

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [LabelText("On Healed")]
        [Tooltip("Inspector setting.")]
        public List<HealthEventCommandBinding> OnHealed = new();

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [LabelText("On Died")]
        [Tooltip("Inspector setting.")]
        public List<HealthEventCommandBinding> OnDied = new();

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [LabelText("On Revived")]
        [Tooltip("Inspector setting.")]
        public List<HealthEventCommandBinding> OnRevived = new();

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [LabelText("On Invincible Started")]
        [Tooltip("Inspector setting.")]
        public List<HealthEventCommandBinding> OnInvincibleStarted = new();

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [LabelText("On Invincible Ended")]
        [Tooltip("Inspector setting.")]
        public List<HealthEventCommandBinding> OnInvincibleEnded = new();
    }

    public sealed class HealthEventCommandHookService : IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
    {
        readonly IScopeNode _ownerScope;
        readonly HealthEventCommandSettings _settings;

        IEntityEventService? _eventService;
        IHealthService? _healthService;
        VNext.ICommandRunner? _commandRunner;
        readonly List<IDisposable> _subscriptions = new();
        CancellationTokenSource? _cts;
        bool _disposed;

        public HealthEventCommandHookService(IScopeNode ownerScope, HealthEventCommandSettings settings)
        {
            _ownerScope = ownerScope;
            _settings = settings ?? new HealthEventCommandSettings();
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            ResolveDependencies();
            SubscribeEvents();
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            UnsubscribeEvents();
        }

        void ResolveDependencies()
        {
            var resolver = _ownerScope?.Resolver;
            if (resolver == null)
                return;

            if (resolver.TryResolve(out IEntityEventService eventService) && eventService != null)
                _eventService = eventService;
            else if (resolver.TryResolve(out IEventService genericEventService) &&
                     genericEventService is IEntityEventService entityEventService)
                _eventService = entityEventService;
            else
                _eventService = null;

            if (resolver.TryResolve(out IHealthService healthService))
                _healthService = healthService;
            else
                _healthService = null;

            if (resolver.TryResolve(out VNext.ICommandRunner commandRunner))
                _commandRunner = commandRunner;
            else
                _commandRunner = null;
        }

        void SubscribeEvents()
        {
            if (_eventService == null || _commandRunner == null)
                return;
            if (_subscriptions.Count > 0)
                return;

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            TrySubscribe(EventKeys.GameLib.Health.OnDamage, _settings.OnDamaged, ct);
            TrySubscribe(EventKeys.GameLib.Health.OnHeal, _settings.OnHealed, ct);
            TrySubscribe(EventKeys.GameLib.Health.OnDeath, _settings.OnDied, ct);
            TrySubscribe(EventKeys.GameLib.Health.OnRevive, _settings.OnRevived, ct);
            TrySubscribe(HealthRuntimeEventKeys.OnInvincibleStarted, _settings.OnInvincibleStarted, ct);
            TrySubscribe(HealthRuntimeEventKeys.OnInvincibleEnded, _settings.OnInvincibleEnded, ct);
        }

        void TrySubscribe(string eventKey, List<HealthEventCommandBinding>? bindings, CancellationToken ct)
        {
            if (_eventService == null)
                return;
            if (bindings == null || bindings.Count == 0)
                return;

            var sub = _eventService.Subscribe(eventKey, (payload, token) =>
            {
                var effectiveToken = token.CanBeCanceled ? token : ct;
                return ExecuteBindingsAsync(bindings, payload, effectiveToken, eventKey);
            });
            _subscriptions.Add(sub);
        }

        async UniTask ExecuteBindingsAsync(
            List<HealthEventCommandBinding> bindings,
            IVarStore payload,
            CancellationToken ct,
            string eventKey)
        {
            if (_commandRunner == null || bindings.Count == 0)
                return;

            var vars = CreateCommandVars(payload);
            var dynCtx = new SimpleDynamicContext(vars, _ownerScope);
            var options = VNext.CommandRunOptions.Default;
            var cmdCtx = new VNext.CommandContext(_ownerScope, vars, _commandRunner, actor: _ownerScope, options);

            try
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var binding = bindings[i];
                    if (binding.Commands.Count == 0)
                        continue;

                    bool shouldRun = binding.Condition.GetOrDefault(dynCtx, true);
                    if (!shouldRun)
                        continue;

                    try
                    {
                        var result = await _commandRunner.ExecuteListAsync(binding.Commands, cmdCtx, ct, cmdCtx.Options);
                        if (result.Status == VNext.CommandRunStatus.Error)
                            Debug.LogError($"[HealthEventCommandHookService] Command failed: event='{eventKey}' message='{result.Message}'");
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[HealthEventCommandHookService] Command execution failed: event='{eventKey}' error='{ex.Message}'");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        IVarStore CreateCommandVars(IVarStore payload)
        {
            var vars = new VarStore();
            payload?.MergeInto(vars, overwrite: true);

            if (_healthService != null)
            {
                vars.TrySetVariant(VarIds.GameLib.Health.newHP, DynamicVariant.FromFloat(_healthService.CurrentHP));
                vars.TrySetVariant(VarIds.GameLib.Health.isInvincible, DynamicVariant.FromBool(_healthService.IsInvincible));
            }

            return vars;
        }

        void UnsubscribeEvents()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch
                {
                    // ignore
                }
                _cts.Dispose();
                _cts = null;
            }

            for (int i = 0; i < _subscriptions.Count; i++)
                _subscriptions[i]?.Dispose();
            _subscriptions.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            UnsubscribeEvents();
            _eventService = null;
            _healthService = null;
            _commandRunner = null;
        }
    }
}
