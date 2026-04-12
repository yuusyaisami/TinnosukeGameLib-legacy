#nullable enable
using System.Collections.Generic;
using System.Threading;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal sealed partial class SliderChannelPlayerRuntime
    {
        void ResolvePresetProvider(IScopeNode scope)
        {
            _presetProvider = _directPresetProvider;
            if (_presetProvider != null)
                return;

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
            }
            else if (_dynamicContext != null)
            {
                _playerPreset = _options.PlayerPresetValue.GetOrDefault(_dynamicContext, new SliderPlayerPreset());
                _visualizerPreset = _options.VisualizerPresetValue.GetOrDefault(_dynamicContext, new SliderVisualizerPreset());
            }
            else
            {
                var vars = ResolveVars(scope);
                var dynamicContext = new SimpleDynamicContext(vars, scope);
                _playerPreset = _options.PlayerPresetValue.GetOrDefault(dynamicContext, new SliderPlayerPreset());
                _visualizerPreset = _options.VisualizerPresetValue.GetOrDefault(dynamicContext, new SliderVisualizerPreset());
            }
        }

        void RefreshVisualizerPreset(IScopeNode scope)
        {
            if (_dynamicContext == null)
                return;

            var dynamicContext = _dynamicContext;
            RefreshCurrentPresets(scope);
            ResolveRange();
            _segmentLayout = SliderRuntimeHelpers.BuildSegmentLayout(_visualizerPreset, dynamicContext, _minValue, _maxValue);

            if (!_hasInitialized)
            {
                LogBindingSnapshot("VisualizerPresetRefreshed");
                UpdateLoggedVisibleBarCount();
                return;
            }

            if (!_isVisible)
            {
                EmitUpdated();
                LogBindingSnapshot("VisualizerPresetRefreshed");
                UpdateLoggedVisibleBarCount();
                return;
            }

            ApplyPublicDisplayedValue(_continuousDisplayedRawValue, allowCrossingCommands: false);
            LogBindingSnapshot("VisualizerPresetRefreshed");
            UpdateLoggedVisibleBarCount();
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
            _isInteracting = false;
            _pendingExternalResync = false;

            RefreshCurrentPresets(scope);
            _commandCts = new CancellationTokenSource();
            ResolveRange();
            _segmentLayout = SliderRuntimeHelpers.BuildSegmentLayout(_visualizerPreset, dynamicContext, _minValue, _maxValue);
            ResolveCommandRunner(scope);
            RefreshBindingConditionStates(executeCommands: false);
            _targetRawValue = Mathf.Clamp(previousTargetRawValue, _minValue, _maxValue);
            _targetNormalizedValue = Normalize(_targetRawValue);
            _continuousDisplayedRawValue = Mathf.Clamp(previousContinuousDisplayedRawValue, _minValue, _maxValue);
            _continuousDisplayedNormalizedValue = Normalize(_continuousDisplayedRawValue);
            _hasInitialized = hadInitialized;
            RefreshActiveBindingEntry(scope, forceRebind: true);
            LogBindingSnapshot("PlayerPresetRefreshed");
            UpdateLoggedVisibleBarCount();
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

        void RefreshBindingConditionStates(bool executeCommands)
        {
            IReadOnlyList<SliderPlayerBindingEntry> entries = _playerPreset.BindingEntries;
            var count = entries.Count;

            if (count <= 0)
            {
                _bindingConditionStates.Clear();
                _hasBindingConditionStateSnapshot = true;
                return;
            }

            while (_bindingConditionStates.Count < count)
                _bindingConditionStates.Add(false);

            if (_bindingConditionStates.Count > count)
                _bindingConditionStates.RemoveRange(count, _bindingConditionStates.Count - count);

            for (var i = 0; i < count; i++)
            {
                var entry = entries[i];
                var current = entry?.EvaluateCondition(_dynamicContext) ?? false;
                var previous = _hasBindingConditionStateSnapshot && i < _bindingConditionStates.Count
                    ? _bindingConditionStates[i]
                    : current;

                _bindingConditionStates[i] = current;
                if (!executeCommands || !_hasBindingConditionStateSnapshot || current == previous)
                    continue;

                LogBindingSnapshot("BindingConditionChanged", $"entryIndex={i} previous={previous} current={current}");
                ExecuteConditionChangedCommands(entry, i, current).Forget();
            }

            _hasBindingConditionStateSnapshot = true;
        }

        void RefreshActiveBindingEntry(IScopeNode scope, bool forceRebind)
        {
            var resolvedIndex = ResolveActiveBindingEntryIndex();
            var resolvedEntry = resolvedIndex >= 0 &&
                                resolvedIndex < _playerPreset.BindingEntries.Count
                ? _playerPreset.BindingEntries[resolvedIndex]
                : null;

            var wasVisible = _isVisible;
            var entryChanged = forceRebind || resolvedIndex != _activeBindingEntryIndex || !ReferenceEquals(resolvedEntry, _activeBindingEntry);
            if (!entryChanged)
                return;

            if (_isInteracting)
                RequestEndInteraction(SliderInteractionEndReason.Disabled);

            UnsubscribeExternal();
            _activeBindingEntry = resolvedEntry;
            _activeBindingEntryIndex = resolvedIndex;
            _scalarBindingSourceCache = default;
            _blackboardBindingSourceCache = default;
            ResolveBindings(scope);
            SubscribeExternal();

            LogBindingSnapshot(
                _activeBindingEntry == null ? "BindingDisabled" : "ActiveBindingChanged",
                _activeBindingEntry == null
                    ? $"resolvedIndex={resolvedIndex}"
                    : $"resolvedIndex={resolvedIndex} order={_activeBindingEntry.Order}");

            if (_activeBindingEntry == null)
            {
                SetHiddenSnapshot();
                return;
            }

            if (!wasVisible || !_hasInitialized)
            {
                SetInitialSnapshot(ResolveInitialRawValue());
                return;
            }

            ApplyPublicDisplayedValue(_continuousDisplayedRawValue, allowCrossingCommands: false);
            _suppressRuntimeCommands = true;
            SetTargetRawValue(ResolveInitialRawValue(), allowCommands: false);
            if (!_transitionActive)
                _suppressRuntimeCommands = false;
        }

        int ResolveActiveBindingEntryIndex()
        {
            var entries = _playerPreset.BindingEntries;
            if (entries == null || entries.Count == 0)
                return -1;

            var bestIndex = -1;
            var bestOrder = int.MinValue;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.EvaluateCondition(_dynamicContext))
                    continue;

                if (bestIndex >= 0 && entry.Order <= bestOrder)
                    continue;

                bestIndex = i;
                bestOrder = entry.Order;
            }

            return bestIndex;
        }

        bool TryResolveBoundaryRawValue(int index, out float rawValue)
        {
            rawValue = 0f;
            if (_segmentLayout == null || _segmentLayout.Boundaries.Count == 0)
                return false;

            var clampedIndex = Mathf.Clamp(index, 0, _segmentLayout.Boundaries.Count - 1);
            rawValue = _segmentLayout.Boundaries[clampedIndex];
            return true;
        }
    }
}
