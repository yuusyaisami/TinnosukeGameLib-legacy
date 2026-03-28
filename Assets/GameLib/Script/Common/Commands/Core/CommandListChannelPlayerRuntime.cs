#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands
{
    public sealed class CommandListChannelPlayerRuntime :
        ICommandListChannelPlayer,
        ICommandListChannelCommandService,
        ICommandListChannelControlService
    {
        readonly IScopeNode _owner;
        readonly string _tag;

        public string Tag => _tag;
        public CommandListPlayerState State => _state;
        public int CurrentStepIndex => _currentStepIndex;
        public int CurrentCommandCount => _presetRuntime.CurrentCommandListPreset.Commands?.Count ?? 0;
        public bool IsExecuting => _isExecuting;
        public float RemainingIntervalSeconds => _remainingIntervalSeconds;
        public CommandListStepDirection StepDirection => _stepDirection;
        public CommandListChannelPresetRuntime PresetRuntime => _presetRuntime;

        readonly CommandListChannelPresetRuntime _presetRuntime;
        readonly VarStore _baseVars = new();
        readonly VarStore _runtimeVars = new();
        readonly VarStore _playbackCallerVars = new();

        IScopeNode? _activeScope;
        ICommandRunner? _commandRunner;
        UniTaskCompletionSource? _currentExecutionCompletionSource;
        CancellationTokenSource? _executionCts;
        CommandListPlayerState _state = CommandListPlayerState.Stopped;
        CommandListStepDirection _stepDirection = CommandListStepDirection.Forward;
        int _currentStepIndex;
        int _executionVersion;
        float _remainingIntervalSeconds;
        bool _isExecuting;
        bool _pausePending;

        public CommandListChannelPlayerRuntime(
            IScopeNode owner,
            string tag,
            CommandListChannelPresetRuntime presetRuntime)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _tag = string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
            _presetRuntime = presetRuntime ?? throw new ArgumentNullException(nameof(presetRuntime));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            _activeScope = scope;
            _commandRunner = ResolveCommandRunner(scope);
            CancelActiveExecution();
            _runtimeVars.Clear();
            _playbackCallerVars.Clear();
            ResetPlaybackState(clearCallerVars: false);
            RematerializeBaseVars(scope);

            if (_presetRuntime.CurrentPlayerPreset.AutoPlay)
                Play(NullVarStore.Instance);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            CancelActiveExecution();
            _activeScope = null;
            _commandRunner = null;
            _baseVars.Clear();
            _runtimeVars.Clear();
            _playbackCallerVars.Clear();
            ResetPlaybackState(clearCallerVars: false);
        }

        public void Tick(float deltaTime)
        {
            if (_state != CommandListPlayerState.Playing)
                return;

            if (deltaTime > 0f)
                _remainingIntervalSeconds -= deltaTime;

            if (_remainingIntervalSeconds > 0f || _isExecuting)
                return;

            TryExecuteDueStepAsync();
        }

        public bool Play(IVarStore? callerVars)
        {
            if (_state == CommandListPlayerState.Playing || _state == CommandListPlayerState.Paused)
                return false;

            if (CurrentCommandCount <= 0)
            {
                ResetPlaybackState(clearCallerVars: true);
                return true;
            }

            CapturePlaybackCallerVars(callerVars);
            ResetPlaybackCursor();
            _state = CommandListPlayerState.Playing;
            _pausePending = false;
            _remainingIntervalSeconds = 0f;
            TryExecuteDueStepAsync();
            return true;
        }

        public bool Pause()
        {
            if (_state != CommandListPlayerState.Playing)
                return false;

            if (_isExecuting)
            {
                _pausePending = true;
                return true;
            }

            _state = CommandListPlayerState.Paused;
            return true;
        }

        public bool Resume()
        {
            if (_state != CommandListPlayerState.Paused)
                return false;

            if (CurrentCommandCount <= 0)
            {
                ResetPlaybackState(clearCallerVars: true);
                return true;
            }

            _state = CommandListPlayerState.Playing;
            _pausePending = false;
            if (!_isExecuting && _remainingIntervalSeconds <= 0f)
                TryExecuteDueStepAsync();
            return true;
        }

        public bool Stop()
        {
            _state = CommandListPlayerState.Stopped;
            _pausePending = false;
            CancelActiveExecution();
            ResetPlaybackState(clearCallerVars: true);
            return true;
        }

        public bool ExecuteNow(IVarStore? callerVars)
        {
            if (_isExecuting)
                return false;

            var commands = _presetRuntime.CurrentCommandListPreset.Commands;
            if (commands == null || commands.Count == 0)
                return true;

            StartExecution(commands, callerVars, isScheduledStep: false);
            return true;
        }

        public UniTask WaitForCurrentExecutionAsync(CancellationToken ct)
        {
            var task = _currentExecutionCompletionSource?.Task ?? UniTask.CompletedTask;
            return ct.CanBeCanceled ? task.AttachExternalCancellation(ct) : task;
        }

        public bool SwapCommandListPreset(CommandListPreset? preset)
        {
            if (!_presetRuntime.SwapCommandListPreset(preset))
                return false;

            _state = CommandListPlayerState.Stopped;
            _pausePending = false;
            CancelActiveExecution();
            ResetPlaybackState(clearCallerVars: true);
            ClampPlaybackCursor();
            return true;
        }

        public bool SwapPlayerPreset(CommandListPlayerPreset? preset)
        {
            if (!_presetRuntime.SwapPlayerPreset(preset))
                return false;

            _state = CommandListPlayerState.Stopped;
            _pausePending = false;
            CancelActiveExecution();
            _runtimeVars.Clear();
            ResetPlaybackState(clearCallerVars: true);
            RematerializeBaseVars(_activeScope ?? _owner);
            return true;
        }

        public bool MutateCommands(CommandListMutationStep? mutation, ICommandListRuntimeMutationService? mutationService)
        {
            if (!_presetRuntime.MutateCommands(mutation, mutationService))
                return false;

            ClampPlaybackCursor();
            return true;
        }

        public bool SetRuntimeVars(VarStorePayload? payload, IVarStore? callerVars, bool overwriteExistingVars)
        {
            if (payload == null)
                return false;

            var scope = _activeScope ?? _owner;
            var dynamicContext = new SimpleDynamicContext(callerVars ?? NullVarStore.Instance, scope);
            payload.ApplyTo(_runtimeVars, dynamicContext, overwriteExistingVars);
            return true;
        }

        public bool ClearRuntimeVars()
        {
            _runtimeVars.Clear();
            return true;
        }

        public bool ResetRuntimeOverrides(bool resetCommands, bool resetPlayer, bool resetRuntimeVars, bool resetPlaybackState)
        {
            if (!resetCommands && !resetPlayer && !resetRuntimeVars && !resetPlaybackState)
                return false;

            var preservedState = _state;
            var shouldCancelExecution = resetCommands || resetPlayer || resetPlaybackState;
            if (shouldCancelExecution)
                CancelActiveExecution();

            if (resetCommands || resetPlayer)
                _presetRuntime.ResetRuntimeOverrides(resetCommands, resetPlayer);

            if (resetPlayer)
                RematerializeBaseVars(_activeScope ?? _owner);

            if (resetRuntimeVars)
                _runtimeVars.Clear();

            if (resetPlaybackState)
            {
                ResetPlaybackState(clearCallerVars: true);
            }
            else
            {
                _state = preservedState;
                _pausePending = false;
                ClampPlaybackCursor();
                if (_state == CommandListPlayerState.Playing && !_isExecuting && _remainingIntervalSeconds <= 0f)
                    TryExecuteDueStepAsync();
            }

            return true;
        }

        void TryExecuteDueStepAsync()
        {
            if (_isExecuting || _state != CommandListPlayerState.Playing)
                return;

            var commands = _presetRuntime.CurrentCommandListPreset.Commands;
            if (commands == null || commands.Count == 0)
            {
                ResetPlaybackState(clearCallerVars: true);
                return;
            }

            ClampPlaybackCursor();
            var source = commands.GetAt(_currentStepIndex);
            if (source == null)
            {
                AdvanceScheduledCursor();
                _remainingIntervalSeconds = ResolveIntervalSeconds();
                if (_state == CommandListPlayerState.Playing && _remainingIntervalSeconds <= 0f)
                    TryExecuteDueStepAsync();
                return;
            }

            var single = new CommandListData();
            single.SetCommands(new List<ICommandSource> { source });
            StartExecution(single, _playbackCallerVars, isScheduledStep: true);
        }

        void StartExecution(CommandListData commands, IVarStore? callerVars, bool isScheduledStep)
        {
            if (commands == null || commands.Count == 0)
                return;

            var scope = _activeScope ?? _owner;
            var runner = _commandRunner ?? ResolveCommandRunner(scope);
            _commandRunner = runner;
            if (runner == null)
            {
                Debug.LogError($"[CommandListChannel] ICommandRunner is missing. Tag={_tag}");
                if (isScheduledStep)
                    ResetPlaybackState(clearCallerVars: true);
                return;
            }

            CancelActiveExecution();
            var cts = new CancellationTokenSource();
            var executionVersion = ++_executionVersion;
            _currentExecutionCompletionSource = new UniTaskCompletionSource();
            _executionCts = cts;
            _isExecuting = true;

            if (isScheduledStep)
                _remainingIntervalSeconds = ResolveIntervalSeconds();

            RunExecutionAsync(commands, callerVars, isScheduledStep, runner, scope, cts, executionVersion).Forget();
        }

        async UniTaskVoid RunExecutionAsync(
            CommandListData commands,
            IVarStore? callerVars,
            bool isScheduledStep,
            ICommandRunner runner,
            IScopeNode scope,
            CancellationTokenSource cts,
            int executionVersion)
        {
            try
            {
                var vars = BuildExecutionVars(callerVars);
                var options = CommandRunOptions.Default.WithSuppressCancelLog(true);
                var context = new CommandContext(scope, vars, runner, scope, options);
                var result = await runner.ExecuteListAsync(commands, context, cts.Token, options);
                if (result.Status == CommandRunStatus.Error)
                    Debug.LogError($"[CommandListChannel] Command execution failed. Tag={_tag} Message={result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommandListChannel] Command execution exception. Tag={_tag} Message={ex.Message}");
            }
            finally
            {
                var isCurrentExecution = executionVersion == _executionVersion && ReferenceEquals(_executionCts, cts);
                var completionSource = isCurrentExecution ? _currentExecutionCompletionSource : null;
                if (isCurrentExecution)
                {
                    _executionCts = null;
                    _currentExecutionCompletionSource = null;
                }

                cts.Dispose();
                if (isCurrentExecution)
                {
                    _isExecuting = false;

                    if (isScheduledStep && _state == CommandListPlayerState.Playing)
                        AdvanceScheduledCursor();

                    if (_state == CommandListPlayerState.Playing && _pausePending)
                    {
                        _pausePending = false;
                        _state = CommandListPlayerState.Paused;
                    }
                    else if (_state == CommandListPlayerState.Playing)
                    {
                        ClampPlaybackCursor();
                        if (_remainingIntervalSeconds <= 0f)
                            TryExecuteDueStepAsync();
                    }

                    completionSource?.TrySetResult();
                }
            }
        }

        void AdvanceScheduledCursor()
        {
            var count = CurrentCommandCount;
            if (count <= 0)
            {
                ResetPlaybackState(clearCallerVars: true);
                return;
            }

            var mode = _presetRuntime.CurrentPlayerPreset.PlaybackMode;
            switch (mode)
            {
                case CommandListPlaybackMode.OneShot:
                    if (count <= 1 || _currentStepIndex >= count - 1)
                    {
                        ResetPlaybackState(clearCallerVars: true);
                        return;
                    }

                    _currentStepIndex++;
                    _stepDirection = CommandListStepDirection.Forward;
                    return;

                case CommandListPlaybackMode.PingPong:
                    if (count <= 1)
                    {
                        _currentStepIndex = 0;
                        _stepDirection = CommandListStepDirection.Forward;
                        return;
                    }

                    if (_stepDirection == CommandListStepDirection.Forward)
                    {
                        if (_currentStepIndex >= count - 1)
                        {
                            _stepDirection = CommandListStepDirection.Backward;
                            _currentStepIndex = count - 2;
                            return;
                        }

                        _currentStepIndex++;
                        return;
                    }

                    if (_currentStepIndex <= 0)
                    {
                        _stepDirection = CommandListStepDirection.Forward;
                        _currentStepIndex = 1;
                        return;
                    }

                    _currentStepIndex--;
                    return;

                case CommandListPlaybackMode.Loop:
                default:
                    if (count <= 1)
                    {
                        _currentStepIndex = 0;
                        _stepDirection = CommandListStepDirection.Forward;
                        return;
                    }

                    _currentStepIndex++;
                    if (_currentStepIndex >= count)
                        _currentStepIndex = 0;
                    _stepDirection = CommandListStepDirection.Forward;
                    return;
            }
        }

        void ClampPlaybackCursor()
        {
            var count = CurrentCommandCount;
            if (count <= 0)
            {
                ResetPlaybackState(clearCallerVars: true);
                return;
            }

            if (_currentStepIndex < 0)
                _currentStepIndex = 0;
            else if (_currentStepIndex >= count)
                _currentStepIndex = count - 1;

            if (count <= 1)
                _stepDirection = CommandListStepDirection.Forward;
        }

        void ResetPlaybackCursor()
        {
            _currentStepIndex = 0;
            _stepDirection = CommandListStepDirection.Forward;
            _remainingIntervalSeconds = 0f;
        }

        void ResetPlaybackState(bool clearCallerVars)
        {
            _state = CommandListPlayerState.Stopped;
            _pausePending = false;
            _currentStepIndex = 0;
            _stepDirection = CommandListStepDirection.Forward;
            _remainingIntervalSeconds = 0f;
            if (clearCallerVars)
                _playbackCallerVars.Clear();
        }

        void CancelActiveExecution()
        {
            var cts = _executionCts;
            var completionSource = _currentExecutionCompletionSource;
            if (cts == null)
            {
                completionSource?.TrySetResult();
                _currentExecutionCompletionSource = null;
                return;
            }

            _executionVersion++;
            _executionCts = null;
            _currentExecutionCompletionSource = null;
            _isExecuting = false;

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cts.Dispose();
            completionSource?.TrySetResult();
        }

        void CapturePlaybackCallerVars(IVarStore? callerVars)
        {
            _playbackCallerVars.Clear();
            (callerVars ?? NullVarStore.Instance).MergeInto(_playbackCallerVars, overwrite: true);
        }

        void RematerializeBaseVars(IScopeNode scope)
        {
            _baseVars.Clear();
            var payload = _presetRuntime.CurrentPlayerPreset.Variables;
            if (payload == null)
                return;

            var vars = ResolveVars(scope);
            var context = new SimpleDynamicContext(vars, scope);
            payload.ApplyTo(_baseVars, context, overwrite: true);
        }

        VarStore BuildExecutionVars(IVarStore? callerVars)
        {
            var merged = new VarStore();
            (callerVars ?? NullVarStore.Instance).MergeInto(merged, overwrite: true);
            _baseVars.MergeInto(merged, overwrite: true);
            _runtimeVars.MergeInto(merged, overwrite: true);
            return merged;
        }

        float ResolveIntervalSeconds()
        {
            return Mathf.Max(0f, _presetRuntime.CurrentPlayerPreset.IntervalSeconds);
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        static ICommandRunner? ResolveCommandRunner(IScopeNode scope)
        {
            if (scope.TryResolveInAncestors<ICommandRunner>(out var runner) && runner != null)
                return runner;

            if (scope.Resolver != null &&
                scope.Resolver.TryResolve<ICommandRunner>(out var resolved) &&
                resolved != null)
            {
                return resolved;
            }

            return null;
        }
    }
}
