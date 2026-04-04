#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using Game.Commands.VNext;
using Game.Common;
using Game.Health;
using Game.Trait;
using Game.Vars.Generated;
using UnityEngine;
using VContainer;

namespace Game.StatusEffect
{
    public sealed class StatusEffectBuildContext : IDynamicContext
    {
        public IScopeNode OwnerScope { get; }
        public IVarStore Vars { get; }
        public IVarStore RuntimeVars { get; }
        public IScopeNode? CommandRootScope { get; }
        public StatusEffectApplyRequest Request { get; }
        public BaseStatusEffectDefinitionData Definition { get; }
        public string InstanceId { get; }
        public string RuntimeTag { get; }
        public StatusEffectResolvedIntensities ResolvedIntensities { get; }
        public StatusEffectStackPreset ResolvedStackPreset { get; }

        public IScopeNode Scope => OwnerScope;

        public StatusEffectBuildContext(
            IScopeNode ownerScope,
            IVarStore vars,
            IVarStore runtimeVars,
            IScopeNode? commandRootScope,
            StatusEffectApplyRequest request,
            BaseStatusEffectDefinitionData definition,
            string instanceId,
            string runtimeTag,
            StatusEffectResolvedIntensities resolvedIntensities,
            StatusEffectStackPreset resolvedStackPreset)
        {
            OwnerScope = ownerScope;
            Vars = vars ?? NullVarStore.Instance;
            RuntimeVars = runtimeVars ?? NullVarStore.Instance;
            CommandRootScope = commandRootScope;
            Request = request;
            Definition = definition;
            InstanceId = instanceId ?? string.Empty;
            RuntimeTag = runtimeTag ?? string.Empty;
            ResolvedIntensities = resolvedIntensities;
            ResolvedStackPreset = resolvedStackPreset ?? StatusEffectStackPreset.CreateDurationRefreshPreset();
        }

        public bool TryResolveLocal<T>(out T service) where T : class
        {
            service = default!;
            var resolver = OwnerScope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out service) && service != null;
        }

        public bool TryResolveFromActor<T>(ActorSource actorSource, out T service) where T : class
        {
            service = default!;
            var scope = ActorSourceFastResolver.Resolve(OwnerScope, actorSource, CommandRootScope);
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            return resolver.TryResolve(out service) && service != null;
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            if (OwnerScope?.Resolver == null)
                return null!;

            if (!OwnerScope.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry) || registry == null)
                return null!;

