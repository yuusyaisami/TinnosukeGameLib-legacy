#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game.Spawn;
using UnityEngine;
using VContainer;
using Game.UI;

namespace Game.UI
{
    public sealed class DialogChannelRuntime : IDisposable
    {
        readonly string _channelKey;
        readonly DialogChannelDef _def;
        readonly ISceneSpawnerRegistry _spawnerRegistry;
        readonly IUIModalStackService? _modalStackService;
        readonly IUIModalStackTelemetry? _modalTelemetry;
        DialogEventBinding[] _bindings;
        readonly List<DialogEventBinding> _additionalBindings = new(8);

        CancellationTokenSource? _lifetimeCts;
        IObjectResolver? _resolver;
        Transform? _root;

        IScopeNode? _dialogScope;
        IScopeNode? _owner;
        VNext.ICommandRunner? _commandRunner;
        IEventService? _eventService;
        IUIDialogRuntimeService? _runtimeService;
        IUIModalRoot? _modalRoot;

        bool _modalStackSubscribed;

        readonly List<IDisposable> _eventSubscriptions = new(8);
        readonly HashSet<string> _processingEventKeys = new(StringComparer.Ordinal);

        public bool IsVisible => _resolver != null;

        public string ChannelKey => _channelKey;
        public IScopeNode? DialogScope => _dialogScope;
        public IScopeNode? Owner => _owner;

        public DialogChannelRuntime(
            string channelKey,
            DialogChannelDef def,
            ISceneSpawnerRegistry spawnerRegistry,
            IUIModalStackService? modalStackService = null,
            IUIModalStackTelemetry? modalTelemetry = null)
        {
            _channelKey = channelKey ?? string.Empty;
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _spawnerRegistry = spawnerRegistry ?? throw new ArgumentNullException(nameof(spawnerRegistry));
            _modalStackService = modalStackService;
            _modalTelemetry = modalTelemetry;
            _bindings = def.Bindings ?? Array.Empty<DialogEventBinding>();
        }
        /// <summary>
        /// 現在あるバインディングをすべて置き換えます。
        /// </summary>
        /// <param name="bindings">新しいバインディングの配列</param>
        public void SetSubscribeBindings(DialogEventBinding[] bindings)
        {
            // Replace the primary bindings list and, if the dialog is visible, re-subscribe
            _bindings = bindings ?? Array.Empty<DialogEventBinding>();

            if (!IsVisible || _eventService == null)
                return;

            ClearSubscribeBindings();

            // Re-subscribe additional bindings
            if (_additionalBindings != null && _additionalBindings.Count > 0)
            {
                for (int i = 0; i < _additionalBindings.Count; i++)
                {
                    var tmp = _additionalBindings[i];
                    SubscribeBindingsCore(_eventService, tmp);
                }
            }
        }

        /// <summary>
        /// 元あるBindingsから追加でBindingsを登録します。
        /// </summary>
        /// <param name="bindings">追加するバインディングの配列</param>
        public void AddSubscribeBindings(DialogEventBinding[] bindings)
        {
            // Append bindings to the additional list and, if visible, subscribe them now.
            if (bindings == null || bindings.Length == 0)
                return;

            for (int i = 0; i < bindings.Length; i++)
            {
                _additionalBindings.Add(bindings[i]);
            }

            if (!IsVisible || _eventService == null)
                return;

            // Add-only: do not clear existing subscriptions; just subscribe the new bindings.
            SubscribeBindingsCore(_eventService, bindings);
        }

        public void Show(UIDialogRequest request)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await ShowAsync(request, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }

        public async UniTask ShowAsync(UIDialogRequest request, CancellationToken ct = default)
        {
            Hide(DialogCloseReason.Replaced);

            _lifetimeCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCts.Token);

            try
            {
                await ShowInternalAsync(request, linkedCts.Token);
                if (!IsVisible)
                    CancelLifetime();
            }
            catch (OperationCanceledException)
            {
                Hide(DialogCloseReason.Explicit);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Hide(DialogCloseReason.Explicit);
            }
        }

