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
        public IScopeNode? CommandRootScope { get; }
        public StatusEffectApplyRequest Request { get; }
        public BaseStatusEffectDefinitionData Definition { get; }
        public string InstanceId { get; }
        public string RuntimeTag { get; }
        public float ResolvedIntensity { get; }

        public IScopeNode Scope => OwnerScope;

        public StatusEffectBuildContext(
            IScopeNode ownerScope,
            IVarStore vars,
            IScopeNode? commandRootScope,
            StatusEffectApplyRequest request,
            BaseStatusEffectDefinitionData definition,
            string instanceId,
            string runtimeTag,
            float resolvedIntensity)
        {
            OwnerScope = ownerScope;
            Vars = vars ?? NullVarStore.Instance;
            CommandRootScope = commandRootScope;
            Request = request;
            Definition = definition;
            InstanceId = instanceId ?? string.Empty;
            RuntimeTag = runtimeTag ?? string.Empty;
            ResolvedIntensity = resolvedIntensity;
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

        readonly VarStore _vars = new();

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

            InstanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
            RuntimeTag = runtimeTag ?? string.Empty;
            Intensity = intensity;
            StackCount = 1;

            if (_countController != null)
                _inverseIntervalAction = _countController.InverseIntervalAction;
        }

        public string DefinitionId => _definition.DefinitionId ?? string.Empty;
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
            ApplyOperations();
            _isEnabled = true;
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
            if (_isEnabled && !_isUseBlocked)
            {
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

        public bool Use(IScopeNode? userScope)
        {
            if (!_isRegistered || _isUseBlocked)
                return false;

            if (_countController != null && !_countController.ConsumeUse())
            {
                HandleCountExhausted();
                WriteRuntimeVars();
                return false;
            }

            ExecuteHook(StatusEffectHookKind.Use, userScope ?? _scope);

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
            bool intensityChanged = false;
            bool durationChanged = false;

            switch (request.StackMode)
            {
                case EffectStackMode.Ignore:
                    return;

                case EffectStackMode.Refresh:
                    durationChanged = ApplyDurationStack(request, context, refreshOnly: true);
                    break;

                case EffectStackMode.ExtendDuration:
                    durationChanged = ApplyDurationStack(request, context, refreshOnly: false);
                    break;

                case EffectStackMode.StackIntensity:
                    intensityChanged = ApplyIntensityStack(context.ResolvedIntensity, replace: false);
                    break;

                case EffectStackMode.StackBoth:
                    intensityChanged = ApplyIntensityStack(context.ResolvedIntensity, replace: false);
                    durationChanged = ApplyDurationStack(request, context, refreshOnly: false);
                    break;

                case EffectStackMode.Replace:
                    intensityChanged = ApplyIntensityStack(context.ResolvedIntensity, replace: true);
                    durationChanged = ApplyDurationStack(request, context, refreshOnly: true);
                    break;
            }

            if (intensityChanged)
            {
                ReapplyOperationsIfNeeded();
                ExecuteHook(StatusEffectHookKind.StackIntensity, _scope);
            }

            if (durationChanged)
                ExecuteHook(StatusEffectHookKind.StackDuration, _scope);

            if (intensityChanged || durationChanged)
                StackCount++;

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

        bool ApplyDurationStack(StatusEffectApplyRequest request, StatusEffectBuildContext context, bool refreshOnly)
        {
            if (_durationController == null)
                return false;

            float duration = request.OverrideDuration
                ? request.DurationOverride.GetOrDefault(context, _durationController.TotalDuration)
                : _durationController.TotalDuration;
            var mode = refreshOnly ? EffectStackMode.Refresh : request.StackMode;
            return _durationController.ApplyStack(duration, mode);
        }

        bool ApplyIntensityStack(float incomingIntensity, bool replace)
        {
            if (replace)
            {
                if (Mathf.Approximately(Intensity, incomingIntensity))
                    return false;

                Intensity = incomingIntensity;
                return true;
            }

            Intensity += incomingIntensity;
            return true;
        }

        void ReapplyOperationsIfNeeded()
        {
            if (!_isEnabled)
                return;

            RemoveOperations(permanent: false);
            ApplyOperations();
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

        void ExecuteHook(StatusEffectHookKind kind, IScopeNode actorScope)
        {
            var runner = _owner.CommandRunner;
            if (_scope == null || runner == null)
                return;

            var commands = _hooks.Resolve(kind);
            if (commands == null || commands.Count == 0)
                return;

            WriteRuntimeVars();
            var context = new CommandContext(_scope, _vars, runner, actorScope, CommandRunOptions.Default);
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
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.effectId, DynamicVariant.FromString(DefinitionId));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.instanceId, DynamicVariant.FromString(InstanceId));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.runtimeTag, DynamicVariant.FromString(RuntimeTag));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.effectType, DynamicVariant.FromInt((int)Type));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.isEnabled, DynamicVariant.FromBool(_isEnabled));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.isApplied, DynamicVariant.FromBool(_isApplied));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.isActive, DynamicVariant.FromBool(IsActive));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.isUseBlocked, DynamicVariant.FromBool(_isUseBlocked));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.stackCount, DynamicVariant.FromInt(StackCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.intensity, DynamicVariant.FromFloat(Intensity));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.usedCount, DynamicVariant.FromInt(UsedCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.remainingDuration, DynamicVariant.FromFloat(RemainingDuration));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.totalDuration, DynamicVariant.FromFloat(TotalDuration));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.remainingInverseInterval, DynamicVariant.FromFloat(RemainingInverseInterval));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.remainingUseCount, DynamicVariant.FromInt(RemainingUseCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.maxUseCount, DynamicVariant.FromInt(MaxUseCount));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.nameTemplate, DynamicVariant.FromString(_visualData?.DisplayName?.Template ?? string.Empty));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.descriptionTemplate, DynamicVariant.FromString(_visualData?.Description?.Template ?? string.Empty));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.nameKey, DynamicVariant.FromString(_nameKey));
            _vars.TrySetVariant(VarIds.GameLib.Base.StatusEffect.Element.descriptionKey, DynamicVariant.FromString(_descriptionKey));
            var visualData = (object?)_visualData;
            if (visualData != null)
                _vars.TrySetManagedRef(VarIds.GameLib.Base.StatusEffect.Element.visualData, visualData);
        }
    }
}
