#nullable enable
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;

namespace Game.UI
{
    internal sealed partial class SliderChannelPlayerRuntime
    {
        void SetInitialSnapshot(float rawValue)
        {
            _isVisible = true;
            ResolveRange();
            var clamped = Mathf.Clamp(rawValue, _minValue, _maxValue);
            _targetRawValue = clamped;
            _targetNormalizedValue = SliderRuntimeHelpers.SnapNormalizedToEdge(Normalize(clamped));
            _continuousDisplayedRawValue = clamped;
            _continuousDisplayedNormalizedValue = _targetNormalizedValue;
            _displayedRawValue = ResolveDisplayedRawValue(clamped);
            _displayedNormalizedValue = SliderRuntimeHelpers.SnapNormalizedToEdge(Normalize(_displayedRawValue));
            if (_displayedNormalizedValue <= 0f)
                _displayedRawValue = _minValue;
            else if (_displayedNormalizedValue >= 1f)
                _displayedRawValue = _maxValue;
            _hasInitialized = true;
            EmitUpdated();
        }

        void SetHiddenSnapshot()
        {
            if (!_isVisible && _hasInitialized)
                return;

            ResetTransition();
            _isVisible = false;
            _hasInitialized = true;
            EmitUpdated();
        }

        void SetTargetRawValue(float rawValue, bool allowCommands)
        {
            ResolveRange();
            var clamped = Mathf.Clamp(rawValue, _minValue, _maxValue);
            var normalized = SliderRuntimeHelpers.SnapNormalizedToEdge(Normalize(clamped));
            if (normalized <= 0f)
                clamped = _minValue;
            else if (normalized >= 1f)
                clamped = _maxValue;

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
                UpdateContinuousDisplayedValue(_targetRawValue, allowCrossingCommands: !_suppressRuntimeCommands, force: true);
                _suppressRuntimeCommands = false;
                return;
            }

            if (transition.DelaySeconds <= 0f && transition.DurationSeconds <= 0f)
            {
                ResetTransition();
                UpdateContinuousDisplayedValue(_targetRawValue, allowCrossingCommands: !_suppressRuntimeCommands, force: true);
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

        void UpdateContinuousDisplayedValue(float rawValue, bool allowCrossingCommands, bool force = false)
        {
            ResolveRange();

            var clamped = Mathf.Clamp(rawValue, _minValue, _maxValue);
            var normalized = SliderRuntimeHelpers.SnapNormalizedToEdge(Normalize(clamped));
            if (normalized <= 0f)
                clamped = _minValue;
            else if (normalized >= 1f)
                clamped = _maxValue;

            if (!force &&
                Mathf.Abs(clamped - _continuousDisplayedRawValue) <= 0.0001f &&
                Mathf.Abs(normalized - _continuousDisplayedNormalizedValue) <= 0.0001f)
                return;

            _continuousDisplayedRawValue = clamped;
            _continuousDisplayedNormalizedValue = normalized;
            ApplyPublicDisplayedValue(clamped, allowCrossingCommands, force);
        }

        void ApplyPublicDisplayedValue(float continuousRawValue, bool allowCrossingCommands, bool force = false)
        {
            var previousRawValue = _displayedRawValue;
            var previousNormalizedValue = _displayedNormalizedValue;
            var displayedRawValue = ResolveDisplayedRawValue(continuousRawValue);
            var displayedNormalizedValue = SliderRuntimeHelpers.SnapNormalizedToEdge(Normalize(displayedRawValue));
            if (displayedNormalizedValue <= 0f)
                displayedRawValue = _minValue;
            else if (displayedNormalizedValue >= 1f)
                displayedRawValue = _maxValue;

            if (!force &&
                Mathf.Abs(displayedRawValue - previousRawValue) <= 0.0001f &&
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
                ? SliderSegmentCrossingDirection.Increase
                : SliderSegmentCrossingDirection.Decrease;

            var crossedEntries = new List<SliderResolvedEntry>();
            if (direction == SliderSegmentCrossingDirection.Increase)
            {
                for (var i = 0; i < _segmentLayout.Entries.Count; i++)
                {
                    var entry = _segmentLayout.Entries[i];
                    if (previousRawValue < entry.RawValue && currentRawValue >= entry.RawValue)
                        crossedEntries.Add(entry);
                }
            }
            else
            {
                for (var i = _segmentLayout.Entries.Count - 1; i >= 0; i--)
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
            SliderRuntimeHelpers.WriteCommonCommandVars(
                vars,
                new SliderOutputSnapshot(
                    snapshot.IsVisible,
                    snapshot.TargetRawValue,
                    snapshot.TargetNormalizedValue,
                    snapshot.DisplayedRawValue,
                    snapshot.DisplayedNormalizedValue),
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
                Debug.LogError($"[SliderPlayerService] Target change commands failed: {ex.Message}");
            }
        }

        async UniTaskVoid ExecuteConditionChangedCommands(
            SliderPlayerBindingEntry? entry,
            int entryIndex,
            bool currentCondition)
        {
            if (_commandRunner == null || _commandCts == null || entry == null)
                return;

            var commands = currentCondition
                ? entry.OnConditionBecameTrueCommands
                : entry.OnConditionBecameFalseCommands;
            if (commands == null || commands.Count == 0)
                return;

            var vars = new VarStore();
            var snapshot = BuildSnapshot();
            SliderRuntimeHelpers.WriteCommonCommandVars(vars, snapshot, 0f, 0f);
            SliderRuntimeHelpers.WriteConditionCommandVars(vars, entryIndex);

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
                Debug.LogError($"[SliderPlayerService] Binding condition commands failed: {ex.Message}");
            }
        }

        async UniTaskVoid ExecuteCrossingCommandsAsync(
            List<SliderResolvedEntry> entries,
            SliderSegmentCrossingDirection direction,
            float deltaRawValue,
            float deltaNormalizedValue)
        {
            if (_commandRunner == null || _commandCts == null)
                return;

            var commandDirection = direction == SliderSegmentCrossingDirection.Increase
                ? SliderSegmentCrossingDirection.Increase
                : SliderSegmentCrossingDirection.Decrease;
            var options = CommandRunOptions.Default;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var commands = entry.SourceEntry?.GetCommands(commandDirection);
                if (commands == null || commands.Count == 0)
                    continue;

                var vars = new VarStore();
                var snapshot = BuildSnapshot();
                SliderRuntimeHelpers.WriteCommonCommandVars(
                    vars,
                    new SliderOutputSnapshot(
                        snapshot.IsVisible,
                        snapshot.TargetRawValue,
                        snapshot.TargetNormalizedValue,
                        snapshot.DisplayedRawValue,
                        snapshot.DisplayedNormalizedValue),
                    deltaRawValue,
                    deltaNormalizedValue);
                SliderRuntimeHelpers.WriteCrossingCommandVars(vars, entry, commandDirection);

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
                    Debug.LogError($"[SliderPlayerService] Segment entry commands failed: {ex.Message}");
                }
            }
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
            return SliderRuntimeHelpers.Normalize(rawValue, _minValue, _maxValue);
        }

        float ResolveDisplayedRawValue(float continuousRawValue)
        {
            return SliderRuntimeHelpers.ResolveDisplayedRawValue(
                _playerPreset.SegmentDisplayMode,
                _segmentLayout,
                continuousRawValue,
                _minValue,
                _maxValue);
        }

        SliderOutputSnapshot BuildSnapshot()
        {
            return new SliderOutputSnapshot(
                _isVisible,
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
    }
}
