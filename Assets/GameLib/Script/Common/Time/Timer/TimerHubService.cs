#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Commands.VNext;
using Game.Scalar;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Game.Times
{
    public interface ITimerRuntime
    {
        string Key { get; }
        bool IsRunning { get; }
        float CurrentTime { get; }
        float TimeScale { get; }
        TimerDeltaMode DeltaMode { get; }

        void Start();
        void Stop();
        void Reset();
        void SetTime(float time);
        bool TryAddCurrent(float delta);
        void SetTimeScale(float timeScale);
    }

    public interface ITimerHubService
    {
        bool TryGetRuntime(string key, out ITimerRuntime runtime);
        bool RegisterTimer(TimerChannelDef def, bool overwrite = false);
        bool UnregisterTimer(string key);
    }

    public sealed class TimerHubService :
        ITimerHubService,
        ITickable,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly ITimerHubSettings _settings;
        readonly IScopeNode _scope;
        readonly bool _enableDebugLog;
        readonly Dictionary<string, TimerRuntime> _runtimes = new(StringComparer.Ordinal);
        readonly List<TimerRuntime> _running = new(16);
        bool _initialized;

        public TimerHubService(ITimerHubSettings settings, IScopeNode scope, bool enableDebugLog = false)
        {
            _settings = settings;
            _scope = scope;
            _enableDebugLog = enableDebugLog;
        }

        public bool TryGetRuntime(string key, out ITimerRuntime runtime)
        {
            runtime = null!;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (_runtimes.TryGetValue(key.Trim(), out var found) && found != null)
            {
                runtime = found;
                return true;
            }

            return false;
        }

        public bool RegisterTimer(TimerChannelDef def, bool overwrite = false)
        {
            if (def == null)
                return false;

            var key = def.Key?.Trim();
            if (string.IsNullOrEmpty(key))
                return false;

            if (_runtimes.TryGetValue(key, out var existing))
            {
                if (!overwrite)
                    return false;

                RemoveRuntime(existing);
            }

            var runtime = CreateRuntime(def, key);
            _runtimes[key] = runtime;
            return true;
        }

        public bool UnregisterTimer(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (!_runtimes.TryGetValue(key.Trim(), out var runtime))
                return false;

            RemoveRuntime(runtime);
            _runtimes.Remove(key.Trim());
            return true;
        }

        void ITickable.Tick()
        {
            if (_running.Count == 0)
                return;

            var dtScaled = UnityEngine.Time.deltaTime;
            var dtUnscaled = UnityEngine.Time.unscaledDeltaTime;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var runtime = _running[i];
                if (runtime == null || !runtime.IsRunning)
                {
                    RemoveRunningAt(i);
                    continue;
                }

                runtime.Advance(dtScaled, dtUnscaled);
            }
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            if (_initialized && !isReset)
                return;

            _initialized = true;
            if (_settings != null && _settings.AutoInitializeOnStart)
            {
                RegisterInitialTimers();
            }
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _initialized = false;
            _runtimes.Clear();
            _running.Clear();
        }

        void RegisterInitialTimers()
        {
            var list = _settings?.Timers;
            if (list == null || list.Length == 0)
                return;

            for (int i = 0; i < list.Length; i++)
            {
                var def = list[i];
                if (def == null)
                    continue;

                RegisterTimer(def, overwrite: true);
            }
        }

        TimerRuntime CreateRuntime(TimerChannelDef def, string key)
        {
            IBlackboardService? blackboard = null;
            IBaseScalarService? scalar = null;
            ICommandRunner? runner = null;
            var resolver = _scope.Resolver;
            if (resolver != null)
            {
                resolver.TryResolve(out blackboard);
                resolver.TryResolve(out scalar);
                resolver.TryResolve(out runner);
            }

            var runtime = new TimerRuntime(
                key,
                def,
                _scope!,
                blackboard,
                scalar,
                runner,
                _enableDebugLog,
                RegisterRunning,
                UnregisterRunning);

            if (def.AutoStart)
                runtime.Start();

            return runtime;
        }

        void RegisterRunning(TimerRuntime runtime)
        {
            if (runtime == null || runtime.IsListed)
                return;

            runtime.IsListed = true;
            _running.Add(runtime);
        }

        void UnregisterRunning(TimerRuntime runtime)
        {
            if (runtime == null || !runtime.IsListed)
                return;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_running[i], runtime))
                {
                    RemoveRunningAt(i);
                    return;
                }
            }
        }

        void RemoveRunningAt(int index)
        {
            var last = _running.Count - 1;
            if (index < 0 || index > last)
                return;

            var runtime = _running[index];
            if (runtime != null)
                runtime.IsListed = false;

            if (index == last)
            {
                _running.RemoveAt(last);
                return;
            }

            _running[index] = _running[last];
            _running.RemoveAt(last);
        }

        void RemoveRuntime(TimerRuntime runtime)
        {
            if (runtime == null)
                return;

            UnregisterRunning(runtime);
        }
    }

    sealed class TimerRuntime : ITimerRuntime
    {
        sealed class TriggerRuntime
        {
            public readonly float TimeSeconds;
            public readonly CommandListData Commands;
            public bool Fired;

            public TriggerRuntime(float timeSeconds, CommandListData commands)
            {
                TimeSeconds = timeSeconds;
                Commands = commands ?? new CommandListData();
            }
        }

        readonly IScopeNode _scope;
        readonly IBlackboardService? _blackboard;
        readonly IBaseScalarService? _scalar;
        readonly ICommandRunner? _runner;
        readonly bool _enableDebugLog;
        readonly TimerOutputTarget _output;
        readonly Action<TimerRuntime> _onStart;
        readonly Action<TimerRuntime> _onStop;
        readonly float _initialTime;
        readonly float _minTime;
        readonly float _maxTime;
        readonly TimerDeltaMode _deltaMode;
        readonly TimerDirection _direction;
        readonly TriggerRuntime[] _triggers;

        float _time;
        float _timeScale;
        bool _running;
        int _runVersion;
        int _lastAddCurrentFrame = -1;
        CancellationTokenSource _runCommandsCts;

        public string Key { get; }
        public bool IsRunning => _running;
        public float CurrentTime => _time;
        public float TimeScale => _timeScale;
        public TimerDeltaMode DeltaMode => _deltaMode;
        internal bool IsListed { get; set; }

        public TimerRuntime(
            string key,
            TimerChannelDef def,
            IScopeNode scope,
            IBlackboardService? blackboard,
            IBaseScalarService? scalar,
            ICommandRunner? runner,
            bool enableDebugLog,
            Action<TimerRuntime> onStart,
            Action<TimerRuntime> onStop)
        {
            Key = key ?? string.Empty;
            _scope = scope;
            _blackboard = blackboard;
            _scalar = scalar;
            _runner = runner;
            _enableDebugLog = enableDebugLog;
            _output = def.Output;
            _initialTime = def.InitialTime;
            _minTime = def.MinTime;
            _maxTime = def.MaxTime;
            _deltaMode = def.DeltaMode;
            _direction = def.Direction;
            _time = def.InitialTime;
            _timeScale = def.TimeScale;
            _onStart = onStart;
            _onStop = onStop;

            _triggers = BuildTriggers(def.Triggers);
            RefreshTriggerStates();
            _runVersion = 1;
            _runCommandsCts = new CancellationTokenSource();

            WriteOutput();
            LogDebug($"Created. InitialTime={_time:F3}, Direction={_direction}, Min={_minTime:F3}, Max={_maxTime:F3}");
        }

        void LogDebug(string message)
        {
            if (!_enableDebugLog)
                return;

            Debug.Log($"[TimerRuntime] {message} key={Key} time={_time:F3} running={_running} run={_runVersion}");
        }

        void RenewRunContext()
        {
            unchecked { _runVersion++; }

            if (_runCommandsCts != null)
            {
                if (!_runCommandsCts.IsCancellationRequested)
                    _runCommandsCts.Cancel();
                _runCommandsCts.Dispose();
            }

            _runCommandsCts = new CancellationTokenSource();
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            RenewRunContext();
            LogDebug("Start called");
            _onStart?.Invoke(this);
        }

        public void Stop()
        {
            if (!_running)
                return;

            _running = false;
            LogDebug("Stop called by command/API");
            _onStop?.Invoke(this);
            WriteOutput();
        }

        public void Reset()
        {
            _time = Mathf.Clamp(_initialTime, _minTime, _maxTime);
            RenewRunContext();
            LogDebug("Reset called");
            RefreshTriggerStates();
            WriteOutput();
        }

        public void SetTime(float time)
        {
            // AddCurrent と同フレームで SetTime が重なると、見た目上「一瞬だけ反映して戻る」状態になりやすい。
            // 同一フレーム内では AddCurrent の更新を優先し、次フレーム以降の SetTime を有効化する。
            if (_lastAddCurrentFrame == UnityEngine.Time.frameCount)
            {
                LogDebug("SetTime ignored because AddCurrent already updated this timer in the same frame");
                return;
            }

            _time = Mathf.Clamp(time, _minTime, _maxTime);
            RenewRunContext();
            LogDebug($"SetTime called -> {_time:F3}");
            RefreshTriggerStates();
            WriteOutput();
        }

        public bool TryAddCurrent(float delta)
        {
            if (!_running)
                return false;

            if (Mathf.Approximately(delta, 0f))
                return true;

            var prev = _time;
            var expected = prev + delta;
            _time = Mathf.Clamp(expected, _minTime, _maxTime);
            var clipped = !Mathf.Approximately(_time, expected);

            TryFireTriggers(prev, _time);
            RefreshTriggerStates();

            var movedForward = _direction == TimerDirection.Up
                ? delta > 0f
                : delta < 0f;
            if (clipped && movedForward)
            {
                LogDebug("Auto stop by boundary clamp after AddCurrent");
                _running = false;
            }

            WriteOutput();
            _lastAddCurrentFrame = UnityEngine.Time.frameCount;
            LogDebug($"AddCurrent called delta={delta:F3} -> {_time:F3}");
            return true;
        }

        public void SetTimeScale(float timeScale)
        {
            _timeScale = timeScale;
        }

        public void Advance(float deltaTimeScaled, float deltaTimeUnscaled)
        {
            if (!_running)
                return;

            var prev = _time;
            var dt = _deltaMode == TimerDeltaMode.Unscaled ? deltaTimeUnscaled : deltaTimeScaled;
            var delta = dt * _timeScale;
            var expected = _direction == TimerDirection.Down ? prev - delta : prev + delta;

            if (_direction == TimerDirection.Down)
                _time -= delta;
            else
                _time += delta;

            // Clamp to min/max range
            _time = Mathf.Clamp(_time, _minTime, _maxTime);
            var clipped = !Mathf.Approximately(_time, expected);

            TryFireTriggers(prev, _time);

            // Stop if clamped to boundary
            if (clipped)
            {
                LogDebug("Auto stop by boundary clamp");
                _running = false;
            }

            WriteOutput();
        }

        void WriteOutput()
        {
            var value = DynamicVariant.FromFloat(_time);

            switch (_output.Kind)
            {
                case TimerOutputKind.Scalar:
                    WriteScalar(_output.ScalarKey, value.AsFloat, _output.ScalarScope);
                    break;
                case TimerOutputKind.Blackboard:
                    WriteBlackboard(_output.BlackboardVarId, value, _output.BlackboardScope);
                    break;
            }
        }

        void WriteScalar(ScalarKey key, float value, TimerScalarWriteScope scope)
        {
            if (_scalar == null)
                return;

            if (scope == TimerScalarWriteScope.Global)
                _scalar.SetGlobalBase(key, value);
            else
                _scalar.SetLocalBase(key, value);
        }

        void WriteBlackboard(int varId, in DynamicVariant value, TimerBlackboardWriteScope scope)
        {
            if (_blackboard == null || varId == 0)
                return;

            if (scope == TimerBlackboardWriteScope.Global)
            {
                _blackboard.TryGlobalSetVariant(varId, in value, GlobalBlackboardSetFallback.CreateGameLogicRoot);
                return;
            }

            _blackboard.TryLocalSetVariant(varId, in value);
        }

        TriggerRuntime[] BuildTriggers(TimerTriggerEntry[]? entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<TriggerRuntime>();

            var list = new List<TriggerRuntime>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                list.Add(new TriggerRuntime(entry.TimeSeconds, entry.Commands));
            }

            return list.Count > 0 ? list.ToArray() : Array.Empty<TriggerRuntime>();
        }

        void RefreshTriggerStates()
        {
            if (_triggers == null || _triggers.Length == 0)
                return;

            for (int i = 0; i < _triggers.Length; i++)
            {
                var t = _triggers[i];
                if (t == null)
                    continue;

                t.Fired = _direction == TimerDirection.Down
                    ? _time <= t.TimeSeconds
                    : _time >= t.TimeSeconds;
            }
        }

        void TryFireTriggers(float prevTime, float currentTime)
        {
            if (_triggers == null || _triggers.Length == 0)
                return;

            for (int i = 0; i < _triggers.Length; i++)
            {
                var t = _triggers[i];
                if (t == null || t.Fired)
                    continue;

                if (!HasCrossed(prevTime, currentTime, t.TimeSeconds))
                    continue;

                t.Fired = true;
                var triggerRunVersion = _runVersion;
                var token = _runCommandsCts != null ? _runCommandsCts.Token : CancellationToken.None;
                LogDebug($"Trigger fired at {t.TimeSeconds:F3}");
                RunCommandsAsync(t.Commands, triggerRunVersion, token).Forget();
            }
        }

        bool HasCrossed(float prev, float current, float target)
        {
            if (_direction == TimerDirection.Down)
                return prev > target && current <= target;

            return prev < target && current >= target;
        }

        async UniTaskVoid RunCommandsAsync(CommandListData commands, int triggerRunVersion, CancellationToken token)
        {
            if (_runner == null || commands == null || commands.Count == 0)
                return;

            if (triggerRunVersion != _runVersion)
                return;

            try
            {
                var options = CommandRunOptions.Default;
                var ctx = new CommandContext(_scope, NullVarStore.Instance, _runner, _scope, options);
                var result = await _runner.ExecuteListAsync(commands, ctx, token, options);
                if (triggerRunVersion != _runVersion)
                    return;

                if (result.Status == CommandRunStatus.Error)
                    Debug.LogError($"[TimerRuntime] Trigger commands failed. key={Key} message={result.Message}");
            }
            catch (OperationCanceledException)
            {
                LogDebug("Trigger command execution canceled due to run switch");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
