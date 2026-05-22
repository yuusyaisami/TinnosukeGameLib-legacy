#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Trait;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class RuntimeTraitPresentationCommandMutationExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RuntimeTraitPresentationCommandMutation;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RuntimeTraitPresentationCommandMutationCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RuntimeTraitPresentationCommandMutationCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.TargetActorSource, ctx, ct);
            if (targetScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "Target scope could not be resolved.");

            EnsureScopeBuiltIfNeeded(targetScope);

            if (targetScope.Resolver == null ||
                !targetScope.Resolver.TryResolve<IRuntimeTraitPresentationCommandMutationService>(out var presentationMutationService) ||
                presentationMutationService == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IRuntimeTraitPresentationCommandMutationService is missing on target scope.");
            }

            targetScope.Resolver.TryResolve<ICommandListRuntimeMutationService>(out var mutationService);

            var mutationStep = new CommandListMutationStep
            {
                Operation = typed.Mutation.Operation,
                Commands = typed.Mutation.Commands,
            };

            if (mutationStep.RequiresCommands() && mutationStep.Commands == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Mutation commands are required.");

            if (!presentationMutationService.MutatePresentationCommands(typed.TargetStream, mutationStep, mutationService))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to mutate runtime trait presentation commands.");
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }
    }
}
