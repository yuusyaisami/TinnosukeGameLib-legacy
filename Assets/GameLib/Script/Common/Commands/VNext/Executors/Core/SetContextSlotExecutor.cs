#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Commands.VNext
{
    public sealed class SetContextSlotExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetContextSlot;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;

            if (data is not SetContextSlotCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetContextSlotCommandData is required.");

            if (!CommandLtsSlotUtility.IsContextSlot(typed.Slot))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Context slot is required. slot={typed.Slot}");

            var targetScope = ActorSourceFastResolver.Resolve(ctx, typed.ActorSource);
            ctx.SetScope(typed.Slot, targetScope);
            return UniTask.CompletedTask;
        }
    }
}
