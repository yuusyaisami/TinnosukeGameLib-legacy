#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;

namespace Game.Commands.VNext
{
    public sealed class FunctionExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Function;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not FunctionCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "FunctionCommandData is required.");

            var commands = typed.Function?.Commands;
            if (commands == null || commands.Count == 0)
                return;

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var mergedVars = new VarStore();
            (ctx.Vars ?? NullVarStore.Instance).MergeInto(mergedVars, overwrite: true);
            ApplyInitialVars(typed.InitialVars, ctx, mergedVars);

            var runCtx = new CommandContext(
                ctx.Scope,
                mergedVars,
                runner,
                ctx.Actor,
                ctx.Options,
                ctx.CommandRootScope,
                ctx.RootActor,
                ctx.CallerActor,
                ctx);

            var result = await runner.ExecuteListAsync(commands, runCtx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }

        static void ApplyInitialVars(
            System.Collections.Generic.IReadOnlyList<FunctionInitialVarEntry>? entries,
            CommandContext ctx,
            IVarStore dest)
        {
            if (entries == null || entries.Count == 0 || dest == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var varId = ResolveVarId(entry.Key);
                if (varId == 0)
                    continue;

                var value = entry.Value.Evaluate(ctx);
                if (value.Kind == ValueKind.ManagedRef)
                {
                    if (value.AsManagedRef != null)
                        dest.TrySetManagedRef(varId, value.AsManagedRef);
                    else
                        dest.TryUnset(varId);
                    continue;
                }

                if (value.Kind == ValueKind.Null)
                {
                    dest.TryUnset(varId);
                    continue;
                }

                dest.TrySetVariant(varId, value);
            }
        }

        static int ResolveVarId(VarKeyRef key)
        {
            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolved) && resolved != 0)
                return resolved;

            return key.VarId;
        }
    }
}
