#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VNext = Game.Commands.VNext;
using VContainer;
using Game.Common;

namespace Game.UI
{
    public sealed class UIModalStackAffectCommandService : IScopeAcquireHandler, IScopeReleaseHandler
    {
        static int s_globalExecutingDepth;

        readonly IScopeNode _owner;
        readonly IUIModalStackAffectCommandOptions _options;

        IUIModalStackService? _modalStackService;
        VNext.ICommandRunner? _commandRunner;
        CancellationTokenSource? _commandCts;

        bool _wasInScope;
        IUIModalRoot? _lastRoot;
        bool _isExecutingCommands;
        bool _hasPendingScopeTransition;
        bool _pendingInScope;
        bool _pendingImmediate;
        bool _pendingFlushScheduled;

        string OwnerName => _owner?.Identity?.SelfTransform != null
            ? _owner.Identity.SelfTransform.name
            : "(unknown)";
        bool IsGlobalLockEnabled => _options.UseGlobalExecutionLock;

        public UIModalStackAffectCommandService(
            IScopeNode owner,
            IUIModalStackAffectCommandOptions options)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve(out IUIModalStackService modalStackService) || modalStackService == null)
                return;

            resolver.TryResolve(out VNext.ICommandRunner runner);

            _modalStackService = modalStackService;
            _commandRunner = runner;

            _lastRoot = _modalStackService.CurrentInputRoot;
            _wasInScope = EvaluateInScope(_modalStackService.ActiveRoots);

            _modalStackService.OnModalStackChanged += HandleModalStackChanged;
            _modalStackService.OnActiveRootsChanged += HandleActiveRootsChanged;

            if (_options.ExecuteOnAcquire && _wasInScope)
            {
                ExecuteImmediateInScopeCommands().Forget();
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            if (_modalStackService != null)
            {
                _modalStackService.OnModalStackChanged -= HandleModalStackChanged;
                _modalStackService.OnActiveRootsChanged -= HandleActiveRootsChanged;
            }

            StopCommands();

            if (_options.ExecuteOnRelease && _wasInScope)
            {
                ExecuteImmediateOutOfScopeCommands().Forget();
            }

            _modalStackService = null;
            _commandRunner = null;
            _lastRoot = null;
            _wasInScope = false;
            _hasPendingScopeTransition = false;
            _pendingInScope = false;
            _pendingImmediate = false;
            _pendingFlushScheduled = false;
        }

        void HandleModalStackChanged(UIModalStackChangeContext context)
        {
            var previousRoot = context.PreviousRoot ?? _lastRoot;
            var resolvedContext = previousRoot == context.PreviousRoot
            ? context
            : new UIModalStackChangeContext(context.StackKey, previousRoot, context.CurrentRoot, context.ChangeType);

            var kind = EvaluateChangeKind(resolvedContext.PreviousRoot, resolvedContext.CurrentRoot);
            _lastRoot = context.CurrentRoot;

            // Policy-based ignore for descendant changes
            if (ShouldIgnoreChange(kind, _options.Policy))
                return;
        }

        void HandleActiveRootsChanged(UIModalStackRootsChangeContext context)
        {
            if (context.ChangeType == UIModalStackChangeType.Temporary)
            {
                return;
            }

            if (context.ChangeKind == ActiveRootsChangeKind.StackChanged && context.StackChangeKind.HasValue)
            {
                if (ShouldIgnoreChange(context.StackChangeKind.Value, _options.Policy))
                    return;
            }

            var inScope = EvaluateInScope(context.CurrentRoots);
            if (_isExecutingCommands || (IsGlobalLockEnabled && s_globalExecutingDepth > 0))
            {
                _hasPendingScopeTransition = true;
                _pendingInScope = inScope;
                _pendingImmediate = context.ChangeType == UIModalStackChangeType.Immediate;
                SchedulePendingFlush();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log(
                //    $"[UIModalStackAffectCommandService] Suppressed recursive ActiveRootsChanged while executing commands. " +
                //    $"Owner={OwnerName}, ChangeType={context.ChangeType}, ChangeKind={context.ChangeKind}, InScopeNow={_wasInScope}, " +
                //    $"LocalExecuting={_isExecutingCommands}, GlobalExecutingDepth={s_globalExecutingDepth}");
#endif
                return;
            }

            ApplyScopeTransition(inScope, context.ChangeType == UIModalStackChangeType.Immediate);
        }

        ModalStackChangeKind EvaluateChangeKind(IUIModalRoot? previousRoot, IUIModalRoot? currentRoot)
        {
            if (previousRoot == null || currentRoot == null)
                return ModalStackChangeKind.RootSwap;

            if (previousRoot.IsDescendant(currentRoot.OwnerScope))
                return ModalStackChangeKind.DescendantPush;

            if (currentRoot.IsDescendant(previousRoot.OwnerScope))
                return ModalStackChangeKind.DescendantPop;

            return ModalStackChangeKind.RootSwap;
        }

        static bool ShouldIgnoreChange(ModalStackChangeKind kind, UIModalStackAffectPolicy policy)
        {
            switch (policy)
            {
                case UIModalStackAffectPolicy.IgnoreNestedChange:
                    return kind == ModalStackChangeKind.DescendantPush || kind == ModalStackChangeKind.DescendantPop;
                case UIModalStackAffectPolicy.IgnoreDescendantPush:
                    return kind == ModalStackChangeKind.DescendantPush;
                case UIModalStackAffectPolicy.IgnoreDescendantPop:
                    return kind == ModalStackChangeKind.DescendantPop;
            }

            return false;
        }

        bool EvaluateInScope(IReadOnlyList<UIModalActiveRoot> roots)
        {
            if (roots == null || roots.Count == 0)
                return true;

            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i].Root;
                if (root != null && root.IsDescendant(_owner))
                    return true;
            }

