using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VNext = Game.Commands.VNext;

namespace Game.Actions
{
    public interface IGameStateMachineService
    {
        void ChangeState(GameState newState);
        GameState GetCurrentState();
    }

    public sealed class GameStateMachineService :
        IGameStateMachineService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _scope;
        readonly IGameStateMachineSettings _settings;
        readonly VNext.ICommandRunner _commandRunner;
        readonly Dictionary<GameState, GameStateCommandEntry> _commandLookup = new();

        CancellationTokenSource _lifetimeCts;
        GameState _currentState = GameState.Default;
        bool _hasState;

        enum StateCommandPhase
        {
            Start,
            End
        }

        public GameStateMachineService(
            IScopeNode scope,
            IGameStateMachineSettings settings,
            VNext.ICommandRunner commandRunner)
        {
            _scope = scope;
            _settings = settings;
            _commandRunner = commandRunner;
        }

        public void ChangeState(GameState newState)
        {
            ChangeStateAsync(newState).Forget();
        }

        public GameState GetCurrentState()
        {
            return _currentState;
        }

        async UniTask ChangeStateAsync(GameState newState)
        {
            if (_hasState && _currentState == newState)
                return;

            var previousState = _currentState;
            var hadState = _hasState;
            _currentState = newState;
            _hasState = true;

            var cancellationToken = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            if (cancellationToken.IsCancellationRequested)
                return;

            if (hadState)
                await ExecuteCommandsAsync(previousState, StateCommandPhase.End, cancellationToken);

            await ExecuteCommandsAsync(newState, StateCommandPhase.Start, cancellationToken);
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            ResetLifetimeCts();
            BuildCommandLookup();
            //Debug.Log($"[GameStateMachineService] Acquired. InitialState={_settings.InitialState}, ExecuteInitialStateCommandsOnAcquire={_settings.ExecuteInitialStateCommandsOnAcquire}, scope.Name={_scope?.Identity.SelfTransform.name}, scope.Type={_scope?.Identity.Kind}");

            if (_settings.ExecuteInitialStateCommandsOnAcquire)
            {
                _hasState = true;
                RunInitialStateCommandsOnAcquireAsync().Forget();
            }
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            ResetState();
        }

        void ResetLifetimeCts()
        {
            if (_lifetimeCts != null)
            {
                _lifetimeCts.Cancel();
                _lifetimeCts.Dispose();
            }

            _lifetimeCts = new CancellationTokenSource();
        }

        void ResetState()
        {
            if (_lifetimeCts != null)
            {
                _lifetimeCts.Cancel();
                _lifetimeCts.Dispose();
                _lifetimeCts = null;
            }

            _commandLookup.Clear();
            _currentState = default;
            _hasState = false;
        }

        void BuildCommandLookup()
        {
            _commandLookup.Clear();

            var entries = _settings?.StateCommands;
            if (entries == null || entries.Length == 0)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                _commandLookup[entry.key] = entry;
            }
        }

        async UniTask RunInitialStateCommandsOnAcquireAsync()
        {
            var cancellationToken = _lifetimeCts != null ? _lifetimeCts.Token : CancellationToken.None;
            if (cancellationToken.IsCancellationRequested)
                return;

            var delayFrames = Mathf.Max(0, _settings.InitialStateCommandDelayFramesOnAcquire);
            for (var i = 0; i < delayFrames; i++)
            {
                await UniTask.NextFrame(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return;
            }

            await ChangeStateAsync(_settings.InitialState);
        }

        async UniTask ExecuteCommandsAsync(GameState state, StateCommandPhase phase, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return;

            if (!_commandLookup.TryGetValue(state, out var entry) || entry == null)
                return;

            var commands = phase == StateCommandPhase.Start ? entry.startCommands : entry.endCommands;
            if (commands == null || commands.Count == 0)
                return;

            if (_scope == null || _scope.Resolver == null || _commandRunner == null)
                return;

            var options = VNext.CommandRunOptions.Default;
            var vars = new VarStore(initialCapacity: 8);
            if (_commandRunner is VNext.ICommandRunnerDefaultVarsProvider defaultVarsProvider)
                defaultVarsProvider.ApplyDefaultVars(vars, overwrite: false);

            var ctx = new VNext.CommandContext(_scope, vars, _commandRunner, _scope, options);

            try
            {
                var result = await _commandRunner.ExecuteListAsync(commands, ctx, ct, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[GameStateMachineService] {phase} commands failed for {state}: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
