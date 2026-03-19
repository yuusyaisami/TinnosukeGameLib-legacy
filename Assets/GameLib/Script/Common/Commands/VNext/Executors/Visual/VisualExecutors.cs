#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.MaterialFx;
using Game.Visual;
using VContainer;

namespace Game.Commands.VNext
{
    static class VisualMaterialFxEntryResolver
    {
        public static IReadOnlyList<MaterialFxPresetEntry> ResolveEntries(IReadOnlyList<MaterialFxPresetEntry>? entries, CommandContext ctx)
        {
            if (entries == null || entries.Count == 0)
                return System.Array.Empty<MaterialFxPresetEntry>();

            var resolved = new MaterialFxPresetEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                resolved[i] = entries[i].Resolve(ctx);

            return resolved;
        }
    }

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
                entries = VisualMaterialFxEntryResolver.ResolveEntries(payload.Entries, ctx);
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
            IReadOnlyList<MaterialFxPresetEntry> entries = VisualMaterialFxEntryResolver.ResolveEntries(payload.Entries, ctx);
            visual.Broadcast(selector, entries, basePriority: cmd.BasePriority);

            return UniTask.CompletedTask;
        }
    }
}