            return false;
        }

        async UniTaskVoid ExecuteInScopeCommands()
        {
            await ExecuteCommandsAsync(_options.OnBecameInScope, "OnBecameInScope");
        }

        async UniTaskVoid ExecuteOutOfScopeCommands()
        {
            await ExecuteCommandsAsync(_options.OnBecameOutOfScope, "OnBecameOutOfScope");
        }

        async UniTaskVoid ExecuteImmediateInScopeCommands()
        {
            await ExecuteCommandsAsync(_options.OnBecameImmediateInScope, "OnBecameImmediateInScope");
        }

        async UniTaskVoid ExecuteImmediateOutOfScopeCommands()
        {
            await ExecuteCommandsAsync(_options.OnBecameImmediateOutOfScope, "OnBecameImmediateOutOfScope");
        }

        async UniTask ExecuteCommandsAsync(VNext.CommandListData commands, string label)
        {
            var runner = _commandRunner;
            if (runner == null)
                return;

            if (commands == null || commands.Count == 0)
                return;

            if (_isExecutingCommands)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[UIModalStackAffectCommandService] Skip '{label}' because command execution is already in progress. Owner={OwnerName}");
#endif
                return;
            }

            ResetCommandState();

            if (_commandCts == null)
                return;

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, NullVarStore.Instance, runner, _owner, options);

            try
            {
                _isExecutingCommands = true;
                if (IsGlobalLockEnabled)
                    s_globalExecutingDepth++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[UIModalStackAffectCommandService] Execute '{label}' start. Owner={OwnerName}, GlobalExecutingDepth={s_globalExecutingDepth}");
#endif
                var result = await runner.ExecuteListAsync(commands, ctx, _commandCts.Token, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[UIModalStackAffectCommandService] {label} command failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                if (IsGlobalLockEnabled && s_globalExecutingDepth > 0)
                    s_globalExecutingDepth--;
                _isExecutingCommands = false;
                FlushPendingScopeTransition();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[UIModalStackAffectCommandService] Execute '{label}' end. Owner={OwnerName}, GlobalExecutingDepth={s_globalExecutingDepth}");
#endif
            }
        }

        void ApplyScopeTransition(bool inScope, bool immediate)
        {
            if (inScope == _wasInScope)
                return;

            _wasInScope = inScope;

            if (immediate)
            {
                if (inScope)
                    ExecuteImmediateInScopeCommands().Forget();
                else
                    ExecuteImmediateOutOfScopeCommands().Forget();
                return;
            }

            if (inScope)
                ExecuteInScopeCommands().Forget();
            else
                ExecuteOutOfScopeCommands().Forget();
        }

        void FlushPendingScopeTransition()
        {
            if (!_hasPendingScopeTransition)
                return;

            if (_isExecutingCommands || (IsGlobalLockEnabled && s_globalExecutingDepth > 0))
                return;

            var inScope = _pendingInScope;
            var immediate = _pendingImmediate;
            _hasPendingScopeTransition = false;
            _pendingInScope = false;
            _pendingImmediate = false;

            ApplyScopeTransition(inScope, immediate);
        }

        void SchedulePendingFlush()
        {
            if (_pendingFlushScheduled)
                return;

            _pendingFlushScheduled = true;
            UniTask.Void(async () =>
            {
                try
                {
                    while (_hasPendingScopeTransition && (_isExecutingCommands || (IsGlobalLockEnabled && s_globalExecutingDepth > 0)))
                    {
                        await UniTask.Yield();
                    }

                    FlushPendingScopeTransition();
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    _pendingFlushScheduled = false;
                }
            });
        }

        void ResetCommandState()
        {
            StopCommands();
            _commandCts = new CancellationTokenSource();
        }

        void StopCommands()
        {
            if (_commandCts == null)
                return;

            _commandCts.Cancel();
            _commandCts.Dispose();
            _commandCts = null;
        }
    }
}
