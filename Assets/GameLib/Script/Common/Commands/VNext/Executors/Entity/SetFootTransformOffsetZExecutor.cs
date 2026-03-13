#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Entity;
using UnityEngine;
using VContainer;
namespace Game.Commands.VNext
{
    public sealed class SetFootTransformOffsetZExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetFootTransformOffsetZ;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetFootTransformOffsetZCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetFootTransformOffsetZCommandData is required.");

            var foot = ResolveFootTransform(ctx);
            if (foot == null)
            {
                return UniTask.CompletedTask;
            }

            var amount = typed.Value.Resolve(ctx);
            if (typed.Mode == FootTransformOffsetZMode.Add)
            {
                foot.OffsetZ += amount;
            }
            else
            {
                foot.OffsetZ = amount;
            }

            return UniTask.CompletedTask;
        }

        static FootTransformMB? ResolveFootTransform(CommandContext ctx)
        {
            if (ctx == null)
                return null;

            if (ctx.Resolver.TryResolve<FootTransformMB>(out var direct) && direct != null)
                return direct;

            if (ctx.Scope is Component scopeComponent)
            {
                var found = scopeComponent.GetComponent<FootTransformMB>();
                if (found != null)
                    return found;

                found = scopeComponent.GetComponentInChildren<FootTransformMB>();
                if (found != null)
                    return found;
            }

            if (ctx.Actor is Component actorComponent)
            {
                var foundActor = actorComponent.GetComponent<FootTransformMB>();
                if (foundActor != null)
                    return foundActor;
            }

            return null;
        }
    }
}
