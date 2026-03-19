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
        readonly Dictionary<string, StatusEffectRuntime> _effects = new(StringComparer.Ordinal);
        readonly List<string> _removeQueue = new(8);

        ICommandRunner? _commandRunner;
        IRichTextRefService? _richTextRefService;
        ICommandListRuntimeMutationService? _mutationService;
        IEntityEventService? _eventService;
        bool _disposed;
        bool _isActive;

        const string OnEffectAppliedKey = EventKeys.GameLib.StatusEffect.OnApplied;
        const string OnEffectRemovedKey = EventKeys.GameLib.StatusEffect.OnRemoved;

        public StatusEffectService(IScopeNode scope)
        {
            _scope = scope;
        }

        internal ICommandRunner? CommandRunner => _commandRunner ??= ResolveOptional<ICommandRunner>();
        internal IRichTextRefService? RichTextRefService => _richTextRefService ??= ResolveOptional<IRichTextRefService>();
        internal ICommandListRuntimeMutationService? MutationService => _mutationService ??= ResolveOptional<ICommandListRuntimeMutationService>();
        internal IEntityEventService? EventService => _eventService ??= ResolveOptional<IEntityEventService>();

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

            var evalContext = evaluationContext ?? new SimpleDynamicContext(NullVarStore.Instance, _scope);
            var definition = request.Definition.GetOrDefault(evalContext, default!);
            if (definition == null || string.IsNullOrWhiteSpace(definition.DefinitionId))
                return false;

            var runtimeTag = string.IsNullOrWhiteSpace(request.RuntimeTag)
                ? definition.DefaultRuntimeTag ?? string.Empty
                : request.RuntimeTag;
            var slotKey = BuildSlotKey(definition.DefinitionId, runtimeTag);
            var resolvedIntensity = ResolveIntensity(definition, request, evalContext);
            var runtimeVars = new VarStore();

            if (_effects.TryGetValue(slotKey, out var existing) && existing != null)
            {
                var stackContext = new StatusEffectBuildContext(
                    _scope,
                    evalContext.Vars,
                    existing.Vars,
                    evalContext.CommandRootScope ?? _scope,
                    request,
                    definition,
                    existing.InstanceId,
                    runtimeTag,
                    resolvedIntensity);

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
                resolvedIntensity);

            if (!TryBuildRuntime(definition, buildContext, slotKey, out var runtime))
            {
                instanceId = string.Empty;
                return false;
            }

            _effects[slotKey] = runtime;
            if (_isActive)
                runtime.ApplyInitial();

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

        public int Use(StatusEffectRuntimeFilter filter, IScopeNode? userScope = null, CommandContext? sourceContext = null)
        {
            if (_disposed || !_isActive)
                return 0;

            int count = 0;
            foreach (var pair in _effects)
            {
                var runtime = pair.Value;
                if (runtime == null || !filter.Matches(runtime))
                    continue;

                if (runtime.Use(userScope ?? _scope, sourceContext))
                    count++;

                if (runtime.IsRemoveRequested)
                    _removeQueue.Add(pair.Key);
            }

            ProcessRemoveQueue();
            return count;
        }

        public int Reset(StatusEffectRuntimeFilter filter)
        {
            int count = 0;
            foreach (var runtime in _effects.Values)
            {
                if (runtime == null || !filter.Matches(runtime))
                    continue;

                runtime.ResetRuntime();
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

            var deltaTime = Time.deltaTime;
            foreach (var pair in _effects)
            {
                var runtime = pair.Value;
                if (runtime == null)
                    continue;

                runtime.Tick(deltaTime);
                if (runtime.IsRemoveRequested)
                    _removeQueue.Add(pair.Key);
            }

            ProcessRemoveQueue();
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _isActive = true;

            foreach (var runtime in _effects.Values)
            {
                if (runtime == null)
                    continue;

                if (isReset)
                    runtime.ResetRuntime();
                else
                    runtime.ResumeFromScopeAcquire();
            }
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

            IStatusEffectDurationController? durationController = null;
            if (definition.UseDuration)
            {
                if (definition.DurationDefinition == null ||
                    !definition.DurationDefinition.TryCreateController(buildContext, out durationController) ||
                    durationController == null)
                {
                    Debug.LogWarning($"[StatusEffectService] Failed to create duration controller. DefinitionId={definition.DefinitionId}");
                    return false;
                }
            }

            IStatusEffectCountController? countController = null;
            if (definition.UseCount)
            {
                if (definition.CountDefinition == null ||
                    !definition.CountDefinition.TryCreateController(buildContext, out countController) ||
                    countController == null)
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
                buildContext.ResolvedIntensity,
                runtimeVars,
                operations,
                hooks,
                durationController,
                countController);
            return true;
        }

        float ResolveIntensity(BaseStatusEffectDefinitionData definition, StatusEffectApplyRequest request, IDynamicContext evaluationContext)
        {
            var defaultIntensity = definition.DefaultIntensity.GetOrDefault(evaluationContext, 1f);
            if (!request.Intensity.HasSource)
                return defaultIntensity;

            return request.Intensity.GetOrDefault(evaluationContext, defaultIntensity);
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
            var payload = new VarStore();
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.effectId, DynamicVariant.FromString(runtime.DefinitionId));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.instanceId, DynamicVariant.FromString(runtime.InstanceId));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.runtimeTag, DynamicVariant.FromString(runtime.RuntimeTag));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.effectType, DynamicVariant.FromInt((int)runtime.Type));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.isUseBlocked, DynamicVariant.FromBool(runtime.IsUseBlocked));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.totalDuration, DynamicVariant.FromFloat(runtime.TotalDuration));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.remainingInverseInterval, DynamicVariant.FromFloat(runtime.RemainingInverseInterval));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.intensity, DynamicVariant.FromFloat(runtime.Intensity));
            payload.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.usedCount, DynamicVariant.FromInt(runtime.UsedCount));
            return payload;
        }
    }
}
