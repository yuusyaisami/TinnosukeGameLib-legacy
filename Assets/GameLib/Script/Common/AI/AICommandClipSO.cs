#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using VNext = Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.AI
{
    /// <summary>
    /// Command ベースの AI Clip。最も一般的な使用パターン。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/Command Clip", fileName = "AICommandClip")]
    public sealed class AICommandClipSO : AIClipSO
    {
        [Header("Commands")]
        [LabelText("On Enter")]
        public VNext.CommandListData OnEnterCommands = new();

        [LabelText("On Resume")]
        public VNext.CommandListData OnResumeCommands = new();

        [LabelText("On Suspend")]
        public VNext.CommandListData OnSuspendCommands = new();

        [LabelText("On Exit")]
        public VNext.CommandListData OnExitCommands = new();

        [LabelText("On Update")]
        public VNext.CommandListData OnUpdateCommands = new();

        [Header("Debug")]
        [LabelText("Debug Log Command Context")]
        public bool DebugLogCommandContext = false;

        [Header("Monitor Rules")]
        [LabelText("Monitor Rules")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<MonitorRule> MonitorRules = new();

        [LabelText("Monitor Evaluation Mode")]
        public MonitorEvaluationMode MonitorEvaluationMode = MonitorEvaluationMode.EventDriven;

        [LabelText("Default Execution Behavior")]
        public ExecutionBehavior DefaultExecutionBehavior = ExecutionBehavior.SkipIfRunning;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (MonitorRules == null || MonitorRules.Count == 0)
                return;

            for (int i = 0; i < MonitorRules.Count; i++)
            {
                var rule = MonitorRules[i];
                rule.EnsureDefaults();
                MonitorRules[i] = rule;
            }
        }
#endif

        public override AIClipRuntime CreateRuntime(in AIAgentContext ctx)
        {
            return new AICommandClipRuntime(this);
        }
    }

    /// <summary>
    /// AICommandClipSO の Runtime
    /// </summary>
    public sealed class AICommandClipRuntime : AIClipRuntime
    {
        readonly AICommandClipSO _source;
        CancellationTokenSource? _cts;

        public AICommandClipRuntime(AICommandClipSO source)
        {
            _source = source;
        }

        protected override void OnInitialize(in AIAgentContext ctx)
        {
            _cts = new CancellationTokenSource();
        }

        public override void OnEnter(in AIAgentContext ctx)
        {
            ExecuteCommands(_source.OnEnterCommands, ctx);
        }

        public override void OnResume(in AIAgentContext ctx)
        {
            ExecuteCommands(_source.OnResumeCommands, ctx);

            // Monitor ルールを追加
            SetupMonitorRules(ctx);
        }

        public override void OnSuspend(in AIAgentContext ctx)
        {
            ExecuteCommands(_source.OnSuspendCommands, ctx);

            // Monitor ルールをクリア
            ClearMonitorRules(ctx);
        }

        public override void OnExit(in AIAgentContext ctx)
        {
            ExecuteCommands(_source.OnExitCommands, ctx);

            // CTS をキャンセル
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public override void OnUpdate(in AIAgentContext ctx)
        {
            ExecuteCommands(_source.OnUpdateCommands, ctx);
        }

        public override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void ExecuteCommands(VNext.CommandListData? commands, in AIAgentContext ctx)
        {
            if (commands == null || commands.Count == 0)
                return;

            var cmdCtx = ctx.ToCommandContext();
            var token = _cts?.Token ?? CancellationToken.None;

            if (_source.DebugLogCommandContext)
            {
                Debug.Log(
                    $"[AICommandClipRuntime] ExecuteCommands " +
                    $"Clip={_source.name} Count={commands.Count} " +
                    $"AIScope={DescribeScope(ctx.Scope)} RunnerScope={DescribeScope(ctx.Runner.Scope)} " +
                    $"CmdScope={DescribeScope(cmdCtx.Scope)} CmdActor={DescribeScope(cmdCtx.Actor)}");
            }

            // fire-and-forget ではなく、エラーハンドリング付きで実行
            RunCommandsAsync(commands, cmdCtx, token).Forget();
        }

        async UniTaskVoid RunCommandsAsync(VNext.CommandListData commands, VNext.CommandContext ctx, CancellationToken ct)
        {
            try
            {
                var result = await ctx.Runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[AICommandClipRuntime] Command execution failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void SetupMonitorRules(in AIAgentContext ctx)
        {
            var hub = GetMonitorHub(ctx);
            if (hub == null) return;

            hub.EvaluationMode = _source.MonitorEvaluationMode;
            hub.DefaultExecutionBehavior = _source.DefaultExecutionBehavior;

            for (int i = 0; i < _source.MonitorRules.Count; i++)
            {
                hub.AddRule(_source.MonitorRules[i]);
            }
        }

        void ClearMonitorRules(in AIAgentContext ctx)
        {
            var hub = GetMonitorHub(ctx);
            hub?.ClearRules();
        }

        IMonitorChannelHub? GetMonitorHub(in AIAgentContext ctx)
        {
            return ctx.MonitorHub;
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "(null)";

            var id = scope.Identity?.Id;
            var category = scope.Identity?.Category;
            var idText = string.IsNullOrEmpty(id) ? "(no-id)" : id;
            var catText = string.IsNullOrEmpty(category) ? "-" : category;
            return $"{idText}({scope.Kind},cat={catText})";
        }
    }
}