        /// <summary>
        /// Show the dialog and await one of the specified event keys. The first matching event will complete the returned task.
        /// This method will subscribe transient handlers to the event service (instance-local) and will clean up subscriptions when
        /// completed or cancelled. If the dialog is closed before an awaited event occurs, the task completes with WasCancelled=true.
        /// </summary>
        public async UniTask<DialogAwaitResult> ShowAndWaitAsync(UIDialogRequest request, Game.UI.DialogAwaitSpec spec, CancellationToken ct = default)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));

            if (spec.EventKeys == null || spec.EventKeys.Length == 0)
                throw new ArgumentException("spec.EventKeys must contain at least one event key", nameof(spec));

            // Show the dialog first (respecting cancellation).
            await ShowAsync(request, ct);

            // If not visible or no event service resolved, bail out as cancelled.
            if (!IsVisible || _eventService == null)
                return new Game.UI.DialogAwaitResult { WasCancelled = true, CloseReason = _resolver != null ? null : DialogCloseReason.Explicit };

            var tcs = new UniTaskCompletionSource<Game.UI.DialogAwaitResult>();
            var subs = new List<IDisposable>();
            var completed = 0; // 0 = not completed, 1 = completed

            void CompleteResult(string key, IVarStore? payload, DialogCloseReason? closeReason = null)
            {
                if (Interlocked.Exchange(ref completed, 1) == 1)
                    return;

                var idx = spec.MapKeyToIndex(key);
                var result = new Game.UI.DialogAwaitResult
                {
                    EventKey = key ?? string.Empty,
                    SelectedIndex = idx,
                    Payload = payload,
                    WasCancelled = false,
                    CloseReason = closeReason
                };

                try { tcs.TrySetResult(result); } catch { }

                try
                {
                    for (int i = 0; i < subs.Count; i++) try { subs[i]?.Dispose(); } catch { }
                }
                catch { }
            }

            // Subscribe transient handlers to the event service.
            for (int i = 0; i < spec.EventKeys.Length; i++)
            {
                var key = spec.EventKeys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var sub = _eventService.Subscribe(key, (payload, _) =>
                {
                    // NOTE:
                    // - This handler is for awaiting only.
                    // - Do NOT share _processingEventKeys with binding execution;
                    //   otherwise the awaited completion can be blocked by a bound handler.
                    CompleteResult(key, payload, DialogCloseReason.ActionInvoked);
                    if (spec.CloseAfterEvent)
                        Hide(DialogCloseReason.ActionInvoked);

                    return UniTask.CompletedTask;
                });

                if (sub != null)
                    subs.Add(sub);
            }

            // Cancellation: if either the caller cancels or the dialog lifetime ends, complete as cancelled.
            CancellationTokenRegistration? regCaller = null;
            CancellationTokenRegistration? regLifetime = null;
            try
            {
                if (ct.CanBeCanceled)
                {
                    regCaller = ct.Register(() =>
                    {
                        if (Interlocked.Exchange(ref completed, 1) == 1)
                            return;

                        try { tcs.TrySetResult(new Game.UI.DialogAwaitResult { WasCancelled = true }); } catch { }

                        try { for (int i = 0; i < subs.Count; i++) try { subs[i]?.Dispose(); } catch { } } catch { }
                    });
                }

                if (_lifetimeCts != null)
                {
                    var lt = _lifetimeCts;
                    regLifetime = lt.Token.Register(() =>
                    {
                        if (Interlocked.Exchange(ref completed, 1) == 1)
                            return;

                        try { tcs.TrySetResult(new Game.UI.DialogAwaitResult { WasCancelled = true, CloseReason = DialogCloseReason.Explicit }); } catch { }

                        try { for (int i = 0; i < subs.Count; i++) try { subs[i]?.Dispose(); } catch { } } catch { }
                    });
                }

                // If the dialog is hidden while waiting, the subscriptions will be disposed by Hide(); we should additionally monitor tcs.
                var res = await tcs.Task;
                return res;
            }
            finally
            {
                try { regCaller?.Dispose(); } catch { }
                try { regLifetime?.Dispose(); } catch { }

                // Ensure transient subscriptions are disposed.
                try { for (int i = 0; i < subs.Count; i++) try { subs[i]?.Dispose(); } catch { } } catch { }
            }
        }

        async UniTask ShowInternalAsync(UIDialogRequest request, CancellationToken ct)
        {
            if (request.Owner == null || request.Owner.Resolver == null)
                return;

            if (!TryBuildSpawnParams(request, out var spawnParams))
                return;

            var resolver = await SpawnDialogAsync(spawnParams, ct);
            ct.ThrowIfCancellationRequested();

            if (resolver == null)
                return;

            ExtractSpawnedInfo(resolver, out var root, out var dialogScope, out _, out _);
            if (dialogScope == null)
            {
                await ReleaseSpawnedInstanceAsync(root, dialogScope, resolver);
                return;
            }

            if (request.InitialVariables != null &&
                resolver.TryResolve<IBlackboardService>(out var bb) &&
                bb != null)
            {
                request.InitialVariables.MergeInto(bb.LocalVars, overwrite: true);
            }

            if (!TryResolveEventService(resolver, out var eventService))
            {
                await ReleaseSpawnedInstanceAsync(root, dialogScope, resolver);
                return;
            }

            var runner = ResolveCommandRunner(request, resolver);

            _resolver = resolver;
            _root = root;
            _dialogScope = dialogScope;
            _owner = request.Owner;
            _eventService = eventService;
            _commandRunner = runner;

            _runtimeService = ResolveRuntimeService(resolver);

            // Use Set semantics when showing: replace current subscriptions.
            // Per-request override can replace the channel's configured bindings.
            var baseBindings = request.SubscribeBindingsOverride ?? _bindings;
            SetSubscribeBindings(baseBindings);

            // Per-request additional bindings.
            if (request.AdditionalSubscribeBindings != null && request.AdditionalSubscribeBindings.Length > 0)
                AddSubscribeBindings(request.AdditionalSubscribeBindings);

            if (_def.PushToModalStackOnShow &&
                _modalStackService != null &&
                resolver.TryResolve<IUIModalRoot>(out var modalRoot) &&
                modalRoot != null)
            {
                _modalRoot = modalRoot;
                _modalStackService.PushModal(modalRoot, _def.ModalOptions);
            }

            if (_def.AutoCloseOnModalStackChange && _modalTelemetry != null)
            {
                _modalTelemetry.OnModalStackChanged += HandleModalStackChanged;
                _modalStackSubscribed = true;
            }

            if (_runtimeService != null && _dialogScope != null)
            {
                var runtimeContext = new UIDialogRuntimeContext(
                    dialogScope: _dialogScope,
                    owner: request.Owner,
                    channelKey: _channelKey,
                    options: _def.RuntimeOptions);

                _runtimeService.OnShow(in runtimeContext);
            }
        }

        bool TryBuildSpawnParams(UIDialogRequest request, out SpawnParams spawnParams)
        {
            spawnParams = SpawnParams.Default;
            spawnParams.Position = Vector3.zero;
            spawnParams.Rotation = Quaternion.identity;
            spawnParams.Scale = Vector3.one;
            spawnParams.WorldSpace = false;
            spawnParams.AllowPooling = false;

            Transform? transformParentOverride = null;

            if (_def.SpawnSource == DialogSpawnSource.RuntimeTemplate)
            {
                if (_def.DialogRuntimeTemplate == null)
                    return false;

                spawnParams.Template = _def.DialogRuntimeTemplate;
                spawnParams.AllowPooling = _def.AllowPooling && _def.DialogRuntimeTemplate.UsePooling;
            }
            else
            {
                if (_def.DialogPrefab == null)
                    return false;

                spawnParams.Prefab = _def.DialogPrefab;

                if (request.PrefabTransformParentOverride != null)
                {
                    transformParentOverride = request.PrefabTransformParentOverride;
                }
                else if (_def.PrefabParentMode == DialogPrefabParentMode.OwnerTransform)
                {
                    transformParentOverride = request.Owner.Identity?.SelfTransform;
                }
            }

            spawnParams.TransformParent = transformParentOverride;
            spawnParams.LifetimeScopeParent = request.LifetimeScopeParentOverride ?? request.Owner;
            return true;
        }

        async UniTask<IObjectResolver?> SpawnDialogAsync(SpawnParams spawnParams, CancellationToken ct)
        {
            if (_spawnerRegistry == null)
                return null;

            if (spawnParams.Template != null)
            {
                var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                    _spawnerRegistry,
                    SpawnerKind.RuntimeUIElement,
                    tag: string.Empty,
                    allowTagFallback: true,
                    allowRuntimeUiFallback: true);

                if (!resolved.HasValue || resolved.Spawner == null)
                    return null;

                return await resolved.Spawner.SpawnAsync(spawnParams, ct);
            }

            if (spawnParams.Prefab != null)
            {
                var resolved = SceneSpawnerResolver.TryResolveAsyncSpawner(
                    _spawnerRegistry,
                    SpawnerKind.UIElement,
                    tag: string.Empty,
                    allowTagFallback: true,
                    allowRuntimeUiFallback: false);

                if (!resolved.HasValue || resolved.Spawner == null)
                    return null;

                return await resolved.Spawner.SpawnAsync(spawnParams, ct);
            }

            return null;
        }

        static async UniTask ReleaseSpawnedInstanceAsync(
            Transform? root,
            IScopeNode? scope,
            IObjectResolver? resolver)
        {
            if (resolver == null)
                return;

            await UniTask.SwitchToMainThread();

            try
            {
                if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                {
                    if (runtimeScope.Resolver != null &&
                        runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                        pool != null)
                    {
                        pool.Release(runtimeScope);
                        return;
                    }

                    if (root != null)
                        UnityEngine.Object.Destroy(root.gameObject);
                    else
                        UnityEngine.Object.Destroy(runtimeScope.gameObject);
                    return;
                }

                if (scope is BaseLifetimeScope baseScope)
                {
                    await baseScope.DespawnAsync(CancellationToken.None);
                    return;
                }

                if (root != null)
                    UnityEngine.Object.Destroy(root.gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogChannelRuntime] Release failed: {ex.Message}");
            }
        }

        static void ExtractSpawnedInfo(
            IObjectResolver? resolver,
            out Transform? root,
            out IScopeNode? scopeNode,
            out RuntimeLifetimeScope? runtimeScope,
            out BaseLifetimeScope? baseScope)
        {
            root = null;
            scopeNode = null;
            runtimeScope = null;
            baseScope = null;

            if (resolver == null)
                return;

            resolver.TryResolve(out runtimeScope);

            if (runtimeScope != null)
                root = runtimeScope.transform;

            if (root == null)
            {
                if (resolver.TryResolve<Transform>(out var tr) && tr != null)
                    root = tr;
                else if (resolver.TryResolve<GameObject>(out var go) && go != null)
                    root = go.transform;
            }

            scopeNode = runtimeScope;
            if (scopeNode == null && resolver.TryResolve<IScopeNode>(out var resolved) && resolved != null)
                scopeNode = resolved;

            if (scopeNode == null && root != null)
            {
                var comps = root.GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] is IScopeNode node)
                    {
                        scopeNode = node;
                        break;
                    }
                }
            }

            baseScope = scopeNode as BaseLifetimeScope;
        }

        static bool TryResolveEventService(IObjectResolver resolver, out IEventService eventService)
        {
            eventService = null!;

            if (resolver.TryResolve<IUIElementEventService>(out var uiElement) && uiElement != null)
            {
                eventService = uiElement;
                return true;
            }

            if (resolver.TryResolve<IUIEventService>(out var ui) && ui != null)
            {
                eventService = ui;
                return true;
            }

            if (resolver.TryResolve<IEventService>(out var any) && any != null)
            {
                eventService = any;
                return true;
            }

            return false;
        }

        static VNext.ICommandRunner? ResolveCommandRunner(UIDialogRequest request, IObjectResolver resolver)
        {
            if (resolver.TryResolve<VNext.ICommandRunner>(out var runner) && runner != null)
            {
                return runner;
            }

            var ownerResolver = request.Owner.Resolver;
            if (ownerResolver != null && ownerResolver.TryResolve<VNext.ICommandRunner>(out var ownerRunner) && ownerRunner != null)
            {
                return ownerRunner;
            }

            return null;
        }

        static IUIDialogRuntimeService? ResolveRuntimeService(IObjectResolver resolver)
        {
            if (resolver.TryResolve<IUIDialogRuntimeService>(out var svc) && svc != null)
            {
                return svc;
            }

            return null;
        }

        void ClearSubscribeBindings()
        {
            for (int i = 0; i < _eventSubscriptions.Count; i++)
            {
                try { _eventSubscriptions[i]?.Dispose(); } catch { }
            }

            _eventSubscriptions.Clear();
            _processingEventKeys.Clear();
        }

        // duplicate overloads removed (kept a single pair above)

        void SubscribeBindingsCore(IEventService eventService, DialogEventBinding[]? bindings)
        {
            if (bindings == null || bindings.Length == 0)
                return;

            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (string.IsNullOrWhiteSpace(b.EventKey))
                    continue;

                var eventKey = b.EventKey;
                var commands = b.Commands;
                var closeAfterInvoke = b.CloseAfterInvoke;

                var sub = eventService.Subscribe(eventKey, (payload, _) =>
                    HandleEventAsync(eventKey, commands, closeAfterInvoke, payload));

                if (sub != null)
                    _eventSubscriptions.Add(sub);
            }
        }

        void SubscribeBindingsCore(IEventService eventService, DialogEventBinding binding)
        {
            if (string.IsNullOrWhiteSpace(binding.EventKey))
                return;

            var eventKey = binding.EventKey;
            var commands = binding.Commands;
            var closeAfterInvoke = binding.CloseAfterInvoke;

            var sub = eventService.Subscribe(eventKey, (payload, _) =>
                HandleEventAsync(eventKey, commands, closeAfterInvoke, payload));

            if (sub != null)
                _eventSubscriptions.Add(sub);
        }

        async UniTask HandleEventAsync(string eventKey, VNext.CommandListData commands, bool closeAfterInvoke, IVarStore? payload)
        {
            if (_lifetimeCts == null || _lifetimeCts.IsCancellationRequested)
                return;

            if (_dialogScope == null || _owner == null)
                return;

            if (string.IsNullOrWhiteSpace(eventKey))
                return;

            if (!_processingEventKeys.Add(eventKey))
                return;

            try
            {
                if (commands == null || commands.Count == 0)
                {
                    if (closeAfterInvoke)
                        Hide(DialogCloseReason.ActionInvoked);
                    return;
                }

                var runner = _commandRunner;
                if (runner == null)
                {
                    if (closeAfterInvoke)
                        Hide(DialogCloseReason.ActionInvoked);
                    return;
                }
                var lifetimeToken = _lifetimeCts.Token;

                var varsCopy = new VarStore();

                // Merge dialog-scope initial variables (blackboard) first.
                // Payload overrides (same as typical event semantics).
                try
                {
                    var dialogResolver = _dialogScope?.Resolver;
                    if (dialogResolver != null && dialogResolver.TryResolve<IBlackboardService>(out var bb) && bb != null)
                        bb.MergeInto(varsCopy, overwrite: true);
                }
                catch
                {
                }

                if (payload != null)
                    payload.MergeInto(varsCopy, overwrite: true);

                void SetRef(string stableKey, object value)
                {
                    if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                        varsCopy.TrySetManagedRef(varId, value);
                }

                void SetString(string stableKey, string value)
                {
                    if (VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0)
                        varsCopy.TrySetVariant(varId, DynamicVariant.FromString(value));
                }

                if (_dialogScope != null)
                    SetRef(UIDialogVarKeys.DialogScope, _dialogScope);
                if (_owner != null)
                    SetRef(UIDialogVarKeys.DialogOwner, _owner);
                SetString(UIDialogVarKeys.DialogChannelKey, _channelKey);

                var ctx = new VNext.CommandContext(runner.Scope, varsCopy, runner, actor: _dialogScope, options: VNext.CommandRunOptions.Default);

                try
                {
                    await runner.ExecuteListAsync(commands, ctx, lifetimeToken, ctx.Options);
                }
                catch (OperationCanceledException)
                {
                }

                if (closeAfterInvoke)
                    Hide(DialogCloseReason.ActionInvoked);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _processingEventKeys.Remove(eventKey);
            }
        }

        void HandleModalStackChanged(UIModalStackChangeContext context)
        {
            if (!IsVisible)
                return;

            Hide(DialogCloseReason.ModalStackChanged);
        }

        public void Hide(DialogCloseReason reason = DialogCloseReason.Explicit)
        {
            CancelLifetime();

            if (_modalStackSubscribed && _modalTelemetry != null)
            {
                _modalTelemetry.OnModalStackChanged -= HandleModalStackChanged;
                _modalStackSubscribed = false;
            }

            for (int i = 0; i < _eventSubscriptions.Count; i++)
            {
                try { _eventSubscriptions[i]?.Dispose(); } catch { }
            }
            _eventSubscriptions.Clear();
            _processingEventKeys.Clear();
            _additionalBindings.Clear();

            if (_runtimeService != null && _dialogScope != null && _owner != null)
            {
                var runtimeContext = new UIDialogRuntimeContext(
                    dialogScope: _dialogScope,
                    owner: _owner,
                    channelKey: _channelKey,
                    options: _def.RuntimeOptions);

                try
                {
                    _runtimeService.OnHide(in runtimeContext, reason);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (_modalRoot != null && _modalStackService != null)
            {
                try { _modalStackService.PopModal(_modalRoot); } catch { }
            }
            _modalRoot = null;

            var resolver = _resolver;
            var root = _root;
            var scope = _dialogScope;

            _resolver = null;
            _root = null;

            if (resolver != null)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await ReleaseSpawnedInstanceAsync(root, scope, resolver);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[DialogChannelRuntime] Release failed: {ex.Message}");
                    }
                });
            }

            _dialogScope = null;
            _owner = null;
            _eventService = null;
            _commandRunner = null;
            _runtimeService = null;
        }

        void CancelLifetime()
        {
            var cts = _lifetimeCts;
            if (cts == null)
                return;

            _lifetimeCts = null;
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        public void Dispose()
        {
            Hide(DialogCloseReason.Explicit);
        }
    }
}
