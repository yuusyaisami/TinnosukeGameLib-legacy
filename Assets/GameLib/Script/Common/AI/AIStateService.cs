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
    /// AI 縺ｮ迥ｶ諷狗ｮ｡逅・し繝ｼ繝薙せ螳溯｣・
    /// </summary>
    public sealed class AIStateService : IAIStateService, IAIStateTelemetry, IScopeTickHandler
    {
        // 險ｭ螳・
        readonly AIClipProfileSO _profile;
        readonly int _maxStackDepth;
        readonly int _maxTransitionsPerFrame;

        // 繧ｳ繝ｳ繝・く繧ｹ繝域ｧ狗ｯ臥畑
        readonly IScopeNode _scope;
        readonly Component? _scopeComponent;
        readonly ITargetChannelHub? _targetHub;
        readonly IMoveToInputPointService? _moveToPoint;
        readonly IMovementChannelHub? _movementHub;
        readonly VNext.ICommandRunner _runner;
        readonly IActionBlockService? _actionBlockService;

        // MonitorHub 縺ｯ DI 縺九ｉ蜿門ｾ暦ｼ・ommandRunnerMB 縺檎匳骭ｲ貂茨ｼ・
        readonly IMonitorChannelHub _monitorHub;

        // 迥ｶ諷・
        readonly VarStore _vars = new();
        readonly AIClipRuntime?[] _stack;
        readonly Dictionary<string, AIClipRuntime> _runtimeCache = new(StringComparer.Ordinal);

        int _stackTop = -1;  // 遨ｺ縺ｮ蝣ｴ蜷医・ -1
        int _transitionCountThisFrame;
        bool _disposed;

        // 繝・Ξ繝｡繝医Μ
        int _telemetryVersion;

        // 繝・ヰ繝・げ逕ｨ
        readonly Queue<(int frame, string desc)> _transitionHistory = new(16);
        bool _wasBuildConsoleBlocked;

        public string? ActiveClipKey => _stackTop >= 0 ? _stack[_stackTop]?.StableKey : null;
        public int StackDepth => _stackTop + 1;
        public IVarStore Vars => _vars;
        public IMonitorChannelHub MonitorHub => _monitorHub;

        /// <summary>
        /// AI 縺・ActionBlock (Entity.AIControl) 縺ｧ繝悶Ο繝・け縺輔ｌ縺ｦ縺・ｋ縺・
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
        // 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ
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

            // MonitorHub 繧・Agent 縺ｮ Vars 縺ｫ繧｢繧ｿ繝・メ
            _monitorHub.AttachToVars(_vars);

            // 繝励Ο繝輔ぃ繧､繝ｫ縺ｮ蛻晄悄螟画焚繧偵さ繝斐・
            if (profile.InitialVariables != null)
            {
                profile.InitialVariables.ApplyTo(_vars, overwrite: true);
            }

            // 繝・ヵ繧ｩ繝ｫ繝・Clip 繧・Push
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

            // ActionBlock 縺ｫ繧医ｋ繝悶Ο繝・け荳ｭ縺ｯ AI 縺ｮ Update 繧定｡後ｏ縺ｪ縺・
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

            // 1) Interrupt 隧穂ｾ｡
            EvaluateInterrupts(ctx);

            // 2) RequestPop 蜃ｦ逅・
            ProcessPopRequests(ctx);

            // 3) Active Clip 縺ｮ Update
            UpdateActiveClip(ctx);

            // Note: MonitorChannelHub 縺ｯ IScopeTickHandler 縺ｨ縺励※ VContainer 縺瑚・蜍・Tick 縺吶ｋ縺溘ａ縲・
            // 縺薙％縺ｧ縺ｮ謇句虚 Tick 縺ｯ荳崎ｦ・
        }

        void IScopeTickHandler.Tick()
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

            // Reenter 繝√ぉ繝・け
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

            // 繧ｹ繧ｿ繝・け貅｢繧後メ繧ｧ繝・け
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

            // 譌ｧ Top 繧・Suspend
            if (_stackTop >= 0)
            {
                _stack[_stackTop]?.OnSuspend(ctx);
            }

            // Runtime 繧貞叙蠕励∪縺溘・逕滓・
            var runtime = GetOrCreateRuntime(clip, ctx);

            // Push
            _stackTop++;
            _stack[_stackTop] = runtime;

            // Enter 竊・Resume
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

                // Suspend 竊・Exit
                popping.OnSuspend(ctx);
                popping.OnExit(ctx);

                RecordTransition($"Pop {popping.StableKey}");
            }

            _stack[_stackTop] = null;
            _stackTop--;

            // 譁ｰ Top 繧・Resume
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

            // 繧ｿ繝ｼ繧ｲ繝・ヨ縺瑚ｦ九▽縺九▲縺溷ｴ蜷医ヽesume 繧貞他縺ｶ
            if (_stackTop >= 0 && _stack[_stackTop]?.StableKey == targetKey)
            {
                _stack[_stackTop]?.OnResume(ctx);
            }
        }

        AIClipRuntime GetOrCreateRuntime(AIClipSO clip, in AIAgentContext ctx)
        {
            // 繧ｭ繝｣繝・す繝･縺九ｉ蜿門ｾ暦ｼ・C 蝗樣∩・・
            if (_runtimeCache.TryGetValue(clip.StableKey, out var cached))
            {
                return cached;
            }

            // 譁ｰ隕冗函謌・
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

            // MonitorHub 縺九ｉ Detach
            _monitorHub.DetachFromVars(_vars);

            // 蜈ｨ Clip 縺ｮ OnExit 縺ｨ OnDispose 繧貞他縺ｶ
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
