#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Game.ActionBlock.Keys;
using Game.Commands;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.BuildConsole;
using Game.Input;
using Game.Movement;
using Game.Targeting;
using UnityEngine;
using VContainer.Unity;

namespace Game.AI
{
    /// <summary>
    /// AI の状態管理サービス実装
    /// </summary>
    public sealed class AIStateService : IAIStateService, IAIStateTelemetry, ITickable
    {
        // 設定
        readonly AIClipProfileSO _profile;
        readonly int _maxStackDepth;
        readonly int _maxTransitionsPerFrame;

        // コンテキスト構築用
        readonly IScopeNode _scope;
        readonly Component? _scopeComponent;
        readonly ITargetChannelHub? _targetHub;
        readonly IMoveToInputPointService? _moveToPoint;
        readonly IMovementChannelHub? _movementHub;
        readonly VNext.ICommandRunner _runner;
        readonly IActionBlockService? _actionBlockService;

        // MonitorHub は DI から取得（CommandRunnerMB が登録済）
        readonly IMonitorChannelHub _monitorHub;

        // 状態
        readonly VarStore _vars = new();
        readonly AIClipRuntime?[] _stack;
        readonly Dictionary<string, AIClipRuntime> _runtimeCache = new(StringComparer.Ordinal);

        int _stackTop = -1;  // 空の場合は -1
        int _transitionCountThisFrame;
        bool _disposed;

        // テレメトリ
        int _telemetryVersion;

        // デバッグ用
        readonly Queue<(int frame, string desc)> _transitionHistory = new(16);
        bool _wasBuildConsoleBlocked;

        public string? ActiveClipKey => _stackTop >= 0 ? _stack[_stackTop]?.StableKey : null;
        public int StackDepth => _stackTop + 1;
        public IVarStore Vars => _vars;
        public IMonitorChannelHub MonitorHub => _monitorHub;

        /// <summary>
        /// AI が ActionBlock (Entity.AIControl) でブロックされているか
        /// </summary>
        public bool IsBlocked => _actionBlockService?.IsBlocked(ActionBlockKeys.Entity.AIControl) ?? false;

        // ================================================================
        // IAIStateTelemetry
        // ================================================================

        public int TelemetryVersion => _telemetryVersion;

        public AIStateSnapshot GetSnapshot()
        {
            var stackEntries = new List<AIClipStackEntry>();
            for (int i = 0; i <= _stackTop; i++)
            {
                var clip = _stack[i];
                if (clip != null)
                {
                    stackEntries.Add(new AIClipStackEntry(
                        i,
                        clip.StableKey,
                        clip.Priority,
                        i == _stackTop,
                        clip.IsPopRequested
                    ));
                }
            }

            var transitions = new List<AITransitionEntry>();
            foreach (var (frame, desc) in _transitionHistory)
            {
                transitions.Add(new AITransitionEntry(frame, desc));
            }

            return new AIStateSnapshot(
                _telemetryVersion,
                ActiveClipKey,
                StackDepth,
                IsBlocked,
                stackEntries,
                transitions
            );
        }

        void BumpTelemetry()
        {
            unchecked { _telemetryVersion++; }
        }

        // ================================================================
        // コンストラクタ
        // ================================================================

        public AIStateService(
            AIClipProfileSO profile,
            IScopeNode scope,
            VNext.ICommandRunner runner,
            IMonitorChannelHub monitorHub,
            ITargetChannelHub? targetHub = null,
            IMoveToInputPointService? moveToPoint = null,
            IMovementChannelHub? movementHub = null,
            IActionBlockService? actionBlockService = null)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _scopeComponent = scope as Component;
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _monitorHub = monitorHub ?? throw new ArgumentNullException(nameof(monitorHub));
            _targetHub = targetHub;
            _moveToPoint = moveToPoint;
            _movementHub = movementHub;
            _actionBlockService = actionBlockService;

            _maxStackDepth = Mathf.Max(1, profile.MaxStackDepth);
            _maxTransitionsPerFrame = Mathf.Max(1, profile.MaxTransitionsPerFrame);
            _stack = new AIClipRuntime[_maxStackDepth];

            // MonitorHub を Agent の Vars にアタッチ
            _monitorHub.AttachToVars(_vars);

            // プロファイルの初期変数をコピー
            if (profile.InitialVariables != null)
            {
                profile.InitialVariables.ApplyTo(_vars, overwrite: true);
            }

