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
        public float ResolvedIntensity { get; }
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
            float resolvedIntensity,
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
            ResolvedIntensity = resolvedIntensity;
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
        readonly IStatusEffectDurationController? _durationController;
        readonly IStatusEffectCountController? _countController;
        readonly string _slotKey;
        readonly StatusEffectOperationDynamicContext _globalEvaluationContext;

        readonly VarStore _vars;

        StatusEffectStackPreset _runtimeStackPreset;
        float _lastGlobalIntensityValue;
        float _lastGlobalDurationValue;
        float _lastGlobalCurrentCountValue;
        float _lastGlobalMaxCountValue;
        bool _hasLastGlobalIntensityValue;
        bool _hasLastGlobalDurationValue;
        bool _hasLastGlobalCurrentCountValue;
        bool _hasLastGlobalMaxCountValue;

        bool _isRemoveRequested;
        bool _isEnabled = true;
        bool _isApplied;
        bool _isRegistered = true;
        bool _isSuspendedByScopeRelease;
        bool _isUseBlocked;
        float _inverseIntervalRemaining;
        StatusEffectInverseIntervalAction _inverseIntervalAction = StatusEffectInverseIntervalAction.None;
        string _nameKey = string.Empty;
        string _descriptionKey = string.Empty;

        public StatusEffectRuntime(
            StatusEffectService owner,
            IScopeNode scope,
            BaseStatusEffectDefinitionData definition,
            string instanceId,
            string runtimeTag,
            string slotKey,
            float intensity,
            StatusEffectStackPreset? runtimeStackPreset,
            VarStore vars,
            List<IStatusEffectOperationRuntime> operations,
            StatusEffectHookSet hooks,
            IStatusEffectDurationController? durationController,
            IStatusEffectCountController? countController)
        {
            _owner = owner;
            _scope = scope;
            _definition = definition;
            _visualData = definition.VisualData ?? new EffectVisualData();
            _operations = operations ?? new List<IStatusEffectOperationRuntime>();
            _hooks = hooks ?? new StatusEffectHookSet();
            _durationController = durationController;
            _countController = countController;
            _slotKey = slotKey ?? string.Empty;
            _vars = vars ?? new VarStore();
            _runtimeStackPreset = runtimeStackPreset ?? StatusEffectStackPreset.CreateDurationRefreshPreset();
            _globalEvaluationContext = new StatusEffectOperationDynamicContext(_vars, _scope, _scope);

            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
            RuntimeTag = runtimeTag ?? string.Empty;
            Intensity = intensity;
            StackCount = 1;

            if (_countController != null)
                _inverseIntervalAction = _countController.InverseIntervalAction;
        }

        public string DefinitionId => _definition.DefinitionId ?? string.Empty;
        public BaseStatusEffectDefinitionData Definition => _definition;
        public string InstanceId { get; }
        public string RuntimeTag { get; }
        public string SlotKey => _slotKey;
        public float Intensity { get; private set; }
        public int StackCount { get; private set; }
        public bool IsRegistered => _isRegistered;
        public bool IsEnabled => _isEnabled;
        public bool IsApplied => _isApplied;
        public bool IsRemoveRequested => _isRemoveRequested;
        public bool IsUseBlocked => _isUseBlocked;
        public float RemainingDuration => _durationController?.RemainingDuration ?? -1f;
        public float TotalDuration => _durationController?.TotalDuration ?? -1f;
        public int MaxUseCount => _countController?.MaxCount ?? 0;
        public int UsedCount => _countController?.UsedCount ?? 0;
        public int RemainingUseCount => _countController?.RemainingCount ?? -1;
        public float RemainingInverseInterval => _inverseIntervalRemaining;
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
                if (_countController != null && _countController.ActivePolicy == StatusEffectActivePolicy.RegisteredEvenIfDisabled)
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
            WriteRuntimeVars();
            ExecuteHook(StatusEffectHookKind.Apply, _scope);
        }

        public void Enable()
        {
            if (!_isRegistered || _isEnabled)
                return;

            _isEnabled = true;
            if (!_isUseBlocked)
            {
                _isApplied = false;
                WriteRuntimeVars();
                ApplyOperations();
                _isApplied = true;
            }

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
            if (_isEnabled && !_isUseBlocked)
            {
                _isApplied = false;
                WriteRuntimeVars();
                ApplyOperations();
                _isApplied = true;
            }

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
            if (!_isRegistered || _isUseBlocked)
                return false;

            if (_countController != null && !_countController.ConsumeUse())
            {
                HandleCountExhausted();
                WriteRuntimeVars();
                return false;
            }

            ExecuteHook(StatusEffectHookKind.Use, userScope ?? _scope, sourceContext);

            if (_countController != null)
            {
                if (!_countController.CanUse)
                    HandleCountExhausted();

                if (_countController.InverseIntervalDuration > 0f)
                    StartInverseInterval();
            }

            WriteRuntimeVars();
            return true;
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

            if (_inverseIntervalRemaining > 0f)
            {
                _inverseIntervalRemaining -= Mathf.Max(0f, deltaTime);
                if (_inverseIntervalRemaining <= 0f)
                    CompleteInverseInterval();
            }

            WriteRuntimeVars();
        }

        public void ResetRuntime()
        {
            if (!_isRegistered)
                return;

            _durationController?.Reset();
            _countController?.Reset();
            _inverseIntervalRemaining = 0f;
            _isUseBlocked = false;
            _isSuspendedByScopeRelease = false;
            RemoveOperations(permanent: false);
            _isEnabled = true;
            _isApplied = false;
            ApplyInitial();
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

            if (preset.ApplyIntensity)
            {
                var intensity = Intensity;
                intensityChanged = ApplyFloatRule(
                    context,
                    preset.Intensity,
                    context.ResolvedIntensity,
                    ref intensity);
                if (intensityChanged)
                    Intensity = intensity;
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
                RemainingInverseInterval,
                Intensity,
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
            switch (_durationController?.EndAction)
            {
                case EffectLifetimeEndAction.Disable:
                    Disable();
                    break;

                case EffectLifetimeEndAction.Remove:
                    RequestRemove();
                    break;
            }
        }

        void HandleCountExhausted()
        {
            switch (_countController?.ExhaustedAction)
            {
                case EffectCountExhaustedAction.Remove:
                    RequestRemove();
                    break;

                case EffectCountExhaustedAction.Disable:
                    Disable();
                    _isUseBlocked = true;
                    break;

                case EffectCountExhaustedAction.DisableUseOnly:
                    _isUseBlocked = true;
                    break;
            }
        }

        void StartInverseInterval()
        {
            if (_countController == null || _countController.InverseIntervalDuration <= 0f)
                return;

            _inverseIntervalRemaining = _countController.InverseIntervalDuration;
            _inverseIntervalAction = _countController.InverseIntervalAction;

            switch (_inverseIntervalAction)
            {
                case StatusEffectInverseIntervalAction.Disable:
                    _isUseBlocked = true;
                    Disable(runHook: false);
                    break;

                case StatusEffectInverseIntervalAction.BlockUseOnly:
                    _isUseBlocked = true;
                    break;
            }
        }

        void CompleteInverseInterval()
        {
            _inverseIntervalRemaining = 0f;

            switch (_inverseIntervalAction)
            {
                case StatusEffectInverseIntervalAction.Disable:
                    _isUseBlocked = false;
                    if (_isRegistered)
                        Enable();
                    break;

                case StatusEffectInverseIntervalAction.BlockUseOnly:
                    _isUseBlocked = false;
                    WriteRuntimeVars();
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

            var local = rule.LocalValue.HasSource
                ? rule.LocalValue.GetOrDefault(context, localFallback)
                : localFallback;

            var changed = _durationController.ApplyStack(local, rule.Operation);

            if (!rule.UseGlobalValue)
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

            var local = rule.LocalValue.HasSource
                ? rule.LocalValue.GetOrDefault(context, localFallback)
                : localFallback;
            currentValue = ApplyOperation(currentValue, local, rule.Operation);

            if (rule.UseGlobalValue)
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

            var local = rule.LocalValue.HasSource
                ? rule.LocalValue.GetOrDefault(context, localFallback)
                : localFallback;
            currentValue = ApplyOperationInt(currentValue, local, rule.Operation, minValue);

            if (rule.UseGlobalValue)
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

            var local = rule.LocalValue.HasSource
                ? rule.LocalValue.GetOrDefault(context, _countController.MaxCount)
                : _countController.MaxCount;
            var changed = _countController.ApplyMaxCountStack(Mathf.RoundToInt(local), rule.Operation);

            if (!rule.UseGlobalValue)
                return changed;

            var global = rule.GlobalValue.HasSource
                ? rule.GlobalValue.GetOrDefault(context, 0f)
                : 0f;
            if (rule.IgnoreGlobalWhenMinusOne && Mathf.Approximately(global, -1f))
                return changed;

            return _countController.ApplyMaxCountStack(Mathf.RoundToInt(global), rule.Operation) || changed;
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

            WriteRuntimeVars();
            for (int i = 0; i < _operations.Count; i++)
                _operations[i]?.RefreshValue();
            _isApplied = true;
        }

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

            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectId, DynamicVariant.FromString(DefinitionId));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.instanceId, DynamicVariant.FromString(InstanceId));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.runtimeTag, DynamicVariant.FromString(RuntimeTag));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.effectType, DynamicVariant.FromInt((int)Type));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isEnabled, DynamicVariant.FromBool(_isEnabled));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isApplied, DynamicVariant.FromBool(_isApplied));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isActive, DynamicVariant.FromBool(IsActive));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.isUseBlocked, DynamicVariant.FromBool(_isUseBlocked));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.stackCount, DynamicVariant.FromInt(StackCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.intensity, DynamicVariant.FromFloat(Intensity));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.usedCount, DynamicVariant.FromInt(UsedCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingDuration, DynamicVariant.FromFloat(RemainingDuration));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.totalDuration, DynamicVariant.FromFloat(TotalDuration));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Runtime.Element.remainingInverseInterval, DynamicVariant.FromFloat(RemainingInverseInterval));
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
            if (_runtimeStackPreset == null)
                return;

            var intensityValue = EvaluateGlobalRuleValue(_runtimeStackPreset.ApplyIntensity, _runtimeStackPreset.Intensity);
            var durationValue = EvaluateGlobalRuleValue(_runtimeStackPreset.ApplyDuration, _runtimeStackPreset.Duration);
            var currentCountValue = EvaluateGlobalRuleValue(_runtimeStackPreset.ApplyCurrentCount, _runtimeStackPreset.CurrentCount);
            var maxCountValue = EvaluateGlobalRuleValue(_runtimeStackPreset.ApplyMaxCount, _runtimeStackPreset.MaxCount);

            TryWriteRuntimeGlobalFloat(
                VarIds.GameLib.Base.StatusEffect.Runtime.Global.intensity,
                intensityValue,
                ref _lastGlobalIntensityValue,
                ref _hasLastGlobalIntensityValue);

            TryWriteRuntimeGlobalFloat(
                VarIds.GameLib.Base.StatusEffect.Runtime.Global.duration,
                durationValue,
                ref _lastGlobalDurationValue,
                ref _hasLastGlobalDurationValue);

            TryWriteRuntimeGlobalFloat(
                VarIds.GameLib.Base.StatusEffect.Runtime.Global.currentCount,
                currentCountValue,
                ref _lastGlobalCurrentCountValue,
                ref _hasLastGlobalCurrentCountValue);

            TryWriteRuntimeGlobalFloat(
                VarIds.GameLib.Base.StatusEffect.Runtime.Global.maxCount,
                maxCountValue,
                ref _lastGlobalMaxCountValue,
                ref _hasLastGlobalMaxCountValue);
        }

        float EvaluateGlobalRuleValue(bool applyRule, StatusEffectStackRule? rule)
        {
            if (!applyRule || rule == null || !rule.UseGlobalValue)
                return 0f;

            return rule.GlobalValue.HasSource
                ? rule.GlobalValue.GetOrDefault(_globalEvaluationContext, 0f)
                : 0f;
        }

        void TryWriteRuntimeGlobalFloat(
            int varId,
            float value,
            ref float lastValue,
            ref bool hasLastValue)
        {
            if (varId == 0)
                return;

            _vars.TrySetVariant(varId, DynamicVariant.FromFloat(value));

            if (hasLastValue && Mathf.Approximately(lastValue, value))
                return;

            hasLastValue = true;
            lastValue = value;

            var resolver = _scope?.Resolver;
            if (resolver == null)
                return;

            if (!resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard?.LocalVars == null)
                return;

            blackboard.LocalVars.TrySetVariant(varId, DynamicVariant.FromFloat(value));
        }
    }
}
