#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CommandChannelControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CommandChannelControl;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CommandChannelControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CommandChannelControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var ownerScope = ResolveOwnerScope(typed, ctx);
            if (ownerScope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "CommandChannel owner scope is null.");

            EnsureScopeBuiltIfNeeded(ownerScope);

            if (!TryResolve(ownerScope, out ICommandChannelHubService? hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ICommandChannelHubService is missing on target scope.");

            switch (typed.Operation)
            {
                case CommandChannelControlOperation.RegisterOrUpdate:
                    ValidateTag(typed.Tag, typed.Operation);
                    if (!TryResolveCommandList(typed.RegisterCommands, ownerScope, ctx, out var registerCommands) || registerCommands == null)
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to resolve register command list source.");

                    if (!hub.RegisterOrUpdate(typed.Tag, registerCommands))
                        throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Failed to register or update command channel entry.");
                    break;

                case CommandChannelControlOperation.Unregister:
                    ValidateTag(typed.Tag, typed.Operation);
                    hub.Unregister(typed.Tag);
                    break;

                case CommandChannelControlOperation.ClearAll:
                    hub.Clear();
                    break;

                case CommandChannelControlOperation.MutateCommands:
                    ValidateTag(typed.Tag, typed.Operation);
                    TryResolve(ownerScope, out ICommandListRuntimeMutationService? mutationService);

                    var mutationStep = new CommandListMutationStep
                    {
                        Operation = typed.Mutation.Operation,
                        Commands = typed.Mutation.Commands,
                    };

                    if (mutationStep.RequiresCommands())
                    {
                        if (!TryResolveCommandList(typed.MutationCommands, ownerScope, ctx, out var mutationCommands) || mutationCommands == null)
                            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to resolve mutation command list source.");

                        mutationStep.Commands = mutationCommands;
                    }

                    if (!hub.MutateCommands(typed.Tag, mutationStep, mutationService))
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to mutate command list for command channel tag.");
                    break;

                case CommandChannelControlOperation.SetPayload:
                    ValidateTag(typed.Tag, typed.Operation);
                    if (!hub.SetPayload(typed.Tag, typed.Payload, typed.PayloadOverwriteExistingVars))
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to set payload for command channel tag.");
                    break;

                case CommandChannelControlOperation.ClearPayload:
                    ValidateTag(typed.Tag, typed.Operation);
                    if (!hub.ClearPayload(typed.Tag))
                        throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Failed to clear payload for command channel tag.");
                    break;

                default:
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Unsupported operation: {typed.Operation}");
            }

            return UniTask.CompletedTask;
        }

        static IScopeNode? ResolveOwnerScope(CommandChannelControlCommandData typed, CommandContext ctx)
        {
            var ownerScope = ActorSourceFastResolver.Resolve(ctx, typed.ActorSource);
            if (typed.ActorSource.Kind != ActorSourceKind.Current && ownerScope == null)
            {
                var message =
                    $"CommandChannel owner resolve failed. RequestedOwner={typed.ActorSource.Kind}, Tag={typed.Tag}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[CommandChannelControlExecutor] {message}");
#endif
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, message);
            }

            return ownerScope ?? ctx.Actor ?? ctx.Scope;
        }

        static void ValidateTag(string tag, CommandChannelControlOperation operation)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                return;

            throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Tag is required for {operation}.");
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }

        static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve<T>(out var resolved) && (value = resolved) != null;
        }

        static bool TryResolveCommandList(
            DynamicValue<CommandListData> source,
            IScopeNode ownerScope,
            CommandContext ctx,
            out CommandListData? commands)
        {
            commands = null;
            var dynamicContext = new SimpleDynamicContext(ctx.Vars, ownerScope);
            if (!source.TryGet(dynamicContext, out var resolved) || resolved == null)
                return false;

            commands = resolved;
            return true;
        }
    }
}
