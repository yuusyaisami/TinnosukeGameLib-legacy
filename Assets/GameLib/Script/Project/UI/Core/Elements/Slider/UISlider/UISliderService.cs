#nullable enable
using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Game.Commands.VNext;
using Game.Common;
using Game.Scalar;

namespace Game.UI
{
    public sealed class UISliderService
        : IUISliderController,
          IUISliderOutput,
          IScopeAcquireHandler,
          IScopeReleaseHandler
    {
        readonly IUISliderValueOptions _options;
        readonly Slider _slider;
        IDynamicContext? _dynamicContext;

        float _rawValue;
        float _normalizedValue;
        bool _isEditing;
        float _editStartRaw;

        float _lastEmittedRaw;
        float _lastEmittedNormalized;
        bool _lastEmittedEditing;
        bool _hasEmitted;

        bool _pendingExternalResync;

        IBaseScalarService? _scalarService;
        IDisposable? _scalarSubscription;
        ActorSourceResolveCache _scalarBindingSourceCache;
        bool _suppressScalarEcho;
        float _lastScalarWrite;

        IVarStore? _blackboardVars;
        int _blackboardVarId;
        ActorSourceResolveCache _blackboardBindingSourceCache;
        bool _suppressVarEcho;
        int _lastVarWriteVersion;

        public event Action<UISliderOutputSnapshot>? OnUpdated;

        public float NormalizedValue => _normalizedValue;
        public float RawValue => _rawValue;
        public bool IsEditing => _isEditing;

        public UISliderService(IUISliderValueOptions options, Slider slider)
        {
            _options = options;
            _slider = slider;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            UnsubscribeExternal();
            ResolveBindings(scope);
            SubscribeExternal();
            InitializeValue();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnsubscribeExternal();
            _dynamicContext = null;
            _isEditing = false;
            _pendingExternalResync = false;
            _suppressScalarEcho = false;
            _lastScalarWrite = 0f;
            _suppressVarEcho = false;
            _lastVarWriteVersion = 0;
            _scalarBindingSourceCache = default;
            _blackboardBindingSourceCache = default;
        }

        public void RequestBeginEdit(UISliderEditMode mode)
        {
            if (!_options.IsEditable) return;
            if (_isEditing) return;

            _isEditing = true;
            _editStartRaw = _rawValue;
            EmitUpdated();
        }

        public void RequestEndEdit(UISliderEndEditReason reason)
        {
            if (!_isEditing) return;

            bool revert = reason == UISliderEndEditReason.Cancel &&
                          _options.CancelBehavior == UISliderCancelBehavior.RevertToStart;

            _isEditing = false;

            if (revert)
                SetRawValue(_editStartRaw, UISliderChangeSource.UserNavigate, allowExternalWrite: true);
            else
                EmitUpdated();

            if (_pendingExternalResync)
            {
                _pendingExternalResync = false;
                SyncFromExternal();
            }
        }

        public void RequestSetNormalized(float normalized, UISliderChangeSource source)
        {
            if (!_isEditing || !_options.IsEditable) return;

            GetValueRange(out var min, out var max);
            float raw = Mathf.Lerp(min, max, Mathf.Clamp01(normalized));
            SetRawValue(raw, source, allowExternalWrite: true);
        }

        public void RequestStep(int step, UISliderChangeSource source)
        {
            if (!_isEditing || !_options.IsEditable) return;
            if (step == 0) return;

            float stepSize = GetStepSize();
            if (stepSize <= 0f) return;

            float raw = _rawValue + step * stepSize;
            SetRawValue(raw, source, allowExternalWrite: true);
        }

        void ResolveBindings(IScopeNode scope)
        {
            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;
            _dynamicContext = null;

            if (scope == null)
                return;

            var resolver = scope.Resolver;

            IVarStore vars = NullVarStore.Instance;
            if (resolver != null &&
                resolver.TryResolve<IVarStore>(out var resolvedVars) &&
                resolvedVars != null)
                vars = resolvedVars;

            _dynamicContext = new SimpleDynamicContext(vars, scope);

            if (_options.UseScalarBinding && _options.ScalarKey.Id != 0)
            {
                if (TryResolveScalarService(scope, out var scalar))
                    _scalarService = scalar;
            }

            if (_options.UseBlackboardBinding)
            {
                _blackboardVarId = ResolveVarId(_options.BlackboardKey);
                if (_blackboardVarId != 0 && TryResolveBlackboardVars(scope, out var blackboardVars))
                    _blackboardVars = blackboardVars;
            }
        }

        void SubscribeExternal()
        {
            if (_scalarService != null && _options.UseScalarBinding && _options.ScalarKey.Id != 0)
                _scalarSubscription = _scalarService.GlobalSubscribe(_options.ScalarKey, HandleScalarChanged);

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged += HandleVarChanged;
        }

        void UnsubscribeExternal()
        {
            _scalarSubscription?.Dispose();
            _scalarSubscription = null;

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged -= HandleVarChanged;

            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;
        }

        bool TryResolveScalarService(IScopeNode scope, out IBaseScalarService? scalarService)
        {
            scalarService = null;

            var targetScope = ActorSourceFastResolver.ResolveCached(
                scope,
                _options.ScalarBindingSource,
                ref _scalarBindingSourceCache);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBaseScalarService>(out var resolved) || resolved == null)
                return false;

            scalarService = resolved;
            return true;
        }

