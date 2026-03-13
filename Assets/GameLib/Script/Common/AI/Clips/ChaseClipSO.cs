#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.BuildConsole;
using Game.Movement;
using Game.Targeting;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.AI.Clips
{
    /// <summary>
    /// Chase（追跡）Clip。ターゲットを追跡して移動。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/Clips/Chase", fileName = "ChaseClip")]
    public sealed class ChaseClipSO : AIClipSO
    {
        [Header("Targeting")]
        [LabelText("Target Channel Tag")]
        [Tooltip("追跡対象のターゲットチャネルタグ")]
        public string TargetChannelTag = "enemy";

        [Header("Movement")]
        [LabelText("Chase Speed")]
        [MinValue(0f)]
        public float ChaseSpeed = 5f;

        [LabelText("Arrival Distance")]
        [MinValue(0.01f)]
        public float ArrivalDistance = 1f;

        [LabelText("Movement Input Type")]
        [Tooltip("移動のブロック判定に使用する入力タイプ")]
        public MovementInputType MovementInputType = MovementInputType.AI;

        [LabelText("Start Move Delay Seconds")]
        [Tooltip("Chase が有効化されてから実際に移動を開始するまでの待機秒数")]
        [MinValue(0f)]
        public float StartMoveDelaySeconds = 0f;

        [Header("Commands")]
        [LabelText("On Active Enter")]
        [Tooltip("Chase が有効状態（Top）になった時に実行")]
        public VNext.CommandListData OnActiveEnterCommands = new();

        [LabelText("On Active Exit")]
        [Tooltip("Chase が有効状態（Top）から外れた時に実行")]
        public VNext.CommandListData OnActiveExitCommands = new();

        [Header("Behavior")]
        [LabelText("Pop On No Target")]
        [Tooltip("ターゲットがいなくなったら自動で Pop するか")]
        public bool PopOnNoTarget = true;

        [LabelText("Pop On Arrival")]
        [Tooltip("到着したら自動で Pop するか")]
        public bool PopOnArrival = true;

        public override AIClipRuntime CreateRuntime(in AIAgentContext ctx)
        {
            return new ChaseClipRuntime(this);
        }
    }

    public sealed class ChaseClipRuntime : AIClipRuntime
    {
        readonly ChaseClipSO _source;
        ITargetChannelRuntime? _targetChannel;
        MoveToInputPointRequest _request;
        CancellationTokenSource? _cts;
        float _activeElapsedSeconds;
        string _lastStatusKey = string.Empty;
        int _lastTrackingLogFrame = -1;

        public ChaseClipRuntime(ChaseClipSO source)
        {
            _source = source;
        }

        protected override void OnInitialize(in AIAgentContext ctx)
        {
            // MoveToPointRequest を事前生成（GC 回避）
            _request = new MoveToInputPointRequest(
                speed: _source.ChaseSpeed,
                arrivalDistance: _source.ArrivalDistance,
                stopOnArrive: false,
                inputType: _source.MovementInputType
            );

            _cts = new CancellationTokenSource();
        }

        public override void OnResume(in AIAgentContext ctx)
        {
            TryResolveTargetChannel(ctx);
            _activeElapsedSeconds = 0f;
            _lastStatusKey = string.Empty;
            _lastTrackingLogFrame = -1;
            LogStatus(ctx, "resume",
                $"Chase resume | Tag={_source.TargetChannelTag} MoveTo={(ctx.MoveToInputPoint != null)} TargetHub={(ctx.TargetHub != null)} Delay={_source.StartMoveDelaySeconds:F2} Speed={_source.ChaseSpeed:F2}",
                LogType.Log,
                force: true);
            ExecuteCommands(_source.OnActiveEnterCommands, ctx);
        }

        public override void OnSuspend(in AIAgentContext ctx)
        {
            // 移動をクリア
            ctx.MoveToInputPoint?.ClearTarget();
            LogStatus(ctx, "suspend", "Chase suspend", LogType.Log, force: true);
            ExecuteCommands(_source.OnActiveExitCommands, ctx);
        }

        public override void OnUpdate(in AIAgentContext ctx)
        {
            if (_targetChannel == null)
            {
                TryResolveTargetChannel(ctx);
            }

            if (_targetChannel == null)
            {
                LogStatus(ctx, "target-channel-missing",
                    $"Chase target channel missing | Tag={_source.TargetChannelTag} TargetHub={(ctx.TargetHub != null)}",
                    LogType.Warning);
                if (_source.PopOnNoTarget)
                {
                    LogStatus(ctx, "pop-no-target", "Chase pop requested: no target channel", LogType.Warning, force: true);
                    RequestPop();
                }
                return;
            }

            var hits = _targetChannel.Hits;
            if (hits == null || hits.Count == 0)
            {
                // ターゲットなし
                ctx.MoveToInputPoint?.ClearTarget();
                LogStatus(ctx, "no-hits",
                    $"Chase no target hits | Tag={_source.TargetChannelTag}",
                    LogType.Warning);
                if (_source.PopOnNoTarget)
                {
                    LogStatus(ctx, "pop-no-hits", "Chase pop requested: target hits empty", LogType.Warning, force: true);
                    RequestPop();
                }
                return;
            }

            _activeElapsedSeconds += Mathf.Max(0f, ctx.DeltaTime);
            if (_activeElapsedSeconds < _source.StartMoveDelaySeconds)
            {
                ctx.MoveToInputPoint?.ClearTarget();
                LogStatus(ctx, "delay",
                    $"Chase waiting delay | Elapsed={_activeElapsedSeconds:F2}/{_source.StartMoveDelaySeconds:F2}",
                    LogType.Log);
                return;
            }

            if (ctx.MoveToInputPoint == null)
            {
                LogStatus(ctx, "move-to-missing", "Chase cannot move: IMoveToInputPointService missing", LogType.Error);
                return;
            }

            // 最初のターゲットを追跡
            var target = hits[0];
            var targetPos = target.Position;
            var currentPos = ctx.Transform != null ? (Vector2)ctx.Transform.position : Vector2.zero;
            var distance = Vector2.Distance(currentPos, targetPos);

            if (_lastStatusKey != "tracking" || ctx.FrameCount - _lastTrackingLogFrame >= 30)
            {
                _lastTrackingLogFrame = ctx.FrameCount;
                LogStatus(ctx, "tracking",
                    $"Chase tracking | Hits={hits.Count} Target=({targetPos.x:F2},{targetPos.y:F2}) Distance={distance:F2}",
                    LogType.Log,
                    force: true);
            }

            // MoveToPoint に目標を設定
            ctx.MoveToInputPoint.SetTarget(targetPos, _request);

            // 到着判定
            if (_source.PopOnArrival)
            {
                if (distance <= _source.ArrivalDistance)
                {
                    LogStatus(ctx, "arrival", $"Chase arrived | Distance={distance:F2}", LogType.Log, force: true);
                    RequestPop();
                }
            }
        }

        public override void OnExit(in AIAgentContext ctx)
        {
            ctx.MoveToInputPoint?.ClearTarget();
            _targetChannel = null;
            LogStatus(ctx, "exit", "Chase exit", LogType.Log, force: true);
        }

        void TryResolveTargetChannel(in AIAgentContext ctx)
        {
            if (ctx.TargetHub == null || string.IsNullOrEmpty(_source.TargetChannelTag))
            {
                _targetChannel = null;
                return;
            }

            if (!ctx.TargetHub.TryGetRuntime(_source.TargetChannelTag, out var runtime) || runtime == null)
            {
                _targetChannel = null;
                return;
            }

            _targetChannel = runtime;
        }

        public override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void LogStatus(in AIAgentContext ctx, string key, string message, LogType logType, bool force = false)
        {
            if (!force && string.Equals(_lastStatusKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _lastStatusKey = key;
            BuildConsoleLog.Scope(ctx.Scope, message, logType);
        }

        void ExecuteCommands(VNext.CommandListData? commands, in AIAgentContext ctx)
        {
            if (commands == null || commands.Count == 0)
                return;

            var cmdCtx = ctx.ToCommandContext();
            var token = _cts?.Token ?? CancellationToken.None;
            RunCommandsAsync(commands, cmdCtx, token).Forget();
        }

        async UniTaskVoid RunCommandsAsync(VNext.CommandListData commands, VNext.CommandContext ctx, CancellationToken ct)
        {
            try
            {
                var result = await ctx.Runner.ExecuteListAsync(commands, ctx, ct, ctx.Options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[ChaseClipRuntime] Command execution failed: {result.Message}");
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
