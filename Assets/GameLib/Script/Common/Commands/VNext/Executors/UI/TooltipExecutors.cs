#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Channel;
using Game.Common;

namespace Game.Commands.VNext
{
    public sealed class ShowTooltipExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ShowTooltip;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ShowTooltipCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ShowTooltipCommandData is required.");

            if (!ctx.Scope.TryResolveInAncestors<ITextChannelHubService>(out var hub) || hub == null)
                return;

            var channelKey = string.IsNullOrWhiteSpace(typed.textChannelKey) ? "default" : typed.textChannelKey;
            if (!hub.TryGetPlayer(channelKey, out var player) || player == null)
                return;

            var text = typed.text.Resolve(ctx);
            player.SetText(text, TextPlayMode.Instant);
            player.SetVisible(true);

            if (typed.contentCommands == null || typed.contentCommands.Count == 0)
                return;

            if (ctx.Runner == null)
                return;

            var result = await ctx.Runner.ExecuteListAsync(typed.contentCommands, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message ?? "Tooltip content commands failed.");
        }
    }

    public sealed class HideTooltipExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.HideTooltip;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not HideTooltipCommandData)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "HideTooltipCommandData is required.");

            if (!ctx.Scope.TryResolveInAncestors<ITextChannelHubService>(out var hub) || hub == null)
                return UniTask.CompletedTask;

            var players = hub.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null)
                    continue;
                p.SetVisible(false);
            }

            return UniTask.CompletedTask;
        }
    }
}
