#nullable enable
using System;
using Game.Commands.VNext;
using UnityEngine;

namespace Game.UI
{
    internal sealed class WorldSpaceSliderInteractionRuntime : ISliderInteractionRuntime
    {
        readonly ISliderOptions _options;
        readonly ISliderPlayerRuntime _player;
        readonly ISliderRuntimePresetProvider _presetProvider;

        IScopeNode? _activeScope;
        ISliderInteractionAdapter? _adapter;
        ActorSourceResolveCache _areaActorSourceCache;
        bool _captured;

        public WorldSpaceSliderInteractionRuntime(
            ISliderOptions options,
            ISliderPlayerRuntime player,
            ISliderRuntimePresetProvider presetProvider)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _presetProvider = presetProvider ?? throw new ArgumentNullException(nameof(presetProvider));
        }

        public UISelectionBlockMask DesiredSelectionBlockMask => UISelectionBlockMask.None;

        public void BindAdapter(ISliderInteractionAdapter? adapter)
        {
            _adapter = adapter;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;
            _activeScope = scope;
            _areaActorSourceCache = default;
            _captured = false;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _activeScope = null;
            _captured = false;
            _areaActorSourceCache = default;
        }

        public void Tick()
        {
            if (!_player.IsInteracting)
                _captured = false;
        }

        public bool HandleSignal(SliderInteractionSignal signal)
        {
            if (_adapter == null || !_adapter.IsAvailable || _activeScope == null || !_player.IsUserInputEnabled)
                return false;

            if (!MatchesTriggerButton(signal.Kind))
                return false;

            switch (signal.Phase)
            {
                case SliderInteractionSignalPhase.Down:
                    if (!_adapter.IsHovered && !_adapter.AllowsDirectPointerPressWithoutSelection)
                        return false;

                    if (!_player.RequestBeginInteraction())
                        return false;

                    _captured = true;
                    if (signal.HasWorldPosition && TryResolveBoundaryIndexFromWorld(signal.WorldPosition, out var downIndex))
                        _player.RequestBoundaryIndex(downIndex, SliderChangeSource.UserPointer);
                    return true;

                case SliderInteractionSignalPhase.Held:
                    if (!_captured || !_player.IsInteracting)
                        return false;

                    if (signal.HasWorldPosition && TryResolveBoundaryIndexFromWorld(signal.WorldPosition, out var heldIndex))
                        _player.RequestBoundaryIndex(heldIndex, SliderChangeSource.UserPointer);
                    return true;

                case SliderInteractionSignalPhase.Up:
                    if (!_captured)
                        return false;

                    if (signal.HasWorldPosition && TryResolveBoundaryIndexFromWorld(signal.WorldPosition, out var upIndex))
                        _player.RequestBoundaryIndex(upIndex, SliderChangeSource.UserPointer);
                    _player.RequestEndInteraction(SliderInteractionEndReason.PointerUp);
                    _captured = false;
                    return true;

                default:
                    return false;
            }
        }

        bool MatchesTriggerButton(SliderInteractionSignalKind kind)
        {
            return _player.WorldTriggerButton == SliderWorldTriggerButton.Right
                ? kind == SliderInteractionSignalKind.PointerSecondary
                : kind == SliderInteractionSignalKind.PointerPrimary;
        }

        bool TryResolveBoundaryIndexFromWorld(Vector3 worldPosition, out int boundaryIndex)
        {
            boundaryIndex = 0;
            if (_activeScope == null || _player.BoundaryCount <= 0)
                return false;

            var status = SliderRuntimeHelpers.TryResolveWorldRangeSnapshot(
                _activeScope,
                _options,
                ref _areaActorSourceCache,
                out var rangeSnapshot);
            if (status != SliderRangeResolveStatus.Success)
                return false;

            if (!SliderRuntimeHelpers.TryMapWorldPointToNormalized(
                    rangeSnapshot,
                    _presetProvider.CurrentVisualizerPreset.Segmented.FillAxis,
                    _presetProvider.CurrentVisualizerPreset.Segmented.OriginSide,
                    _player.PaddingStart,
                    _player.PaddingEnd,
                    worldPosition,
                    out var normalizedValue))
            {
                return false;
            }

            boundaryIndex = _player.ResolveNearestBoundaryIndex(normalizedValue);
            return true;
        }
    }
}
