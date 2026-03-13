#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Collision
{
    public sealed class HitColliderControllerService : IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
    {
        const int SelfResolveRetryFrames = 60;

        readonly HitColliderControllerMB _mb;
        readonly IScopeNode _ownerScope;
        readonly IHitColliderChannelHub _hub;
        readonly IHitColliderScopeRegistry _scopeRegistry;
        readonly VNext.ICommandRunner? _runner;
        IUnityCollisionManager? _unityManager;
        bool _loggedMissingUnityManager;
        bool _attemptedSelfRepairWithUnityService;

        readonly List<RuntimeBinding> _bindings = new(8);
        readonly HashSet<VNext.CommandListData> _runtimeMutatedCommandLists = new();
        CancellationTokenSource? _cts;
        CancellationTokenSource? _selfHandleWatchCts;
        DynamicColliderHandle _boundSelfHandle;

        sealed class RuntimeBinding
        {
            public string RuleName = string.Empty;
            public HitColliderChannelRuntime Runtime = null!;
            public Action<HitContactEvent> OnEnter = null!;
            public Action<HitContactEvent> OnStay = null!;
            public Action<HitContactEvent> OnExit = null!;
            public Dictionary<StayContactKey, float>? StayLastExecutionTimeByContact;
            public Dictionary<EnterExitContactKey, float>? EnterExitLastExecutionTimeByContact;
        }

        readonly List<DynamicColliderHandle> _dynamicContactsScratch = new(16);

        readonly struct StayContactKey : IEquatable<StayContactKey>
        {
            readonly DynamicColliderHandle _dynamic;
            readonly StaticColliderHandle _static;
            readonly bool _isDynamic;

            StayContactKey(DynamicColliderHandle dynamicHandle)
            {
                _dynamic = dynamicHandle;
                _static = default;
                _isDynamic = true;
            }

            StayContactKey(StaticColliderHandle staticHandle)
            {
                _dynamic = default;
                _static = staticHandle;
                _isDynamic = false;
            }

            public static bool TryCreate(in HitContact contact, out StayContactKey key)
            {
                if (contact.IsDynamic)
                {
                    key = new StayContactKey(contact.Dynamic);
                    return true;
                }

                if (contact.IsStatic)
                {
                    key = new StayContactKey(contact.Static);
                    return true;
                }

                key = default;
                return false;
            }

            public bool Equals(StayContactKey other)
            {
                if (_isDynamic != other._isDynamic)
                    return false;

                return _isDynamic
                    ? _dynamic.Equals(other._dynamic)
                    : _static.Equals(other._static);
            }

            public override bool Equals(object? obj) => obj is StayContactKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var baseHash = _isDynamic ? 1 : 2;
                    var payloadHash = _isDynamic ? _dynamic.GetHashCode() : _static.GetHashCode();
                    return (baseHash * 397) ^ payloadHash;
                }
            }
        }

        readonly struct EnterExitContactKey : IEquatable<EnterExitContactKey>
        {
            readonly DynamicColliderHandle _self;
            readonly DynamicColliderHandle _otherDynamic;
            readonly StaticColliderHandle _otherStatic;
            readonly HitEventType _eventType;
            readonly byte _kind;

            EnterExitContactKey(DynamicColliderHandle self, DynamicColliderHandle otherDynamic, HitEventType eventType)
            {
                _self = self;
                _otherDynamic = otherDynamic;
                _otherStatic = default;
                _eventType = eventType;
                _kind = 1;
            }

            EnterExitContactKey(DynamicColliderHandle self, StaticColliderHandle otherStatic, HitEventType eventType)
            {
                _self = self;
                _otherDynamic = default;
                _otherStatic = otherStatic;
                _eventType = eventType;
                _kind = 2;
            }

            public static bool TryCreate(in HitContactEvent evt, HitEventType eventType, out EnterExitContactKey key)
            {
                var self = evt.RoutedHit.Hit.Self;
                if (!self.IsValid)
                {
                    key = default;
                    return false;
                }

                var otherDynamic = evt.RoutedHit.Hit.OtherDynamic;
                if (otherDynamic.IsValid)
                {
                    key = new EnterExitContactKey(self, otherDynamic, eventType);
                    return true;
                }

                var otherStatic = evt.RoutedHit.Hit.OtherStatic;
                if (otherStatic.IsValid)
                {
                    key = new EnterExitContactKey(self, otherStatic, eventType);
                    return true;
                }

                key = default;
                return false;
            }

            public bool Equals(EnterExitContactKey other)
            {
                if (_kind != other._kind || _eventType != other._eventType)
                    return false;
                if (!_self.Equals(other._self))
                    return false;

                return _kind switch
                {
                    1 => _otherDynamic.Equals(other._otherDynamic),
                    2 => _otherStatic.Equals(other._otherStatic),
                    _ => false,
                };
            }

            public override bool Equals(object? obj) => obj is EnterExitContactKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = ((int)_kind * 397) ^ (int)_eventType;
                    hash = (hash * 397) ^ _self.GetHashCode();
                    hash = (hash * 397) ^ (_kind == 1 ? _otherDynamic.GetHashCode() : _otherStatic.GetHashCode());
                    return hash;
                }
            }
        }

        public HitColliderControllerService(
            HitColliderControllerMB mb,
            IScopeNode ownerScope,
            IHitColliderChannelHub hub,
            IHitColliderScopeRegistry scopeRegistry,
            IObjectResolver resolver)
        {
            _mb = mb;
            _ownerScope = ownerScope;
            _hub = hub;
            _scopeRegistry = scopeRegistry;

            // Runner is optional: if not present, controller becomes a pure watcher.
            if (resolver.TryResolve<VNext.ICommandRunner>(out var runner))
                _runner = runner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _mb.ResetDebugRuntime();
            _attemptedSelfRepairWithUnityService = false;
            RebindRules();
            _selfHandleWatchCts?.Cancel();
            _selfHandleWatchCts?.Dispose();
            _selfHandleWatchCts = new CancellationTokenSource();
            WatchSelfHandleChangesAsync(_selfHandleWatchCts.Token).Forget();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _selfHandleWatchCts?.Cancel();
            _selfHandleWatchCts?.Dispose();
            _selfHandleWatchCts = null;
            ClearRuntimeMutatedCommandLists();
            CleanupBindings();
        }

        void HandleEvent(HitColliderControllerRule rule, RuntimeBinding binding, HitContactEvent evt, HitEventType eventType)
        {
            if (_ownerScope == null || _ownerScope.Resolver == null)
            {
                _mb.RecordDebugSkip(rule.Name, eventType, "OwnerScopeResolverMissing", evt.RoutedHit);
                return;
            }
            if (!rule.Enabled)
            {
                _mb.RecordDebugSkip(rule.Name, eventType, "RuleDisabled", evt.RoutedHit);
                return;
            }

            _mb.RecordDebugEvent(rule.Name, eventType, evt.RoutedHit);

            if (eventType == HitEventType.Stay && !CanRunStayByInterval(rule, binding, evt))
            {
                _mb.RecordDebugSkip(rule.Name, eventType, "StayIntervalThrottle", evt.RoutedHit);
                return;
            }

            if ((eventType == HitEventType.Enter || eventType == HitEventType.Exit) &&
                !CanRunEnterExitByInterval(rule, binding, evt, eventType))
            {
                _mb.RecordDebugSkip(rule.Name, eventType, "EnterExitIntervalThrottle", evt.RoutedHit);
                return;
            }

            if (eventType == HitEventType.Exit)
                ClearStayIntervalCounter(binding, evt);

            var vars = new VarStore(initialCapacity: 8);
            WriteVars(vars, evt.RoutedHit, eventType);

            var ct = _cts != null ? _cts.Token : CancellationToken.None;

            switch (rule.CommandTarget)
            {
                case HitColliderCommandTarget.Self:
                    _mb.RecordDebugExecuted(ExecuteOnSelf(GetCommandList(rule, eventType), vars, ct, rule.Name, evt.RoutedHit), false);
                    break;
                case HitColliderCommandTarget.Other:
                    _mb.RecordDebugExecuted(false, ExecuteOnOther(GetCommandList(rule, eventType), vars, evt.RoutedHit, ct, rule.Name, eventType));
                    break;
                case HitColliderCommandTarget.Both:
                    var selfList = GetCommandList(rule, eventType, forSelfWhenBoth: true);
                    var otherList = GetCommandList(rule, eventType, forSelfWhenBoth: false);
                    if (rule.ParallelWhenBoth)
                    {
                        var selfExecuted = ExecuteOnSelf(selfList, vars, ct, rule.Name, evt.RoutedHit);
                        var otherExecuted = ExecuteOnOther(otherList, vars, evt.RoutedHit, ct, rule.Name, eventType);
                        _mb.RecordDebugExecuted(selfExecuted, otherExecuted);
                    }
                    else
                    {
                        UniTask.Void(async () =>
                        {
                            var selfExecuted = await ExecuteOnSelfAsync(selfList, vars, ct, rule.Name, evt.RoutedHit, eventType);
                            var otherExecuted = await ExecuteOnOtherAsync(otherList, vars, evt.RoutedHit, ct, rule.Name, eventType);
                            _mb.RecordDebugExecuted(selfExecuted, otherExecuted);
                        });
                    }
                    break;
            }
        }

        static bool CanRunStayByInterval(HitColliderControllerRule rule, RuntimeBinding binding, in HitContactEvent evt)
        {
            var intervalSeconds = rule.StayIntervalSeconds;
            if (intervalSeconds <= 0f)
                return true;

            if (!StayContactKey.TryCreate(in evt.Contact, out var key))
                return true;

            binding.StayLastExecutionTimeByContact ??= new Dictionary<StayContactKey, float>(8);
            var dict = binding.StayLastExecutionTimeByContact;
            var now = Time.time;
            if (dict.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < intervalSeconds)
                    return false;
            }

            dict[key] = now;
            return true;
        }

        static void ClearStayIntervalCounter(RuntimeBinding binding, in HitContactEvent evt)
        {
            var dict = binding.StayLastExecutionTimeByContact;
            if (dict == null || dict.Count == 0)
                return;

            if (!StayContactKey.TryCreate(in evt.Contact, out var key))
                return;

            dict.Remove(key);
        }

        static bool CanRunEnterExitByInterval(
            HitColliderControllerRule rule,
            RuntimeBinding binding,
            in HitContactEvent evt,
            HitEventType eventType)
        {
            var intervalSeconds = rule.EnterExitDuplicateIntervalSeconds;
            if (intervalSeconds <= 0f)
                return true;

            if (!EnterExitContactKey.TryCreate(in evt, eventType, out var key))
                return true;

            binding.EnterExitLastExecutionTimeByContact ??= new Dictionary<EnterExitContactKey, float>(8);
            var dict = binding.EnterExitLastExecutionTimeByContact;
            var now = Time.time;
            if (dict.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < intervalSeconds)
                    return false;
            }

            dict[key] = now;
            return true;
        }

        static VNext.CommandListData? GetCommandList(HitColliderControllerRule rule, HitEventType eventType, bool forSelfWhenBoth = true)
        {
            if (rule.CommandTarget == HitColliderCommandTarget.Both)
            {
                return eventType switch
                {
                    HitEventType.Enter => forSelfWhenBoth ? rule.OnEnterCommandsSelf : rule.OnEnterCommandsOther,
                    HitEventType.Stay => forSelfWhenBoth ? rule.OnStayCommandsSelf : rule.OnStayCommandsOther,
                    HitEventType.Exit => forSelfWhenBoth ? rule.OnExitCommandsSelf : rule.OnExitCommandsOther,
                    _ => null,
                };
            }

            return eventType switch
            {
                HitEventType.Enter => rule.OnEnterCommands,
                HitEventType.Stay => rule.OnStayCommands,
                HitEventType.Exit => rule.OnExitCommands,
                _ => null,
            };
        }

        void WriteVars(IVarStore vars, in RoutedHit rh, HitEventType eventType)
        {
            vars.TrySetManagedRef(HitColliderChannelVarIds.Hit, rh.Hit);
            vars.TrySetManagedRef(HitColliderChannelVarIds.HitMeta, rh.Meta);
            vars.TrySetVariant(HitColliderChannelVarIds.IsOtherSide, DynamicVariant.FromBool(rh.IsOtherSide));
            vars.TrySetVariant(HitColliderChannelVarIds.HitEvent, DynamicVariant.FromInt((int)eventType));

            vars.TrySetManagedRef(HitColliderChannelVarIds.SelfScope, _ownerScope);

            if (rh.Hit.OtherDynamic.IsValid && _scopeRegistry.TryResolve(rh.Hit.OtherDynamic, out var other) && other != null)
                vars.TrySetManagedRef(HitColliderChannelVarIds.OtherScope, other);
            else
                vars.TryUnset(HitColliderChannelVarIds.OtherScope);
        }

        async UniTask ExecuteAsyncTask(
            VNext.CommandListData list,
            VNext.CommandContext ctx,
            CancellationToken ct,
            string ruleName)
        {
            try
            {
                var result = await ctx.Runner.ExecuteListAsync(list, ctx, ct, ctx.Options);
                if (result.Status == VNext.CommandRunStatus.Error)
                {
                    // Stayで連打され得るので、ここは最小限の情報だけ出す。
                    Debug.LogError($"[HitColliderController] Command failed (rule='{ruleName}'): {result.Message}");
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HitColliderController] Command execution failed (rule='{ruleName}'): {ex.Message}");
            }
        }

        bool ExecuteOnSelf(VNext.CommandListData? list, IVarStore vars, CancellationToken ct, string ruleName, in RoutedHit routedHit)
        {
            if (list == null || list.Count == 0)
            {
                _mb.RecordDebugSkip(ruleName, routedHit.Event, "SelfCommandListEmpty", routedHit);
                return false;
            }

            var runner = _runner;
            if (runner == null)
            {
                _mb.RecordDebugSkip(ruleName, routedHit.Event, "SelfRunnerMissing", routedHit);
                return false;
            }

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_ownerScope, vars, runner, actor: _ownerScope, options);
            ExecuteAsyncTask(list, ctx, ct, ruleName).Forget();
            return true;
        }

        bool ExecuteOnOther(VNext.CommandListData? list, IVarStore vars, in RoutedHit rh, CancellationToken ct, string ruleName, HitEventType eventType)
        {
            if (list == null || list.Count == 0)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherCommandListEmpty", rh);
                return false;
            }

            if (!rh.Hit.OtherDynamic.IsValid)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherDynamicInvalid", rh);
                return false;
            }

            if (!_scopeRegistry.TryResolve(rh.Hit.OtherDynamic, out var otherScope) || otherScope?.Resolver == null)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherScopeNotResolved", rh);
                return false;
            }

            if (!otherScope.Resolver.TryResolve<VNext.ICommandRunner>(out var otherRunner) || otherRunner == null)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherRunnerMissing", rh);
                return false;
            }

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(
                otherScope,
                vars,
                otherRunner,
                actor: otherScope,
                options,
                commandRootScope: _ownerScope,
                rootActor: _ownerScope,
                callerActor: _ownerScope);
            ExecuteAsyncTask(list, ctx, ct, ruleName).Forget();
            return true;
        }

        UniTask<bool> ExecuteOnSelfAsync(VNext.CommandListData? list, IVarStore vars, CancellationToken ct, string ruleName, in RoutedHit routedHit, HitEventType eventType)
        {
            if (list == null || list.Count == 0)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "SelfCommandListEmpty", routedHit);
                return UniTask.FromResult(false);
            }

            var runner = _runner;
            if (runner == null)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "SelfRunnerMissing", routedHit);
                return UniTask.FromResult(false);
            }

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_ownerScope, vars, runner, actor: _ownerScope, options);
            return ExecuteOnSelfAsyncCore(list, ctx, ct, ruleName);
        }

        UniTask<bool> ExecuteOnSelfAsyncCore(
            VNext.CommandListData list,
            VNext.CommandContext ctx,
            CancellationToken ct,
            string ruleName)
        {
            return ExecuteOnSelfAsyncCoreInternal(list, ctx, ct, ruleName);
        }

        async UniTask<bool> ExecuteOnSelfAsyncCoreInternal(
            VNext.CommandListData list,
            VNext.CommandContext ctx,
            CancellationToken ct,
            string ruleName)
        {
            await ExecuteAsyncTask(list, ctx, ct, ruleName);
            return true;
        }

        UniTask<bool> ExecuteOnOtherAsync(VNext.CommandListData? list, IVarStore vars, in RoutedHit rh, CancellationToken ct, string ruleName, HitEventType eventType)
        {
            if (list == null || list.Count == 0)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherCommandListEmpty", rh);
                return UniTask.FromResult(false);
            }

            if (!rh.Hit.OtherDynamic.IsValid)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherDynamicInvalid", rh);
                return UniTask.FromResult(false);
            }

            if (!_scopeRegistry.TryResolve(rh.Hit.OtherDynamic, out var otherScope) || otherScope?.Resolver == null)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherScopeNotResolved", rh);
                return UniTask.FromResult(false);
            }

            if (!otherScope.Resolver.TryResolve<VNext.ICommandRunner>(out var otherRunner) || otherRunner == null)
            {
                _mb.RecordDebugSkip(ruleName, eventType, "OtherRunnerMissing", rh);
                return UniTask.FromResult(false);
            }

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(
                otherScope,
                vars,
                otherRunner,
                actor: otherScope,
                options,
                commandRootScope: _ownerScope,
                rootActor: _ownerScope,
                callerActor: _ownerScope);
            return ExecuteOnSelfAsyncCore(list, ctx, ct, ruleName);
        }

        void CleanupBindings()
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
                _cts.Dispose();
                _cts = null;
            }

            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                if (b == null)
                    continue;

                var rt = b.Runtime;
                if (rt != null)
                {
                    if (b.OnEnter != null) rt.Enter -= b.OnEnter;
                    if (b.OnStay != null) rt.Stay -= b.OnStay;
                    if (b.OnExit != null) rt.Exit -= b.OnExit;
                }
            }
            _bindings.Clear();
            _boundSelfHandle = default;
        }

        public void RebindRules()
        {
            CleanupBindings();
            _mb.SetDebugBindingState("Rebinding");

            var rules = _mb.Rules;
            if (rules == null || rules.Count == 0)
            {
                _mb.SetDebugBindingState("NoRules");
                return;
            }

            _cts = new CancellationTokenSource();

            // Self handle registration (UnityColliderObjectService / ColliderObjectService) can occur
            // in the same scope acquire, but handler order is not guaranteed. Retry a few frames.
            BindWhenSelfReadyAsync(rules, _cts.Token).Forget();
        }

        async UniTaskVoid BindWhenSelfReadyAsync(IReadOnlyList<HitColliderControllerRule> rules, CancellationToken ct)
        {
            for (int attempt = 0; attempt <= SelfResolveRetryFrames; attempt++)
            {
                if (ct.IsCancellationRequested)
                    return;

                if (TryResolveSelfHandle(out var self) && self.IsValid)
                {
                    BindRules(self, rules);
                    return;
                }

                if (attempt == SelfResolveRetryFrames)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (!ct.IsCancellationRequested)
            {
                _mb.SetDebugBindingState(default, 0, "SelfHandleResolveFailed");
                if (HasAnySelfHandleProvider())
                {
                    Debug.LogWarning($"[HitColliderControllerService] Failed to resolve self collider handle after {SelfResolveRetryFrames} frames. scope='{_ownerScope?.Identity?.Id ?? "(unknown)"}' object='{_mb.gameObject.name}'");
                }
            }
        }

        bool TryResolveSelfHandle(out DynamicColliderHandle handle)
        {
            handle = default;

            if (_mb.TryGetSelfHandle(out handle) && handle.IsValid)
                return true;

            var resolver = _ownerScope?.Resolver;
            if (resolver != null)
            {
                if (resolver.TryResolve<UnityColliderObjectService>(out var unityService) &&
                    unityService != null)
                {
                    if (unityService.DynamicHandle.IsValid)
                    {
                        handle = unityService.DynamicHandle;
                        return true;
                    }

                    if (!_attemptedSelfRepairWithUnityService && TryShouldRepairUnityColliderRegistration())
                    {
                        _attemptedSelfRepairWithUnityService = true;
                        unityService.SetEnabled(true);

                        if (unityService.DynamicHandle.IsValid)
                        {
                            handle = unityService.DynamicHandle;
                            return true;
                        }
                    }
                }

                if (resolver.TryResolve<ColliderObjectService>(out var colliderObjectService) &&
                    colliderObjectService != null &&
                    colliderObjectService.DynamicHandle.IsValid)
                {
                    handle = colliderObjectService.DynamicHandle;
                    return true;
                }
            }

            if (TryGetSelfHandleFromUnityCollider(out handle) && handle.IsValid)
                return true;

            return false;
        }

        bool TryShouldRepairUnityColliderRegistration()
        {
            var collider = ResolveSelfCollider2D();
            if (collider == null)
                return false;

            return collider.enabled;
        }

        bool HasAnySelfHandleProvider()
        {
            var provider = _mb.SelfProvider;
            if (provider != null)
                return provider is ColliderObjectMB || provider is UnityColliderObjectMB || provider is Collider2D;

            if (_mb.TryGetComponent<ColliderObjectMB>(out var colliderObject) && colliderObject != null)
                return true;

            if (_mb.TryGetComponent<UnityColliderObjectMB>(out var unityColliderObject) && unityColliderObject != null)
                return true;

            return _mb.TryGetComponent<Collider2D>(out var collider) && collider != null;
        }

        bool TryGetSelfHandleFromUnityCollider(out DynamicColliderHandle handle)
        {
            handle = default;

            if (!TryEnsureUnityManager())
                return false;

            var col = ResolveSelfCollider2D();
            if (col == null)
                return false;

            if (_unityManager!.TryGetDynamicHandle(col, out var h) && h.IsValid)
            {
                handle = h;
                return true;
            }

            return false;
        }

        Collider2D? ResolveSelfCollider2D()
        {
            if (_mb.SelfProvider is Collider2D directCollider)
                return directCollider;

            if (_mb.SelfProvider is UnityColliderObjectMB unityProvider)
                return unityProvider.Collider;

            if (_mb.TryGetComponent<UnityColliderObjectMB>(out var unityColliderObject) && unityColliderObject != null)
                return unityColliderObject.Collider;

            return _mb.GetComponent<Collider2D>();
        }

        bool TryEnsureUnityManager()
        {
            if (_unityManager != null)
                return true;

            var resolver = _ownerScope?.Resolver;
            if (resolver == null)
                return false;

            if (resolver.TryResolve(typeof(IUnityCollisionManager), out var managerObj) &&
                managerObj is IUnityCollisionManager manager)
            {
                _unityManager = manager;
                return true;
            }

            if (!_loggedMissingUnityManager)
            {
                _loggedMissingUnityManager = true;
                Game.LTSLog.LogWarning("[HitColliderControllerService] IUnityCollisionManager is not registered. UnityColliderObjectMB may be missing.", _mb);
            }

            return false;
        }

        void BindRules(DynamicColliderHandle self, IReadOnlyList<HitColliderControllerRule> rules)
        {
            var boundCount = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || !rule.Enabled)
                    continue;
                if (!rule.HasAnyCommands())
                    continue;

                var spec = rule.ToSpec();
                var runtime = _hub.GetOrCreate(self, spec);
                //Debug.Log($"[HitColliderController] Bound rule '{rule.Name}' to collider handle {self.Id}.");

                var binding = new RuntimeBinding
                {
                    RuleName = string.IsNullOrWhiteSpace(rule.Name) ? "default" : rule.Name,
                    Runtime = runtime,
                };
                binding.OnEnter = evt => HandleEvent(rule, binding, evt, HitEventType.Enter);
                binding.OnStay = evt => HandleEvent(rule, binding, evt, HitEventType.Stay);
                binding.OnExit = evt => HandleEvent(rule, binding, evt, HitEventType.Exit);

                runtime.Enter += binding.OnEnter;
                runtime.Stay += binding.OnStay;
                runtime.Exit += binding.OnExit;

                _bindings.Add(binding);
                boundCount++;
            }

            _boundSelfHandle = self;
            _mb.SetDebugBindingState(self, boundCount, boundCount > 0 ? "Bound" : "NoEnabledRulesWithCommands");
        }

        public bool TryGetCurrentHitTargetScopes(string? ruleName, List<IScopeNode> targets)
        {
            if (targets == null)
                return false;

            targets.Clear();
            var normalized = string.IsNullOrWhiteSpace(ruleName) ? "default" : ruleName!.Trim();

            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding == null || binding.Runtime == null)
                    continue;

                if (!string.Equals(binding.RuleName, normalized, StringComparison.Ordinal))
                    continue;

                _dynamicContactsScratch.Clear();
                binding.Runtime.FillDynamicContacts(_dynamicContactsScratch);

                for (int c = 0; c < _dynamicContactsScratch.Count; c++)
                {
                    var handle = _dynamicContactsScratch[c];
                    if (!handle.IsValid)
                        continue;

                    if (!_scopeRegistry.TryResolve(handle, out var targetScope) || targetScope == null)
                        continue;

                    var exists = false;
                    for (int t = 0; t < targets.Count; t++)
                    {
                        if (ReferenceEquals(targets[t], targetScope))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                        targets.Add(targetScope);
                }
            }

            return targets.Count > 0;
        }

        async UniTaskVoid WatchSelfHandleChangesAsync(CancellationToken ct)
        {
            // Root cause memo:
            // UnityColliderObjectService can unregister/register the collider during runtime lifecycle,
            // which may change DynamicColliderHandle.Id while this controller is already bound.
            // If we keep watching the old handle, router events never reach this rule runtime and
            // Enter/Stay/Exit remain 0 even though collisions are occurring.
            // To prevent this, monitor current self handle and force rebind on handle change.
            while (!ct.IsCancellationRequested)
            {
                var hasCurrent =
                    (TryResolveSelfHandle(out var current) && current.IsValid);

                if (hasCurrent && current.IsValid && !SameHandle(_boundSelfHandle, current))
                {
                    _mb.SetDebugBindingState(current, _bindings.Count, "RebindingByHandleChange");
                    RebindRules();
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        static bool SameHandle(DynamicColliderHandle a, DynamicColliderHandle b)
        {
            return a.IdPlusOne == b.IdPlusOne && a.Generation == b.Generation;
        }

        public void Dispose()
        {
            _selfHandleWatchCts?.Cancel();
            _selfHandleWatchCts?.Dispose();
            _selfHandleWatchCts = null;
            ClearRuntimeMutatedCommandLists();
            CleanupBindings();
        }

        public void RegisterRuntimeMutatedCommandList(VNext.CommandListData? list)
        {
            if (list == null)
                return;

            _runtimeMutatedCommandLists.Add(list);
        }

        void ClearRuntimeMutatedCommandLists()
        {
            if (_runtimeMutatedCommandLists.Count == 0)
                return;

            foreach (var list in _runtimeMutatedCommandLists)
            {
                list?.ClearRuntimeMutations();
            }
            _runtimeMutatedCommandLists.Clear();
        }
    }
}
