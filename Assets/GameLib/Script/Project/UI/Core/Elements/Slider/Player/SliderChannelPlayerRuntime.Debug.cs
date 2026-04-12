#nullable enable
using System.Text;
using UnityEngine;

namespace Game.UI
{
    internal sealed partial class SliderChannelPlayerRuntime
    {
        int _lastBindingDebugVisibleBarCount = int.MinValue;

        void LogBindingSnapshot(string reason, string? detail = null)
        {
            if (!SliderRuntimeHelpers.ShouldEmitBindingDebugLog(_options))
                return;

            SliderRuntimeHelpers.LogBindingDebug(_options, BuildBindingSnapshotMessage(reason, detail));
        }

        string BuildBindingSnapshotMessage(string reason, string? detail)
        {
            var builder = new StringBuilder(512);
            builder.Append("Trigger=").Append(reason);
            if (!string.IsNullOrWhiteSpace(detail))
                builder.Append(" | ").Append(detail);

            builder.AppendLine();
            builder.Append("Settings: ").Append(BuildSettingsSummary()).AppendLine();
            builder.Append("Binding: ").Append(BuildBindingSummary()).AppendLine();
            builder.Append("Values: ").Append(BuildValueSummary()).AppendLine();
            builder.Append("Layout: ").Append(BuildLayoutSummary());
            return builder.ToString();
        }

        string BuildSettingsSummary()
        {
            var initialValue = ResolveFloat(_playerPreset.InitialValue, _minValue);
            var step = ResolveFloat(_visualizerPreset.Segmented.IntervalStep, 0f);
            var barSpanScale = ResolveFloat(_visualizerPreset.Segmented.BarSpanScale, 1f);

            return $"range=[{FormatFloat(_minValue)}..{FormatFloat(_maxValue)}] initial={FormatFloat(initialValue)} displayMode={_playerPreset.SegmentDisplayMode} placement={_visualizerPreset.Segmented.PlacementMode} splitBars={_visualizerPreset.Segmented.SplitBarsByLayout} intervalStep={FormatFloat(step)} barSpanScale={FormatFloat(barSpanScale)} inputEnabled={_playerPreset.UserInput.Enabled} uiInputMode={_playerPreset.UserInput.UIInputMode} transitions=(inc {FormatTransition(_playerPreset.IncreaseTransition)}, dec {FormatTransition(_playerPreset.DecreaseTransition)})";
        }

        string BuildBindingSummary()
        {
            if (_activeBindingEntry == null)
                return $"activeEntry=(none) bindingEntries={_playerPreset.BindingEntries.Count}";

            var conditionState = _activeBindingEntryIndex >= 0 && _activeBindingEntryIndex < _bindingConditionStates.Count
                ? _bindingConditionStates[_activeBindingEntryIndex]
                : false;

            return $"activeEntryIndex={_activeBindingEntryIndex} order={_activeBindingEntry.Order} condition={conditionState} priority={_activeBindingEntry.BindingPriority} scalar={DescribeScalarBinding()} blackboard={DescribeBlackboardBinding()} boundValue={DescribeBoundValue()}";
        }

        string DescribeScalarBinding()
        {
            if (_activeBindingEntry == null || !_activeBindingEntry.UseScalarBinding || _activeBindingEntry.ScalarKey.Id == 0)
                return "off";

            var keyLabel = _activeBindingEntry.ScalarKey.FormatLabel();
            if (TryReadScalar(out var scalarValue))
                return $"on scope={FormatScopePath(_scalarBindingScope)} key={keyLabel} value={FormatFloat(scalarValue)}";

            return $"on scope={FormatScopePath(_scalarBindingScope)} key={keyLabel} value=(unavailable)";
        }

        string DescribeBlackboardBinding()
        {
            if (_activeBindingEntry == null || !_activeBindingEntry.UseBlackboardBinding || _blackboardVarId == 0)
                return "off";

            var keyLabel = FormatVarKeyRef(_activeBindingEntry.BlackboardKey);
            if (TryReadBlackboard(out var blackboardValue))
                return $"on scope={FormatScopePath(_blackboardBindingScope)} key={keyLabel} varId={_blackboardVarId} value={FormatFloat(blackboardValue)}";

            return $"on scope={FormatScopePath(_blackboardBindingScope)} key={keyLabel} varId={_blackboardVarId} value=(unavailable)";
        }

        string DescribeBoundValue()
        {
            if (TryReadBoundValue(out var boundValue))
                return FormatFloat(boundValue);

            return "(unavailable)";
        }