            return registry.Resolve(filter, OwnerScope);
        }
    }

    public sealed class StatusEffectOperationDynamicContext : IDynamicContext
    {
        public IVarStore Vars { get; }
        public IScopeNode Scope { get; }
        public IScopeNode? CommandRootScope { get; }

        public StatusEffectOperationDynamicContext(
            IVarStore vars,
            IScopeNode scope,
            IScopeNode? commandRootScope)
        {
            Vars = vars ?? NullVarStore.Instance;
            Scope = scope;
            CommandRootScope = commandRootScope;
        }

        public IScopeNode ResolveOtherScope(CommandTargetIdentityFilter filter)
        {
            if (Scope?.Resolver == null)
                return null!;

            if (!Scope.Resolver.TryResolve<IBaseLifetimeScopeRegistry>(out var registry) || registry == null)
                return null!;

            return registry.Resolve(filter, Scope);
        }
    }

    public sealed class StatusEffectRuntime
    {
        readonly StatusEffectService _owner;
        readonly IScopeNode _scope;
        readonly BaseStatusEffectDefinitionData _definition;
        readonly EffectVisualData _visualData;
        readonly List<IStatusEffectOperationRuntime> _operations;
        readonly StatusEffectHookSet _hooks;
        readonly IStatusEffectDurationDefinition? _durationDefinition;
        readonly IStatusEffectDurationController? _durationController;
        readonly IStatusEffectUseCooldownDefinition? _useCooldownDefinition;
        readonly IStatusEffectUseCooldownController? _useCooldownController;
        readonly IStatusEffectCountDefinition? _countDefinition;
        readonly IStatusEffectCountController? _countController;
        readonly string _slotKey;

        readonly VarStore _vars;
        StatusEffectResolvedIntensities _intensities;

        StatusEffectStackPreset _runtimeStackPreset;

        bool _isRemoveRequested;
        bool _isEnabled = true;
        bool _isApplied;
        bool _isRegistered = true;
        bool _isSuspendedByScopeRelease;
        bool _isUseBlocked;
        bool _hasHandledGlobalLifetimeExpiration;
        bool _hasHandledGlobalCountExhaustion;
        bool _hasHandledLocalCountExhaustion;
        string _nameKey = string.Empty;
        string _descriptionKey = string.Empty;

        public StatusEffectRuntime(
            StatusEffectService owner,
            IScopeNode scope,
            BaseStatusEffectDefinitionData definition,
            string instanceId,
            string runtimeTag,
            string slotKey,
            StatusEffectResolvedIntensities intensities,
            StatusEffectStackPreset? runtimeStackPreset,
            VarStore vars,
            List<IStatusEffectOperationRuntime> operations,
            StatusEffectHookSet hooks,
            IStatusEffectDurationDefinition? durationDefinition,
            IStatusEffectDurationController? durationController,
            IStatusEffectUseCooldownDefinition? useCooldownDefinition,
            IStatusEffectUseCooldownController? useCooldownController,
            IStatusEffectCountDefinition? countDefinition,
            IStatusEffectCountController? countController)
        {
            _owner = owner;
            _scope = scope;
            _definition = definition;
            _visualData = definition.VisualData ?? new EffectVisualData();
            _operations = operations ?? new List<IStatusEffectOperationRuntime>();
            _hooks = hooks ?? new StatusEffectHookSet();
            _durationDefinition = durationDefinition;
            _durationController = durationController;
            _useCooldownDefinition = useCooldownDefinition;
            _useCooldownController = useCooldownController;
            _countDefinition = countDefinition;
            _countController = countController;
            _slotKey = slotKey ?? string.Empty;
            _vars = vars ?? new VarStore();
            _runtimeStackPreset = runtimeStackPreset ?? StatusEffectStackPreset.CreateDurationRefreshPreset();

            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
            RuntimeTag = runtimeTag ?? string.Empty;
            _intensities = intensities;
            StackCount = 1;
        }

        public string DefinitionId => _definition.DefinitionId ?? string.Empty;
        public BaseStatusEffectDefinitionData Definition => _definition;
        public string InstanceId { get; }
        public string RuntimeTag { get; }
        public string SlotKey => _slotKey;
        public float IntensityA => _intensities.A;
        public float IntensityB => _intensities.B;
        public float IntensityC => _intensities.C;
        public float IntensityD => _intensities.D;
        public float IntensityE => _intensities.E;
        public float IntensityF => _intensities.F;
        public float IntensityG => _intensities.G;
        public int StackCount { get; private set; }
        public bool IsRegistered => _isRegistered;
        public bool IsEnabled => _isEnabled;
        public bool IsApplied => _isApplied;
        public bool IsRemoveRequested => _isRemoveRequested;
        public bool IsUseBlocked => _isUseBlocked;
        public bool UsesServiceGlobalLifetime => _definition.UseDuration && (_durationDefinition?.SyncWithGlobalLifetime ?? false);
        public bool UsesServiceGlobalUseCooldown => _definition.UseUseCooldown && (_useCooldownDefinition?.SyncWithGlobalUseCooldown ?? false);
        public bool UsesServiceGlobalCount => _definition.UseCount && (_countDefinition?.SyncWithGlobalCount ?? false);
        public bool UsesAnyServiceGlobalUseState => UsesServiceGlobalUseCooldown || UsesServiceGlobalCount;
        public float RemainingDuration => UsesServiceGlobalLifetime ? _owner.GlobalLifetimeRemaining : _durationController?.RemainingDuration ?? -1f;
        public float TotalDuration => UsesServiceGlobalLifetime ? _owner.GlobalLifetimeTotal : _durationController?.TotalDuration ?? -1f;
        public int MaxUseCount => UsesServiceGlobalCount ? _owner.GlobalMaxCount : _countController?.MaxCount ?? 0;
        public int UsedCount => UsesServiceGlobalCount ? _owner.GlobalUsedCount : _countController?.UsedCount ?? 0;
        public int RemainingUseCount => UsesServiceGlobalCount ? _owner.GlobalCurrentCount : _countController?.RemainingCount ?? -1;
        public float RemainingUseCooldown => UsesServiceGlobalUseCooldown ? _owner.GlobalCooldownRemaining : _useCooldownController?.RemainingDuration ?? 0f;
        public EffectType Type => _visualData?.EffectType ?? EffectType.Neutral;
        public string DisplayName => _visualData?.DisplayNameText ?? DefinitionId;
        public string NameKey => _nameKey;
        public string DescriptionKey => _descriptionKey;
        public Sprite? Icon => _visualData?.Icon;
        public IVarStore Vars => _vars;

        public bool IsActive
        {
            get
            {
                if (ResolveActivePolicy() == StatusEffectActivePolicy.RegisteredEvenIfDisabled)
                    return _isRegistered;

                return _isRegistered && _isEnabled && _isApplied;
            }
        }

        public void ApplyInitial()
        {
            if (_isApplied)
                return;

            RegisterRichText();
            _isEnabled = true;
            _isApplied = false;
            WriteRuntimeVars();
            ApplyOperations();
            _isApplied = true;
            RefreshUseBlockedState();
            WriteRuntimeVars();
            ExecuteHook(StatusEffectHookKind.Apply, _scope);
        }

        public void Enable()
        {
            if (!_isRegistered || _isEnabled)
                return;

            _isEnabled = true;
            _isApplied = false;
            WriteRuntimeVars();
            ApplyOperations();
            _isApplied = true;

            RefreshUseBlockedState();
            WriteRuntimeVars();
            ExecuteHook(StatusEffectHookKind.Enable, _scope);
        }

        public void Disable(bool runHook = true)
        {
            if (!_isRegistered || !_isEnabled)
                return;

            RemoveOperations(permanent: false);
            _isEnabled = false;
            _isApplied = false;
            WriteRuntimeVars();

            if (runHook)
                ExecuteHook(StatusEffectHookKind.Disable, _scope);
        }

        public void SuspendForScopeRelease()
        {
            if (!_isRegistered)
                return;

            if (_isApplied)
                RemoveOperations(permanent: false);

            _isApplied = false;
            _isSuspendedByScopeRelease = true;
            WriteRuntimeVars();
        }

        public void ResumeFromScopeAcquire()
        {
            if (!_isRegistered || !_isSuspendedByScopeRelease)
                return;

            _isSuspendedByScopeRelease = false;
            RegisterRichText();
            if (_isEnabled)
            {
                _isApplied = false;
                WriteRuntimeVars();
                ApplyOperations();
                _isApplied = true;
            }

            RefreshUseBlockedState();
            WriteRuntimeVars();
        }

        public void Remove()
        {
            if (!_isRegistered)
                return;

            RemoveOperations(permanent: true);
            _isEnabled = false;
            _isApplied = false;
            _isRegistered = false;
            _isSuspendedByScopeRelease = false;
            _isRemoveRequested = false;
            WriteRuntimeVars();
            ExecuteRemoveHookAndFinalize();
        }

        public void RequestRemove()
        {
            _isRemoveRequested = true;
        }

        public bool Use(IScopeNode? userScope, CommandContext? sourceContext = null)
        {
            RefreshUseBlockedState();
            if (!_isRegistered || _isUseBlocked)
                return false;

            if (_countController != null && !_countController.ConsumeUse())
            {
                EvaluateLocalCountExhaustion();
                WriteRuntimeVars();
                return false;
            }

            ExecuteHook(StatusEffectHookKind.Use, userScope ?? _scope, sourceContext);

            if (_countController != null)
            {
                if (!_countController.CanUse)
                    EvaluateLocalCountExhaustion();
            }

            _useCooldownController?.Start();

            RefreshUseBlockedState();
            WriteRuntimeVars();
            return true;
        }

        public bool CanUseViaGlobalRequest()
        {
            RefreshUseBlockedState();
            if (!_isRegistered || !UsesAnyServiceGlobalUseState || _isUseBlocked)
                return false;

            return CanUseLocalControllers();
        }

        public bool UseViaGlobal(IScopeNode? userScope, CommandContext? sourceContext = null)
        {
            if (!_isRegistered || !UsesAnyServiceGlobalUseState || !CanUseLocalControllers())
                return false;

            if (_countController != null && !_countController.ConsumeUse())
            {
                EvaluateLocalCountExhaustion();
                WriteRuntimeVars();
                return false;
            }

            ExecuteHook(StatusEffectHookKind.Use, userScope ?? _scope, sourceContext);

            if (_countController != null && !_countController.CanUse)
                EvaluateLocalCountExhaustion();

            _useCooldownController?.Start();

            RefreshUseBlockedState();
            WriteRuntimeVars();
            return true;
        }

        bool CanUseLocalControllers()
        {
            if (_countController != null && !_countController.CanUse)
                return false;

            if (_useCooldownController != null && !_useCooldownController.CanUse)
                return false;

            return true;
        }

        public void RefreshFromServiceGlobalState(bool applyActions)
        {
            if (!_isRegistered)
                return;

            if (UsesServiceGlobalLifetime)
            {
                if (!applyActions || !_owner.IsGlobalLifetimeExpired)
                {
                    _hasHandledGlobalLifetimeExpiration = false;
                }
                else if (!_hasHandledGlobalLifetimeExpiration)
                {
                    _hasHandledGlobalLifetimeExpiration = true;
                    ApplyLifetimeExpiredAction(_durationDefinition?.EndAction ?? EffectLifetimeEndAction.Remove);
                }
            }

            if (UsesServiceGlobalCount)
            {
                if (!_owner.IsGlobalCountExhausted)
                {
                    _hasHandledGlobalCountExhaustion = false;
                }
                else if (applyActions && !_hasHandledGlobalCountExhaustion)
                {
                    _hasHandledGlobalCountExhaustion = true;
                    ApplyCountExhaustedAction(_countDefinition?.ExhaustedAction ?? EffectCountExhaustedAction.Disable);
                }
            }

            RefreshUseBlockedState();
            WriteRuntimeVars();
        }

        public void Tick(float deltaTime)
        {
            if (!_isRegistered)
                return;

            if (_durationController != null)
            {
                _durationController.Tick(deltaTime);
                if (_durationController.IsExpired)
                    HandleDurationExpired();
            }

            _useCooldownController?.Tick(deltaTime);

            RefreshUseBlockedState();
            WriteRuntimeVars();
        }

        public void ResetRuntime()
        {
            if (!_isRegistered)
                return;

            _durationController?.Reset();
            _countController?.Reset();
            _useCooldownController?.Reset();
            _isSuspendedByScopeRelease = false;
            _hasHandledGlobalLifetimeExpiration = false;
            _hasHandledGlobalCountExhaustion = false;
            _hasHandledLocalCountExhaustion = false;
            RemoveOperations(permanent: false);
            _isEnabled = true;
            _isApplied = false;
            ApplyInitial();
            RefreshFromServiceGlobalState(applyActions: true);
        }

        public void ApplyMutations(StatusEffectHookMutationSet? mutations)
        {
            _hooks.ApplyMutations(mutations, _owner.MutationService);
        }

        public void ApplyStack(StatusEffectApplyRequest request, StatusEffectBuildContext context)
        {
            var preset = context.ResolvedStackPreset;
            if (preset == null)
                return;

            _runtimeStackPreset = preset;

            if (preset.IgnoreIfExisting)
                return;

            bool intensityChanged = false;
            bool durationChanged = false;
            bool stackCountChanged = false;

            for (int i = 0; i < StatusEffectIntensitySlotUtility.OrderedSlots.Length; i++)
            {
                var slot = StatusEffectIntensitySlotUtility.OrderedSlots[i];
                if (!preset.ShouldApplyIntensity(slot))
                    continue;

                var intensity = _intensities.Get(slot);
                var changed = ApplyFloatRule(
                    context,
                    preset.GetIntensityRule(slot),
                    context.ResolvedIntensities.Get(slot),
                    ref intensity);
                if (!changed)
                    continue;

                _intensities.Set(slot, intensity);
                intensityChanged = true;
            }

            if (preset.ApplyDuration)
                durationChanged = ApplyDurationRule(request, context, preset.Duration);

            int stackCount = StackCount;
            if (preset.ApplyCurrentCount)
            {
                stackCountChanged = ApplyIntRule(
                    context,
                    preset.CurrentCount,
                    1,
                    ref stackCount,
                    minValue: 1);
            }
            if (stackCountChanged)
                StackCount = stackCount;

            if (preset.ApplyMaxCount)
                ApplyMaxCountRule(context, preset.MaxCount);

            EvaluateLocalCountExhaustion();
            RefreshFromServiceGlobalState(applyActions: true);

            if (intensityChanged)
            {
                RefreshOperationsIfNeeded();
                ExecuteHook(StatusEffectHookKind.StackIntensity, _scope);
            }

            if (durationChanged)
                ExecuteHook(StatusEffectHookKind.StackDuration, _scope);

            WriteRuntimeVars();
        }

        public EffectState ToState()
        {
            return new EffectState(
                DefinitionId,
                InstanceId,
                RuntimeTag,
                DisplayName,
                _nameKey,
                _descriptionKey,
                Icon,
                Type,
                RemainingDuration,
                TotalDuration,
                RemainingUseCooldown,
                _intensities.A,
                _intensities.B,
                _intensities.C,
                _intensities.D,
                _intensities.E,
                _intensities.F,
                _intensities.G,
                StackCount,
                _isEnabled,
                _isApplied,
                IsActive,
                _isUseBlocked,
                UsedCount,
                RemainingUseCount,
                MaxUseCount);
        }

        void HandleDurationExpired()
        {
            ApplyLifetimeExpiredAction(_durationController?.EndAction ?? _durationDefinition?.EndAction ?? EffectLifetimeEndAction.Remove);
        }

        void EvaluateLocalCountExhaustion()
        {
            if (_countController == null)
                return;

            if (_countController.CanUse)
            {
                _hasHandledLocalCountExhaustion = false;
                return;
            }

            if (_hasHandledLocalCountExhaustion)
                return;

            _hasHandledLocalCountExhaustion = true;
            ApplyCountExhaustedAction(_countController.ExhaustedAction);
        }

        void ApplyLifetimeExpiredAction(EffectLifetimeEndAction action)
        {
            switch (action)
            {
                case EffectLifetimeEndAction.Disable:
                    Disable();
                    break;

                case EffectLifetimeEndAction.Remove:
                    RequestRemove();
                    break;
            }
        }

        void ApplyCountExhaustedAction(EffectCountExhaustedAction action)
        {
            switch (action)
            {
                case EffectCountExhaustedAction.Remove:
                    RequestRemove();
                    break;

                case EffectCountExhaustedAction.Disable:
                    Disable();
                    break;

                case EffectCountExhaustedAction.DisableUseOnly:
                    break;
            }
        }

        bool ApplyDurationRule(StatusEffectApplyRequest request, StatusEffectBuildContext context, StatusEffectStackRule? rule)
        {
            if (_durationController == null || rule == null)
                return false;

            float localFallback = request.OverrideDuration
                ? request.DurationOverride.GetOrDefault(context, _durationController.TotalDuration)
                : _durationController.TotalDuration;

            var changed = false;
            if (rule.ApplyLocalValue)
            {
                var local = rule.LocalValue.HasSource
                    ? rule.LocalValue.GetOrDefault(context, localFallback)
                    : localFallback;
                changed = _durationController.ApplyStack(local, rule.Operation);
            }

            if (!rule.ApplyGlobalValue)
                return changed;

            var global = rule.GlobalValue.HasSource
                ? rule.GlobalValue.GetOrDefault(context, 0f)
                : 0f;
            if (rule.IgnoreGlobalWhenMinusOne && Mathf.Approximately(global, -1f))
                return changed;

            return _durationController.ApplyStack(global, rule.Operation) || changed;
        }

        bool ApplyFloatRule(
            IDynamicContext context,
            StatusEffectStackRule? rule,
            float localFallback,
            ref float currentValue)
        {
            if (rule == null)
                return false;

            var before = currentValue;

            if (rule.ApplyLocalValue)
            {
                var local = rule.LocalValue.HasSource
                    ? rule.LocalValue.GetOrDefault(context, localFallback)
                    : localFallback;
                currentValue = ApplyOperation(currentValue, local, rule.Operation);
            }

            if (rule.ApplyGlobalValue)
            {
                var global = rule.GlobalValue.HasSource
                    ? rule.GlobalValue.GetOrDefault(context, 0f)
                    : 0f;
                if (!(rule.IgnoreGlobalWhenMinusOne && Mathf.Approximately(global, -1f)))
                    currentValue = ApplyOperation(currentValue, global, rule.Operation);
            }

            return !Mathf.Approximately(before, currentValue);
        }

        bool ApplyIntRule(
            IDynamicContext context,
            StatusEffectStackRule? rule,
            int localFallback,
            ref int currentValue,
            int minValue)
        {
            if (rule == null)
                return false;

            var before = currentValue;

            if (rule.ApplyLocalValue)
            {
                var local = rule.LocalValue.HasSource
                    ? rule.LocalValue.GetOrDefault(context, localFallback)
                    : localFallback;
                currentValue = ApplyOperationInt(currentValue, local, rule.Operation, minValue);
            }

            if (rule.ApplyGlobalValue)
            {
                var global = rule.GlobalValue.HasSource
                    ? rule.GlobalValue.GetOrDefault(context, 0f)
                    : 0f;
                if (!(rule.IgnoreGlobalWhenMinusOne && Mathf.Approximately(global, -1f)))
                    currentValue = ApplyOperationInt(currentValue, global, rule.Operation, minValue);
            }

            return before != currentValue;
        }

        bool ApplyMaxCountRule(IDynamicContext context, StatusEffectStackRule? rule)
        {
            if (_countController == null || rule == null)
                return false;

            var changed = false;
            if (rule.ApplyLocalValue)
            {
                var local = rule.LocalValue.HasSource
                    ? rule.LocalValue.GetOrDefault(context, _countController.MaxCount)
                    : _countController.MaxCount;
                changed = _countController.ApplyMaxCountStack(Mathf.RoundToInt(local), rule.Operation);
            }

            if (!rule.ApplyGlobalValue)
            {
                if (_countController.CanUse)
                    _hasHandledLocalCountExhaustion = false;
                return changed;
            }

            var global = rule.GlobalValue.HasSource
                ? rule.GlobalValue.GetOrDefault(context, 0f)
                : 0f;
            if (rule.IgnoreGlobalWhenMinusOne && Mathf.Approximately(global, -1f))
            {
                if (_countController.CanUse)
                    _hasHandledLocalCountExhaustion = false;
                return changed;
            }

            changed = _countController.ApplyMaxCountStack(Mathf.RoundToInt(global), rule.Operation) || changed;
            if (_countController.CanUse)
                _hasHandledLocalCountExhaustion = false;
            return changed;
        }

        static float ApplyOperation(float current, float value, StatusEffectStackOperation operation)
        {
            return operation switch
            {
                StatusEffectStackOperation.Set => value,
                StatusEffectStackOperation.Add => current + value,
                StatusEffectStackOperation.Mul => current * value,
                _ => current,
            };
        }

        static int ApplyOperationInt(int current, float value, StatusEffectStackOperation operation, int minValue)
        {
            int next = operation switch
            {
                StatusEffectStackOperation.Set => Mathf.RoundToInt(value),
                StatusEffectStackOperation.Add => current + Mathf.RoundToInt(value),
                StatusEffectStackOperation.Mul => Mathf.RoundToInt(current * value),
                _ => current,
            };

            return Mathf.Max(minValue, next);
        }

        void RefreshOperationsIfNeeded()
        {
            if (!_isEnabled)
                return;

            RefreshUseBlockedState();
            WriteRuntimeVars();
            for (int i = 0; i < _operations.Count; i++)
                _operations[i]?.RefreshValue();
            _isApplied = true;
        }

        StatusEffectActivePolicy ResolveActivePolicy()
        {
            if (_countController != null)
                return _countController.ActivePolicy;

            if (_countDefinition != null)
                return _countDefinition.ActivePolicy;

            return StatusEffectActivePolicy.EnabledOnly;
        }

        void RefreshUseBlockedState()
        {
            bool isBlocked = false;

            if (_countController != null && !_countController.CanUse && ShouldBlockFromCountExhaustion(_countController.ExhaustedAction))
                isBlocked = true;

            if (UsesServiceGlobalCount && _owner.IsGlobalCountExhausted && ShouldBlockFromCountExhaustion(_countDefinition?.ExhaustedAction ?? EffectCountExhaustedAction.Disable))
                isBlocked = true;

            if (_useCooldownController != null && !_useCooldownController.CanUse)
                isBlocked = true;

            if (UsesServiceGlobalUseCooldown && _owner.IsGlobalUseCooldownActive)
                isBlocked = true;

            _isUseBlocked = isBlocked;
        }

        static bool ShouldBlockFromCountExhaustion(EffectCountExhaustedAction action)
            => action == EffectCountExhaustedAction.Disable || action == EffectCountExhaustedAction.DisableUseOnly;

        void ApplyOperations()
        {
            for (int i = 0; i < _operations.Count; i++)
                _operations[i]?.Apply();
        }

        void RemoveOperations(bool permanent)
        {
            for (int i = 0; i < _operations.Count; i++)
            {
                if (permanent)
                    _operations[i]?.Remove();
                else
                    _operations[i]?.Disable();
            }
        }

        void RegisterRichText()
        {
            var richText = _owner.RichTextRefService;
            if (richText == null)
                return;

            var baseKey = $"StatusEffect:{DefinitionId}:{InstanceId}";
            _descriptionKey = baseKey;
            _nameKey = $"{baseKey}:name";

            TryRegisterTemplate(richText, _descriptionKey, _visualData?.Description);
            TryRegisterTemplate(richText, _nameKey, _visualData?.DisplayName);
        }

        void UnregisterRichText()
        {
            var richText = _owner.RichTextRefService;
            if (richText == null)
                return;

            if (!string.IsNullOrEmpty(_descriptionKey))
                richText.TryUnregister(_descriptionKey);
            if (!string.IsNullOrEmpty(_nameKey))
                richText.TryUnregister(_nameKey);

            _descriptionKey = string.Empty;
            _nameKey = string.Empty;
        }

        static void TryRegisterTemplate(IRichTextRefService richText, string key, RichTextTemplateData? template)
        {
            if (string.IsNullOrWhiteSpace(key) || template == null || string.IsNullOrWhiteSpace(template.Template))
                return;

            var source = new RichTextSource
            {
                Template = template.Template
            };
            source.SetExternalVariables(template.Variables, includeLocalVariables: false);
            richText.TryRegister(key, new RichTextProvider(source), overwrite: true);
        }

        void ExecuteHook(StatusEffectHookKind kind, IScopeNode actorScope, CommandContext? sourceContext = null)
        {
            var runner = _owner.CommandRunner;
            if (_scope == null || runner == null)
                return;

            var commands = _hooks.Resolve(kind);
            if (commands == null || commands.Count == 0)
                return;

            WriteRuntimeVars();
            var context = sourceContext != null
                ? new CommandContext(
                    _scope,
                    _vars,
                    runner,
                    actorScope,
                    sourceContext.Options,
                    sourceContext.CommandRootScope,
                    sourceContext.RootActor,
                    sourceContext.Actor,
                    sourceContext)
                : new CommandContext(_scope, _vars, runner, actorScope, CommandRunOptions.Default);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, context, CancellationToken.None, context.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }

        void ExecuteRemoveHookAndFinalize()
        {
            var runner = _owner.CommandRunner;
            if (_scope == null || runner == null)
            {
                UnregisterRichText();
                return;
            }

            var commands = _hooks.Resolve(StatusEffectHookKind.Remove);
            if (commands == null || commands.Count == 0)
            {
                UnregisterRichText();
                return;
            }

            var context = new CommandContext(_scope, _vars, runner, _scope, CommandRunOptions.Default);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, context, CancellationToken.None, context.Options);
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
                    UnregisterRichText();
                }
            });
        }

        void WriteRuntimeVars()
        {
            WriteRuntimeGlobalVars();
            RefreshUseBlockedState();

            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectId, DynamicVariant.FromString(DefinitionId));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.instanceId, DynamicVariant.FromString(InstanceId));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.runtimeTag, DynamicVariant.FromString(RuntimeTag));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectType, DynamicVariant.FromInt((int)Type));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isEnabled, DynamicVariant.FromBool(_isEnabled));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isApplied, DynamicVariant.FromBool(_isApplied));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isActive, DynamicVariant.FromBool(IsActive));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isUseBlocked, DynamicVariant.FromBool(_isUseBlocked));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.stackCount, DynamicVariant.FromInt(StackCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityA, DynamicVariant.FromFloat(_intensities.A));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityB, DynamicVariant.FromFloat(_intensities.B));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityC, DynamicVariant.FromFloat(_intensities.C));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityD, DynamicVariant.FromFloat(_intensities.D));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityE, DynamicVariant.FromFloat(_intensities.E));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityF, DynamicVariant.FromFloat(_intensities.F));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensityG, DynamicVariant.FromFloat(_intensities.G));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.usedCount, DynamicVariant.FromInt(UsedCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingDuration, DynamicVariant.FromFloat(RemainingDuration));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.totalDuration, DynamicVariant.FromFloat(TotalDuration));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingInverseInterval, DynamicVariant.FromFloat(RemainingUseCooldown));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingUseCount, DynamicVariant.FromInt(RemainingUseCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.maxUseCount, DynamicVariant.FromInt(MaxUseCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.nameTemplate, DynamicVariant.FromString(_visualData?.DisplayName?.Template ?? string.Empty));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.descriptionTemplate, DynamicVariant.FromString(_visualData?.Description?.Template ?? string.Empty));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.nameKey, DynamicVariant.FromString(_nameKey));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.descriptionKey, DynamicVariant.FromString(_descriptionKey));
            var visualData = (object?)_visualData;
            if (visualData != null)
                _vars.TrySetManagedRef(VarIds.GameLib.Base.StatusEffect.Runtime.Element.visualData, visualData);
        }

        void WriteRuntimeGlobalVars()
        {
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.lifetimeRemaining, DynamicVariant.FromFloat(_owner.GlobalLifetimeRemaining));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.lifetimeTotal, DynamicVariant.FromFloat(_owner.GlobalLifetimeTotal));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.cooldownRemaining, DynamicVariant.FromFloat(_owner.GlobalCooldownRemaining));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.cooldownMax, DynamicVariant.FromFloat(_owner.GlobalCooldownMax));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.currentCount, DynamicVariant.FromInt(_owner.GlobalCurrentCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.maxCount, DynamicVariant.FromInt(_owner.GlobalMaxCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Global.canUse, DynamicVariant.FromBool(_owner.GlobalCanUse));
        }
    }
}