            // デフォルト Clip を Push
            if (profile.DefaultClip != null)
            {
                PushClipInternal(profile.DefaultClip, CreateContext());
            }

            BumpTelemetry();
        }

        AIAgentContext CreateContext()
        {
            return new AIAgentContext(
                _scope, _scopeComponent, _vars, _monitorHub, _targetHub, _moveToPoint, _movementHub, _runner,
                Time.deltaTime, Time.frameCount);
        }

        public void Tick(float deltaTime)
        {
            if (_disposed) return;
            if (_stackTop < 0) return;

            // ActionBlock によるブロック中は AI の Update を行わない
            if (IsBlocked)
            {
                if (!_wasBuildConsoleBlocked)
                {
                    _wasBuildConsoleBlocked = true;
                    BuildConsoleLog.Scope(_scope, "AIState blocked by ActionBlock(Entity.AIControl)", LogType.Warning);
                }
                return;
            }

            if (_wasBuildConsoleBlocked)
            {
                _wasBuildConsoleBlocked = false;
                BuildConsoleLog.Scope(_scope, "AIState unblocked", LogType.Log);
            }

            _transitionCountThisFrame = 0;
            var ctx = CreateContext();

            // 1) Interrupt 評価
            EvaluateInterrupts(ctx);

            // 2) RequestPop 処理
            ProcessPopRequests(ctx);

            // 3) Active Clip の Update
            UpdateActiveClip(ctx);

            // Note: MonitorChannelHub は ITickable として VContainer が自動 Tick するため、
            // ここでの手動 Tick は不要
        }

        void ITickable.Tick()
        {
            Tick(Time.deltaTime);
        }

        void EvaluateInterrupts(in AIAgentContext ctx)
        {
            if (_stackTop < 0) return;

            var active = _stack[_stackTop];
            if (active == null) return;

            var triggered = active.EvaluateInterrupts(ctx);
            if (triggered != null)
            {
                ApplyInterrupt(triggered, ctx);
            }
        }

        void ApplyInterrupt(InterruptRuleRuntime rule, in AIAgentContext ctx)
        {
            if (_transitionCountThisFrame >= _maxTransitionsPerFrame)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException(
                    $"[AIStateService] MaxTransitionsPerFrame exceeded ({_maxTransitionsPerFrame})");
#else
                Debug.LogWarning($"[AIStateService] MaxTransitionsPerFrame exceeded. Skipping interrupt.");
                return;
#endif
            }

            if (rule.TargetClip == null) return;

            switch (rule.Policy)
            {
                case InterruptPolicy.Push:
                    PushClipInternal(rule.TargetClip, ctx);
                    break;

                case InterruptPolicy.Replace:
                    PopClipInternal(ctx);
                    PushClipInternal(rule.TargetClip, ctx);
                    break;

                case InterruptPolicy.PopUntil:
                    PopUntilClip(rule.TargetClip.StableKey, ctx);
                    break;
            }

