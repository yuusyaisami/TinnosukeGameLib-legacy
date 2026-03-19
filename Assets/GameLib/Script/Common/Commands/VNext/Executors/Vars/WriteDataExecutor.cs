#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Scalar;
using VContainer;
using UnityEngine;

namespace Game.Commands.VNext
{
    public sealed class WriteDataExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.WriteData;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WriteDataCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteDataCommandData is required.");

            var sourceCtx = await ResolveSourceContextAsync(ctx, typed, ct);
            var serviceScope = sourceCtx.Actor ?? sourceCtx.Scope;
            IBlackboardService? blackboard = null;
            TryResolveBlackboard(serviceScope, out blackboard);

            try
            {
                ApplyVarOps(typed.VarOps, sourceCtx, serviceScope, blackboard);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WriteDataExecutor] ApplyVarOps failed. scope={DescribeScope(serviceScope)}");
                Debug.LogException(ex);
            }

            try
            {
                ApplyScalarOps(typed.ScalarOps, sourceCtx, serviceScope?.Resolver);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WriteDataExecutor] ApplyScalarOps failed. scope={DescribeScope(serviceScope)}");
                Debug.LogException(ex);
            }

            return;
        }

        static async UniTask<CommandContext> ResolveSourceContextAsync(CommandContext ctx, WriteDataCommandData typed, CancellationToken ct)
        {
            var (resolvedScope, _) = await ActorScopeResolver.ResolveAsync(typed.Source, ctx, ct);
            var sourceScope = resolvedScope ?? ctx.Actor ?? ctx.Scope;
            if (ReferenceEquals(sourceScope, ctx.Scope) && ReferenceEquals(sourceScope, ctx.Actor))
                return ctx;

            return new CommandContext(
                sourceScope,
                ctx.Vars,
                ctx.Runner,
                sourceScope,
                ctx.Options,
                commandRootScope: ctx.CommandRootScope,
                rootActor: ctx.RootActor,
                callerActor: ctx.Actor,
                sourceContext: ctx);
        }

        static void ApplyVarOps(List<VarWriteOp> ops, CommandContext ctx, IScopeNode? scope, IBlackboardService? blackboard)
        {
            if (ops == null || ops.Count == 0)
                return;

            var commandVars = ResolveCommandVars(ctx, scope, ops);

            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op == null)
                    continue;

                var varId = op.Key.VarId;
                if (varId == 0)
                    continue;

                try
                {
                    switch (op.Op)
                    {
                        case VarWriteOpKind.Unset:
                            if (!TryUnsetVariant(op.Target, ctx, scope, blackboard, commandVars, varId))
                                LogVarOpFailed("Unset", op.Target, scope, varId);
                            break;

                        case VarWriteOpKind.Set:
                            {
                                var v = op.Value.Evaluate(ctx);
                                if (!TrySetVariant(op.Target, ctx, scope, blackboard, commandVars, varId, v))
                                    LogVarOpFailed("Set", op.Target, scope, varId);
                                break;
                            }

                        case VarWriteOpKind.Add:
                            {
                                var add = op.Value.GetOrDefault<float>(ctx, 0f);
                                var cur = GetNumericOrDefault(op.Target, ctx, scope, blackboard, commandVars, varId, 0f);
                                if (!TrySetVariant(op.Target, ctx, scope, blackboard, commandVars, varId, DynamicVariant.FromFloat(cur + add)))
                                    LogVarOpFailed("Add", op.Target, scope, varId);
                                break;
                            }

                        case VarWriteOpKind.Mul:
                            {
                                var mul = op.Value.GetOrDefault<float>(ctx, 1f);
                                var cur = GetNumericOrDefault(op.Target, ctx, scope, blackboard, commandVars, varId, 1f);
                                if (!TrySetVariant(op.Target, ctx, scope, blackboard, commandVars, varId, DynamicVariant.FromFloat(cur * mul)))
                                    LogVarOpFailed("Mul", op.Target, scope, varId);
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WriteDataExecutor] Var op failed. op={op.Op} target={op.Target} varId={varId} scope={DescribeScope(scope)}");
                    Debug.LogException(ex);
                }
            }
        }

        static bool TryResolveBlackboard(IScopeNode? scope, out IBlackboardService? blackboard)
        {
            blackboard = null;
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve(out blackboard) || blackboard == null)
                return false;

            return true;
        }

        static IVarStore ResolveCommandVars(CommandContext ctx, IScopeNode? scope, List<VarWriteOp> ops)
        {
            if (!HasCommandVarOps(ops))
                return ctx.Vars;

            if (ctx.Vars is not NullVarStore)
                return ctx.Vars;

            if (scope?.Resolver != null && scope.Resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                return vars;

            return new VarStore();
        }

        static bool HasCommandVarOps(List<VarWriteOp> ops)
        {
            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op != null && op.Target == VarStoreTarget.CommandVars)
                    return true;
            }

            return false;
        }

        static IVarStore? ResolveVarStore(VarStoreTarget target, CommandContext ctx, IBlackboardService? blackboard)
        {
            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return ctx.Vars;
                case VarStoreTarget.BlackboardLocal:
                    return blackboard?.LocalVars;
                default:
                    return null;
            }
        }

        static bool TryGetVariant(VarStoreTarget target, CommandContext ctx, IScopeNode? scope, IBlackboardService? blackboard, IVarStore commandVars, int varId, out DynamicVariant value)
        {
            value = default;

            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    if (commandVars.GetVarKind(varId) == ValueKind.ManagedRef)
                        return false;
                    return commandVars.TryGetVariant(varId, out value);
                case VarStoreTarget.BlackboardLocal:
                    if (blackboard?.LocalVars != null && blackboard.LocalVars.GetVarKind(varId) == ValueKind.ManagedRef)
                        return false;
                    return blackboard?.TryLocalGetVariant(varId, out value) ?? false;
                case VarStoreTarget.BlackboardGlobal:
                    if (scope == null)
                        return false;
                    return TryGetHierarchicalBlackboardVariant(scope, varId, out value);
                default:
                    return false;
            }
        }

        static bool TrySetVariant(VarStoreTarget target, CommandContext ctx, IScopeNode? scope, IBlackboardService? blackboard, IVarStore commandVars, int varId, DynamicVariant value)
        {
            if (value.Kind == ValueKind.Null)
                return TryUnsetOrAlreadyUnset(target, scope, blackboard, commandVars, varId);

            if (value.Kind == ValueKind.ManagedRef)
                return TrySetManagedRef(target, scope, blackboard, commandVars, varId, value.AsManagedRef);

            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return commandVars.TrySetVariant(varId, value);
                case VarStoreTarget.BlackboardLocal:
                    return blackboard?.TryLocalSetVariant(varId, value) ?? false;
                case VarStoreTarget.BlackboardGlobal:
                    if (scope == null)
                        return false;
                    return TrySetHierarchicalBlackboardVariant(scope, varId, value);
                default:
                    return false;
            }
        }

        static bool TrySetManagedRef(VarStoreTarget target, IScopeNode? scope, IBlackboardService? blackboard, IVarStore commandVars, int varId, object value)
        {
            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return commandVars.TrySetManagedRef(varId, value);
                case VarStoreTarget.BlackboardLocal:
                    return blackboard?.LocalVars?.TrySetManagedRef(varId, value) ?? false;
                case VarStoreTarget.BlackboardGlobal:
                    if (scope == null)
                        return false;
                    return TrySetHierarchicalBlackboardManagedRef(scope, varId, value);
                default:
                    return false;
            }
        }

        static bool TryUnsetVariant(VarStoreTarget target, CommandContext ctx, IScopeNode? scope, IBlackboardService? blackboard, IVarStore commandVars, int varId)
        {
            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return commandVars.TryUnset(varId);
                case VarStoreTarget.BlackboardLocal:
                    return blackboard?.LocalVars?.TryUnset(varId) ?? false;
                case VarStoreTarget.BlackboardGlobal:
                    if (scope == null)
                        return false;
                    return TryUnsetHierarchicalBlackboardVariant(scope, varId);
                default:
                    return false;
            }
        }

        static bool TryUnsetOrAlreadyUnset(VarStoreTarget target, IScopeNode? scope, IBlackboardService? blackboard, IVarStore commandVars, int varId)
        {
            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    if (!commandVars.Contains(varId))
                        return true;
                    return commandVars.TryUnset(varId);

                case VarStoreTarget.BlackboardLocal:
                    if (blackboard?.LocalVars == null)
                        return false;
                    if (!blackboard.LocalVars.Contains(varId))
                        return true;
                    return blackboard.LocalVars.TryUnset(varId);

                case VarStoreTarget.BlackboardGlobal:
                    if (scope == null)
                        return false;
                    return TryUnsetHierarchicalBlackboardVariantOrAlreadyUnset(scope, varId);

                default:
                    return false;
            }
        }

        static float GetNumericOrDefault(VarStoreTarget target, CommandContext ctx, IScopeNode? scope, IBlackboardService? blackboard, IVarStore commandVars, int varId, float defaultValue)
        {
            if (TryGetVariant(target, ctx, scope, blackboard, commandVars, varId, out var v))
            {
                if (v.TryGet<float>(out var f))
                    return f;
                if (v.TryGet<int>(out var i))
                    return i;
            }
            return defaultValue;
        }

        static void ApplyScalarOps(List<ScalarWriteOp> ops, CommandContext ctx, VContainer.IObjectResolver? resolver)
        {
            if (ops == null || ops.Count == 0)
                return;

            if (resolver == null || !resolver.TryResolve<IBaseScalarService>(out var scalar) || scalar == null)
            {
                Debug.LogError($"[WriteDataExecutor] Scalar ops require IBaseScalarService, but no scalar service exists in this LTS. scope={DescribeScope(ctx.Scope)}");
                return;
            }

            var source = (object)(ctx.Actor ?? ctx.Scope);

            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op == null)
                    continue;

                switch (op.Op)
                {
                    case ScalarWriteOpKind.ClearAll:
                        scalar.ClearAll(null);
                        break;

                    case ScalarWriteOpKind.ClearKey:
                        scalar.ClearAll(op.Key);
                        break;

                    case ScalarWriteOpKind.SetLocalBase:
                        scalar.SetLocalBase(op.Key, op.Value.GetOrDefault(ctx, 0f));
                        break;

                    case ScalarWriteOpKind.SetGlobalBase:
                        scalar.SetGlobalBase(op.Key, op.Value.GetOrDefault(ctx, 0f));
                        break;

                    case ScalarWriteOpKind.LocalAdd:
                        HandleScalarHandle(op, ctx, scalar.LocalAdd(op.Key, op.Layer ?? string.Empty, op.Value.GetOrDefault(ctx, 0f), ResolveDuration(op, ctx), source, op.Tag));
                        break;

                    case ScalarWriteOpKind.GlobalAdd:
                        HandleScalarHandle(op, ctx, scalar.GlobalAdd(op.Key, op.Layer ?? string.Empty, op.Value.GetOrDefault(ctx, 0f), ResolveDuration(op, ctx), source, op.Tag));
                        break;

                    case ScalarWriteOpKind.LocalMul:
                        HandleScalarHandle(op, ctx, scalar.LocalMul(op.Key, op.Layer ?? string.Empty, op.Value.GetOrDefault(ctx, 1f), op.MulPhase, ResolveDuration(op, ctx), source, op.Tag));
                        break;

                    case ScalarWriteOpKind.GlobalMul:
                        HandleScalarHandle(op, ctx, scalar.GlobalMul(op.Key, op.Layer ?? string.Empty, op.Value.GetOrDefault(ctx, 1f), op.MulPhase, ResolveDuration(op, ctx), source, op.Tag));
                        break;

                    case ScalarWriteOpKind.DisposeHandleVar:
                        DisposeHandleFromVar(op, ctx);
                        break;
                }
            }
        }

        static float ResolveDuration(ScalarWriteOp op, CommandContext ctx)
        {
            var duration = op.DurationSeconds.GetOrDefault(ctx, -1f);
            return duration <= 0f ? -1f : duration;
        }

        static void HandleScalarHandle(ScalarWriteOp op, CommandContext ctx, ScalarHandle handle)
        {
            var varId = op.StoreHandleVar.VarId;
            if (varId == 0)
                return;

            ctx.Vars.TrySetManagedRef(varId, handle);
        }

        static void DisposeHandleFromVar(ScalarWriteOp op, CommandContext ctx)
        {
            var varId = op.HandleVar.VarId;
            if (varId == 0)
                return;

            if (!ctx.Vars.TryGetManagedRef(varId, out var obj) || obj == null)
                return;

            if (obj is ScalarHandle handle)
            {
                handle.Dispose();
                if (op.UnsetAfterDispose)
                    ctx.Vars.TryUnset(varId);
            }
        }

        static bool TryGetHierarchicalBlackboardVariant(IScopeNode origin, int varId, out DynamicVariant value)
        {
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                if (!TryResolveBlackboard(node, out var bb) || bb == null)
                    continue;

                var local = bb.LocalVars;
                if (local != null && local.Contains(varId))
                {
                    if (local.GetVarKind(varId) == ValueKind.ManagedRef)
                        continue;

                    if (bb.TryLocalGetVariant(varId, out value))
                        return true;
                }
            }

            value = default;
            return false;
        }

        static bool TrySetHierarchicalBlackboardVariant(IScopeNode origin, int varId, DynamicVariant value)
        {
            IBlackboardService? fallback = null;

            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                if (!TryResolveBlackboard(node, out var bb) || bb == null)
                    continue;

                fallback ??= bb;
                var local = bb.LocalVars;
                if (local != null && local.Contains(varId))
                    return bb.TryLocalSetVariant(varId, value);
            }

            return fallback?.TryLocalSetVariant(varId, value) ?? false;
        }

        static bool TrySetHierarchicalBlackboardManagedRef(IScopeNode origin, int varId, object value)
        {
            IBlackboardService? fallback = null;

            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                if (!TryResolveBlackboard(node, out var bb) || bb == null)
                    continue;

                fallback ??= bb;
                var local = bb.LocalVars;
                if (local != null && local.Contains(varId))
                    return local.TrySetManagedRef(varId, value);
            }

            return fallback?.LocalVars?.TrySetManagedRef(varId, value) ?? false;
        }

        static bool TryUnsetHierarchicalBlackboardVariant(IScopeNode origin, int varId)
        {
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                if (!TryResolveBlackboard(node, out var bb) || bb == null)
                    continue;

                var local = bb.LocalVars;
                if (local != null && local.Contains(varId))
                    return local.TryUnset(varId);
            }

            return false;
        }

        static bool TryUnsetHierarchicalBlackboardVariantOrAlreadyUnset(IScopeNode origin, int varId)
        {
            var foundBlackboard = false;
            for (IScopeNode? node = origin; node != null; node = node.Parent)
            {
                if (!TryResolveBlackboard(node, out var bb) || bb == null)
                    continue;

                foundBlackboard = true;
                var local = bb.LocalVars;
                if (local != null && local.Contains(varId))
                    return local.TryUnset(varId);
            }

            return foundBlackboard;
        }

        static void LogVarOpFailed(string opName, VarStoreTarget target, IScopeNode? scope, int varId)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[WriteDataExecutor] Var op failed. op={opName} target={target} varId={varId} scope={DescribeScope(scope)}");
#endif
        }

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";
            if (scope is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";
            var id = scope.Identity?.Id;
            if (!string.IsNullOrEmpty(id))
                return $"{id} ({scope.Kind})";
            return scope.GetType().Name;
        }
    }
}
