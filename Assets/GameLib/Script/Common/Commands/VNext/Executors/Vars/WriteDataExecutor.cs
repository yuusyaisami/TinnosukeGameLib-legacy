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
        sealed class WriteDataDebugRuntime
        {
            public readonly bool Enabled;
            public readonly WriteDataDebugSettings Settings;

            public WriteDataDebugRuntime(WriteDataCommandData data)
            {
                Enabled = data != null && data.DebugMode;
                Settings = data?.Debug ?? new WriteDataDebugSettings();
            }

            public void LogCommandSummary(WriteDataCommandData data, IScopeNode? sourceScope, IScopeNode? targetScope)
            {
                if (!Enabled || data == null || !Settings.IncludeCommandSummary)
                    return;

                Emit($"Command Start source={DescribeScope(sourceScope)} target={DescribeScope(targetScope)} vars={data.VarOps?.Count ?? 0} scalars={data.ScalarOps?.Count ?? 0}");
            }

            public void LogVarOp(
                int index,
                string opName,
                VarStoreTarget target,
                int varId,
                IScopeNode? scope,
                bool success,
                bool hasInput,
                DynamicVariant input,
                bool hasBefore,
                DynamicVariant before,
                bool hasAfter,
                DynamicVariant after)
            {
                if (!Enabled || Settings.Vars == null || !Settings.Vars.Enabled)
                    return;

                if (success && !Settings.Vars.LogSuccess)
                    return;
                if (!success && !Settings.Vars.LogFailure)
                    return;

                var level = !success ? WriteDataDebugLogLevel.Warning : Settings.LogLevel;
                var msg = $"Var {opName} {(success ? "OK" : "FAILED")} target={target}";

                if (Settings.IncludeOpIndex)
                    msg += $" index={index}";

                msg += $" varId={varId}";

                if (Settings.Vars.IncludeVarKey)
                    msg += $" key={DescribeVarKey(varId)}";

                if (Settings.Vars.IncludeInputValue && hasInput)
                    msg += $" input={DescribeVariant(input)}";

                if (Settings.Vars.IncludeBeforeValue)
                    msg += $" before={(hasBefore ? DescribeVariant(before) : "<unset>")}";

                if (Settings.Vars.IncludeAfterValue)
                    msg += $" after={(hasAfter ? DescribeVariant(after) : "<unset>")}";

                if (Settings.Vars.IncludeScope)
                    msg += $" scope={DescribeScope(scope)}";

                Emit(msg, level);
            }

            public void LogScalarOp(
                int index,
                ScalarWriteOp op,
                IScopeNode? scope,
                bool success,
                bool hasInput,
                float input,
                bool hasBefore,
                float before,
                bool hasAfter,
                float after,
                float duration)
            {
                if (!Enabled || Settings.Scalars == null || !Settings.Scalars.Enabled)
                    return;

                if (success && !Settings.Scalars.LogSuccess)
                    return;
                if (!success && !Settings.Scalars.LogFailure)
                    return;

                var level = !success ? WriteDataDebugLogLevel.Warning : Settings.LogLevel;
                var msg = $"Scalar {op.Op} {(success ? "OK" : "FAILED")}";

                if (Settings.IncludeOpIndex)
                    msg += $" index={index}";

                if (Settings.Scalars.IncludeScalarKey)
                    msg += $" key={DescribeScalarKey(op.Key)}";

                if (Settings.Scalars.IncludeInputValue && hasInput)
                    msg += $" input={input}";

                if (Settings.Scalars.IncludeBeforeValue)
                    msg += $" before={(hasBefore ? before.ToString() : "<unset>")}";

                if (Settings.Scalars.IncludeAfterValue)
                    msg += $" after={(hasAfter ? after.ToString() : "<unset>")}";

                if (Settings.Scalars.IncludeLayer && !string.IsNullOrEmpty(op.Layer))
                    msg += $" layer={op.Layer}";

                if (Settings.Scalars.IncludeDuration)
                    msg += $" duration={duration}";

                if (Settings.Scalars.IncludeTag && !string.IsNullOrEmpty(op.Tag))
                    msg += $" tag={op.Tag}";

                if (Settings.Scalars.IncludeScope)
                    msg += $" scope={DescribeScope(scope)}";

                Emit(msg, level);
            }

            public void LogScalarServiceMissing(IScopeNode? scope)
            {
                if (!Enabled || Settings.Scalars == null || !Settings.Scalars.Enabled || !Settings.Scalars.LogFailure)
                    return;

                Emit($"Scalar service missing. scope={DescribeScope(scope)}", WriteDataDebugLogLevel.Warning);
            }

            void Emit(string message, WriteDataDebugLogLevel? overrideLevel = null)
            {
                var level = overrideLevel ?? Settings.LogLevel;
                var prefix = string.IsNullOrWhiteSpace(Settings.Prefix) ? "[WriteDataDebug]" : Settings.Prefix.Trim();
                var line = $"{prefix} {message}";

                switch (level)
                {
                    case WriteDataDebugLogLevel.Error:
                        Debug.LogError(line);
                        break;
                    case WriteDataDebugLogLevel.Warning:
                        Debug.LogWarning(line);
                        break;
                    default:
                        Debug.Log(line);
                        break;
                }
            }
        }

        public int CommandId => CommandIds.WriteData;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not WriteDataCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "WriteDataCommandData is required.");

            var sourceCtx = await ResolveSourceContextAsync(ctx, typed, ct);
            var targetScope = await ResolveTargetScopeAsync(ctx, typed, ct);
            var serviceScope = targetScope ?? ctx.Actor ?? ctx.Scope;
            var debug = new WriteDataDebugRuntime(typed);
            IBlackboardService? blackboard = null;
            TryResolveBlackboard(serviceScope, out blackboard);
            debug.LogCommandSummary(typed, sourceCtx.Scope, serviceScope);

            try
            {
                ApplyVarOps(typed.VarOps, sourceCtx, serviceScope, blackboard, debug);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WriteDataExecutor] ApplyVarOps failed. scope={DescribeScope(serviceScope)}");
                Debug.LogException(ex);
            }

            try
            {
                ApplyScalarOps(typed.ScalarOps, sourceCtx, serviceScope, serviceScope?.Resolver, debug);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WriteDataExecutor] ApplyScalarOps failed. scope={DescribeScope(serviceScope)}");
                Debug.LogException(ex);
            }

            return;
        }

        static async UniTask<IScopeNode> ResolveTargetScopeAsync(CommandContext ctx, WriteDataCommandData typed, CancellationToken ct)
        {
            var (resolvedScope, _) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            return resolvedScope ?? ctx.Actor ?? ctx.Scope;
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

        static void ApplyVarOps(List<VarWriteOp> ops, CommandContext ctx, IScopeNode? targetScope, IBlackboardService? blackboard, WriteDataDebugRuntime debug)
        {
            if (ops == null || ops.Count == 0)
                return;

            var commandVars = ResolveCommandVars(ctx, targetScope, ops);

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
                    var hasBefore = false;
                    var before = DynamicVariant.Null;
                    if (debug.Enabled && debug.Settings.Vars != null && debug.Settings.Vars.IncludeBeforeValue)
                        hasBefore = TryGetVariant(op.Target, ctx, targetScope, blackboard, commandVars, varId, out before);

                    var success = false;
                    var hasInput = false;
                    var input = DynamicVariant.Null;

                    switch (op.Op)
                    {
                        case VarWriteOpKind.Unset:
                            success = TryUnsetVariant(op.Target, ctx, targetScope, blackboard, commandVars, varId);
                            if (!success)
                                LogVarOpFailed("Unset", op.Target, targetScope, varId);
                            break;

                        case VarWriteOpKind.Set:
                            {
                                var v = op.Value.Evaluate(ctx);
                                hasInput = true;
                                input = v;
                                success = TrySetVariant(op.Target, ctx, targetScope, blackboard, commandVars, varId, v);
                                if (!success)
                                    LogVarOpFailed("Set", op.Target, targetScope, varId);
                                break;
                            }

                        case VarWriteOpKind.Add:
                            {
                                var add = op.Value.GetOrDefault<float>(ctx, 0f);
                                var cur = GetNumericOrDefault(op.Target, ctx, targetScope, blackboard, commandVars, varId, 0f);
                                hasInput = true;
                                input = DynamicVariant.FromFloat(add);
                                success = TrySetVariant(op.Target, ctx, targetScope, blackboard, commandVars, varId, DynamicVariant.FromFloat(cur + add));
                                if (!success)
                                    LogVarOpFailed("Add", op.Target, targetScope, varId);
                                break;
                            }

                        case VarWriteOpKind.Mul:
                            {
                                var mul = op.Value.GetOrDefault<float>(ctx, 1f);
                                var cur = GetNumericOrDefault(op.Target, ctx, targetScope, blackboard, commandVars, varId, 1f);
                                hasInput = true;
                                input = DynamicVariant.FromFloat(mul);
                                success = TrySetVariant(op.Target, ctx, targetScope, blackboard, commandVars, varId, DynamicVariant.FromFloat(cur * mul));
                                if (!success)
                                    LogVarOpFailed("Mul", op.Target, targetScope, varId);
                                break;
                            }
                    }

                    var hasAfter = false;
                    var after = DynamicVariant.Null;
                    if (debug.Enabled && debug.Settings.Vars != null && debug.Settings.Vars.IncludeAfterValue)
                        hasAfter = TryGetVariant(op.Target, ctx, targetScope, blackboard, commandVars, varId, out after);

                    debug.LogVarOp(i, op.Op.ToString(), op.Target, varId, targetScope, success, hasInput, input, hasBefore, before, hasAfter, after);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WriteDataExecutor] Var op failed. op={op.Op} target={op.Target} varId={varId} scope={DescribeScope(targetScope)}");
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

        static IVarStore ResolveCommandVars(CommandContext ctx, IScopeNode? targetScope, List<VarWriteOp> ops)
        {
            if (!HasCommandVarOps(ops))
                return ctx.Vars;

            if (ctx.Vars is not NullVarStore)
                return ctx.Vars;

            if (targetScope?.Resolver != null && targetScope.Resolver.TryResolve<IVarStore>(out var vars) && vars != null)
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

        static bool TryGetVariant(VarStoreTarget target, CommandContext ctx, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore commandVars, int varId, out DynamicVariant value)
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
                    if (targetScope == null)
                        return false;
                    return TryGetHierarchicalBlackboardVariant(targetScope, varId, out value);
                default:
                    return false;
            }
        }

        static bool TrySetVariant(VarStoreTarget target, CommandContext ctx, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore commandVars, int varId, DynamicVariant value)
        {
            if (value.Kind == ValueKind.Null)
                return TryUnsetOrAlreadyUnset(target, targetScope, blackboard, commandVars, varId);

            if (value.Kind == ValueKind.ManagedRef)
                return TrySetManagedRef(target, targetScope, blackboard, commandVars, varId, value.AsManagedRef);

            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return commandVars.TrySetVariant(varId, value);
                case VarStoreTarget.BlackboardLocal:
                    return blackboard?.TryLocalSetVariant(varId, value) ?? false;
                case VarStoreTarget.BlackboardGlobal:
                    if (targetScope == null)
                        return false;
                    return TrySetHierarchicalBlackboardVariant(targetScope, varId, value);
                default:
                    return false;
            }
        }

        static bool TrySetManagedRef(VarStoreTarget target, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore commandVars, int varId, object value)
        {
            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return commandVars.TrySetManagedRef(varId, value);
                case VarStoreTarget.BlackboardLocal:
                    return blackboard?.LocalVars?.TrySetManagedRef(varId, value) ?? false;
                case VarStoreTarget.BlackboardGlobal:
                    if (targetScope == null)
                        return false;
                    return TrySetHierarchicalBlackboardManagedRef(targetScope, varId, value);
                default:
                    return false;
            }
        }

        static bool TryUnsetVariant(VarStoreTarget target, CommandContext ctx, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore commandVars, int varId)
        {
            switch (target)
            {
                case VarStoreTarget.CommandVars:
                    return commandVars.TryUnset(varId);
                case VarStoreTarget.BlackboardLocal:
                    return blackboard?.LocalVars?.TryUnset(varId) ?? false;
                case VarStoreTarget.BlackboardGlobal:
                    if (targetScope == null)
                        return false;
                    return TryUnsetHierarchicalBlackboardVariant(targetScope, varId);
                default:
                    return false;
            }
        }

        static bool TryUnsetOrAlreadyUnset(VarStoreTarget target, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore commandVars, int varId)
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
                    if (targetScope == null)
                        return false;
                    return TryUnsetHierarchicalBlackboardVariantOrAlreadyUnset(targetScope, varId);

                default:
                    return false;
            }
        }

        static float GetNumericOrDefault(VarStoreTarget target, CommandContext ctx, IScopeNode? targetScope, IBlackboardService? blackboard, IVarStore commandVars, int varId, float defaultValue)
        {
            if (TryGetVariant(target, ctx, targetScope, blackboard, commandVars, varId, out var v))
            {
                if (v.TryGet<float>(out var f))
                    return f;
                if (v.TryGet<int>(out var i))
                    return i;
            }
            return defaultValue;
        }

        static void ApplyScalarOps(List<ScalarWriteOp> ops, CommandContext ctx, IScopeNode? serviceScope, VContainer.IObjectResolver? resolver, WriteDataDebugRuntime debug)
        {
            if (ops == null || ops.Count == 0)
                return;

            if (resolver == null || !resolver.TryResolve<IBaseScalarService>(out var scalar) || scalar == null)
            {
                Debug.LogError($"[WriteDataExecutor] Scalar ops require IBaseScalarService, but no scalar service exists in this LTS. scope={DescribeScope(ctx.Scope)}");
                debug.LogScalarServiceMissing(serviceScope ?? ctx.Scope);
                return;
            }

            var source = (object)(ctx.Actor ?? ctx.Scope);

            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op == null)
                    continue;

                var duration = ResolveDuration(op, ctx);
                var hasInput = OpUsesInput(op.Op);
                var input = hasInput ? op.Value.GetOrDefault(ctx, GetDefaultInputForScalarOp(op.Op)) : 0f;

                var hasBefore = TryGetScalarValueForOp(scalar, op, out var before);
                var success = true;

                switch (op.Op)
                {
                    case ScalarWriteOpKind.ClearAll:
                        scalar.ClearAll(null);
                        break;

                    case ScalarWriteOpKind.ClearKey:
                        scalar.ClearAll(op.Key);
                        break;

                    case ScalarWriteOpKind.SetLocalBase:
                        scalar.SetLocalBase(op.Key, input);
                        break;

                    case ScalarWriteOpKind.SetGlobalBase:
                        scalar.SetGlobalBase(op.Key, input);
                        break;

                    case ScalarWriteOpKind.LocalAdd:
                        HandleScalarHandle(op, ctx, scalar.LocalAdd(op.Key, op.Layer ?? string.Empty, input, duration, source, op.Tag));
                        break;

                    case ScalarWriteOpKind.GlobalAdd:
                        HandleScalarHandle(op, ctx, scalar.GlobalAdd(op.Key, op.Layer ?? string.Empty, input, duration, source, op.Tag));
                        break;

                    case ScalarWriteOpKind.LocalMul:
                        HandleScalarHandle(op, ctx, scalar.LocalMul(op.Key, op.Layer ?? string.Empty, input, op.MulPhase, duration, source, op.Tag));
                        break;

                    case ScalarWriteOpKind.GlobalMul:
                        HandleScalarHandle(op, ctx, scalar.GlobalMul(op.Key, op.Layer ?? string.Empty, input, op.MulPhase, duration, source, op.Tag));
                        break;

                    case ScalarWriteOpKind.DisposeHandleVar:
                        success = DisposeHandleFromVar(op, ctx);
                        break;
                }

                var hasAfter = TryGetScalarValueForOp(scalar, op, out var after);
                debug.LogScalarOp(i, op, serviceScope ?? ctx.Scope, success, hasInput, input, hasBefore, before, hasAfter, after, duration);
            }
        }

        static bool OpUsesInput(ScalarWriteOpKind op)
        {
            return op == ScalarWriteOpKind.SetLocalBase
                || op == ScalarWriteOpKind.SetGlobalBase
                || op == ScalarWriteOpKind.LocalAdd
                || op == ScalarWriteOpKind.GlobalAdd
                || op == ScalarWriteOpKind.LocalMul
                || op == ScalarWriteOpKind.GlobalMul;
        }

        static float GetDefaultInputForScalarOp(ScalarWriteOpKind op)
        {
            return op == ScalarWriteOpKind.LocalMul || op == ScalarWriteOpKind.GlobalMul ? 1f : 0f;
        }

        static bool TryGetScalarValueForOp(IBaseScalarService scalar, ScalarWriteOp op, out float value)
        {
            value = 0f;

            switch (op.Op)
            {
                case ScalarWriteOpKind.SetLocalBase:
                case ScalarWriteOpKind.LocalAdd:
                case ScalarWriteOpKind.LocalMul:
                case ScalarWriteOpKind.ClearKey:
                    return scalar.LocalTryGet(op.Key, out value);

                case ScalarWriteOpKind.SetGlobalBase:
                case ScalarWriteOpKind.GlobalAdd:
                case ScalarWriteOpKind.GlobalMul:
                    return scalar.GlobalTryGet(op.Key, out value);

                default:
                    return false;
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

        static bool DisposeHandleFromVar(ScalarWriteOp op, CommandContext ctx)
        {
            var varId = op.HandleVar.VarId;
            if (varId == 0)
                return false;

            if (!ctx.Vars.TryGetManagedRef(varId, out var obj) || obj == null)
                return false;

            if (obj is ScalarHandle handle)
            {
                handle.Dispose();
                if (op.UnsetAfterDispose)
                    ctx.Vars.TryUnset(varId);
                return true;
            }

            return false;
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

        static string DescribeVariant(DynamicVariant value)
        {
            if (value.Kind == ValueKind.Null)
                return "Null";

            if (value.Kind == ValueKind.ManagedRef)
            {
                var managed = value.AsManagedRef;
                return managed == null ? "ManagedRef:null" : $"ManagedRef:{managed.GetType().Name}";
            }

            return $"{value.Kind}:{value}";
        }

        static string DescribeVarKey(int varId)
        {
            return VarIdResolver.TryGetIdToStable(varId) ?? $"varId={varId}";
        }

        static string DescribeScalarKey(ScalarKey key)
        {
            return string.IsNullOrEmpty(key.Name) ? key.Id.ToString() : $"{key.Name} ({key.Id})";
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
