#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.Events.Generated;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.StatusEffect
{
    public sealed class StatusEffectService :
        IStatusEffectService,
        ITickable,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IDisposable
    {
        readonly IScopeNode _scope;
        readonly IStatusEffectServiceOptions? _options;
        readonly Dictionary<string, StatusEffectRuntime> _effects = new(StringComparer.Ordinal);
        readonly List<string> _removeQueue = new(8);

        ICommandRunner? _commandRunner;
        IRichTextRefService? _richTextRefService;
        ICommandListRuntimeMutationService? _mutationService;
        IEntityEventService? _eventService;
        IBlackboardService? _blackboardService;
        bool _disposed;
        bool _isActive;
        bool _hasInitializedGlobalState;
        float _globalLifetimeRemaining = -1f;
        float _globalLifetimeTotal = -1f;
        bool _skipNextGlobalLifetimeTick;
        float _globalCooldownRemaining;
        float _globalCooldownTotal;
        bool _skipNextGlobalCooldownTick;
        int _globalCurrentCount = -1;
        int _globalMaxCount;
        bool _globalCanUse = true;
        bool _hasWrittenGlobalState;
        float _lastWrittenGlobalLifetimeRemaining;
        float _lastWrittenGlobalLifetimeTotal;
        float _lastWrittenGlobalCooldownRemaining;
        float _lastWrittenGlobalCooldownMax;
        int _lastWrittenGlobalCurrentCount;
        int _lastWrittenGlobalMaxCount;
        bool _lastWrittenGlobalCanUse;
        StatusEffectGlobalLifetimeSettings? _globalLifetimeSettingsOverride;
        StatusEffectGlobalUseCooldownSettings? _globalUseCooldownSettingsOverride;
        StatusEffectGlobalCountSettings? _globalCountSettingsOverride;
        bool _hasGlobalLifetimeSettingsOverride;
        bool _hasGlobalUseCooldownSettingsOverride;
        bool _hasGlobalCountSettingsOverride;
        bool _isGlobalLifetimeEnabled;
        bool _isGlobalUseCooldownEnabled;
        bool _isGlobalCountEnabled;

        const string OnEffectAppliedKey = EventKeys.GameLib.StatusEffect.OnApplied;
        const string OnEffectRemovedKey = EventKeys.GameLib.StatusEffect.OnRemoved;

        public StatusEffectService(IScopeNode scope, IStatusEffectServiceOptions? options = null)
        {
            _scope = scope;
            _options = options;
        }

        internal ICommandRunner? CommandRunner => _commandRunner ??= ResolveOptional<ICommandRunner>();
        internal IRichTextRefService? RichTextRefService => _richTextRefService ??= ResolveOptional<IRichTextRefService>();
        internal ICommandListRuntimeMutationService? MutationService => _mutationService ??= ResolveOptional<ICommandListRuntimeMutationService>();
        internal IEntityEventService? EventService => _eventService ??= ResolveOptional<IEntityEventService>();
        internal IBlackboardService? BlackboardService => _blackboardService ??= ResolveOptional<IBlackboardService>();
        internal float GlobalLifetimeRemaining => _globalLifetimeRemaining;
        internal float GlobalLifetimeTotal => _globalLifetimeTotal;
        internal float GlobalCooldownRemaining => _globalCooldownRemaining;
        internal float GlobalCooldownMax => _globalCooldownTotal;
        internal int GlobalCurrentCount => _globalCurrentCount;
        internal int GlobalMaxCount => _globalMaxCount;
        internal int GlobalUsedCount => _globalMaxCount > 0 && _globalCurrentCount >= 0 ? Mathf.Max(0, _globalMaxCount - _globalCurrentCount) : 0;
        internal bool GlobalCanUse => _globalCanUse;
        internal bool IsGlobalLifetimeExpired => _globalLifetimeTotal >= 0f && _globalLifetimeRemaining <= 0f;
        internal bool IsGlobalUseCooldownActive => _globalCooldownRemaining > 0f;
        internal bool IsGlobalCountExhausted => _globalMaxCount > 0 && _globalCurrentCount == 0;

        public int ActiveEffectCount
        {
            get
            {
                int count = 0;
                foreach (var effect in _effects.Values)
                {
                    if (effect != null && effect.IsActive)
                        count++;
                }

                return count;
            }
        }

        public bool TryApply(StatusEffectApplyRequest request, IDynamicContext? evaluationContext, out string instanceId)
        {
            instanceId = string.Empty;
            if (_disposed || request == null)
                return false;

            EnsureGlobalStateInitialized(forceReset: false);
            var evalContext = evaluationContext ?? new SimpleDynamicContext(NullVarStore.Instance, _scope);
            var definition = request.Definition.GetOrDefault(evalContext, default!);
            if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId))
                return false;

            var runtimeTag = string.IsNullOrWhiteSpace(request.RuntimeTag)
                ? definition.DefaultRuntimeTag ?? string.Empty
                : request.RuntimeTag;
            var slotKey = BuildSlotKey(definition.DefinitionId, runtimeTag);
            var resolvedStackPreset = ResolveStackPreset(request, evalContext);
            var resolvedIntensities = ResolveIntensities(request, evalContext, resolvedStackPreset);
            var runtimeVars = new VarStore();
            var hookMutationLabel = request.HookMutations != null ? request.HookMutations.GetType().Name : "<null>";

            if (_effects.TryGetValue(slotKey, out var existing) && existing != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[StatusEffectService] TryApply reuse definition={definition.DefinitionId} slot={slotKey} instance={existing.InstanceId} tag={runtimeTag} active={_isActive} hookMutations={hookMutationLabel}");
#endif
                var stackContext = new StatusEffectBuildContext(
                    _scope,
                    evalContext.Vars,
                    existing.Vars,
                    evalContext.CommandRootScope ?? _scope,
                    request,
                    definition,
                    existing.InstanceId,
                    runtimeTag,
                    resolvedIntensities,
                    resolvedStackPreset);

                existing.ApplyMutations(request.HookMutations);
                existing.ApplyStack(request, stackContext);
                instanceId = existing.InstanceId;
                return true;
            }

            instanceId = Guid.NewGuid().ToString("N");
            var buildContext = new StatusEffectBuildContext(
                _scope,
                evalContext.Vars,
                runtimeVars,
                evalContext.CommandRootScope ?? _scope,
                request,
                definition,
                instanceId,
                runtimeTag,
                resolvedIntensities,
                resolvedStackPreset);

            if (!TryBuildRuntime(definition, buildContext, slotKey, out var runtime))
            {
                instanceId = string.Empty;
                return false;
            }

            _effects[slotKey] = runtime;
            if (_isActive)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[StatusEffectService] TryApply immediate ApplyInitial definition={definition.DefinitionId} slot={slotKey} instance={instanceId} tag={runtimeTag} active={_isActive} hookMutations={hookMutationLabel}");
#endif
                runtime.ApplyInitial();
                runtime.RefreshFromServiceGlobalState(applyActions: true);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_isActive)
            {
                Debug.Log(
                    $"[StatusEffectService] TryApply created while inactive definition={definition.DefinitionId} slot={slotKey} instance={instanceId} tag={runtimeTag} active={_isActive} hookMutations={hookMutationLabel}");
            }