        bool TryResolveBlackboardVars(IScopeNode scope, out IVarStore? blackboardVars)
        {
            blackboardVars = null;

            var targetScope = ActorSourceFastResolver.ResolveCached(
                scope,
                _options.BlackboardBindingSource,
                ref _blackboardBindingSourceCache);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return false;

            blackboardVars = blackboard.LocalVars;
            return blackboardVars != null;
        }

        void InitializeValue()
        {
            _pendingExternalResync = false;
            _hasEmitted = false;

            GetValueRange(out var minValue, out var maxValue);
            _slider.minValue = minValue;
            _slider.maxValue = maxValue;

            if (TryReadExternal(out var externalValue))
            {
                SetRawValue(externalValue, UISliderChangeSource.ExternalBinding, allowExternalWrite: false);
                return;
            }

            SetRawValue(ResolveFloat(_options.InitialValue, 0f), UISliderChangeSource.Initialization, allowExternalWrite: false);
        }

        void SyncFromExternal()
        {
            if (TryReadExternal(out var value))
                SetRawValue(value, UISliderChangeSource.ExternalBinding, allowExternalWrite: false);
        }

        bool TryReadExternal(out float value)
        {
            value = 0f;

            if (_options.BindingPriority == UISliderExternalBindingPriority.Scalar)
            {
                if (TryReadScalar(out value)) return true;
                if (TryReadBlackboard(out value)) return true;
                return false;
            }

            if (TryReadBlackboard(out value)) return true;
            if (TryReadScalar(out value)) return true;
            return false;
        }

        bool TryReadScalar(out float value)
        {
            value = 0f;
            if (_scalarService == null || !_options.UseScalarBinding || _options.ScalarKey.Id == 0)
                return false;

            return _scalarService.GlobalTryGet(_options.ScalarKey, out value);
        }

        bool TryReadBlackboard(out float value)
        {
            value = 0f;
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            if (!_blackboardVars.TryGetVariant(_blackboardVarId, out var variant))
                return false;

            return variant.TryGet(out value);
        }

        void HandleScalarChanged(ScalarValueChangedArgs args)
        {
            if (_suppressScalarEcho)
            {
                if (Mathf.Abs(args.NewValue - _lastScalarWrite) <= Mathf.Max(0f, _options.UpdateEpsilon))
                {
                    _suppressScalarEcho = false;
                    return;
                }
                _suppressScalarEcho = false;
            }

            if (_isEditing)
            {
                _pendingExternalResync = true;
                return;
            }
            //Debug.Log($"[UISliderService] HandleScalarChanged newValue={args.NewValue}");
            SetRawValue(args.NewValue, UISliderChangeSource.ExternalBinding, allowExternalWrite: false);
        }

        void HandleVarChanged(int varId)
        {
            if (_blackboardVars == null || varId != _blackboardVarId)
                return;

            if (_suppressVarEcho)
            {
                if (_blackboardVars.GlobalVersion == _lastVarWriteVersion)
                {
                    _suppressVarEcho = false;
                    return;
                }
                _suppressVarEcho = false;
            }

            if (_isEditing)
            {
                _pendingExternalResync = true;
                return;
            }

            if (_blackboardVars.TryGetVariant(varId, out var variant) && variant.TryGet(out float value))
                SetRawValue(value, UISliderChangeSource.ExternalBinding, allowExternalWrite: false);
        }

        void SetRawValue(float raw, UISliderChangeSource source, bool allowExternalWrite)
        {
            float snapped = ApplySnap(raw);
            float normalized = ComputeNormalized(snapped);

            float epsilon = Mathf.Max(0f, _options.UpdateEpsilon);
            bool changed = !_hasEmitted || Mathf.Abs(snapped - _rawValue) > epsilon;

            _rawValue = snapped;
            _normalizedValue = normalized;

            _slider.SetValueWithoutNotify(_rawValue);

            if (allowExternalWrite && (source == UISliderChangeSource.UserPointer || source == UISliderChangeSource.UserNavigate))
                WriteExternal(snapped);

            if (changed || !_hasEmitted)
                EmitUpdated();
        }