            _transitionCountThisFrame++;
            RecordTransition($"Interrupt -> {rule.TargetClip.StableKey}");
        }

        void ProcessPopRequests(in AIAgentContext ctx)
        {
            while (_stackTop >= 0 && _stack[_stackTop]?.IsPopRequested == true)
            {
                if (_transitionCountThisFrame >= _maxTransitionsPerFrame)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    throw new InvalidOperationException(
                        $"[AIStateService] MaxTransitionsPerFrame exceeded during pop requests");
#else
                    Debug.LogWarning($"[AIStateService] MaxTransitionsPerFrame exceeded. Stopping pop chain.");
                    break;
#endif
                }

                PopClipInternal(ctx);
                _transitionCountThisFrame++;
            }
        }

        void UpdateActiveClip(in AIAgentContext ctx)
        {
            if (_stackTop < 0) return;

            var active = _stack[_stackTop];
            if (active == null) return;

            if (active.ShouldUpdate(ctx.FrameCount))
            {
                active.OnUpdate(ctx);
            }
        }

        // ================================================================
        // Push / Pop
        // ================================================================

        public void PushClip(AIClipSO clip)
        {
            if (_disposed) return;
            PushClipInternal(clip, CreateContext());
        }

        public void PopClip()
        {
            if (_disposed) return;
            PopClipInternal(CreateContext());
        }

        void PushClipInternal(AIClipSO? clip, in AIAgentContext ctx)
        {
            if (clip == null) return;

            // Reenter チェック
            if (_stackTop >= 0 && !clip.AllowReenter)
            {
                if (_stack[_stackTop]?.StableKey == clip.StableKey)
                {
                    if (_profile.EnableDebugLogging)
                    {
                        Debug.LogWarning($"[AIStateService] Reenter blocked for {clip.StableKey}");
                    }
                    return;
                }
            }

            // スタック溢れチェック
            if (_stackTop + 1 >= _maxStackDepth)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException(
                    $"[AIStateService] Stack overflow. MaxDepth={_maxStackDepth}");
#else
                Debug.LogError($"[AIStateService] Stack overflow. Cannot push {clip.StableKey}");
                return;
#endif
            }

            // 旧 Top を Suspend
            if (_stackTop >= 0)
            {
                _stack[_stackTop]?.OnSuspend(ctx);
            }

            // Runtime を取得または生成
            var runtime = GetOrCreateRuntime(clip, ctx);

            // Push
            _stackTop++;
            _stack[_stackTop] = runtime;

            // Enter → Resume
            runtime.OnEnter(ctx);
            runtime.OnResume(ctx);

            RecordTransition($"Push {clip.StableKey}");
            BumpTelemetry();
        }

        void PopClipInternal(in AIAgentContext ctx)
        {
            if (_stackTop < 0) return;

            var popping = _stack[_stackTop];
            if (popping != null)
            {
                popping.ClearPopRequest();

                // Suspend → Exit
                popping.OnSuspend(ctx);
                popping.OnExit(ctx);

                RecordTransition($"Pop {popping.StableKey}");
            }

            _stack[_stackTop] = null;
            _stackTop--;

            // 新 Top を Resume
            if (_stackTop >= 0)
            {
                _stack[_stackTop]?.OnResume(ctx);
            }

            BumpTelemetry();
        }

        void PopUntilClip(string targetKey, in AIAgentContext ctx)
        {
            while (_stackTop >= 0 && _stack[_stackTop]?.StableKey != targetKey)
            {
                PopClipInternal(ctx);
            }

            // ターゲットが見つかった場合、Resume を呼ぶ
            if (_stackTop >= 0 && _stack[_stackTop]?.StableKey == targetKey)
            {
                _stack[_stackTop]?.OnResume(ctx);
            }
        }

        AIClipRuntime GetOrCreateRuntime(AIClipSO clip, in AIAgentContext ctx)
        {
            // キャッシュから取得（GC 回避）
            if (_runtimeCache.TryGetValue(clip.StableKey, out var cached))
            {
                return cached;
            }

            // 新規生成
            var runtime = clip.CreateRuntime(ctx);
            runtime.Initialize(clip, ctx);
            _runtimeCache[clip.StableKey] = runtime;

            return runtime;
        }

        // ================================================================
        // Debug
        // ================================================================

        void RecordTransition(string description)
        {
            if (_transitionHistory.Count >= 16)
                _transitionHistory.Dequeue();
            _transitionHistory.Enqueue((Time.frameCount, description));
        }

        public string GetStackDump()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[AIStateService] Stack (depth={StackDepth}):");
            for (int i = _stackTop; i >= 0; i--)
            {
                var clip = _stack[i];
                var marker = (i == _stackTop) ? " <-- TOP" : "";
                sb.AppendLine($"  [{i}] {clip?.StableKey ?? "(null)"}{marker}");
            }
            sb.AppendLine("Recent transitions:");
            foreach (var (frame, desc) in _transitionHistory)
            {
                sb.AppendLine($"  [{frame}] {desc}");
            }
            return sb.ToString();
        }

        // ================================================================
        // Dispose
        // ================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // MonitorHub から Detach
            _monitorHub.DetachFromVars(_vars);

            // 全 Clip の OnExit と OnDispose を呼ぶ
            var ctx = CreateContext();
            while (_stackTop >= 0)
            {
                var clip = _stack[_stackTop];
                if (clip != null)
                {
                    clip.OnSuspend(ctx);
                    clip.OnExit(ctx);
                }
                _stack[_stackTop] = null;
                _stackTop--;
            }

            foreach (var runtime in _runtimeCache.Values)
            {
                runtime.OnDispose();
            }
            _runtimeCache.Clear();

            BumpTelemetry();
        }
    }
}
