#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using UnityEngine;
using VContainer;
using VContainer.Unity;

using UnityTime = UnityEngine.Time;

namespace Game.Common
{
    public sealed class RuntimeScopeLifecycleService : IScopeLifecycleService, IScopeLifecycleConditionController, IScopeReleaseHandler, ITickable
    {
        readonly RuntimeLifetimeScope _scope;
        readonly ScopeLifecycleConfig _config;
        readonly IObjectResolver _resolver;

        CancellationTokenSource? _spawnCts;
        CancellationTokenSource? _despawnCts;
        bool _autoDespawnRequested;

        bool _hasConditionOverride;
        DynamicValue<bool> _conditionOverride;

        public RuntimeScopeLifecycleService(
            RuntimeLifetimeScope scope,
            ScopeLifecycleConfig config,
            IObjectResolver resolver)
        {
            _scope = scope;
            _config = config;
            _resolver = resolver;
        }

        public void SetConditionOverride(DynamicValue<bool> condition)
        {
            _conditionOverride = condition;
            _hasConditionOverride = true;
        }

        public void ClearConditionOverride()
        {
            _conditionOverride = default;
            _hasConditionOverride = false;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ClearConditionOverride();
            _autoDespawnRequested = false;
        }

        public void Tick()
        {
            if (_autoDespawnRequested)
                return;

            if (!_config.AutoDespawnWhenConditionFalse)
                return;

            if (!EvaluateLifecycleCondition())
            {
                _autoDespawnRequested = true;
                UniTask.Void(async () =>
                {
                    try
                    {
                        await HandleDespawnAsync(CancellationToken.None);
                        if (_scope != null && _scope.gameObject != null)
                            UnityEngine.Object.Destroy(_scope.gameObject);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        _autoDespawnRequested = false;
                    }
                });
            }
        }

        VNext.ICommandRunner? TryGetRunner()
        {
            if (_resolver.TryResolve<VNext.ICommandRunner>(out var direct) && direct != null)
                return direct;

            IScopeNode? node = _scope;
            while (node != null)
            {
                if (node.Resolver != null &&
                    node.Resolver.TryResolve<VNext.ICommandRunner>(out var runner) &&
                    runner != null)
                {
                    return runner;
                }

                node = node.Parent;
            }

            return null;
        }

        bool EvaluateLifecycleCondition()
        {
            var condition = _hasConditionOverride ? _conditionOverride : _config.AutoDespawnCondition;
            if (!condition.HasSource)
                return true;

            var vars = new VarStore();
            if (_resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                blackboard.MergeInto(vars, overwrite: true);

            var resolveContext = new VNext.CommandResolveContext(
                _scope,
                vars,
                _scope,
                _scope.Resolver,
                VNext.NullCommandCatalog.Instance,
                VNext.NullCommandKeyResolver.Instance,
                VNext.NullCommandResolveLogger.Instance,
                allowRuntimeKeyFallback: true,
                runtimeContext: null);

            return condition.EvaluateBool(resolveContext);
        }

        public async UniTask HandleSpawnAsync(CancellationToken ct)
        {
            _despawnCts?.Cancel();

            _spawnCts?.Cancel();
            _spawnCts?.Dispose();
            _spawnCts = new CancellationTokenSource();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _spawnCts.Token);
            var token = linked.Token;

            var variables = new VarStore();
            void SetFloat(string stableKey, float value)
            {
                if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                    variables.TrySetVariant(varId, DynamicVariant.FromFloat(value));
            }

            if (_config.SpawnDelaySeconds > 0f)
                SetFloat("spawnDelay", _config.SpawnDelaySeconds);

            var runner = TryGetRunner();
            VNext.CommandContext? ctx = null;
            if (runner != null)
                ctx = new VNext.CommandContext(_scope, variables, runner);

            var startTime = UnityTime.realtimeSinceStartupAsDouble;

            try
            {
                if (_config.RunSpawnOnStart && ctx != null && _config.SpawnOnStartCommands.Count > 0)
                {

                    await runner!.ExecuteListAsync(_config.SpawnOnStartCommands, ctx, token, ctx.Options);
                }

                token.ThrowIfCancellationRequested();

                if (_config.SpawnDelaySeconds > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_config.SpawnDelaySeconds),
                        DelayType.UnscaledDeltaTime,
                        PlayerLoopTiming.Update,
                        token);

                token.ThrowIfCancellationRequested();

                var elapsed = (float)(UnityTime.realtimeSinceStartupAsDouble - startTime);
                SetFloat("spawnDuration", elapsed);

                if (_config.RunSpawnOnEnd && ctx != null && _config.SpawnOnEndCommands.Count > 0)
                {
                    try
                    {
                        Debug.Log($"[RuntimeScopeLifecycleService] Running SpawnOnEndCommands for scope={_scope.gameObject.name}, Count={_config.SpawnOnEndCommands.Count}");
                    }
                    catch { }
                    await runner!.ExecuteListAsync(_config.SpawnOnEndCommands, ctx, token, ctx.Options);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        public async UniTask HandleDespawnAsync(CancellationToken ct)
        {
            _spawnCts?.Cancel();

            _despawnCts?.Cancel();
            _despawnCts?.Dispose();
            _despawnCts = new CancellationTokenSource();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _despawnCts.Token);
            var token = linked.Token;

            var variables = new VarStore();
            void SetFloat(string stableKey, float value)
            {
                if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                    variables.TrySetVariant(varId, DynamicVariant.FromFloat(value));
            }

            if (_config.DespawnDelaySeconds > 0f)
                SetFloat("despawnDelay", _config.DespawnDelaySeconds);

            var runner = TryGetRunner();
            VNext.CommandContext? ctx = null;
            if (runner != null)
                ctx = new VNext.CommandContext(_scope, variables, runner);

            var startTime = UnityTime.realtimeSinceStartupAsDouble;

            try
            {
                if (_config.RunDespawnOnStart && ctx != null && _config.DespawnOnStartCommands.Count > 0)
                    await runner!.ExecuteListAsync(_config.DespawnOnStartCommands, ctx, token, ctx.Options);

                token.ThrowIfCancellationRequested();

                if (_config.DespawnDelaySeconds > 0f)
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_config.DespawnDelaySeconds),
                        DelayType.UnscaledDeltaTime,
                        PlayerLoopTiming.Update,
                        token);

                token.ThrowIfCancellationRequested();

                var elapsed = (float)(UnityTime.realtimeSinceStartupAsDouble - startTime);
                SetFloat("despawnDuration", elapsed);

                if (_config.RunDespawnOnEnd && ctx != null && _config.DespawnOnEndCommands.Count > 0)
                    await runner!.ExecuteListAsync(_config.DespawnOnEndCommands, ctx, token, ctx.Options);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }
    }
}
