#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class ScopeLifecycleConditionExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ScopeLifecycleCondition;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ScopeLifecycleConditionCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ScopeLifecycleConditionCommandData is required.");

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "Target scope could not be resolved.");

            var resolver = targetScope.Resolver;
            if (resolver == null || !resolver.TryResolve<IScopeLifecycleConditionController>(out var controller) || controller == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IScopeLifecycleConditionController is missing on target scope.");

            if (typed.Operation == ScopeLifecycleConditionOperation.ClearOverride)
            {
                controller.ClearConditionOverride();
                return;
            }

            controller.SetConditionOverride(typed.Condition);
        }
    }
}
