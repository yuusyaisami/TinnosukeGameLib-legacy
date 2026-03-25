#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Game.Scalar;
using Game.Times;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class WorldSliderPlayerService :
        IWorldSliderPlayerService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly IScopeNode _owner;
        readonly IWorldSliderOptions _options;
        IWorldSliderRuntimePresetProvider? _presetProvider;
        IScopeNode? _activeScope;

        IDynamicContext? _dynamicContext;
        WorldSliderPlayerPreset _playerPreset = new();
        WorldSliderVisualizerPreset _visualizerPreset = new();
        WorldSliderResolvedSegmentLayout? _segmentLayout;

        IBaseScalarService? _scalarService;
        IDisposable? _scalarSubscription;
        ActorSourceResolveCache _scalarBindingSourceCache;

        IVarStore? _blackboardVars;
        int _blackboardVarId;
        ActorSourceResolveCache _blackboardBindingSourceCache;

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

        bool _transitionActive;
        float _transitionDelayRemaining;
        float _transitionDuration;
        float _transitionElapsed;
        float _transitionFromRawValue;
        float _transitionToRawValue;
        Ease _transitionEase = Ease.Linear;
        bool _suppressRuntimeCommands;

        public event Action<WorldSliderOutputSnapshot>? OnUpdated;

        public float TargetRawValue => _targetRawValue;
        public float TargetNormalizedValue => _targetNormalizedValue;
        public float DisplayedRawValue => _displayedRawValue;
        public float DisplayedNormalizedValue => _displayedNormalizedValue;

        public WorldSliderPlayerService(IScopeNode owner, IWorldSliderOptions options)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _options = options ?? throw new ArgumentNullException(nameof(options));
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
            _timeScaleBehavior = WorldSliderRuntimeHelpers.ResolveTimeScaleBehavior(scope);

            ResolveRange();
            _segmentLayout = WorldSliderRuntimeHelpers.BuildSegmentLayout(_visualizerPreset, _dynamicContext, _minValue, _maxValue);
            ResolveBindings(scope);
            ResolveCommandRunner(scope);
            SubscribeExternal();
            SubscribePresetProvider();

            var initialRawValue = ResolveInitialRawValue();
            SetInitialSnapshot(initialRawValue);
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
            _scalarBindingSourceCache = default;
            _blackboardBindingSourceCache = default;
            _suppressRuntimeCommands = false;
        }

        public void Tick()
        {
            if (!_transitionActive)
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

        void ResolveBindings(IScopeNode scope)
        {
            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;

            if (_playerPreset.UseScalarBinding &&
                _playerPreset.ScalarKey.Id != 0 &&
                TryResolveScalarService(scope, out var scalarService))
            {
                _scalarService = scalarService;
            }

            if (_playerPreset.UseBlackboardBinding)
            {
                _blackboardVarId = WorldSliderRuntimeHelpers.ResolveVarId(_playerPreset.BlackboardKey);
                if (_blackboardVarId != 0 &&
                    TryResolveBlackboardVars(scope, out var blackboardVars))
                {
                    _blackboardVars = blackboardVars;
                }
            }
        }

        void ResolveCommandRunner(IScopeNode scope)
        {
            _commandRunner = null;
            if (!scope.TryResolveInAncestors(out _commandRunner))
                scope.Resolver?.TryResolve(out _commandRunner);
        }

        void SubscribeExternal()
        {
            if (_scalarService != null && _playerPreset.UseScalarBinding && _playerPreset.ScalarKey.Id != 0)
                _scalarSubscription = _scalarService.GlobalSubscribe(_playerPreset.ScalarKey, HandleScalarChanged);

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged += HandleBlackboardVarChanged;
        }

        void UnsubscribeExternal()
        {
            _scalarSubscription?.Dispose();
            _scalarSubscription = null;

            if (_blackboardVars != null && _blackboardVarId != 0)
                _blackboardVars.OnVarChanged -= HandleBlackboardVarChanged;

            _scalarService = null;
            _blackboardVars = null;
            _blackboardVarId = 0;
        }

        bool TryResolveScalarService(IScopeNode scope, out IBaseScalarService? scalarService)
        {
            scalarService = null;
            var targetScope = ActorSourceFastResolver.ResolveCached(
                scope,
                _playerPreset.ScalarBindingSource,
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
                _playerPreset.BlackboardBindingSource,
                ref _blackboardBindingSourceCache);
            if (targetScope?.Resolver == null)
                return false;

            if (!targetScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) || blackboard == null)
                return false;

            blackboardVars = blackboard.LocalVars;
            return blackboardVars != null;
        }

        void HandleScalarChanged(ScalarValueChangedArgs args)
        {
            _ = args;
            RefreshTargetFromBinding();
        }

        void HandleBlackboardVarChanged(int varId)
        {
            if (_blackboardVarId == 0 || varId != _blackboardVarId)
                return;

            RefreshTargetFromBinding();
        }

        void RefreshTargetFromBinding()
        {
            ResolveRange();
            if (!TryReadBoundValue(out var rawValue))
                return;

            _suppressRuntimeCommands = false;
            SetTargetRawValue(rawValue, allowCommands: true);
        }

        void SetInitialSnapshot(float rawValue)
        {
            ResolveRange();
            var clamped = Mathf.Clamp(rawValue, _minValue, _maxValue);
            _targetRawValue = clamped;
            _targetNormalizedValue = Normalize(clamped);
            _continuousDisplayedRawValue = clamped;
            _continuousDisplayedNormalizedValue = _targetNormalizedValue;
            _displayedRawValue = ResolveDisplayedRawValue(clamped);
            _displayedNormalizedValue = Normalize(_displayedRawValue);
            _hasInitialized = true;
            EmitUpdated();
        }

        void SetTargetRawValue(float rawValue, bool allowCommands)
        {
            ResolveRange();
            var clamped = Mathf.Clamp(rawValue, _minValue, _maxValue);
            var normalized = Normalize(clamped);

            if (!_hasInitialized)
            {
                SetInitialSnapshot(clamped);
                return;
            }

            if (Mathf.Abs(clamped - _targetRawValue) <= 0.0001f &&
                Mathf.Abs(normalized - _targetNormalizedValue) <= 0.0001f)
                return;

            var previousTargetRawValue = _targetRawValue;
            var previousTargetNormalizedValue = _targetNormalizedValue;

            _targetRawValue = clamped;
            _targetNormalizedValue = normalized;
            EmitUpdated();

            if (allowCommands)
            {
                ExecuteTargetChangedCommands(
                    previousTargetRawValue,
                    previousTargetNormalizedValue,
                    clamped,
                    normalized).Forget();
            }

            StartDisplayedTransition();
        }

        void StartDisplayedTransition()
        {
            var delta = _targetRawValue - _continuousDisplayedRawValue;
            var transition = delta >= 0f
                ? _playerPreset.IncreaseTransition
                : _playerPreset.DecreaseTransition;

            if (Mathf.Abs(delta) <= 0.0001f)
            {
                ResetTransition();
                UpdateContinuousDisplayedValue(_targetRawValue, allowCrossingCommands: !_suppressRuntimeCommands);
                _suppressRuntimeCommands = false;
                return;
            }

            if (transition.DelaySeconds <= 0f && transition.DurationSeconds <= 0f)
            {
                ResetTransition();
                UpdateContinuousDisplayedValue(_targetRawValue, allowCrossingCommands: !_suppressRuntimeCommands);
                _suppressRuntimeCommands = false;
                return;
            }

            _transitionActive = true;
            _transitionDelayRemaining = transition.DelaySeconds;
            _transitionDuration = transition.DurationSeconds;
            _transitionElapsed = 0f;
            _transitionFromRawValue = _continuousDisplayedRawValue;
            _transitionToRawValue = _targetRawValue;
            _transitionEase = transition.Ease;
        }

        void UpdateContinuousDisplayedValue(float rawValue, bool allowCrossingCommands)
        {
            ResolveRange();

            var clamped = Mathf.Clamp(rawValue, _minValue, _maxValue);
            var normalized = Normalize(clamped);

            if (Mathf.Abs(clamped - _continuousDisplayedRawValue) <= 0.0001f &&
                Mathf.Abs(normalized - _continuousDisplayedNormalizedValue) <= 0.0001f)
                return;

            _continuousDisplayedRawValue = clamped;
            _continuousDisplayedNormalizedValue = normalized;
            ApplyPublicDisplayedValue(clamped, allowCrossingCommands);
        }

        void ApplyPublicDisplayedValue(float continuousRawValue, bool allowCrossingCommands)
        {
            var previousRawValue = _displayedRawValue;
            var previousNormalizedValue = _displayedNormalizedValue;
            var displayedRawValue = ResolveDisplayedRawValue(continuousRawValue);
            var displayedNormalizedValue = Normalize(displayedRawValue);

            if (Mathf.Abs(displayedRawValue - previousRawValue) <= 0.0001f &&
                Mathf.Abs(displayedNormalizedValue - previousNormalizedValue) <= 0.0001f)
                return;

            _displayedRawValue = displayedRawValue;
            _displayedNormalizedValue = displayedNormalizedValue;

            if (allowCrossingCommands)
                ExecuteCrossingCommands(previousRawValue, previousNormalizedValue, displayedRawValue, displayedNormalizedValue);

            EmitUpdated();
        }

        void ExecuteCrossingCommands(
            float previousRawValue,
            float previousNormalizedValue,
            float currentRawValue,
            float currentNormalizedValue)
        {
            if (_segmentLayout == null || _segmentLayout.Entries.Count == 0)
                return;

            if (Mathf.Abs(currentRawValue - previousRawValue) <= 0.0001f)
                return;

            var direction = currentRawValue > previousRawValue
                ? WorldSliderSegmentCrossingDirection.Increase
                : WorldSliderSegmentCrossingDirection.Decrease;

            var crossedEntries = new List<WorldSliderResolvedEntry>();
            if (direction == WorldSliderSegmentCrossingDirection.Increase)
            {
                for (int i = 0; i < _segmentLayout.Entries.Count; i++)
                {
                    var entry = _segmentLayout.Entries[i];
                    if (previousRawValue < entry.RawValue && currentRawValue >= entry.RawValue)
                        crossedEntries.Add(entry);
                }
            }
            else
            {
                for (int i = _segmentLayout.Entries.Count - 1; i >= 0; i--)
                {
                    var entry = _segmentLayout.Entries[i];
                    if (previousRawValue >= entry.RawValue && currentRawValue < entry.RawValue)
                        crossedEntries.Add(entry);
                }
            }

            if (crossedEntries.Count == 0)
                return;

            ExecuteCrossingCommandsAsync(
                crossedEntries,
                direction,
                currentRawValue - previousRawValue,
                currentNormalizedValue - previousNormalizedValue).Forget();
        }

        async UniTaskVoid ExecuteTargetChangedCommands(
            float previousTargetRawValue,
            float previousTargetNormalizedValue,
            float currentTargetRawValue,
            float currentTargetNormalizedValue)
        {
            if (_commandRunner == null || _commandCts == null)
                return;

            var commands = _playerPreset.OnTargetValueChangedCommands;
            if (commands == null || commands.Count == 0)
                return;

            var vars = new VarStore();
            var snapshot = BuildSnapshot();
            WorldSliderRuntimeHelpers.WriteCommonCommandVars(
                vars,
                snapshot,
                currentTargetRawValue - previousTargetRawValue,
                currentTargetNormalizedValue - previousTargetNormalizedValue);

            var options = CommandRunOptions.Default;
            var ctx = new CommandContext(_owner, vars, _commandRunner, _owner, options);

            try
            {
                await _commandRunner.ExecuteListAsync(commands, ctx, _commandCts.Token, options);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldSliderPlayerService] Target change commands failed: {ex.Message}");
            }
        }

        async UniTaskVoid ExecuteCrossingCommandsAsync(
            List<WorldSliderResolvedEntry> entries,
            WorldSliderSegmentCrossingDirection direction,
            float deltaRawValue,
            float deltaNormalizedValue)
        {
            if (_commandRunner == null || _commandCts == null)
                return;

            var options = CommandRunOptions.Default;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var commands = entry.SourceEntry?.GetCommands(direction);
                if (commands == null || commands.Count == 0)
                    continue;

                var vars = new VarStore();
                var snapshot = BuildSnapshot();
                WorldSliderRuntimeHelpers.WriteCommonCommandVars(vars, snapshot, deltaRawValue, deltaNormalizedValue);
                WorldSliderRuntimeHelpers.WriteCrossingCommandVars(vars, entry, direction);

                var ctx = new CommandContext(_owner, vars, _commandRunner, _owner, options);
                try
                {
                    await _commandRunner.ExecuteListAsync(commands, ctx, _commandCts.Token, options);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[WorldSliderPlayerService] Segment entry commands failed: {ex.Message}");
                }
            }
        }

        float ResolveInitialRawValue()
        {
            ResolveRange();
            if (TryReadBoundValue(out var boundValue))
                return boundValue;

            return ResolveFloat(_playerPreset.InitialValue, _minValue);
        }

        bool TryReadBoundValue(out float rawValue)
        {
            rawValue = 0f;
            if (_playerPreset.BindingPriority == WorldSliderBindingPriority.Scalar)
            {
                if (TryReadScalar(out rawValue))
                    return true;
                if (TryReadBlackboard(out rawValue))
                    return true;
                return false;
            }

            if (TryReadBlackboard(out rawValue))
                return true;
            return TryReadScalar(out rawValue);
        }

        bool TryReadScalar(out float rawValue)
        {
            rawValue = 0f;
            return _scalarService != null &&
                   _playerPreset.UseScalarBinding &&
                   _playerPreset.ScalarKey.Id != 0 &&
                   _scalarService.GlobalTryGet(_playerPreset.ScalarKey, out rawValue);
        }

        bool TryReadBlackboard(out float rawValue)
        {
            rawValue = 0f;
            if (_blackboardVars == null || _blackboardVarId == 0)
                return false;

            return _blackboardVars.TryGetVariant(_blackboardVarId, out var variant) &&
                   variant.TryGet(out rawValue);
        }

        void ResolveRange()
        {
            var resolvedMinValue = ResolveFloat(_playerPreset.MinValue, 0f);
            var resolvedMaxValue = ResolveFloat(_playerPreset.MaxValue, 1f);
            _minValue = Mathf.Min(resolvedMinValue, resolvedMaxValue);
            _maxValue = Mathf.Max(resolvedMinValue, resolvedMaxValue);
        }

        float ResolveFloat(DynamicValue<float> dynamicValue, float fallback)
        {
            if (_dynamicContext != null)
                return dynamicValue.GetOrDefault(_dynamicContext, fallback);

            return dynamicValue.GetOrDefaultWithoutContext(fallback);
        }

        float Normalize(float rawValue)
        {
            return WorldSliderRuntimeHelpers.Normalize(rawValue, _minValue, _maxValue);
        }

        float ResolveDisplayedRawValue(float continuousRawValue)
        {
            return WorldSliderRuntimeHelpers.ResolveDisplayedRawValue(
                _playerPreset.SegmentDisplayMode,
                _visualizerPreset.Mode,
                _segmentLayout,
                continuousRawValue,
                _minValue,
                _maxValue);
        }

        WorldSliderOutputSnapshot BuildSnapshot()
        {
            return new WorldSliderOutputSnapshot(
                _targetRawValue,
                _targetNormalizedValue,
                _displayedRawValue,
                _displayedNormalizedValue);
        }

        void EmitUpdated()
        {
            OnUpdated?.Invoke(BuildSnapshot());
        }

        void ResetTransition()
        {
            _transitionActive = false;
            _transitionDelayRemaining = 0f;
            _transitionDuration = 0f;
            _transitionElapsed = 0f;
            _transitionFromRawValue = 0f;
            _transitionToRawValue = 0f;
            _transitionEase = Ease.Linear;
        }

        void ResolvePresetProvider(IScopeNode scope)
        {
            _presetProvider = null;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return;

            resolver.TryResolve(out _presetProvider);
        }

        void SubscribePresetProvider()
        {
            if (_presetProvider == null)
                return;

            _presetProvider.OnPlayerPresetChanged += HandlePlayerPresetChanged;
            _presetProvider.OnVisualizerPresetChanged += HandleVisualizerPresetChanged;
        }

        void UnsubscribePresetProvider()
        {
            if (_presetProvider == null)
                return;

            _presetProvider.OnPlayerPresetChanged -= HandlePlayerPresetChanged;
            _presetProvider.OnVisualizerPresetChanged -= HandleVisualizerPresetChanged;
        }

        void HandlePlayerPresetChanged()
        {
            if (_activeScope == null)
                return;

            RefreshPlayerPreset(_activeScope);
        }

        void HandleVisualizerPresetChanged()
        {
            if (_activeScope == null)
                return;

            RefreshVisualizerPreset(_activeScope);
        }

        void RefreshCurrentPresets(IScopeNode scope)
        {
            if (_presetProvider != null)
            {
                _playerPreset = _presetProvider.CurrentPlayerPreset;
                _visualizerPreset = _presetProvider.CurrentVisualizerPreset;
                return;
            }

            if (_dynamicContext != null)
            {
                _playerPreset = WorldSliderRuntimeHelpers.ResolvePlayerPreset(_options.PlayerPresetValue, _dynamicContext);
                _visualizerPreset = WorldSliderRuntimeHelpers.ResolveVisualizerPreset(_options.VisualizerPresetValue, _dynamicContext);
                return;
            }

            var vars = ResolveVars(scope);
            var dynamicContext = new SimpleDynamicContext(vars, scope);
            _playerPreset = WorldSliderRuntimeHelpers.ResolvePlayerPreset(_options.PlayerPresetValue, dynamicContext);
            _visualizerPreset = WorldSliderRuntimeHelpers.ResolveVisualizerPreset(_options.VisualizerPresetValue, dynamicContext);
        }

        void RefreshVisualizerPreset(IScopeNode scope)
        {
            if (_dynamicContext == null)
                return;

            var dynamicContext = _dynamicContext;
            RefreshCurrentPresets(scope);
            ResolveRange();
            _segmentLayout = WorldSliderRuntimeHelpers.BuildSegmentLayout(_visualizerPreset, dynamicContext, _minValue, _maxValue);

            if (!_hasInitialized)
                return;

            ApplyPublicDisplayedValue(_continuousDisplayedRawValue, allowCrossingCommands: false);
        }

        void RefreshPlayerPreset(IScopeNode scope)
        {
            if (_dynamicContext == null)
                return;

            var dynamicContext = _dynamicContext;
            var previousContinuousDisplayedRawValue = _continuousDisplayedRawValue;
            var previousTargetRawValue = _targetRawValue;
            var hadInitialized = _hasInitialized;

            UnsubscribeExternal();
            StopCommands();
            ResetTransition();

            RefreshCurrentPresets(scope);
            _commandCts = new CancellationTokenSource();
            ResolveRange();
            _segmentLayout = WorldSliderRuntimeHelpers.BuildSegmentLayout(_visualizerPreset, dynamicContext, _minValue, _maxValue);
            ResolveBindings(scope);
            ResolveCommandRunner(scope);
            SubscribeExternal();

            if (!hadInitialized)
            {
                SetInitialSnapshot(ResolveInitialRawValue());
                return;
            }

            _targetRawValue = Mathf.Clamp(previousTargetRawValue, _minValue, _maxValue);
            _targetNormalizedValue = Normalize(_targetRawValue);
            _continuousDisplayedRawValue = Mathf.Clamp(previousContinuousDisplayedRawValue, _minValue, _maxValue);
            _continuousDisplayedNormalizedValue = Normalize(_continuousDisplayedRawValue);
            _hasInitialized = true;

            ApplyPublicDisplayedValue(_continuousDisplayedRawValue, allowCrossingCommands: false);
            _suppressRuntimeCommands = true;
            SetTargetRawValue(ResolveInitialRawValue(), allowCommands: false);
            if (!_transitionActive)
                _suppressRuntimeCommands = false;
        }

        void StopCommands()
        {
            if (_commandCts == null)
                return;

            _commandCts.Cancel();
            _commandCts.Dispose();
            _commandCts = null;
        }

        static IVarStore ResolveVars(IScopeNode scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }
    }
}
