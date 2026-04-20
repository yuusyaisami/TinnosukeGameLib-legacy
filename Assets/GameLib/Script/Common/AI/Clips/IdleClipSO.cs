#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using VNext = Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.AI.Clips
{
    /// <summary>
    /// Idle・亥ｾ・ｩ滂ｼ韻lip縲らｧｻ蜍暮溷ｺｦ繧・縺ｫ縺励※髱呎ｭ｢縲・
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/Clips/Idle", fileName = "IdleClip")]
    public sealed class IdleClipSO : AIClipSO
    {
        [Header("Movement")]
        [LabelText("Movement Channel Key")]
        [Tooltip("遘ｻ蜍輔メ繝｣繝阪Ν縺ｮ繧ｭ繝ｼ")]
        public string MovementChannelKey = "ai";

        [Header("Commands")]
        [LabelText("On Active Enter")]
        [Tooltip("Inspector setting.")]
        public VNext.CommandListData OnActiveEnterCommands = new();

        [LabelText("On Active Exit")]
        [Tooltip("Inspector setting.")]
        public VNext.CommandListData OnActiveExitCommands = new();

        public override AIClipRuntime CreateRuntime(in AIAgentContext ctx)
        {
            return new IdleClipRuntime(this);
        }
    }

    public sealed class IdleClipRuntime : AIClipRuntime
    {
        readonly IdleClipSO _source;
        CancellationTokenSource? _cts;

        public IdleClipRuntime(IdleClipSO source)
        {
            _source = source;
        }

        protected override void OnInitialize(in AIAgentContext ctx)
        {
            _cts = new CancellationTokenSource();
        }

        public override void OnEnter(in AIAgentContext ctx)
        {
            // 遘ｻ蜍輔ｒ蛛懈ｭ｢
            StopMovement(ctx);
        }

        public override void OnResume(in AIAgentContext ctx)
        {
            // 遘ｻ蜍輔ｒ蛛懈ｭ｢
            StopMovement(ctx);
            ExecuteCommands(_source.OnActiveEnterCommands, ctx);
        }

        public override void OnSuspend(in AIAgentContext ctx)
        {
            ExecuteCommands(_source.OnActiveExitCommands, ctx);
        }

        public override void OnDispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        void StopMovement(in AIAgentContext ctx)
        {
            if (ctx.MovementHub == null) return;

            if (ctx.MovementHub.TryGetChannel(_source.MovementChannelKey, out var channel))
            {
                channel.SetImmediateVelocity(Vector2.zero);
            }

            // MoveToInputPoint 繧ゅけ繝ｪ繧｢
            ctx.MoveToInputPoint?.ClearTarget();
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
                    Debug.LogError($"[IdleClipRuntime] Command execution failed: {result.Message}");
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
