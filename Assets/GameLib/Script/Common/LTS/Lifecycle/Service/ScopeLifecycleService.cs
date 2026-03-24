// Game.Common
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using UnityEngine;
using VContainer;
using VContainer.Unity;

// UnityEngine.Time と Game.Time の曖昧さを解決
using UnityTime = UnityEngine.Time;

namespace Game.Common
{
    public interface IScopeLifecycleConditionController
    {
        void SetConditionOverride(DynamicValue<bool> condition);
        void ClearConditionOverride();
    }

    public interface IScopeLifecycleService
    {
        UniTask HandleSpawnAsync(CancellationToken ct);
        UniTask HandleDespawnAsync(CancellationToken ct);
    }

    public sealed class ScopeLifecycleService : IScopeLifecycleService, IScopeLifecycleConditionController, IScopeReleaseHandler, ITickable
    {
        readonly IScopeNode _scope;
        readonly ScopeLifecycleConfig _config;
        readonly IObjectResolver _resolver;

        CancellationTokenSource _spawnCts;
        CancellationTokenSource _despawnCts;
        bool _spawnInProgress;
        bool _autoDespawnRequested;

        bool _hasConditionOverride;
        DynamicValue<bool> _conditionOverride;

        // Tracks Spawn calls without a matching Despawn.
        // 0 = idle, 1 = spawned, 2+ = duplicate spawn requests without despawn.
        int _spawnBalance;

        public ScopeLifecycleService(
            IScopeNode scope,
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

        VNext.ICommandRunner TryGetRunner()
        {
            if (_resolver.TryResolve<VNext.ICommandRunner>(out var runner))
                return runner;
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

        // -------- Spawn --------

        public async UniTask HandleSpawnAsync(CancellationToken ct)
        {
            if (_spawnInProgress)
            {
                return;
            }

            _spawnInProgress = true;
            try
            {
                var runner = TryGetRunner();
                if (runner == null)
                    return;

                _spawnBalance += 1;
                if (_spawnBalance > 1)
                {
                }

                // 既存の Spawn をキャンセルして多重保護する
                _spawnCts?.Cancel();
                _spawnCts?.Dispose();
                _spawnCts = new CancellationTokenSource();

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _spawnCts.Token);
                var token = linkedCts.Token;

                var variables = new VarStore();

                void SetFloat(string stableKey, float value)
                {
                    if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                        variables.TrySetVariant(varId, DynamicVariant.FromFloat(value));
                }

                // 設定上のスポーンディレイを変数に突っ込む
                if (_config.SpawnDelaySeconds > 0f)
                {
                    SetFloat("spawnDelay", _config.SpawnDelaySeconds);
                }

                var startTime = UnityTime.realtimeSinceStartupAsDouble;

                try
                {
                    // --- Spawn OnStart ---
                    if (_config.RunSpawnOnStart && _config.SpawnOnStartCommands != null && _config.SpawnOnStartCommands.Count > 0)
                    {
                        var ctxStart = new VNext.CommandContext(runner.Scope, variables, runner);
                        await runner.ExecuteListAsync(_config.SpawnOnStartCommands, ctxStart, token, ctxStart.Options);
                    }

                    token.ThrowIfCancellationRequested();

                    // --- Delay (演出時間としての SpawnDelaySeconds) ---
                    if (_config.SpawnDelaySeconds > 0f)
                    {
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(_config.SpawnDelaySeconds),
                            DelayType.UnscaledDeltaTime,
                            PlayerLoopTiming.Update,
                            token);
                    }

                    token.ThrowIfCancellationRequested();

                    // 実測時間を Duration として設定
                    var elapsed = (float)(UnityTime.realtimeSinceStartupAsDouble - startTime);
                    SetFloat("spawnDuration", elapsed);

                    // --- Spawn OnEnd ---
                    if (_config.RunSpawnOnEnd && _config.SpawnOnEndCommands != null && _config.SpawnOnEndCommands.Count > 0)
                    {
                        var ctxEnd = new VNext.CommandContext(runner.Scope, variables, runner);
                        await runner.ExecuteListAsync(_config.SpawnOnEndCommands, ctxEnd, token, ctxEnd.Options);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // Despawn などでキャンセルされた場合は特に何もしない
                }
            }
            finally
            {
                _spawnInProgress = false;
            }
        }

        // -------- Despawn --------

        public async UniTask HandleDespawnAsync(CancellationToken ct)
        {
            // Mark despawn as matching one prior spawn (if any)
            if (_spawnBalance > 0)
                _spawnBalance -= 1;

            // Despawn 開始時に Spawn フェーズを止めるかは設定で制御
            if (_config.CancelSpawnOnDespawn)
            {
                _spawnCts?.Cancel();
            }
            else
            {
            }

            var runner = TryGetRunner();

            _despawnCts?.Cancel();
            _despawnCts?.Dispose();
            _despawnCts = new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _despawnCts.Token);
            var token = linkedCts.Token;

            var variables = new VarStore();

            void SetFloat(string stableKey, float value)
            {
                if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                    variables.TrySetVariant(varId, DynamicVariant.FromFloat(value));
            }

            // 設定上
            if (_config.DespawnDelaySeconds > 0f)
            {
                SetFloat("despawnDelay", _config.DespawnDelaySeconds);
            }

            var startTime = UnityTime.realtimeSinceStartupAsDouble;

            try
            {
                // --- Despawn OnStart ---
                if (_config.RunDespawnOnStart && runner != null && _config.DespawnOnStartCommands != null && _config.DespawnOnStartCommands.Count > 0)
                {
                    var ctxStart = new VNext.CommandContext(runner.Scope, variables, runner);
                    await runner.ExecuteListAsync(_config.DespawnOnStartCommands, ctxStart, token, ctxStart.Options);
                }

                token.ThrowIfCancellationRequested();

                // --- Delay ---
                if (_config.DespawnDelaySeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_config.DespawnDelaySeconds),
                        DelayType.UnscaledDeltaTime,
                        PlayerLoopTiming.Update,
                        token);
                }

                token.ThrowIfCancellationRequested();

                // 実測時間を Despawn OnStart〜OnEnd を Duration として設定
                var elapsed = (float)(UnityTime.realtimeSinceStartupAsDouble - startTime);
                SetFloat("despawnDuration", elapsed);

                // --- Despawn OnEnd ---
                if (_config.RunDespawnOnEnd && runner != null && _config.DespawnOnEndCommands != null && _config.DespawnOnEndCommands.Count > 0)
                {
                    var ctxEnd = new VNext.CommandContext(runner.Scope, variables, runner);
                    await runner.ExecuteListAsync(_config.DespawnOnEndCommands, ctxEnd, token, ctxEnd.Options);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // 外からキャンセル時は Destroy だけに任せる
            }
            finally
            {
                // 最終的な Destroy や Registry などは OnDisable/OnDestroy で処理
                if (_scope is Component c && c && c.gameObject)
                {
                    UnityEngine.Object.Destroy(c.gameObject);
                }
            }
        }
    }
}
