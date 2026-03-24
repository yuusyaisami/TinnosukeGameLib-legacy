#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Trait;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class WriteTraitDataExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WriteTraitData;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WriteTraitDataCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteTraitDataCommandData is required.");

            var dynCtx = new SimpleDynamicContext(ctx.Vars, ctx.Scope);
            if (!typed.TraitSource.TryGet(dynCtx, out var trait) || trait == null)
                return UniTask.CompletedTask;

            var origin = ctx.Actor ?? ctx.Scope;
            var targetScope = ActorSourceFastResolver.Resolve(ctx, typed.TargetActorSource, origin) ?? origin;
            if (!TryResolveBlackboard(targetScope, out var blackboard) || blackboard == null)
                return UniTask.CompletedTask;

            var traitContext = new TraitInstanceContext(targetScope);
            trait.CreateInstance(traitContext);
            traitContext.Vars.MergeInto(blackboard.LocalVars, typed.Overwrite);

            return UniTask.CompletedTask;
        }

        static bool TryResolveBlackboard(IScopeNode? scope, out IBlackboardService? blackboard)
        {
            blackboard = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            if (resolver.TryResolve<IBlackboardService>(out var resolved) && resolved != null)
            {
                blackboard = resolved;
                return true;
            }

            return false;
        }
    }
}