        void WriteExternal(float raw)
        {
            //Debug.Log($"[UISliderService] WriteExternal raw={raw}");
            if (_options.WriteToBothBindings)
            {
                WriteScalar(raw);
                WriteBlackboard(raw);
                return;
            }

            if (_options.BindingPriority == UISliderExternalBindingPriority.Scalar)
            {
                if (!WriteScalar(raw)) WriteBlackboard(raw);
            }
            else
            {
                if (!WriteBlackboard(raw)) WriteScalar(raw);
            }
        }

        bool WriteScalar(float raw)
        {
            if (_scalarService == null || !_options.UseScalarBinding || _options.ScalarKey.Id == 0)
                return false;

            _suppressScalarEcho = true;
            _lastScalarWrite = raw;

            //Debug.Log($"[UISliderService] WriteScalar key={_options.ScalarKey.FormatLabel()} raw={raw}");

            _scalarService.SetGlobalBase(_options.ScalarKey, raw);
            return true;
        }

        bool WriteBlackboard(float raw)
        {
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            int nextVersion = _blackboardVars.GlobalVersion + 1;
            if (_blackboardVars.TrySetVariant(_blackboardVarId, DynamicVariant.FromFloat(raw)))
            {
                _suppressVarEcho = true;
                _lastVarWriteVersion = nextVersion;
                return true;
            }

            _suppressVarEcho = false;
            return false;
        }

        float ApplySnap(float raw)
        {
            GetValueRange(out var min, out var max);
            raw = Mathf.Clamp(raw, min, max);

            float step = Mathf.Max(0f, ResolveFloat(_options.Step, 0f));
            if (step <= 0f)
                return raw;

            if (_options.StepMode == UISliderStepMode.Normalized)
                step *= Mathf.Abs(max - min);

            if (step <= 0f)
                return raw;

            raw = min + Mathf.Round((raw - min) / step) * step;
            return Mathf.Clamp(raw, min, max);
        }

        float ComputeNormalized(float raw)
        {
            GetValueRange(out var min, out var max);
            if (Mathf.Abs(max - min) <= Mathf.Epsilon)
                return 0f;

            return Mathf.Clamp01((raw - min) / (max - min));
        }

        float GetMinValue()
        {
            GetValueRange(out var min, out _);
            return min;
        }

        float GetMaxValue()
        {
            GetValueRange(out _, out var max);
            return max;
        }

        void GetValueRange(out float min, out float max)
        {
            float resolvedMin = ResolveFloat(_options.MinValue, 0f);
            float resolvedMax = ResolveFloat(_options.MaxValue, 1f);
            min = Mathf.Min(resolvedMin, resolvedMax);
            max = Mathf.Max(resolvedMin, resolvedMax);
        }

        float GetStepSize()
        {
            GetValueRange(out var min, out var max);

            float step = Mathf.Max(0f, ResolveFloat(_options.Step, 0f));
            if (step <= 0f)
                return 0f;

            if (_options.StepMode == UISliderStepMode.Normalized)
                step *= Mathf.Abs(max - min);

            return step;
        }

        float ResolveFloat(DynamicValue<float> value, float fallback)
        {
            if (_dynamicContext != null)
                return value.GetOrDefault(_dynamicContext, fallback);

            if (!value.HasSource)
                return value.GetOrDefaultWithoutContext(fallback);

            if (value.TryGetSource<LiteralFloatSource>(out _))
                return value.GetOrDefaultWithoutContext(fallback);

            return fallback;
        }

        void EmitUpdated()
        {
            float epsilon = Mathf.Max(0f, _options.UpdateEpsilon);
            bool sameRaw = _hasEmitted && Mathf.Abs(_rawValue - _lastEmittedRaw) <= epsilon;
            bool sameNormalized = _hasEmitted && Mathf.Abs(_normalizedValue - _lastEmittedNormalized) <= epsilon;
            bool sameEditing = _hasEmitted && _isEditing == _lastEmittedEditing;

            if (sameRaw && sameNormalized && sameEditing)
                return;

            _lastEmittedRaw = _rawValue;
            _lastEmittedNormalized = _normalizedValue;
            _lastEmittedEditing = _isEditing;
            _hasEmitted = true;

            OnUpdated?.Invoke(new UISliderOutputSnapshot(_normalizedValue, _rawValue, _isEditing));
        }

        static int ResolveVarId(VarKeyRef key)
        {
            if (key.VarId != 0)
                return key.VarId;

            if (!string.IsNullOrEmpty(key.StableKey) && VarIdResolver.TryResolve(key.StableKey, out var varId))
                return varId;

            return 0;
        }
    }
}
