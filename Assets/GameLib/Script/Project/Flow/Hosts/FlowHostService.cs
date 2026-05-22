#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using VNext = Game.Commands.VNext;
using UnityEngine;
using VContainer;

namespace Game.Flow
{
    /// <summary>
    /// Flow のホスト�E機�Eを提供するサービス実裁E��E
    /// <para>スコープ�Eライフサイクルに従い Resolver をキャチE��ュして使用します、E/para>
    /// </summary>
    public sealed class FlowHostService : IFlowHost, Game.IScopeAcquireHandler, Game.IScopeReleaseHandler, VNext.IFlowHostCommandBridge
    {
        Game.IScopeNode? _scope;
        IRuntimeResolver? _resolver;

        /// <summary>スコープ獲得時に呼ばれ、Resolver をキャチE��ュします、E/summary>
        public void OnAcquire(Game.IScopeNode scope, bool isReset)
        {
            _scope = scope;
            _resolver = scope?.Resolver;
        }

        /// <summary>スコープ解放時に呼ばれ、�E部参�Eをクリアします、E/summary>
        public void OnRelease(Game.IScopeNode scope, bool isReset)
        {
            _scope = null;
            _resolver = null;
        }

        /// <summary>
        /// シスチE��コールを�E琁E��るエントリポイント、E
        /// </summary>
        public async UniTask<FlowSyscallResult> InvokeAsync(FlowContext context, FlowSyscallRequest request, CancellationToken ct)
        {
            if (context == null)
                return FlowSyscallResult.Failure;

            try
            {
                switch (request.SysId)
                {
                    case FlowSysIds.RunCommand:
                        return await RunCommandAsync(context, request, ct);

                    case FlowSysIds.UiDialogChoice:
                        return await UiDialogChoiceAsync(context, request, ct);

                    default:
                        return FlowSysResultFailure();
                }
            }
            catch (OperationCanceledException)
            {
                return FlowSyscallResult.Failure;
            }
            catch (Exception)
            {
                return FlowSyscallResult.Failure;
            }

            static FlowSyscallResult FlowSysResultFailure() => FlowSyscallResult.Failure;
        }

        /// <summary>
        /// RunCommand を�E琁E��ます。第一引数に CommandKey�E�Etring or int�E�を期征E��ます、E
        /// 第二引数�E�任意）で実行スコープを持E��できます、E
        /// </summary>
        async UniTask<FlowSyscallResult> RunCommandAsync(FlowContext ctx, FlowSyscallRequest req, CancellationToken ct)
        {
            if (req.ArgCount <= 0)
                return FlowSyscallResult.Failure;

            var executionScope = ctx.Scope;
            if (req.ArgCount >= 2 && TryResolveScopeNodeArg(ctx, req.ArgStart + 1, out var actor))
                executionScope = actor;

            if (executionScope == null)
                return FlowSyscallResult.Failure;

            EnsureScopeBuiltIfNeeded(executionScope);

            var resolver = executionScope.Resolver;
            if (resolver == null)
                return FlowSyscallResult.Failure;

            if (!resolver.TryResolve<VNext.ICommandRunner>(out var runner) || runner == null)
                return FlowSyscallResult.Failure;
            if (!resolver.TryResolve<VNext.ICommandCatalog>(out var catalog) || catalog == null)
                return FlowSyscallResult.Failure;
            if (!resolver.TryResolve<VNext.ICommandKeyResolver>(out var keyResolver) || keyResolver == null)
                return FlowSyscallResult.Failure;

            // Arg0: prefer ICommandSource (supports inline + catalog), then fallback to CommandKey (string/int)
            VNext.ICommandData? data = null;
            if (ctx.TryResolveObjectByIndex(req.ArgStart, out var obj) && obj is VNext.ICommandSource source)
            {
                var resolveCtx = new VNext.CommandResolveContext(
                    executionScope,
                    ctx.Vars ?? NullVarStore.Instance,
                    ctx.CommandRootScope,
                    resolver,
                    catalog,
                    keyResolver,
                    VNext.NullCommandResolveLogger.Instance);

                if (!source.TryResolve(resolveCtx, out var resolved) || resolved == null)
                    return FlowSyscallResult.Failure;
                data = resolved;
            }
            else
            {
                if (!ctx.TryResolveArgByIndex(req.ArgStart, out var keyArg, out _))
                    return FlowSyscallResult.Failure;

                if (!TryResolveCommandKey(keyArg, keyResolver, out var keyId))
                    return FlowSyscallResult.Failure;

                if (!catalog.TryResolve(keyId, out var resolved) || resolved == null)
                    return FlowSyscallResult.Failure;
                data = resolved;
            }

            if (data == null)
                return FlowSyscallResult.Failure;

            var options = VNext.CommandRunOptions.Default;
            var cmdCtx = new VNext.CommandContext(executionScope, ctx.Vars ?? NullVarStore.Instance, runner, executionScope, options);
            var result = await runner.ExecuteSingleAsync(data, cmdCtx, ct, options);

            return result.Status == VNext.CommandRunStatus.Completed
                ? FlowSyscallResult.SuccessNoValue
                : FlowSyscallResult.Failure;
        }

