#nullable enable
using System;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
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
        readonly IScopeNode _owner;
        readonly IUISliderValueOptions _options;
        readonly Slider _slider;

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
        bool _suppressScalarEcho;
        float _lastScalarWrite;

        IVarStore? _blackboardVars;
        int _blackboardVarId;
        bool _suppressVarEcho;
        int _lastVarWriteVersion;

        public event Action<UISliderOutputSnapshot>? OnUpdated;

        public float NormalizedValue => _normalizedValue;
        public float RawValue => _rawValue;
        public bool IsEditing => _isEditing;

        public UISliderService(IScopeNode owner, IUISliderValueOptions options, Slider slider)
        {
            _owner = owner;
            _options = options;
            _slider = slider;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            UnsubscribeExternal();
            ResolveBindings();
            SubscribeExternal();
            InitializeValue();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnsubscribeExternal();
            _isEditing = false;
            _pendingExternalResync = false;
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

            float raw = Mathf.Lerp(GetMinValue(), GetMaxValue(), Mathf.Clamp01(normalized));
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

        void ResolveBindings()
        {
            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;

            var resolver = _owner.Resolver;
            if (resolver == null) return;

            if (_options.UseScalarBinding && _options.ScalarKey.Id != 0)
            {
                if (resolver.TryResolve<IBaseScalarService>(out var scalar) && scalar != null)
                    _scalarService = scalar;
            }

            if (_options.UseBlackboardBinding)
            {
                if (resolver.TryResolve<IBlackboardService>(out var bb) && bb != null)
                {
                    _blackboardVars = bb.LocalVars;
                    _blackboardVarId = ResolveVarId(_options.BlackboardKey);
                    if (_blackboardVarId == 0)
                        _blackboardVars = null;
                }
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

        void InitializeValue()
        {
            _pendingExternalResync = false;
            _hasEmitted = false;

            _slider.minValue = GetMinValue();
            _slider.maxValue = GetMaxValue();

            if (TryReadExternal(out var externalValue))
            {
                SetRawValue(externalValue, UISliderChangeSource.ExternalBinding, allowExternalWrite: false);
                return;
            }

            SetRawValue(_options.InitialValue, UISliderChangeSource.Initialization, allowExternalWrite: false);
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
            float min = GetMinValue();
            float max = GetMaxValue();
            raw = Mathf.Clamp(raw, min, max);

            float step = Mathf.Max(0f, _options.Step);
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
            float min = GetMinValue();
            float max = GetMaxValue();
            if (Mathf.Abs(max - min) <= Mathf.Epsilon)
                return 0f;

            return Mathf.Clamp01((raw - min) / (max - min));
        }

        float GetMinValue() => Mathf.Min(_options.MinValue, _options.MaxValue);
        float GetMaxValue() => Mathf.Max(_options.MinValue, _options.MaxValue);

        float GetStepSize()
        {
            float min = GetMinValue();
            float max = GetMaxValue();

            float step = Mathf.Max(0f, _options.Step);
            if (step <= 0f)
                return 0f;

            if (_options.StepMode == UISliderStepMode.Normalized)
                step *= Mathf.Abs(max - min);

            return step;
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
