#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
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

            if (typed.Operation == UIControlOperation.ModalClearAll || typed.Operation == UIControlOperation.ModalLayerClearAll)
            {
                var clearScope = await ResolveControlScopeByUiLtsIdOrThrowAsync(typed, ctx, ct);
                EnsureScopeBuiltIfNeeded(clearScope);

                ExecuteOperation(typed, ctx, clearScope, clearScope);
                return;
            }

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
            var controlScope = ResolveControlScopeOrThrow(typed, ctx, targetScope);
            EnsureScopeBuiltIfNeeded(controlScope);

            ExecuteOperation(typed, ctx, targetScope, controlScope);

            if (typed.Then == null || typed.Then.Count == 0)
                return;

            await ExecuteThenAsync(typed, ctx, targetScope, ct);
        }

        static void ExecuteOperation(UIControlCommandData typed, CommandContext ctx, IScopeNode targetScope, IScopeNode controlScope)
        {
            switch (typed.Operation)
            {
                // ---------------- Modal stack ----------------
                case UIControlOperation.ModalPush:
                    if (TryResolve(controlScope, out IUIModalStackService? modal) && modal != null
                        && TryResolve(targetScope, out IUIModalRoot? root) && root != null)
                    {
                        if (HasStackKey(typed))
                            modal.PushModal(typed.StackKey, root, typed.ModalOptions);
                        else
                            modal.PushModal(root, typed.ModalOptions);
                    }
                    break;

                case UIControlOperation.ModalPop:
                    if (TryResolve(controlScope, out IUIModalStackService? modalPop) && modalPop != null
                        && TryResolve(targetScope, out IUIModalRoot? rootPop) && rootPop != null)
                    {
                        if (HasStackKey(typed))
                            modalPop.PopModal(typed.StackKey, rootPop);
                        else
                            modalPop.PopModal(rootPop);
                    }
                    break;

                case UIControlOperation.ModalPopTop:
                    if (TryResolve(controlScope, out IUIModalStackService? modalPopTop) && modalPopTop != null)
                    {
                        if (HasStackKey(typed))
                            modalPopTop.PopTop(typed.StackKey);
                        else
                            modalPopTop.PopTop();
                    }
                    break;

                case UIControlOperation.ModalClearAll:
                    if (TryResolve(controlScope, out IUIModalStackService? modalClear) && modalClear != null)
                    {
                        modalClear.ClearAll();
                    }
                    break;

                case UIControlOperation.ModalSetDefaultRoot:
                    if (TryResolve(controlScope, out IUIModalStackService? modalSetRoot) && modalSetRoot != null
                        && TryResolve(targetScope, out IUIModalRoot? defaultRoot) && defaultRoot != null)
                    {
                        if (HasStackKey(typed))
                            modalSetRoot.SetDefaultRoot(typed.StackKey, defaultRoot);
                        else
                            modalSetRoot.SetDefaultRoot(defaultRoot);
                    }
                    break;

                // ---------------- Modal stack channel ----------------
                case UIControlOperation.ModalLayerPush:
                    if (TryResolve(controlScope, out IModalStackChannelHubService? modalLayerPush) && modalLayerPush != null
                        && TryResolve(targetScope, out IUIModalRoot? layerPushRoot) && layerPushRoot != null)
                    {
                        modalLayerPush.PushModal(typed.LayerKey, layerPushRoot, typed.ModalOptions);
                    }
                    break;

                case UIControlOperation.ModalLayerPop:
                    if (TryResolve(controlScope, out IModalStackChannelHubService? modalLayerPop) && modalLayerPop != null
                        && TryResolve(targetScope, out IUIModalRoot? layerPopRoot) && layerPopRoot != null)
                    {
                        modalLayerPop.PopModal(typed.LayerKey, layerPopRoot);
                    }
                    break;

                case UIControlOperation.ModalLayerPopTop:
                    if (TryResolve(controlScope, out IModalStackChannelHubService? modalLayerPopTop) && modalLayerPopTop != null)
                    {
                        modalLayerPopTop.PopTop(typed.LayerKey);
                    }
                    break;

                case UIControlOperation.ModalLayerClear:
                    if (TryResolve(controlScope, out IModalStackChannelHubService? modalLayerClear) && modalLayerClear != null)
                    {
                        modalLayerClear.ClearLayer(typed.LayerKey);
                    }
                    break;

                case UIControlOperation.ModalLayerClearAll:
                    if (TryResolve(controlScope, out IModalStackChannelHubService? modalLayerClearAll) && modalLayerClearAll != null)
                    {
                        modalLayerClearAll.ClearAll();
                    }
                    break;

                case UIControlOperation.ModalLayerSetDefaultRoot:
                    if (TryResolve(controlScope, out IModalStackChannelHubService? modalLayerSetRoot) && modalLayerSetRoot != null
                        && TryResolve(targetScope, out IUIModalRoot? layerDefaultRoot) && layerDefaultRoot != null)
                    {
                        modalLayerSetRoot.SetDefaultRoot(typed.LayerKey, layerDefaultRoot);
                    }
                    break;

                // ---------------- Selection ----------------
                case UIControlOperation.Select:
                    if (TryResolve(controlScope, out IUISelectionNavigation? selection) && selection != null)
                    {
                        selection.Select(targetScope);
                    }
                    break;

                case UIControlOperation.TrySelect:
                    if (TryResolve(controlScope, out IUISelectionNavigation? selectionTry) && selectionTry != null)
                    {
                        selectionTry.TrySelect(targetScope);
                    }
                    break;

                case UIControlOperation.ClearSelection:
                    if (TryResolve(controlScope, out IUISelectionNavigation? selectionClear) && selectionClear != null)
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
            var nextCtx = new CommandContext(executionScope, vars, runner, actor: targetScope, options: ctx.Options, commandRootScope: ctx.CommandRootScope, rootActor: ctx.RootActor, callerActor: ctx.Actor, sourceContext: ctx);

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

        static IScopeNode ResolveControlScopeOrThrow(UIControlCommandData typed, CommandContext ctx, IScopeNode targetScope)
        {
            if (string.IsNullOrEmpty(typed.UILifetimeScopeId))
                return targetScope;

            var origin = ctx.Scope ?? targetScope;
            if (!TryResolveScopeRegistry(origin, out var registry) || registry == null)
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.ResolveFailed,
                    $"UIControl could not resolve IBaseLifetimeScopeRegistry while looking for UILifetimeScope '{typed.UILifetimeScopeId}'.");
            }

            var filter = new CommandTargetIdentityFilter
            {
                kind = LifetimeScopeKind.None,
                id = typed.UILifetimeScopeId,
                category = string.Empty,
                requireActive = false,
                searchScope = CommandTargetSearchScope.All,
            };

            var resolved = registry.Resolve(filter, origin);
            if (resolved != null)
                return resolved;

            throw new CommandExecutionException(
                CommandRunFailureKind.ResolveFailed,
                $"UILifetimeScope '{typed.UILifetimeScopeId}' was not found for UIControl.");
        }

        static async UniTask<IScopeNode> ResolveControlScopeByUiLtsIdOrThrowAsync(UIControlCommandData typed, CommandContext ctx, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(typed.UILifetimeScopeId))
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.ResolveFailed,
                    "UIControl.ModalClearAll requires UILifetimeScopeId.");
            }

            var origin = ctx.Scope;
            if (!TryResolveScopeRegistry(origin, out var registry) || registry == null)
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.ResolveFailed,
                    $"UIControl could not resolve IBaseLifetimeScopeRegistry while looking for UILifetimeScope '{typed.UILifetimeScopeId}'.");
            }

            var filter = new CommandTargetIdentityFilter
            {
                kind = LifetimeScopeKind.None,
                id = typed.UILifetimeScopeId,
                category = string.Empty,
                requireActive = false,
                searchScope = CommandTargetSearchScope.All,
            };

            var resolved = registry.Resolve(filter, origin);
            if (resolved != null)
                return resolved;

            if (TryFindUiLifetimeScopeById(typed.UILifetimeScopeId, out var uiScope) && uiScope != null)
            {
                await WaitForUiLifetimeScopeBuildAsync(uiScope, ct);

                origin = ctx.Scope;
                if (!TryResolveScopeRegistry(origin, out registry) || registry == null)
                {
                    throw new CommandExecutionException(
                        CommandRunFailureKind.ResolveFailed,
                        $"UIControl could not resolve IBaseLifetimeScopeRegistry while looking for UILifetimeScope '{typed.UILifetimeScopeId}'.");
                }

                resolved = registry.Resolve(filter, origin);
                if (resolved != null)
                    return resolved;

                return uiScope;
            }

            await UniTask.DelayFrame(1, PlayerLoopTiming.Update, ct);

            ct.ThrowIfCancellationRequested();

            origin = ctx.Scope;
            if (!TryResolveScopeRegistry(origin, out registry) || registry == null)
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.ResolveFailed,
                    $"UIControl could not resolve IBaseLifetimeScopeRegistry while looking for UILifetimeScope '{typed.UILifetimeScopeId}'.");
            }

            resolved = registry.Resolve(filter, origin);
            if (resolved != null)
                return resolved;

            var uiLifetimeScopeId = typed.UILifetimeScopeId ?? string.Empty;
            var diagnostics = BuildUiLifetimeScopeResolutionDiagnostics(uiLifetimeScopeId, origin, registry, filter);

            throw new CommandExecutionException(
                CommandRunFailureKind.ResolveFailed,
                $"UILifetimeScope '{typed.UILifetimeScopeId}' was not found for UIControl.ModalClearAll.\n{diagnostics}");
        }

        static string BuildUiLifetimeScopeResolutionDiagnostics(
            string uiLifetimeScopeId,
            IScopeNode? origin,
            IBaseLifetimeScopeRegistry registry,
            CommandTargetIdentityFilter filter)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Detail:");
            sb.Append("\n- UILifetimeScopeId=").Append(uiLifetimeScopeId ?? string.Empty);
            sb.Append("\n- Origin=").Append(DescribeScope(origin));
            sb.Append("\n- OriginChain=").Append(DescribeScopeChain(origin));

            var registryHits = registry?.ResolveAll(filter, origin);
            sb.Append("\n- RegistryResolveAllCount=").Append(registryHits?.Count ?? 0);
            if (registryHits != null && registryHits.Count > 0)
            {
                sb.Append("\n- RegistryResolveAll=");
                for (var index = 0; index < registryHits.Count; index++)
                {
                    if (index > 0)
                        sb.Append(", ");
                    sb.Append(DescribeScope(registryHits[index]));
                }
            }

            DescribeUiLifetimeScopeSceneCandidates(uiLifetimeScopeId ?? string.Empty, sb);
            return sb.ToString();
        }

        static bool TryFindUiLifetimeScopeById(string uiLifetimeScopeId, out UILifetimeScope? scope)
        {
#if UNITY_2022_2_OR_NEWER
            var candidates = UnityEngine.Object.FindObjectsByType<UILifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var candidates = UnityEngine.Object.FindObjectsOfType<UILifetimeScope>(true);
#endif
            foreach (var candidate in candidates)
            {
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.name, uiLifetimeScopeId, StringComparison.Ordinal))
                {
                    scope = candidate;
                    return true;
                }

                var identity = candidate.GetComponent<LTSIdentityMB>();
                var authoringId = identity != null && !string.IsNullOrEmpty(identity.id) ? identity.id : candidate.name;
                if (string.Equals(authoringId, uiLifetimeScopeId, StringComparison.Ordinal))
                {
                    scope = candidate;
                    return true;
                }
            }

            scope = null;
            return false;
        }

        static async UniTask WaitForUiLifetimeScopeBuildAsync(UILifetimeScope scope, CancellationToken ct)
        {
            if (scope == null)
                return;

            if (scope is BaseLifetimeScope baseScope && !baseScope.IsBuildCompleted)
            {
                await baseScope.WhenBuiltAsync(ct);
                return;
            }

            if (scope.Resolver == null)
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        static void DescribeUiLifetimeScopeSceneCandidates(string? uiLifetimeScopeId, System.Text.StringBuilder sb)
        {
#if UNITY_2022_2_OR_NEWER
            var candidates = UnityEngine.Object.FindObjectsByType<UILifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var candidates = UnityEngine.Object.FindObjectsOfType<UILifetimeScope>(true);
#endif
            sb.Append("\n- SceneUiScopeCount=").Append(candidates?.Length ?? 0);

            if (candidates == null || candidates.Length == 0)
                return;

            sb.Append("\n- SceneUiScopes=");
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                if (candidate == null)
                    continue;

                if (index > 0)
                    sb.Append(", ");

                var identity = candidate.Identity;
                var runtimeId = identity?.Id;
                var displayId = runtimeId ?? candidate.name ?? "(no id)";
                sb.Append(candidate.name).Append('(').Append(displayId).Append(')');
                if (string.Equals(candidate.name, uiLifetimeScopeId, StringComparison.Ordinal) ||
                    string.Equals(displayId, uiLifetimeScopeId, StringComparison.Ordinal))
                    sb.Append("[match]");
            }
        }

        static string DescribeScopeChain(IScopeNode? scope)
        {
            if (scope == null)
                return "null";

            var path = scope.GetPathFromRoot();
            if (path == null || path.Count == 0)
                return DescribeScope(scope);

            var sb = new System.Text.StringBuilder();
            for (var index = 0; index < path.Count; index++)
            {
                var chainNode = path[index];
                if (chainNode == null)
                    continue;

                if (index > 0)
                    sb.Append(" -> ");
                sb.Append(DescribeScope(chainNode));
            }

            return sb.ToString();
        }

        static bool TryResolveScopeRegistry(IScopeNode? origin, out IBaseLifetimeScopeRegistry? registry)
        {
            var current = origin;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var resolved) && resolved != null)
                {
                    registry = resolved;
                    return true;
                }

                current = current.Parent;
            }

            registry = null;
            return false;
        }

        static string DescribeScope(IScopeNode? scope)
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