        /// <summary>
        /// UiDialogChoice を�E琁E��ます：チャネルキーと選択肢�E�イベントキー�E�群を受け取り、E��択結果�E�Ent�E�を返します、E
        /// </summary>
        async UniTask<FlowSyscallResult> UiDialogChoiceAsync(FlowContext ctx, FlowSyscallRequest req, CancellationToken ct)
        {
            await UniTask.CompletedTask;
            return FlowSyscallResult.Failure;
        }

        /// <summary>
        /// 引数を文字�Eとして解決します。数値/真偽も文字�E化して扱ぁE��す、E
        /// </summary>
        static bool TryResolveStringArg(FlowContext ctx, int argIndex, out string value)
        {
            value = string.Empty;
            if (!ctx.TryResolveArgByIndex(argIndex, out var v, out _))
                return false;

            if (v.TryGet<string>(out var s))
            {
                value = s ?? string.Empty;
                return true;
            }

            if (v.TryGet<int>(out var i))
            {
                value = i.ToString();
                return true;
            }

            if (v.TryGet<float>(out var f))
            {
                value = f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            if (v.TryGet<bool>(out var b))
            {
                value = b ? "true" : "false";
                return true;
            }

            value = v.ToString();
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// 引数をスコープノードとして解決します、E
        /// </summary>
        static bool TryResolveScopeNodeArg(FlowContext ctx, int argIndex, out Game.IScopeNode scope)
        {
            scope = null!;

            if (!ctx.TryResolveObjectByIndex(argIndex, out var obj) || obj == null)
                return false;

            if (obj is Game.IScopeNode s)
            {
                scope = s;
                return true;
            }

            return false;
        }

        public async UniTask<VNext.CommandHostCallResult> InvokeAsync(Game.IScopeNode scope, IVarStore vars, int sysId, DynamicVariant[] args, int argCount, CancellationToken ct)
        {
            if (scope == null)
                return VNext.CommandHostCallResult.Failure;

            if (args == null)
                return VNext.CommandHostCallResult.Failure;

            if (argCount < 0)
                argCount = 0;
            if (argCount > args.Length)
                argCount = args.Length;

            try
            {
                switch (sysId)
                {
                    case FlowSysIds.RunCommand:
                        return await RunCommandAsync(scope, vars, args, argCount, ct);
                    case FlowSysIds.UiDialogChoice:
                        return await UiDialogChoiceAsync(scope, vars, args, argCount, ct);
                    default:
                        return VNext.CommandHostCallResult.Failure;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return VNext.CommandHostCallResult.Failure;
            }
        }

        async UniTask<VNext.CommandHostCallResult> RunCommandAsync(Game.IScopeNode scope, IVarStore vars, DynamicVariant[] args, int argCount, CancellationToken ct)
        {
            if (argCount <= 0)
                return VNext.CommandHostCallResult.Failure;

            var executionScope = scope;
            if (argCount >= 2 && TryResolveScopeNodeArg(args[1], out var actor))
                executionScope = actor;

            EnsureScopeBuiltIfNeeded(executionScope);

            var resolver = executionScope?.Resolver;
            if (resolver == null)
                return VNext.CommandHostCallResult.Failure;

            if (!resolver.TryResolve<VNext.ICommandRunner>(out var runner) || runner == null)
                return VNext.CommandHostCallResult.Failure;
            if (!resolver.TryResolve<VNext.ICommandCatalog>(out var catalog) || catalog == null)
                return VNext.CommandHostCallResult.Failure;
            if (!resolver.TryResolve<VNext.ICommandKeyResolver>(out var keyResolver) || keyResolver == null)
                return VNext.CommandHostCallResult.Failure;

            if (!TryResolveCommandKey(args[0], keyResolver, out var keyId))
                return VNext.CommandHostCallResult.Failure;

            if (!catalog.TryResolve(keyId, out var data) || data == null)
                return VNext.CommandHostCallResult.Failure;

            var options = VNext.CommandRunOptions.Default;
            if (executionScope == null)
                return VNext.CommandHostCallResult.Failure;
            var cmdCtx = new VNext.CommandContext(executionScope, vars ?? NullVarStore.Instance, runner, executionScope, options);
            var result = await runner.ExecuteSingleAsync(data, cmdCtx, ct, options);

            if (result.Status == VNext.CommandRunStatus.Canceled)
                throw new OperationCanceledException();

            if (result.Status != VNext.CommandRunStatus.Completed)
                return VNext.CommandHostCallResult.Failure;

            return VNext.CommandHostCallResult.SuccessNoValue;
        }

        async UniTask<VNext.CommandHostCallResult> UiDialogChoiceAsync(Game.IScopeNode scope, IVarStore vars, DynamicVariant[] args, int argCount, CancellationToken ct)
        {
            await UniTask.CompletedTask;
            return VNext.CommandHostCallResult.Failure;
        }

        static bool TryResolveCommandKey(in DynamicVariant value, VNext.ICommandKeyResolver resolver, out VNext.CommandKeyId keyId)
        {
            keyId = default;
            if (resolver == null)
                return false;

            if (value.TryGet<int>(out var i))
            {
                if (i <= 0)
                    return false;
                keyId = new VNext.CommandKeyId(i);
                return true;
            }

            if (value.TryGet<string>(out var s))
            {
                if (string.IsNullOrEmpty(s))
                    return false;
                return resolver.TryResolve(s, out keyId);
            }

            return false;
        }

        static bool TryResolveStringArg(in DynamicVariant v, out string value)
        {
            value = string.Empty;
            if (v.TryGet<string>(out var s))
            {
                value = s ?? string.Empty;
                return true;
            }

            if (v.TryGet<int>(out var i))
            {
                value = i.ToString();
                return true;
            }

            if (v.TryGet<float>(out var f))
            {
                value = f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            if (v.TryGet<bool>(out var b))
            {
                value = b ? "true" : "false";
                return true;
            }

            value = v.ToString();
            return !string.IsNullOrEmpty(value);
        }

        static bool TryResolveScopeNodeArg(in DynamicVariant v, out Game.IScopeNode scope)
        {
            scope = null!;
            if (v.TryGet<Game.IScopeNode>(out var node) && node != null)
            {
                scope = node;
                return true;
            }

            if (v.TryGet(out UnityEngine.Object obj))
                return TryResolveScopeNodeFromObject(obj, out scope);

            return false;
        }

        static bool TryResolveScopeNodeFromObject(UnityEngine.Object obj, out Game.IScopeNode scope)
        {
            scope = null!;
            if (obj == null)
                return false;

            if (obj is Game.IScopeNode s)
            {
                scope = s;
                return true;
            }

            if (obj is Component comp)
            {
                var found = FindScopeNode(comp.gameObject);
                if (found != null)
                {
                    scope = found;
                    return true;
                }
                return false;
            }

            if (obj is GameObject go)
            {
                var found = FindScopeNode(go);
                if (found != null)
                {
                    scope = found;
                    return true;
                }

                scope = null!;
                return false;
            }

            return false;
        }

        static Game.IScopeNode? FindScopeNode(GameObject go)
        {
            if (go == null)
                return null;

            return Game.ScopeFeatureInstallerUtility.TryGetScopeNode(go, includeInactive: true, out var scopeNode)
                ? scopeNode
                : null;
        }

        static void EnsureScopeBuiltIfNeeded(Game.IScopeNode scope)
        {
            Game.ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }
    }
}
