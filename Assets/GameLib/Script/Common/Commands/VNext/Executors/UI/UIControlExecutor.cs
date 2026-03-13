#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class UIControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.UIControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not UIControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "UIControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                if (AllowFallback(ctx.Options))
                {
                    Debug.LogWarning($"[UIControlExecutor] Target resolve failed: {error} Falling back to current scope.");
                    targetScope = ctx.Scope;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error);
                }
            }

            if (targetScope == null)
                return;

            EnsureScopeBuiltIfNeeded(targetScope);

            ExecuteOperation(typed, ctx, targetScope);

            if (typed.Then == null || typed.Then.Count == 0)
                return;

            await ExecuteThenAsync(typed, ctx, targetScope, ct);
        }

        static void ExecuteOperation(UIControlCommandData typed, CommandContext ctx, IScopeNode targetScope)
        {
            switch (typed.Operation)
            {
                // ---------------- Modal stack ----------------
                case UIControlOperation.ModalPush:
                    if (TryResolve(targetScope, out IUIModalStackService? modal) && modal != null
                        && TryResolve(targetScope, out IUIModalRoot? root) && root != null)
                    {
                        if (HasStackKey(typed))
                            modal.PushModal(typed.StackKey, root, typed.ModalOptions);
                        else
                            modal.PushModal(root, typed.ModalOptions);
                    }
                    break;

                case UIControlOperation.ModalPop:
                    if (TryResolve(targetScope, out IUIModalStackService? modalPop) && modalPop != null
                        && TryResolve(targetScope, out IUIModalRoot? rootPop) && rootPop != null)
                    {
                        if (HasStackKey(typed))
                            modalPop.PopModal(typed.StackKey, rootPop);
                        else
                            modalPop.PopModal(rootPop);
                    }
                    break;

                case UIControlOperation.ModalPopTop:
                    if (TryResolve(targetScope, out IUIModalStackService? modalPopTop) && modalPopTop != null)
                    {
                        if (HasStackKey(typed))
                            modalPopTop.PopTop(typed.StackKey);
                        else
                            modalPopTop.PopTop();
                    }
                    break;

                case UIControlOperation.ModalClearAll:
                    if (TryResolve(targetScope, out IUIModalStackService? modalClear) && modalClear != null)
                    {
                        modalClear.ClearAll();
                    }
                    break;

                case UIControlOperation.ModalSetDefaultRoot:
                    if (TryResolve(targetScope, out IUIModalStackService? modalSetRoot) && modalSetRoot != null
                        && TryResolve(targetScope, out IUIModalRoot? defaultRoot) && defaultRoot != null)
                    {
                        if (HasStackKey(typed))
                            modalSetRoot.SetDefaultRoot(typed.StackKey, defaultRoot);
                        else
                            modalSetRoot.SetDefaultRoot(defaultRoot);
                    }
                    break;

                // ---------------- Selection ----------------
                case UIControlOperation.Select:
                    if (TryResolve(targetScope, out IUISelectionNavigation? selection) && selection != null)
                    {
                        selection.Select(targetScope);
                    }
                    break;

                case UIControlOperation.TrySelect:
                    if (TryResolve(targetScope, out IUISelectionNavigation? selectionTry) && selectionTry != null)
                    {
                        selectionTry.TrySelect(targetScope);
                    }
                    break;

                case UIControlOperation.ClearSelection:
                    if (TryResolve(targetScope, out IUISelectionNavigation? selectionClear) && selectionClear != null)
                    {
                        selectionClear.ClearSelection();
                    }
                    break;

                // ---------------- Element state ----------------
                case UIControlOperation.SetActive:
                    if (TryResolve(targetScope, out IUIElementStateController? stActive) && stActive != null)
                    {
                        stActive.SetActive(typed.Active);
                    }
                    break;

                case UIControlOperation.ToggleActive:
                    if (TryResolve(targetScope, out IUIElementStateController? stToggleActive) && stToggleActive != null)
                    {
                        stToggleActive.ToggleActive();
                    }
                    break;

                case UIControlOperation.SetVisible:
                    if (TryResolve(targetScope, out IUIElementStateController? stVisible) && stVisible != null)
                    {
                        stVisible.SetVisible(typed.Visible);
                    }
                    break;

                case UIControlOperation.ToggleVisible:
                    if (TryResolve(targetScope, out IUIElementStateController? stToggleVisible) && stToggleVisible != null)
                    {
                        stToggleVisible.ToggleVisible();
                    }
                    break;

                // ---------------- Navigation ----------------
                case UIControlOperation.SetNavigationSelectable:
                    // NOTE: SetNavigationSelectable(bool) は廃止予定です。
                    // DynamicValue<bool> ベースのシステムに移行してください。
#pragma warning disable CS0618
                    if (TryResolve(targetScope, out UIElementStateService? stNav) && stNav != null)
                    {
                        stNav.SetNavigationSelectable(typed.NavigationSelectable);
                    }
#pragma warning restore CS0618
                    break;

                case UIControlOperation.SetNavigationOverride:
                    if (TryResolve(targetScope, out UIElementStateService? stOverride) && stOverride != null)
                    {
                        stOverride.SetNavigationOverride(typed.NavigationOverride);
                    }
                    break;

                case UIControlOperation.ClearNavigationOverride:
                    if (TryResolve(targetScope, out UIElementStateService? stClearOverride) && stClearOverride != null)
                    {
                        stClearOverride.SetNavigationOverride(null);
                    }
                    break;

                default:
                    Debug.LogWarning($"[UIControlExecutor] Unknown operation: {typed.Operation}");
                    break;
            }
        }

        static async UniTask ExecuteThenAsync(UIControlCommandData typed, CommandContext ctx, IScopeNode targetScope, CancellationToken ct)
        {
            var allowFallback = AllowFallback(ctx.Options);

            var executionScope = targetScope;
            if (!TryResolveRunner(executionScope, out var runner) || runner == null)
            {
                if (allowFallback)
                {
                    Debug.LogWarning($"[UIControlExecutor] Target scope {DescribeScope(executionScope)} has no runner; falling back to current scope.");
                    executionScope = ctx.Scope ?? executionScope;
                    runner = ctx.Runner;
                }
                else
                {
                    throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, $"Target scope {DescribeScope(executionScope)} has no ICommandRunner.");
                }
            }

            if (runner == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "No ICommandRunner available in target or fallback scope.");

            var vars = ResolveVars(typed.VarsPolicy, ctx, executionScope);
            var nextCtx = new CommandContext(executionScope, vars, runner, actor: targetScope, options: ctx.Options, commandRootScope: ctx.CommandRootScope, rootActor: ctx.RootActor, callerActor: ctx.Actor);

            var result = await runner.ExecuteListAsync(typed.Then, nextCtx, ct, nextCtx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();

            if (result.Status == CommandRunStatus.Error || result.FailureCount > 0)
            {
                var msg = $"UIControl then-list failed for actor {DescribeScope(targetScope)}. FailureCount={result.FailureCount}, ErrorIndex={result.ErrorIndex}, Message={result.Message}";
                throw new CommandExecutionException(result.FailureKind, msg);
            }
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
            }
        }

        static bool TryResolveRunner(IScopeNode scope, out ICommandRunner? runner)
        {
            runner = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out runner) && runner != null;
        }

        static bool TryResolve<T>(IScopeNode scope, out T? value) where T : class
        {
            value = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out value) && value != null;
        }

        static IVarStore ResolveVars(VarsPolicy policy, CommandContext ctx, IScopeNode actorScope)
        {
            if (policy == VarsPolicy.UseActorScopeVars)
            {
                var resolver = actorScope?.Resolver;
                if (resolver != null && resolver.TryResolve<IVarStore>(out var vars) && vars != null)
                    return vars;
                return NullVarStore.Instance;
            }

            return ctx.Vars ?? NullVarStore.Instance;
        }

        static bool AllowFallback(CommandRunOptions options)
        {
            if (!options.AllowActorFallback)
                return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }

        static bool HasStackKey(UIControlCommandData typed)
        {
            return !string.IsNullOrEmpty(typed.StackKey);
        }

        static string DescribeScope(IScopeNode scope)
        {
            if (scope == null)
                return "null";
            try
            {
                var id = scope.Identity?.Id ?? "(no id)";
                return $"{scope.Kind}:{id}";
            }
            catch
            {
                return scope.Kind.ToString();
            }
        }
    }
}
