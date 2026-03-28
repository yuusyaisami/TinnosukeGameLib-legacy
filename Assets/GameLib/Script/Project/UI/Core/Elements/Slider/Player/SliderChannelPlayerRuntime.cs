#nullable enable
using System;
using System.Threading;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Game.Scalar;
using Game.Times;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI
{
    internal sealed partial class SliderChannelPlayerRuntime :
        ISliderPlayerRuntime,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly IScopeNode _owner;
        readonly ISliderOptions _options;
        readonly ISliderRuntimePresetProvider? _directPresetProvider;

        ISliderRuntimePresetProvider? _presetProvider;
        IScopeNode? _activeScope;

        IDynamicContext? _dynamicContext;
        SliderPlayerPreset _playerPreset = new();
        SliderVisualizerPreset _visualizerPreset = new();
        SliderResolvedSegmentLayout? _segmentLayout;

        IBaseScalarService? _scalarService;
        IDisposable? _scalarSubscription;
        ActorSourceResolveCache _scalarBindingSourceCache;

        IVarStore? _blackboardVars;
        int _blackboardVarId;
        ActorSourceResolveCache _blackboardBindingSourceCache;
        SliderPlayerBindingEntry? _activeBindingEntry;
        int _activeBindingEntryIndex = -1;

        ICommandRunner? _commandRunner;
        CancellationTokenSource? _commandCts;
        TimeScaleBehavior _timeScaleBehavior = TimeScaleBehavior.Scaled;

        float _minValue;
        float _maxValue = 1f;
        float _targetRawValue;
        float _targetNormalizedValue;
        float _continuousDisplayedRawValue;
        float _continuousDisplayedNormalizedValue;
        float _displayedRawValue;
        float _displayedNormalizedValue;
        bool _hasInitialized;
        bool _isVisible;

        bool _transitionActive;
        float _transitionDelayRemaining;
        float _transitionDuration;
        float _transitionElapsed;
        float _transitionFromRawValue;
        float _transitionToRawValue;
        Ease _transitionEase = Ease.Linear;
        bool _suppressRuntimeCommands;

        bool _isInteracting;
        float _interactionStartRawValue;
        bool _pendingExternalResync;
        bool _suppressScalarEcho;
        float _lastScalarWrite;
        bool _suppressVarEcho;
        int _lastVarWriteVersion;

        public event Action<SliderOutputSnapshot>? OnUpdated;

        public bool IsVisible => _isVisible;
        public float TargetRawValue => _targetRawValue;
        public float TargetNormalizedValue => _targetNormalizedValue;
        public float DisplayedRawValue => _displayedRawValue;
        public float DisplayedNormalizedValue => _displayedNormalizedValue;
        public bool IsInteracting => _isInteracting;
        public bool IsUserInputEnabled => _isVisible && _activeBindingEntry != null && _playerPreset.UserInput.Enabled;
        public SliderUIInputMode UIInputMode => _playerPreset.UserInput.UIInputMode;
        public SliderWorldTriggerButton WorldTriggerButton => _playerPreset.UserInput.WorldTriggerButton;
        public float NavigateRepeatDelay => _playerPreset.UserInput.NavigateRepeatDelay;
        public float NavigateRepeatInterval => _playerPreset.UserInput.NavigateRepeatInterval;
        public float ScrollRepeatDelay => _playerPreset.UserInput.ScrollRepeatDelay;
        public float ScrollRepeatInterval => _playerPreset.UserInput.ScrollRepeatInterval;
        public float PaddingStart => _playerPreset.UserInput.PaddingStart;
        public float PaddingEnd => _playerPreset.UserInput.PaddingEnd;
        public int BoundaryCount => _segmentLayout?.Boundaries.Count ?? 0;
        public int CurrentBoundaryIndex => ResolveNearestBoundaryIndex(_targetNormalizedValue);

        public SliderChannelPlayerRuntime(
            IScopeNode owner,
            ISliderOptions options,
            ISliderRuntimePresetProvider? presetProvider = null)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _directPresetProvider = presetProvider;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            _activeScope = scope;
            UnsubscribeExternal();
            UnsubscribePresetProvider();
            StopCommands();
            ResetTransition();

            var vars = ResolveVars(scope);
            _dynamicContext = new SimpleDynamicContext(vars, scope);
            ResolvePresetProvider(scope);
            RefreshCurrentPresets(scope);
            _commandCts = new CancellationTokenSource();
            _timeScaleBehavior = SliderRuntimeHelpers.ResolveTimeScaleBehavior(scope);
            _isInteracting = false;
            _pendingExternalResync = false;
            _suppressScalarEcho = false;
            _lastScalarWrite = 0f;
            _suppressVarEcho = false;
            _lastVarWriteVersion = 0;

            ResolveRange();
            _segmentLayout = SliderRuntimeHelpers.BuildSegmentLayout(_visualizerPreset, _dynamicContext ?? new SimpleDynamicContext(vars, scope), _minValue, _maxValue);
            ResolveCommandRunner(scope);
            SubscribePresetProvider();
            RefreshActiveBindingEntry(scope, forceRebind: true);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            _activeScope = null;
            UnsubscribeExternal();
            UnsubscribePresetProvider();
            StopCommands();
            ResetTransition();

            _dynamicContext = null;
            _presetProvider = null;
            _commandRunner = null;
            _segmentLayout = null;
            _hasInitialized = false;
            _targetRawValue = 0f;
            _targetNormalizedValue = 0f;
            _continuousDisplayedRawValue = 0f;
            _continuousDisplayedNormalizedValue = 0f;
            _displayedRawValue = 0f;
            _displayedNormalizedValue = 0f;
            _isVisible = false;
            _isInteracting = false;
            _interactionStartRawValue = 0f;
            _scalarBindingSourceCache = default;
            _blackboardBindingSourceCache = default;
            _activeBindingEntry = null;
            _activeBindingEntryIndex = -1;
            _suppressRuntimeCommands = false;
            _pendingExternalResync = false;
            _suppressScalarEcho = false;
            _lastScalarWrite = 0f;
            _suppressVarEcho = false;
            _lastVarWriteVersion = 0;
        }

        public void Tick()
        {
            if (_activeScope != null)
                RefreshActiveBindingEntry(_activeScope, forceRebind: false);

            if (_isInteracting && !IsUserInputEnabled)
                RequestEndInteraction(SliderInteractionEndReason.Disabled);

            if (!_isVisible || !_transitionActive)
                return;

            var deltaTime = _timeScaleBehavior == TimeScaleBehavior.Unscaled
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            if (_transitionDelayRemaining > 0f)
            {
                _transitionDelayRemaining -= deltaTime;
                if (_transitionDelayRemaining > 0f)
                    return;

                deltaTime = -_transitionDelayRemaining;
                _transitionDelayRemaining = 0f;
            }

            if (_transitionDuration <= 0f)
            {
                _transitionActive = false;
                UpdateContinuousDisplayedValue(_transitionToRawValue, allowCrossingCommands: !_suppressRuntimeCommands);
                _suppressRuntimeCommands = false;
                return;
            }

            _transitionElapsed += deltaTime;
            var progress = Mathf.Clamp01(_transitionElapsed / _transitionDuration);
            var eased = DOVirtual.EasedValue(0f, 1f, progress, _transitionEase);
            var rawValue = Mathf.LerpUnclamped(_transitionFromRawValue, _transitionToRawValue, eased);
            UpdateContinuousDisplayedValue(rawValue, allowCrossingCommands: !_suppressRuntimeCommands);

            if (progress >= 1f)
            {
                _transitionActive = false;
                UpdateContinuousDisplayedValue(_transitionToRawValue, allowCrossingCommands: !_suppressRuntimeCommands);
                _suppressRuntimeCommands = false;
            }
        }

        public bool RequestBeginInteraction()
        {
            if (!IsUserInputEnabled)
                return false;

            if (_isInteracting)
                return true;

            _isInteracting = true;
            _interactionStartRawValue = _targetRawValue;
            return true;
        }

        public void RequestEndInteraction(SliderInteractionEndReason reason)
        {
            if (!_isInteracting)
                return;

            _isInteracting = false;

            switch (reason)
            {
                case SliderInteractionEndReason.Cancel:
                    ApplyInteractionRevert();
                    break;

                case SliderInteractionEndReason.Disabled:
                case SliderInteractionEndReason.SelectionLost:
                    SyncFromExternal(allowCommands: false);
                    _pendingExternalResync = false;
                    return;
            }

            if (_pendingExternalResync)
            {
                _pendingExternalResync = false;
                SyncFromExternal(allowCommands: false);
            }
        }

        public bool RequestBoundaryIndex(int index, SliderChangeSource source)
        {
            if (!RequestBeginInteraction())
                return false;

            if (!TryResolveBoundaryRawValue(index, out var rawValue))
                return false;

            var changed = Mathf.Abs(rawValue - _targetRawValue) > 0.0001f;
            SetTargetRawValue(rawValue, allowCommands: true);

            if (changed && (source == SliderChangeSource.UserPointer || source == SliderChangeSource.UserNavigate))
                WriteExternal(rawValue);

            return changed;
        }

        public int ResolveNearestBoundaryIndex(float normalizedValue)
        {
            if (_segmentLayout == null || _segmentLayout.Boundaries.Count == 0)
                return 0;

            var rawValue = Mathf.Lerp(_minValue, _maxValue, Mathf.Clamp01(normalizedValue));
            var bestIndex = 0;
            var bestDelta = float.PositiveInfinity;
            for (var i = 0; i < _segmentLayout.Boundaries.Count; i++)
            {
                var delta = Mathf.Abs(_segmentLayout.Boundaries[i] - rawValue);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public float ResolveBoundaryNormalizedValue(int index)
        {
            if (!TryResolveBoundaryRawValue(index, out var rawValue))
                return 0f;

            return Normalize(rawValue);
        }

        public float ResolveBoundaryRawValue(int index)
        {
            return TryResolveBoundaryRawValue(index, out var rawValue) ? rawValue : 0f;
        }
    }
}
