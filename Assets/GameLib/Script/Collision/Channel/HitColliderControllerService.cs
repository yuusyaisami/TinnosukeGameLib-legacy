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
        readonly HashSet<string> _warnedInvalidContextSlotRules = new(StringComparer.Ordinal);
        CancellationTokenSource? _cts;
        CancellationTokenSource? _selfHandleWatchCts;
        readonly List<DynamicColliderHandle> _boundSelfHandles = new(8);
        readonly List<DynamicColliderHandle> _selfHandlesScratch = new(8);
        readonly List<Collider2D> _selfCollidersScratch = new(8);
        readonly List<string> _selfColliderTagsScratch = new(8);
        readonly HashSet<FrameDedupKey> _frameDedup = new();
        int _frameDedupFrameIndex = -1;

        sealed class RuntimeBinding
        {
            public string RuleName = string.Empty;
            public int RuleIndex;
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

        readonly struct FrameDedupKey : IEquatable<FrameDedupKey>
        {
            readonly int _ruleIndex;
            readonly HitEventType _eventType;
            readonly bool _isOtherSide;
            readonly DynamicColliderHandle _otherDynamic;
            readonly StaticColliderHandle _otherStatic;
            readonly byte _kind;

            FrameDedupKey(int ruleIndex, HitEventType eventType, bool isOtherSide, DynamicColliderHandle otherDynamic)
            {
                _ruleIndex = ruleIndex;
                _eventType = eventType;
                _isOtherSide = isOtherSide;
                _otherDynamic = otherDynamic;
                _otherStatic = default;
                _kind = 1;
            }

            FrameDedupKey(int ruleIndex, HitEventType eventType, bool isOtherSide, StaticColliderHandle otherStatic)
            {
                _ruleIndex = ruleIndex;
                _eventType = eventType;
                _isOtherSide = isOtherSide;
                _otherDynamic = default;
                _otherStatic = otherStatic;
                _kind = 2;
            }

            public static bool TryCreate(int ruleIndex, HitEventType eventType, in RoutedHit routedHit, out FrameDedupKey key)
            {
                if (routedHit.Hit.OtherDynamic.IsValid)
                {
                    key = new FrameDedupKey(ruleIndex, eventType, routedHit.IsOtherSide, routedHit.Hit.OtherDynamic);
                    return true;
                }

                if (routedHit.Hit.OtherStatic.IsValid)
                {
                    key = new FrameDedupKey(ruleIndex, eventType, routedHit.IsOtherSide, routedHit.Hit.OtherStatic);
                    return true;
                }

                key = default;
                return false;
            }

            public bool Equals(FrameDedupKey other)
            {
                if (_ruleIndex != other._ruleIndex ||
                    _eventType != other._eventType ||
                    _isOtherSide != other._isOtherSide ||
                    _kind != other._kind)
                    return false;

                return _kind switch
                {
                    1 => _otherDynamic.Equals(other._otherDynamic),
                    2 => _otherStatic.Equals(other._otherStatic),
                    _ => false,
                };
            }

            public override bool Equals(object? obj) => obj is FrameDedupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (_ruleIndex * 397) ^ (int)_eventType;
                    hash = (hash * 397) ^ (_isOtherSide ? 1 : 0);
                    hash = (hash * 397) ^ _kind;
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
            IRuntimeResolver resolver)
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
            _warnedInvalidContextSlotRules.Clear();
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

            if (ShouldSkipByFrameDedupe(binding.RuleIndex, eventType, evt.RoutedHit))
            {
                _mb.RecordDebugSkip(rule.Name, eventType, "FrameDedupe", evt.RoutedHit);
                return;
            }

            var vars = new VarStore(initialCapacity: 8);
            WriteVars(vars, evt.RoutedHit, eventType);

            var ct = _cts != null ? _cts.Token : CancellationToken.None;

            switch (rule.CommandTarget)
            {
                case HitColliderCommandTarget.Self:
                    _mb.RecordDebugExecuted(ExecuteOnSelf(rule, GetCommandList(rule, eventType), vars, ct, rule.Name, evt.RoutedHit), false);
                    break;
                case HitColliderCommandTarget.Other:
                    _mb.RecordDebugExecuted(false, ExecuteOnOther(rule, GetCommandList(rule, eventType), vars, evt.RoutedHit, ct, rule.Name, eventType));
                    break;
                case HitColliderCommandTarget.Both:
                    var selfList = GetCommandList(rule, eventType, forSelfWhenBoth: true);
                    var otherList = GetCommandList(rule, eventType, forSelfWhenBoth: false);
                    if (rule.ParallelWhenBoth)
                    {
                        var selfExecuted = ExecuteOnSelf(rule, selfList, vars, ct, rule.Name, evt.RoutedHit);
                        var otherExecuted = ExecuteOnOther(rule, otherList, vars, evt.RoutedHit, ct, rule.Name, eventType);
                        _mb.RecordDebugExecuted(selfExecuted, otherExecuted);
                    }
                    else
                    {
                        UniTask.Void(async () =>
                        {
                            var selfExecuted = await ExecuteOnSelfAsync(rule, selfList, vars, ct, rule.Name, evt.RoutedHit, eventType);
                            var otherExecuted = await ExecuteOnOtherAsync(rule, otherList, vars, evt.RoutedHit, ct, rule.Name, eventType);
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

        bool ShouldSkipByFrameDedupe(int ruleIndex, HitEventType eventType, in RoutedHit routedHit)
        {
            if (!FrameDedupKey.TryCreate(ruleIndex, eventType, routedHit, out var key))
                return false;

            var frameIndex = routedHit.Meta.FrameIndex;
            if (_frameDedupFrameIndex != frameIndex)
            {
                _frameDedupFrameIndex = frameIndex;
                _frameDedup.Clear();
            }

            return !_frameDedup.Add(key);
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
            vars.TrySetVariant(HitColliderChannelVarIds.SelfTag, DynamicVariant.FromString(ResolveColliderTag(rh.Hit.Self)));
            vars.TrySetVariant(HitColliderChannelVarIds.OtherTag, DynamicVariant.FromString(ResolveOtherColliderTag(rh)));

            vars.TrySetManagedRef(HitColliderChannelVarIds.SelfScope, _ownerScope);

            if (rh.Hit.OtherDynamic.IsValid && _scopeRegistry.TryResolve(rh.Hit.OtherDynamic, out var other) && other != null)
                vars.TrySetManagedRef(HitColliderChannelVarIds.OtherScope, other);
            else
                vars.TryUnset(HitColliderChannelVarIds.OtherScope);
        }

        string ResolveOtherColliderTag(in RoutedHit routedHit)
        {
            if (routedHit.Hit.OtherDynamic.IsValid)
                return ResolveColliderTag(routedHit.Hit.OtherDynamic);

            return UnityColliderObjectMB.DefaultColliderTag;
        }

        string ResolveColliderTag(DynamicColliderHandle handle)
        {
            if (!handle.IsValid)
                return UnityColliderObjectMB.DefaultColliderTag;

            if (!TryEnsureUnityManager())
                return UnityColliderObjectMB.DefaultColliderTag;

            if (_unityManager != null &&
                _unityManager.TryGetDynamicMetadata(handle, out var metadata) &&
                !string.IsNullOrWhiteSpace(metadata.ColliderTag))
            {
                return metadata.ColliderTag.Trim();
            }

            return UnityColliderObjectMB.DefaultColliderTag;
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
                    // Stayで連打され得るので、ここ�E最小限の惁E��だけ�Eす、E
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

        bool ExecuteOnSelf(HitColliderControllerRule rule, VNext.CommandListData? list, IVarStore vars, CancellationToken ct, string ruleName, in RoutedHit routedHit)
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
            ApplySelfContextSlots(ctx, rule, routedHit);
            ExecuteAsyncTask(list, ctx, ct, ruleName).Forget();
            return true;
        }

        bool ExecuteOnOther(HitColliderControllerRule rule, VNext.CommandListData? list, IVarStore vars, in RoutedHit rh, CancellationToken ct, string ruleName, HitEventType eventType)
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
            ApplyOtherContextSlots(ctx, rule, rh);
            ExecuteAsyncTask(list, ctx, ct, ruleName).Forget();
            return true;
        }

        UniTask<bool> ExecuteOnSelfAsync(HitColliderControllerRule rule, VNext.CommandListData? list, IVarStore vars, CancellationToken ct, string ruleName, in RoutedHit routedHit, HitEventType eventType)
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
            ApplySelfContextSlots(ctx, rule, routedHit);
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

        UniTask<bool> ExecuteOnOtherAsync(HitColliderControllerRule rule, VNext.CommandListData? list, IVarStore vars, in RoutedHit rh, CancellationToken ct, string ruleName, HitEventType eventType)
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
            ApplyOtherContextSlots(ctx, rule, rh);
            return ExecuteOnSelfAsyncCore(list, ctx, ct, ruleName);
        }

        void ApplySelfContextSlots(VNext.CommandContext ctx, HitColliderControllerRule rule, in RoutedHit routedHit)
        {
            var slot = ResolveCounterpartContextSlot(rule);
            if (TryResolveOtherScope(routedHit, out var otherScope))
                ctx.SetScope(slot, otherScope);
            else
                ctx.SetScope(slot, null);
        }

        void ApplyOtherContextSlots(VNext.CommandContext ctx, HitColliderControllerRule rule, in RoutedHit routedHit)
        {
            var slot = ResolveCounterpartContextSlot(rule);
            ctx.SetScope(slot, _ownerScope);
        }

        VNext.CommandLtsSlot ResolveCounterpartContextSlot(HitColliderControllerRule rule)
        {
            if (rule.HasInvalidCounterpartContextSlot())
            {
                var ruleName = string.IsNullOrWhiteSpace(rule.Name) ? "default" : rule.Name;
                if (_warnedInvalidContextSlotRules.Add(ruleName))
                    Debug.LogWarning($"[HitColliderControllerService] Counterpart Context Slot should use ContextA-D. Rule='{ruleName}' Slot={rule.CounterpartContextSlot}. Falling back to ContextA.", _mb);
            }
            return rule.GetEffectiveCounterpartContextSlot();
        }

        bool TryResolveOtherScope(in RoutedHit routedHit, out IScopeNode? otherScope)
        {
            otherScope = null;
            if (!routedHit.Hit.OtherDynamic.IsValid)
                return false;

            return _scopeRegistry.TryResolve(routedHit.Hit.OtherDynamic, out otherScope) && otherScope != null;
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
            _boundSelfHandles.Clear();
            _frameDedup.Clear();
            _frameDedupFrameIndex = -1;
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

                if (TryResolveSelfHandles(_selfHandlesScratch) && _selfHandlesScratch.Count > 0)
                {
                    BindRules(_selfHandlesScratch, rules);
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

        bool TryResolveSelfHandles(List<DynamicColliderHandle> handles)
        {
            handles.Clear();

            var resolver = _ownerScope?.Resolver;
            if (resolver != null)
            {
                if (resolver.TryResolve<UnityColliderObjectService>(out var unityService) && unityService != null)
                {
                    if (unityService.FillRegisteredHandles(handles) > 0)
                        return true;

                    if (!_attemptedSelfRepairWithUnityService && TryShouldRepairUnityColliderRegistration())
                    {
                        _attemptedSelfRepairWithUnityService = true;
                        unityService.SetEnabled(true);

                        if (unityService.FillRegisteredHandles(handles) > 0)
                            return true;
                    }

                    if (unityService.DynamicHandle.IsValid)
                        AddUniqueHandle(handles, unityService.DynamicHandle);
                }
            }

            if (_mb.TryGetSelfHandle(out var handle) && handle.IsValid)
                AddUniqueHandle(handles, handle);

            if (resolver != null)
            {
                if (resolver.TryResolve<ColliderObjectService>(out var colliderObjectService) &&
                    colliderObjectService != null &&
                    colliderObjectService.DynamicHandle.IsValid)
                {
                    AddUniqueHandle(handles, colliderObjectService.DynamicHandle);
                }
            }

            TryGetSelfHandlesFromUnityCollider(handles);

            return handles.Count > 0;
        }

        bool TryShouldRepairUnityColliderRegistration()
        {
            _selfCollidersScratch.Clear();
            FillSelfColliders(_selfCollidersScratch);
            for (var i = 0; i < _selfCollidersScratch.Count; i++)
            {
                var collider = _selfCollidersScratch[i];
                if (collider != null && collider.enabled)
                    return true;
            }

            return false;
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

        bool TryGetSelfHandlesFromUnityCollider(List<DynamicColliderHandle> handles)
        {
            if (!TryEnsureUnityManager())
                return false;

            _selfCollidersScratch.Clear();
            FillSelfColliders(_selfCollidersScratch);
            for (var i = 0; i < _selfCollidersScratch.Count; i++)
            {
                var collider = _selfCollidersScratch[i];
                if (collider == null)
                    continue;

                if (_unityManager!.TryGetDynamicHandle(collider, out var handle) && handle.IsValid)
                    AddUniqueHandle(handles, handle);
            }

            return handles.Count > 0;
        }

        void FillSelfColliders(List<Collider2D> colliders)
        {
            colliders.Clear();

            if (_mb.SelfProvider is Collider2D directCollider)
            {
                AddUniqueCollider(colliders, directCollider);
                return;
            }

            if (_mb.SelfProvider is UnityColliderObjectMB unityProvider)
            {
                _selfColliderTagsScratch.Clear();
                unityProvider.FillConfiguredColliders(colliders, _selfColliderTagsScratch);
                return;
            }

            if (_mb.TryGetComponent<UnityColliderObjectMB>(out var unityColliderObject) && unityColliderObject != null)
            {
                _selfColliderTagsScratch.Clear();
                unityColliderObject.FillConfiguredColliders(colliders, _selfColliderTagsScratch);
                return;
            }

            if (_mb.TryGetComponent<Collider2D>(out var collider) && collider != null)
                AddUniqueCollider(colliders, collider);
        }

        static void AddUniqueHandle(List<DynamicColliderHandle> handles, DynamicColliderHandle handle)
        {
            if (!handle.IsValid)
                return;

            for (var i = 0; i < handles.Count; i++)
            {
                if (SameHandle(handles[i], handle))
                    return;
            }

            handles.Add(handle);
        }

        static void AddUniqueCollider(List<Collider2D> colliders, Collider2D collider)
        {
            if (collider == null)
                return;

            for (var i = 0; i < colliders.Count; i++)
            {
                if (ReferenceEquals(colliders[i], collider))
                    return;
            }

            colliders.Add(collider);
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

        void BindRules(IReadOnlyList<DynamicColliderHandle> selves, IReadOnlyList<HitColliderControllerRule> rules)
        {
            var boundCount = 0;
            for (int s = 0; s < selves.Count; s++)
            {
                var self = selves[s];
                if (!self.IsValid)
                    continue;

                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule == null || !rule.Enabled)
                        continue;
                    if (!rule.HasAnyCommands())
                        continue;

                    var spec = rule.ToSpec();
                    var runtime = _hub.GetOrCreate(self, spec);

                    var binding = new RuntimeBinding
                    {
                        RuleName = string.IsNullOrWhiteSpace(rule.Name) ? "default" : rule.Name,
                        RuleIndex = i,
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
            }

            _boundSelfHandles.Clear();
            for (int i = 0; i < selves.Count; i++)
            {
                if (selves[i].IsValid)
                    _boundSelfHandles.Add(selves[i]);
            }

            var debugHandle = _boundSelfHandles.Count > 0 ? _boundSelfHandles[0] : default;
            _mb.SetDebugBindingState(debugHandle, boundCount, boundCount > 0 ? "Bound" : "NoEnabledRulesWithCommands");
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
                var hasCurrent = TryResolveSelfHandles(_selfHandlesScratch) && _selfHandlesScratch.Count > 0;

                if (hasCurrent && !SameHandleSet(_boundSelfHandles, _selfHandlesScratch))
                {
                    _mb.SetDebugBindingState(_selfHandlesScratch[0], _bindings.Count, "RebindingByHandleChange");
                    RebindRules();
                }
                else if (!hasCurrent && _boundSelfHandles.Count > 0)
                {
                    _mb.SetDebugBindingState(default, _bindings.Count, "RebindingByHandleLost");
                    RebindRules();
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        static bool SameHandle(DynamicColliderHandle a, DynamicColliderHandle b)
        {
            return a.IdPlusOne == b.IdPlusOne && a.Generation == b.Generation;
        }

        static bool SameHandleSet(List<DynamicColliderHandle> a, List<DynamicColliderHandle> b)
        {
            if (a.Count != b.Count)
                return false;

            for (var i = 0; i < a.Count; i++)
            {
                var found = false;
                for (var j = 0; j < b.Count; j++)
                {
                    if (!SameHandle(a[i], b[j]))
                        continue;

                    found = true;
                    break;
                }

                if (!found)
                    return false;
            }

            return true;
        }

        public void Dispose()
        {
            _selfHandleWatchCts?.Cancel();
            _selfHandleWatchCts?.Dispose();
            _selfHandleWatchCts = null;
            _warnedInvalidContextSlotRules.Clear();
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