        string BuildValueSummary()
        {
            return $"visible={_isVisible} hasInitialized={_hasInitialized} interacting={_isInteracting} pendingExternalResync={_pendingExternalResync} suppressRuntimeCommands={_suppressRuntimeCommands} targetRaw={FormatFloat(_targetRawValue)} targetNorm={FormatFloat(_targetNormalizedValue)} continuousRaw={FormatFloat(_continuousDisplayedRawValue)} continuousNorm={FormatFloat(_continuousDisplayedNormalizedValue)} displayedRaw={FormatFloat(_displayedRawValue)} displayedNorm={FormatFloat(_displayedNormalizedValue)} boundaryIndex={CurrentBoundaryIndex}/{Mathf.Max(0, BoundaryCount - 1)}";
        }

        string BuildLayoutSummary()
        {
            var segmentSettings = _visualizerPreset.Segmented;
            var spawnedBarCount = SliderRuntimeHelpers.ResolveVisualSegmentBarCount(segmentSettings, BoundaryCount);
            var visibleBarCount = ResolveVisibleSegmentBarCount();
            var step = ResolveFloat(segmentSettings.IntervalStep, 0f);
            var barSpanScale = ResolveFloat(segmentSettings.BarSpanScale, 1f);

            return $"placement={segmentSettings.PlacementMode} splitBars={segmentSettings.SplitBarsByLayout} boundaries={BoundaryCount} spawnedBars={spawnedBarCount} visibleBars={visibleBarCount} intervalStep={FormatFloat(step)} barSpanScale={FormatFloat(barSpanScale)}";
        }

        bool ShouldLogVisibleBarSnapshot()
        {
            var visibleBarCount = ResolveVisibleSegmentBarCount();
            if (visibleBarCount == _lastBindingDebugVisibleBarCount)
                return false;

            _lastBindingDebugVisibleBarCount = visibleBarCount;
            return true;
        }

        void UpdateLoggedVisibleBarCount()
        {
            _lastBindingDebugVisibleBarCount = ResolveVisibleSegmentBarCount();
        }

        void ResetLoggedVisibleBarCount()
        {
            _lastBindingDebugVisibleBarCount = 0;
        }

        int ResolveVisibleSegmentBarCount()
        {
            if (_segmentLayout == null)
                return 0;

            var segmentSettings = _visualizerPreset.Segmented;
            var spawnedBarCount = SliderRuntimeHelpers.ResolveVisualSegmentBarCount(segmentSettings, BoundaryCount);
            if (spawnedBarCount <= 0)
                return 0;

            var visibleCount = 0;
            for (var i = 0; i < spawnedBarCount; i++)
            {
                SliderRuntimeHelpers.ResolveVisualSegmentBarRange(
                    segmentSettings,
                    this,
                    i,
                    out _,
                    out _,
                    out var startNormalized,
                    out var endNormalized);

                SliderRuntimeHelpers.ResolveDisplayedSegmentBarInterval(
                    _playerPreset.SegmentDisplayMode,
                    segmentSettings.SplitBarsByLayout,
                    _displayedNormalizedValue,
                    startNormalized,
                    endNormalized,
                    out _,
                    out _,
                    out var isVisible);

                if (isVisible)
                    visibleCount++;
            }

            return visibleCount;
        }

        static string FormatVarKeyRef(Game.Common.VarKeyRef key)
        {
            if (!string.IsNullOrWhiteSpace(key.StableKey))
                return key.VarId != 0 ? $"{key.StableKey} (#{key.VarId})" : key.StableKey;

            return key.VarId != 0 ? $"#{key.VarId}" : "(empty)";
        }

        static string FormatScopePath(Game.IScopeNode? scope)
        {
            if (scope == null)
                return "null";

            var path = scope.GetPathFromRoot();
            if (path == null || path.Count == 0)
                return FormatScopeNodeName(scope);

            var builder = new StringBuilder(128);
            for (var i = 0; i < path.Count; i++)
            {
                if (i > 0)
                    builder.Append(" / ");

                builder.Append(FormatScopeNodeName(path[i]));
            }

            return builder.ToString();
        }

        static string FormatScopeNodeName(Game.IScopeNode? scopeNode)
        {
            if (scopeNode == null)
                return "null";

            if (scopeNode is Component component)
            {
                if (!component)
                    return $"{scopeNode.GetType().Name}(Destroyed)";

                var gameObject = component.gameObject;
                return gameObject != null
                    ? $"{scopeNode.GetType().Name}({gameObject.name})"
                    : $"{scopeNode.GetType().Name}(Destroyed)";
            }

            return scopeNode.GetType().Name;
        }

        static string FormatTransition(SliderTransitionSettings settings)
        {
            return $"{FormatFloat(settings.DelaySeconds)}/{FormatFloat(settings.DurationSeconds)}";
        }

        static string FormatFloat(float value)
        {
            return value.ToString("0.###");
        }
    }
}
