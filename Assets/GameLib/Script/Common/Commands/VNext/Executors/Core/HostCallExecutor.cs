#nullable enable
using System;
using System.Buffers;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;
using Game.Common;

namespace Game.Commands.VNext
{
    public sealed class HostCallExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.HostCall;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not HostCallCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "HostCallCommandData is required.");

            var scope = ctx.Scope;
            var resolver = scope?.Resolver;
            if (scope == null || resolver == null || !resolver.TryResolve<IFlowHostCommandBridge>(out var bridge) || bridge == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IFlowHostCommandBridge is missing.");

            var args = typed.Args ?? Array.Empty<DynamicValue>();
            var count = args.Length;
            var pool = ArrayPool<DynamicVariant>.Shared;
            var rented = count > 0 ? pool.Rent(count) : Array.Empty<DynamicVariant>();

            try
            {
                for (int i = 0; i < count; i++)
                    rented[i] = args[i].Evaluate(ctx);

                var result = await bridge.InvokeAsync(ctx.Scope, ctx.Vars ?? NullVarStore.Instance, typed.SysId, rented, count, ct);
                if (!result.HasValue)
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"HostCall failed: SysId={typed.SysId}");

                TryStoreResult(ctx, typed.ResultVarKey, result.Value);
            }
            finally
            {
                if (count > 0)
                    pool.Return(rented, clearArray: true);
            }
        }

        static void TryStoreResult(CommandContext ctx, VarKeyRef key, in DynamicVariant value)
        {
            var varId = key.VarId;
            if (varId == 0 && !string.IsNullOrEmpty(key.StableKey))
            {
                if (!VarIdResolver.TryResolve(key.StableKey, out varId))
                    return;
            }

            if (varId == 0)
                return;

            var vars = ctx.Vars;
            if (vars == null)
                return;

            vars.TrySetVariant(varId, value);
        }
    }
}