#endif

            if (runtime.IsRemoveRequested)
            {
                _removeQueue.Add(slotKey);
                ProcessRemoveQueue();
                return true;
            }

            PublishEffectApplied(runtime);
            return true;
        }

        public int Remove(StatusEffectRuntimeFilter filter)
        {
            if (_disposed)
                return 0;

            int count = 0;
            foreach (var pair in _effects)
            {
                if (pair.Value == null || !filter.Matches(pair.Value))
                    continue;

                _removeQueue.Add(pair.Key);
                count++;
            }

            ProcessRemoveQueue();
            return count;
        }

        public int SetEnabled(StatusEffectRuntimeFilter filter, bool enabled)
        {
            int count = 0;
            foreach (var runtime in _effects.Values)
            {
                if (runtime == null || !filter.Matches(runtime))
                    continue;

                if (enabled)
                    runtime.Enable();
                else
                    runtime.Disable();

                count++;
            }

            return count;
        }

        public int SetOperationEnabled(StatusEffectRuntimeFilter filter, string operationId, bool enabled)
        {
            if (_disposed || string.IsNullOrWhiteSpace(operationId))
                return 0;

            int count = 0;
            foreach (var runtime in _effects.Values)
            {
                if (runtime == null || !filter.Matches(runtime))
                    continue;

                count += runtime.SetOperationEnabled(operationId, enabled);
            }

            return count;
        }

        public int Use(StatusEffectRuntimeFilter filter, IScopeNode? userScope = null, CommandContext? sourceContext = null)
        {
            if (_disposed || !_isActive)
                return 0;

            EnsureGlobalStateInitialized(forceReset: false);
            int count = 0;
            foreach (var pair in _effects)
            {
                var runtime = pair.Value;
                if (runtime == null || runtime.UsesAnyServiceGlobalUseState || !filter.Matches(runtime))
                    continue;

                if (runtime.Use(userScope ?? _scope, sourceContext))
                    count++;

                if (runtime.IsRemoveRequested)
                    _removeQueue.Add(pair.Key);
            }

            ProcessRemoveQueue();
            return count;
        }

        public int UseGlobal(IScopeNode? userScope = null, CommandContext? sourceContext = null)
        {
            if (_disposed || !_isActive)
                return 0;

            EnsureGlobalStateInitialized(forceReset: false);

            bool needsGlobalCountConsumption = false;
            bool needsGlobalCooldownStart = false;
            int candidateCount = 0;

            foreach (var runtime in _effects.Values)
            {
                if (runtime == null || !runtime.UsesAnyServiceGlobalUseState || !runtime.CanUseViaGlobalRequest())
                    continue;

                candidateCount++;
                needsGlobalCountConsumption |= runtime.UsesServiceGlobalCount;
                needsGlobalCooldownStart |= runtime.UsesServiceGlobalUseCooldown;
            }

            if (candidateCount == 0)
                return 0;

            if (needsGlobalCountConsumption && _globalMaxCount > 0 && _globalCurrentCount > 0)
                _globalCurrentCount--;

            if (needsGlobalCooldownStart && _globalCooldownTotal > 0f)
            {
                _globalCooldownRemaining = _globalCooldownTotal;
                _skipNextGlobalCooldownTick = true;
            }

            UpdateGlobalCanUse();
            WriteGlobalStateToBlackboard(force: false);

            int count = 0;
            foreach (var pair in _effects)
            {
                var runtime = pair.Value;
                if (runtime == null || !runtime.UsesAnyServiceGlobalUseState)
                    continue;

                if (runtime.UseViaGlobal(userScope ?? _scope, sourceContext))
                    count++;

                if (runtime.IsRemoveRequested)
                    _removeQueue.Add(pair.Key);
            }

            SyncAllRuntimeGlobalState(applyActions: true);
            ProcessRemoveQueue();
            return count;
        }

        public int RestoreState(StatusEffectRuntimeFilter filter, bool restoreGlobalState = false)
        {
            if (restoreGlobalState)
            {
                EnsureGlobalStateInitialized(forceReset: true);
                SyncAllRuntimeGlobalState(applyActions: _isActive);
            }

            int count = 0;
            foreach (var runtime in _effects.Values)
            {
                if (runtime == null || !filter.Matches(runtime))
                    continue;

                runtime.RestoreRuntimeState();
                count++;
            }

            return count;
        }

        public void ClearAll()
        {
            foreach (var key in _effects.Keys)
                _removeQueue.Add(key);

            ProcessRemoveQueue();
        }

        public void RefreshServiceSettings(bool resetGlobalState = true)
        {
            if (_disposed)
                return;

            EnsureGlobalStateInitialized(forceReset: resetGlobalState || !_hasInitializedGlobalState);
            SyncAllRuntimeGlobalState(applyActions: _isActive);
            WriteGlobalStateToBlackboard(force: true);
        }

        public void ConfigureServiceSettings(StatusEffectServiceSettingsOverrideRequest request, IDynamicContext? evaluationContext = null)
        {
            _ = evaluationContext;
            if (_disposed || request == null)
                return;

            if (request.ApplyGlobalLifetimeSettings)
            {
                _hasGlobalLifetimeSettingsOverride = true;
                _globalLifetimeSettingsOverride = (request.GlobalLifetimeSettings ?? StatusEffectGlobalLifetimeSettings.CreateDisabled()).CreateRuntimeCopy();
            }

            if (request.ApplyGlobalUseCooldownSettings)
            {
                _hasGlobalUseCooldownSettingsOverride = true;
                _globalUseCooldownSettingsOverride = (request.GlobalUseCooldownSettings ?? StatusEffectGlobalUseCooldownSettings.CreateDisabled()).CreateRuntimeCopy();
            }

            if (request.ApplyGlobalCountSettings)
            {
                _hasGlobalCountSettingsOverride = true;
                _globalCountSettingsOverride = (request.GlobalCountSettings ?? StatusEffectGlobalCountSettings.CreateDisabled()).CreateRuntimeCopy();
            }

            RefreshServiceSettings(request.ResetGlobalState);
        }

        public bool HasEffect(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                return false;

            foreach (var runtime in _effects.Values)
            {
                if (runtime != null && string.Equals(runtime.DefinitionId, definitionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public bool TryGetRegisteredDefinition(StatusEffectRuntimeFilter filter, out BaseStatusEffectDefinitionData definition)
        {
            definition = default!;
            foreach (var runtime in _effects.Values)
            {
                if (runtime == null || !filter.Matches(runtime))
                    continue;

                definition = runtime.Definition;
                return definition != null;
            }

            return false;
        }

        public void GetActiveEffectStates(List<EffectState> output)
        {
            output?.Clear();
            if (output == null)
                return;

            foreach (var runtime in _effects.Values)
            {
                if (runtime != null && runtime.IsActive)
                    output.Add(runtime.ToState());
            }
        }

        public void GetStates(List<EffectState> output, StatusEffectRuntimeFilter filter)
        {
            output?.Clear();
            if (output == null)
                return;

            foreach (var runtime in _effects.Values)
            {
                if (runtime != null && filter.Matches(runtime))
                    output.Add(runtime.ToState());
            }
        }

        public void Tick()
        {
            if (_disposed || !_isActive)
                return;

            EnsureGlobalStateInitialized(forceReset: false);
            var deltaTime = Time.deltaTime;
            bool globalStateChanged = false;

            if (_globalLifetimeTotal >= 0f && _globalLifetimeRemaining > 0f)
            {
                if (_skipNextGlobalLifetimeTick)
                {
                    _skipNextGlobalLifetimeTick = false;
                }
                else
                {
                    _globalLifetimeRemaining -= Mathf.Max(0f, deltaTime);
                    if (_globalLifetimeRemaining < 0f)
                        _globalLifetimeRemaining = 0f;
                    globalStateChanged = true;
                }
            }

            if (_globalCooldownRemaining > 0f)
            {
                if (_skipNextGlobalCooldownTick)
                {
                    _skipNextGlobalCooldownTick = false;
                }
                else
                {
                    _globalCooldownRemaining -= Mathf.Max(0f, deltaTime);
                    if (_globalCooldownRemaining < 0f)
                        _globalCooldownRemaining = 0f;
                    globalStateChanged = true;
                }
            }

            if (globalStateChanged)
            {
                UpdateGlobalCanUse();
                WriteGlobalStateToBlackboard(force: false);
            }

            foreach (var pair in _effects)
            {
                var runtime = pair.Value;
                if (runtime == null)
                    continue;

                runtime.Tick(deltaTime);
                if (runtime.IsRemoveRequested)
                    _removeQueue.Add(pair.Key);
            }

            if (globalStateChanged)
                SyncAllRuntimeGlobalState(applyActions: true);

            ProcessRemoveQueue();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _isActive = true;
            EnsureGlobalStateInitialized(forceReset: isReset || !_hasInitializedGlobalState);

            foreach (var runtime in _effects.Values)
            {
                if (runtime == null)
                    continue;

                if (isReset)
                {
                    runtime.RestoreRuntimeState();
                    continue;
                }

                runtime.ResumeFromScopeAcquire();

                if (!runtime.IsApplied && runtime.IsEnabled)
                    runtime.ApplyInitial();
            }

            SyncAllRuntimeGlobalState(applyActions: true);
            ProcessRemoveQueue();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _isActive = false;

            foreach (var runtime in _effects.Values)
                runtime?.SuspendForScopeRelease();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ClearAll();
            _effects.Clear();
            _removeQueue.Clear();
            _commandRunner = null;
            _richTextRefService = null;
            _mutationService = null;
            _eventService = null;
            _blackboardService = null;
        }

        bool TryBuildRuntime(
            BaseStatusEffectDefinitionData definition,
            StatusEffectBuildContext buildContext,
            string slotKey,
            out StatusEffectRuntime runtime)
        {
            runtime = default!;
            var runtimeVars = buildContext.RuntimeVars as VarStore;
            if (runtimeVars == null)
                return false;

            var operations = new List<IStatusEffectOperationRuntime>(definition.Operations.Count);
            for (int i = 0; i < definition.Operations.Count; i++)
            {
                var operation = definition.Operations[i];
                if (operation == null)
                    continue;

                if (!operation.TryBuild(buildContext, out var opRuntime) || opRuntime == null)
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to build operation. DefinitionId={definition.DefinitionId} OperationType={operation.GetType().Name}");
                    return false;
                }

                operations.Add(opRuntime);
            }

            var runtimeResolution = ResolveRuntimeResolution(definition);

            var durationDefinition = runtimeResolution.DurationDefinition;
            IStatusEffectDurationController? durationController = null;
            if (runtimeResolution.UseDuration)
            {
                if (durationDefinition == null)
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to create duration controller. DefinitionId={definition.DefinitionId}");
                    return false;
                }

                if (!runtimeResolution.UsesServiceGlobalLifetime &&
                    (!durationDefinition.TryCreateController(buildContext, out durationController) || durationController == null))
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to create duration controller. DefinitionId={definition.DefinitionId}");
                    return false;
                }
            }

            var useCooldownDefinition = runtimeResolution.UseCooldownDefinition;
            IStatusEffectUseCooldownController? useCooldownController = null;
            if (runtimeResolution.UseUseCooldown)
            {
                if (useCooldownDefinition == null)
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to create use cooldown controller. DefinitionId={definition.DefinitionId}");
                    return false;
                }

                if (!runtimeResolution.UsesServiceGlobalUseCooldown &&
                    (!useCooldownDefinition.TryCreateController(buildContext, out useCooldownController) || useCooldownController == null))
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to create use cooldown controller. DefinitionId={definition.DefinitionId}");
                    return false;
                }
            }

            var countDefinition = runtimeResolution.CountDefinition;
            IStatusEffectCountController? countController = null;
            if (runtimeResolution.UseCount)
            {
                if (countDefinition == null)
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to create count controller. DefinitionId={definition.DefinitionId}");
                    return false;
                }

                if (!runtimeResolution.UsesServiceGlobalCount &&
                    (!countDefinition.TryCreateController(buildContext, out countController) || countController == null))
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to create count controller. DefinitionId={definition.DefinitionId}");
                    return false;
                }
            }

            var hooks = definition.DefaultHooks?.Clone() ?? new StatusEffectHookSet();
            hooks.ApplyMutations(buildContext.Request.HookMutations, MutationService);

            runtime = new StatusEffectRuntime(
                this,
                _scope,
                definition,
                buildContext.InstanceId,
                buildContext.RuntimeTag,
                slotKey,
                buildContext.ResolvedIntensities,
                buildContext.ResolvedStackPreset,
                runtimeVars,
                operations,
                hooks,
                runtimeResolution.IsAutoGlobalMode,
                runtimeResolution.UsesServiceGlobalLifetime,
                runtimeResolution.UsesServiceGlobalUseCooldown,
                runtimeResolution.UsesServiceGlobalCount,
                durationDefinition,
                durationController,
                useCooldownDefinition,
                useCooldownController,
                countDefinition,
                countController);
            return true;
        }

        StatusEffectRuntimeResolution ResolveRuntimeResolution(BaseStatusEffectDefinitionData definition)
        {
            if (definition.RuntimeControlMode != StatusEffectRuntimeControlMode.AutoGlobal)
            {
                var useDuration = definition.UseDuration;
                var useUseCooldown = definition.UseUseCooldown;
                var useCount = definition.UseCount;

                return new StatusEffectRuntimeResolution(
                    isAutoGlobalMode: false,
                    useDuration: useDuration,
                    useUseCooldown: useUseCooldown,
                    useCount: useCount,
                    durationDefinition: useDuration ? definition.DurationDefinition : null,
                    useCooldownDefinition: useUseCooldown ? definition.UseCooldownDefinition : null,
                    countDefinition: useCount ? definition.CountDefinition : null);
            }

            var autoUseDuration = _isGlobalLifetimeEnabled;
            var autoUseCooldown = _isGlobalUseCooldownEnabled;
            var autoUseCount = _isGlobalCountEnabled;

            return new StatusEffectRuntimeResolution(
                isAutoGlobalMode: true,
                useDuration: autoUseDuration,
                useUseCooldown: autoUseCooldown,
                useCount: autoUseCount,
                durationDefinition: autoUseDuration ? CreateAutoGlobalDurationDefinition() : null,
                useCooldownDefinition: autoUseCooldown ? CreateAutoGlobalUseCooldownDefinition() : null,
                countDefinition: autoUseCount ? CreateAutoGlobalCountDefinition() : null);
        }

        static IStatusEffectDurationDefinition CreateAutoGlobalDurationDefinition()
        {
            return new FixedDurationStatusEffectDefinition
            {
                SyncWithGlobalLifetime = true,
                EndAction = EffectLifetimeEndAction.Remove,
            };
        }

        static IStatusEffectUseCooldownDefinition CreateAutoGlobalUseCooldownDefinition()
        {
            return new FixedUseCooldownStatusEffectDefinition
            {
                SyncWithGlobalUseCooldown = true,
                Duration = DynamicValueExtensions.FromLiteral(0f),
            };
        }

        static IStatusEffectCountDefinition CreateAutoGlobalCountDefinition()
        {
            return new DynamicCountStatusEffectDefinition
            {
                SyncWithGlobalCount = true,
                MaxCount = DynamicValueExtensions.FromLiteral(0),
                ExhaustedAction = EffectCountExhaustedAction.Disable,
                ActivePolicy = StatusEffectActivePolicy.RegisteredEvenIfDisabled,
            };
        }

        static StatusEffectResolvedIntensities ResolveIntensities(
            StatusEffectApplyRequest request,
            IDynamicContext evaluationContext,
            StatusEffectStackPreset stackPreset)
        {
            var intensities = request.ResolveIntensities(evaluationContext);

            // For command-driven applies, stack preset local values are commonly used as
            // the first-apply intensity source. If explicit intensity sources are provided,
            // keep those as the authoritative values.
            if (!request.StackPreset.HasSource || stackPreset == null)
                return intensities;

            ApplyInitialIntensityFromStackPresetRule(
                request.IntensityA.HasSource,
                stackPreset,
                StatusEffectIntensitySlot.A,
                evaluationContext,
                ref intensities.A);

            ApplyInitialIntensityFromStackPresetRule(
                request.IntensityB.HasSource,
                stackPreset,
                StatusEffectIntensitySlot.B,
                evaluationContext,
                ref intensities.B);

            ApplyInitialIntensityFromStackPresetRule(
                request.IntensityC.HasSource,
                stackPreset,
                StatusEffectIntensitySlot.C,
                evaluationContext,
                ref intensities.C);

            ApplyInitialIntensityFromStackPresetRule(
                request.IntensityD.HasSource,
                stackPreset,
                StatusEffectIntensitySlot.D,
                evaluationContext,
                ref intensities.D);

            ApplyInitialIntensityFromStackPresetRule(
                request.IntensityE.HasSource,
                stackPreset,
                StatusEffectIntensitySlot.E,
                evaluationContext,
                ref intensities.E);

            ApplyInitialIntensityFromStackPresetRule(
                request.IntensityF.HasSource,
                stackPreset,
                StatusEffectIntensitySlot.F,
                evaluationContext,
                ref intensities.F);

            ApplyInitialIntensityFromStackPresetRule(
                request.IntensityG.HasSource,
                stackPreset,
                StatusEffectIntensitySlot.G,
                evaluationContext,
                ref intensities.G);

            return intensities;
        }

        static void ApplyInitialIntensityFromStackPresetRule(
            bool hasExplicitIntensity,
            StatusEffectStackPreset stackPreset,
            StatusEffectIntensitySlot slot,
            IDynamicContext evaluationContext,
            ref float currentIntensity)
        {
            if (hasExplicitIntensity || !stackPreset.ShouldApplyIntensity(slot))
                return;

            var rule = stackPreset.GetIntensityRule(slot);
            if (rule == null)
                return;

            if (rule.ApplyLocalValue)
            {
                var local = rule.LocalValue.HasSource
                    ? rule.LocalValue.GetOrDefault(evaluationContext, currentIntensity)
                    : currentIntensity;
                currentIntensity = ApplyStackOperation(currentIntensity, local, rule.Operation);
            }

            if (!rule.ApplyGlobalValue)
                return;

            var global = rule.GlobalValue.HasSource
                ? rule.GlobalValue.GetOrDefault(evaluationContext, 0f)
                : 0f;
            if (rule.IgnoreGlobalWhenMinusOne && Mathf.Approximately(global, -1f))
                return;

            currentIntensity = ApplyStackOperation(currentIntensity, global, rule.Operation);
        }

        static float ApplyStackOperation(float current, float value, StatusEffectStackOperation operation)
        {
            return operation switch
            {
                StatusEffectStackOperation.Set => value,
                StatusEffectStackOperation.Add => current + value,
                StatusEffectStackOperation.Mul => current * value,
                _ => current,
            };
        }

        StatusEffectStackPreset ResolveStackPreset(StatusEffectApplyRequest request, IDynamicContext evaluationContext)
        {
            var fallback = StatusEffectStackPreset.CreateDurationRefreshPreset();
            if (!request.StackPreset.HasSource)
                return fallback;

            return request.StackPreset.GetOrDefault(evaluationContext, fallback) ?? fallback;
        }

        static string BuildSlotKey(string definitionId, string runtimeTag)
        {
            if (string.IsNullOrWhiteSpace(runtimeTag))
                return definitionId ?? string.Empty;

            return $"{definitionId}:{runtimeTag}";
        }

        void ProcessRemoveQueue()
        {
            if (_removeQueue.Count == 0)
                return;

            for (int i = 0; i < _removeQueue.Count; i++)
            {
                var key = _removeQueue[i];
                if (!_effects.TryGetValue(key, out var runtime) || runtime == null)
                    continue;

                runtime.Remove();
                _effects.Remove(key);
                PublishEffectRemoved(runtime);
            }

            _removeQueue.Clear();
        }

        T? ResolveOptional<T>() where T : class
        {
            var current = _scope;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<T>(out var service) && service != null)
                    return service;

                current = current.Parent;
            }

            return null;
        }

        void EnsureGlobalStateInitialized(bool forceReset)
        {
            if (_hasInitializedGlobalState && !forceReset)
                return;

            _hasInitializedGlobalState = true;
            var context = CreateGlobalSettingsContext();

            var lifetimeSettings = _hasGlobalLifetimeSettingsOverride
                ? _globalLifetimeSettingsOverride?.CreateRuntimeCopy()
                : _options?.GlobalLifetimeSettingsValue.GetOrDefault(
                    context,
                    new StatusEffectGlobalLifetimeSettings())?.CreateRuntimeCopy();
            if (lifetimeSettings is { Enabled: true } enabledLifetimeSettings)
            {
                _isGlobalLifetimeEnabled = true;
                _globalLifetimeTotal = enabledLifetimeSettings.Duration.GetOrDefault(context, -1f);
                _globalLifetimeRemaining = _globalLifetimeTotal;
                _skipNextGlobalLifetimeTick = _globalLifetimeTotal >= 0f && _globalLifetimeRemaining > 0f;
            }
            else
            {
                _isGlobalLifetimeEnabled = false;
                _globalLifetimeTotal = -1f;
                _globalLifetimeRemaining = -1f;
                _skipNextGlobalLifetimeTick = false;
            }

            var cooldownSettings = _hasGlobalUseCooldownSettingsOverride
                ? _globalUseCooldownSettingsOverride?.CreateRuntimeCopy()
                : _options?.GlobalUseCooldownSettingsValue.GetOrDefault(
                    context,
                    new StatusEffectGlobalUseCooldownSettings())?.CreateRuntimeCopy();
            if (cooldownSettings is { Enabled: true } enabledCooldownSettings)
            {
                _isGlobalUseCooldownEnabled = true;
                _globalCooldownTotal = Mathf.Max(0f, enabledCooldownSettings.Duration.GetOrDefault(context, 0f));
            }
            else
            {
                _isGlobalUseCooldownEnabled = false;
                _globalCooldownTotal = 0f;
            }
            _globalCooldownRemaining = 0f;
            _skipNextGlobalCooldownTick = false;

            var countSettings = _hasGlobalCountSettingsOverride
                ? _globalCountSettingsOverride?.CreateRuntimeCopy()
                : _options?.GlobalCountSettingsValue.GetOrDefault(
                    context,
                    new StatusEffectGlobalCountSettings())?.CreateRuntimeCopy();
            if (countSettings is { Enabled: true } enabledCountSettings)
            {
                _isGlobalCountEnabled = true;
                _globalMaxCount = Mathf.Max(0, enabledCountSettings.MaxCount.GetOrDefault(context, 0));
            }
            else
            {
                _isGlobalCountEnabled = false;
                _globalMaxCount = 0;
            }
            _globalCurrentCount = _globalMaxCount > 0 ? _globalMaxCount : -1;

            UpdateGlobalCanUse();
            WriteGlobalStateToBlackboard(force: true);
        }

        IDynamicContext CreateGlobalSettingsContext()
        {
            var vars = BlackboardService?.LocalVars ?? NullVarStore.Instance;
            return new SimpleDynamicContext(vars, _scope);
        }

        void UpdateGlobalCanUse()
        {
            bool canUse = true;

            if (_globalCooldownRemaining > 0f)
                canUse = false;

            if (_globalMaxCount > 0 && _globalCurrentCount <= 0)
                canUse = false;

            if (_globalLifetimeTotal >= 0f && _globalLifetimeRemaining <= 0f)
                canUse = false;

            _globalCanUse = canUse;
        }

        void WriteGlobalStateToBlackboard(bool force)
        {
            var vars = BlackboardService?.LocalVars;
            if (vars == null)
                return;

            if (!force &&
                _hasWrittenGlobalState &&
                Mathf.Approximately(_lastWrittenGlobalLifetimeRemaining, _globalLifetimeRemaining) &&
                Mathf.Approximately(_lastWrittenGlobalLifetimeTotal, _globalLifetimeTotal) &&
                Mathf.Approximately(_lastWrittenGlobalCooldownRemaining, _globalCooldownRemaining) &&
                Mathf.Approximately(_lastWrittenGlobalCooldownMax, _globalCooldownTotal) &&
                _lastWrittenGlobalCurrentCount == _globalCurrentCount &&
                _lastWrittenGlobalMaxCount == _globalMaxCount &&
                _lastWrittenGlobalCanUse == _globalCanUse)
            {
                return;
            }

            _hasWrittenGlobalState = true;
            _lastWrittenGlobalLifetimeRemaining = _globalLifetimeRemaining;
            _lastWrittenGlobalLifetimeTotal = _globalLifetimeTotal;
            _lastWrittenGlobalCooldownRemaining = _globalCooldownRemaining;
            _lastWrittenGlobalCooldownMax = _globalCooldownTotal;
            _lastWrittenGlobalCurrentCount = _globalCurrentCount;
            _lastWrittenGlobalMaxCount = _globalMaxCount;
            _lastWrittenGlobalCanUse = _globalCanUse;

            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.lifetimeRemaining, DynamicVariant.FromFloat(_globalLifetimeRemaining));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.lifetimeTotal, DynamicVariant.FromFloat(_globalLifetimeTotal));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.cooldownRemaining, DynamicVariant.FromFloat(_globalCooldownRemaining));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.cooldownMax, DynamicVariant.FromFloat(_globalCooldownTotal));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.currentCount, DynamicVariant.FromInt(_globalCurrentCount));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.maxCount, DynamicVariant.FromInt(_globalMaxCount));
            vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.canUse, DynamicVariant.FromBool(_globalCanUse));
        }

        void SyncAllRuntimeGlobalState(bool applyActions)
        {
            foreach (var pair in _effects)
            {
                var runtime = pair.Value;
                if (runtime == null)
                    continue;

                runtime.RefreshFromServiceGlobalState(applyActions);
                if (runtime.IsRemoveRequested)
                    _removeQueue.Add(pair.Key);
            }
        }

        void PublishEffectApplied(StatusEffectRuntime runtime)
        {
            var eventService = EventService;
            if (eventService == null || runtime == null)
                return;

            var payload = CreateEventPayload(runtime);
            UniTask.Void(async () =>
            {
                try
                {
                    await eventService.PublishAsync(OnEffectAppliedKey, payload);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }

        void PublishEffectRemoved(StatusEffectRuntime runtime)
        {
            var eventService = EventService;
            if (eventService == null || runtime == null)
                return;

            var payload = CreateEventPayload(runtime);
            UniTask.Void(async () =>
            {
                try
                {
                    await eventService.PublishAsync(OnEffectRemovedKey, payload);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }

        static VarStore CreateEventPayload(StatusEffectRuntime runtime)
        {
            var state = runtime.ToState();
            var payload = new VarStore();
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectId, DynamicVariant.FromString(state.EffectId));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.instanceId, DynamicVariant.FromString(state.InstanceId));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.runtimeTag, DynamicVariant.FromString(state.RuntimeTag));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectType, DynamicVariant.FromInt((int)state.Type));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isUseBlocked, DynamicVariant.FromBool(state.IsUseBlocked));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.totalDuration, DynamicVariant.FromFloat(state.TotalDuration));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingDuration, DynamicVariant.FromFloat(state.RemainingTime));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingInverseInterval, DynamicVariant.FromFloat(state.RemainingUseCooldown));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityA, DynamicVariant.FromFloat(state.IntensityA));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityB, DynamicVariant.FromFloat(state.IntensityB));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityC, DynamicVariant.FromFloat(state.IntensityC));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityD, DynamicVariant.FromFloat(state.IntensityD));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityE, DynamicVariant.FromFloat(state.IntensityE));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityF, DynamicVariant.FromFloat(state.IntensityF));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityG, DynamicVariant.FromFloat(state.IntensityG));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.stackCount, DynamicVariant.FromInt(state.StackCount));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isEnabled, DynamicVariant.FromBool(state.IsEnabled));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isApplied, DynamicVariant.FromBool(state.IsApplied));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isActive, DynamicVariant.FromBool(state.IsActive));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.usedCount, DynamicVariant.FromInt(state.UsedCount));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingUseCount, DynamicVariant.FromInt(state.RemainingUseCount));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.maxUseCount, DynamicVariant.FromInt(state.MaxUseCount));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.nameTemplate, DynamicVariant.FromString(state.DisplayName));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.descriptionTemplate, DynamicVariant.FromString(string.Empty));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.nameKey, DynamicVariant.FromString(state.NameKey));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.descriptionKey, DynamicVariant.FromString(state.DescriptionKey));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.visualData, DynamicVariant.FromUnityObject(state.Icon));
            return payload;
        }

        readonly struct StatusEffectRuntimeResolution
        {
            public readonly bool IsAutoGlobalMode;
            public readonly bool UseDuration;
            public readonly bool UseUseCooldown;
            public readonly bool UseCount;
            public readonly IStatusEffectDurationDefinition? DurationDefinition;
            public readonly IStatusEffectUseCooldownDefinition? UseCooldownDefinition;
            public readonly IStatusEffectCountDefinition? CountDefinition;

            public bool UsesServiceGlobalLifetime => UseDuration && (DurationDefinition?.SyncWithGlobalLifetime ?? false);
            public bool UsesServiceGlobalUseCooldown => UseUseCooldown && (UseCooldownDefinition?.SyncWithGlobalUseCooldown ?? false);
            public bool UsesServiceGlobalCount => UseCount && (CountDefinition?.SyncWithGlobalCount ?? false);

            public StatusEffectRuntimeResolution(
                bool isAutoGlobalMode,
                bool useDuration,
                bool useUseCooldown,
                bool useCount,
                IStatusEffectDurationDefinition? durationDefinition,
                IStatusEffectUseCooldownDefinition? useCooldownDefinition,
                IStatusEffectCountDefinition? countDefinition)
            {
                IsAutoGlobalMode = isAutoGlobalMode;
                UseDuration = useDuration;
                UseUseCooldown = useUseCooldown;
                UseCount = useCount;
                DurationDefinition = durationDefinition;
                UseCooldownDefinition = useCooldownDefinition;
                CountDefinition = countDefinition;
            }
        }
    }
}
