#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.MaterialFx;
using Game.Visual;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class VisualSetStateExecutor : ICommandExecutor
    {
        static readonly IReadOnlyList<MaterialFxPresetEntry> EmptyEntries = System.Array.Empty<MaterialFxPresetEntry>();

        public int CommandId => CommandIds.VisualSetState;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not VisualSetStateCommandData cmd)
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IVisualSystem>(out var visual) || visual == null)
                return UniTask.CompletedTask;

            var selector = cmd.Selector.ToSelector();
            IReadOnlyList<MaterialFxPresetEntry> entries = EmptyEntries;

            if (cmd.MaterialFxSource.TryGet(ctx, out var payload) && payload != null)
            {
                entries = payload.Entries ?? EmptyEntries;
            }
            else if (!cmd.AllowEmpty)
            {
                return UniTask.CompletedTask;
            }

            visual.SetState(selector, entries, clearMissingKeys: cmd.ClearMissingKeys, basePriority: cmd.BasePriority);
            return UniTask.CompletedTask;
        }
    }

    public sealed class VisualBroadcastExecutor : ICommandExecutor
    {
        static readonly IReadOnlyList<MaterialFxPresetEntry> EmptyEntries = System.Array.Empty<MaterialFxPresetEntry>();

        public int CommandId => CommandIds.VisualBroadcast;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not VisualBroadcastCommandData cmd)
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IVisualSystem>(out var visual) || visual == null)
                return UniTask.CompletedTask;

            if (!cmd.MaterialFxSource.TryGet(ctx, out var payload) || payload == null)
                return UniTask.CompletedTask;

            var selector = cmd.Selector.ToSelector();
            IReadOnlyList<MaterialFxPresetEntry> entries = payload.Entries ?? EmptyEntries;
            visual.Broadcast(selector, entries, basePriority: cmd.BasePriority);

            return UniTask.CompletedTask;
        }
    }
}
