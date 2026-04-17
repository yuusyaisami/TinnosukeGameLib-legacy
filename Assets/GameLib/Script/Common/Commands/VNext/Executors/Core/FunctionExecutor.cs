#nullable enable

using System;
using System.Collections.Generic;
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

            // Keep a baseline after initial-arg injection so untouched args don't leak back.
            var baselineVars = new VarStore();
            mergedVars.MergeInto(baselineVars, overwrite: true);

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

            ApplyFunctionOutputsToCaller(ctx.Vars, baselineVars, mergedVars);
        }

        static void ApplyFunctionOutputsToCaller(IVarStore? callerVars, IVarStore before, IVarStore after)
        {
            if (callerVars == null || callerVars is NullVarStore)
                return;

            var handled = new HashSet<int>();
            ApplyChangedAndAddedIds(before, after, callerVars, handled);
            ApplyRemovedIds(before, after, callerVars, handled);
        }

        static void ApplyChangedAndAddedIds(IVarStore before, IVarStore after, IVarStore destination, HashSet<int> handled)
        {
            foreach (var varId in after.EnumerateVarIds())
            {
                if (varId == 0 || !handled.Add(varId))
                    continue;

                if (AreSameValue(after, before, varId))
                    continue;

                ApplyVarValue(destination, after, varId);
            }
        }

        static void ApplyRemovedIds(IVarStore before, IVarStore after, IVarStore destination, HashSet<int> handled)
        {
            foreach (var varId in before.EnumerateVarIds())
            {
                if (varId == 0 || !handled.Add(varId))
                    continue;

                if (after.Contains(varId))
                    continue;

                destination.TryUnset(varId);
            }
        }

        static bool AreSameValue(IVarStore left, IVarStore right, int varId)
        {
            var leftKind = left.GetVarKind(varId);
            var rightKind = right.GetVarKind(varId);
            if (leftKind != rightKind)
                return false;

            if (leftKind == ValueKind.Null)
                return true;

            if (leftKind == ValueKind.ManagedRef)
            {
                var leftHasRef = left.TryGetManagedRef(varId, out var leftRef);
                var rightHasRef = right.TryGetManagedRef(varId, out var rightRef);
                if (leftHasRef != rightHasRef)
                    return false;
                if (!leftHasRef)
                    return true;
                return ReferenceEquals(leftRef, rightRef);
            }

            var leftHasValue = left.TryGetVariant(varId, out var leftValue);
            var rightHasValue = right.TryGetVariant(varId, out var rightValue);
            if (leftHasValue != rightHasValue)
                return false;
            if (!leftHasValue)
                return true;

            return leftValue.Equals(rightValue);
        }

        static void ApplyVarValue(IVarStore destination, IVarStore source, int varId)
        {
            var sourceKind = source.GetVarKind(varId);
            if (sourceKind == ValueKind.Null)
            {
                destination.TryUnset(varId);
                return;
            }

            if (sourceKind == ValueKind.ManagedRef)
            {
                if (source.TryGetManagedRef(varId, out var managedRef) && managedRef != null)
                    destination.TrySetManagedRef(varId, managedRef);
                else
                    destination.TryUnset(varId);
                return;
            }

            if (source.TryGetVariant(varId, out var variant))
            {
                if (variant.Kind == ValueKind.Null)
                    destination.TryUnset(varId);
                else
                    destination.TrySetVariant(varId, variant);
                return;
            }

            destination.TryUnset(varId);
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
