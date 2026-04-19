#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class TriggerExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.Trigger;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TriggerCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TriggerCommandData is required.");

            var varId = ResolveVarId(typed.Key);
            if (varId == 0)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Trigger key is required.");

            if (!TryResolveCurrentValue(typed.Target, ctx, varId, out var store, out var current, out var error))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);

            if (!TryToggleValue(current, out var isTrue, out var toggled))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Trigger value must be bool/int/float. target={typed.Target} varId={varId} kind={current.Kind}");

            if (!store.TrySetVariant(varId, toggled))
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"Trigger write failed. target={typed.Target} varId={varId}");

            var branch = isTrue ? typed.ThenCommands : typed.ElseCommands;
            if (branch == null || branch.Count == 0)
                return;

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var result = await runner.ExecuteListAsync(branch, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }

        static int ResolveVarId(in VarKeyRef key)
        {
            if (key.VarId != 0)
                return key.VarId;

            if (!string.IsNullOrWhiteSpace(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var resolvedVarId))
                return resolvedVarId;

            return 0;
        }

        static bool TryResolveCurrentValue(VarStoreTarget target, CommandContext ctx, int varId, out IVarStore store, out DynamicVariant current, out string error)
        {
            current = DynamicVariant.Null;
            error = string.Empty;
            store = ctx.Vars;

            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return TryReadStore(store, varId, "CommandVars", out current, out error);

                case VarStoreTarget.BlackboardLocal:
                    if (!TryResolveBlackboard(ctx.Scope, out var blackboard) || blackboard == null)
                    {
                        error = "Blackboard service was not found on the current scope.";
                        return false;
                    }

                    store = blackboard.LocalVars;
                    if (store == null)
                    {
                        error = "Blackboard local store was not found.";
                        return false;
                    }

                    return TryReadStore(store, varId, "BlackboardLocal", out current, out error);

                case VarStoreTarget.BlackboardGlobal:
                    if (!TryResolveGlobalStore(ctx.Scope, varId, out store, out error))
                        return false;

                    return TryReadStore(store, varId, "BlackboardGlobal", out current, out error);

                default:
                    error = $"Unsupported store target: {target}";
                    return false;
            }
        }

        static bool TryReadStore(IVarStore store, int varId, string label, out DynamicVariant current, out string error)
        {
            current = DynamicVariant.Null;
            error = string.Empty;

            if (store == null)
            {
                error = $"{label} store was not found.";
                return false;
            }

            if (!store.Contains(varId))
            {
                error = $"{label} key was not found. varId={varId}";
                return false;
            }

            var kind = store.GetVarKind(varId);
            if (kind == ValueKind.ManagedRef)
            {
                error = $"{label} key is managed ref and cannot be triggered. varId={varId}";
                return false;
            }

            if (!store.TryGetVariant(varId, out current))
            {
                error = $"{label} key could not be read. varId={varId} kind={kind}";
                return false;
            }

            if (current.Kind != ValueKind.Bool && current.Kind != ValueKind.Int && current.Kind != ValueKind.Float)
            {
                error = $"{label} key must be bool/int/float. varId={varId} kind={current.Kind}";
                return false;
            }

            return true;
        }

        static bool TryResolveGlobalStore(IScopeNode origin, int varId, out IVarStore store, out string error)
        {
            store = null!;
            error = string.Empty;

            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                if (!TryResolveBlackboard(node, out var blackboard) || blackboard == null)
                    continue;

                var localVars = blackboard.LocalVars;
                if (localVars == null || !localVars.Contains(varId))
                    continue;

                if (localVars.GetVarKind(varId) == ValueKind.ManagedRef)
                {
                    error = $"BlackboardGlobal key is managed ref and cannot be triggered. varId={varId}";
                    return false;
                }

                store = localVars;
                return true;
            }

            error = $"BlackboardGlobal key was not found. varId={varId}";
            return false;
        }

        static bool TryResolveBlackboard(IScopeNode? scope, out IBlackboardService? blackboard)
        {
            blackboard = null;
            if (scope?.Resolver == null)
                return false;

            return scope.Resolver.TryResolve<IBlackboardService>(out blackboard) && blackboard != null;
        }

        static bool TryToggleValue(in DynamicVariant current, out bool currentState, out DynamicVariant toggled)
        {
            switch (current.Kind)
            {
                case ValueKind.Bool:
                    currentState = current.AsBool;
                    toggled = DynamicVariant.FromBool(!currentState);
                    return true;

                case ValueKind.Int:
                    currentState = current.AsInt != 0;
                    toggled = DynamicVariant.FromInt(currentState ? 0 : 1);
                    return true;

                case ValueKind.Float:
                    currentState = Mathf.Abs(current.AsFloat) > float.Epsilon;
                    toggled = DynamicVariant.FromFloat(currentState ? 0f : 1f);
                    return true;

                default:
                    currentState = false;
                    toggled = DynamicVariant.Null;
                    return false;
            }
        }
    }
}