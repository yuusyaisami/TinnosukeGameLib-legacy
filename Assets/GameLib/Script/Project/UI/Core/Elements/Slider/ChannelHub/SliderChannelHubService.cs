#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.SelectRuntime;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class SliderChannelHubService :
        ISliderChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        readonly IScopeNode _owner;
        readonly SliderChannelHubMB _mb;
        readonly Dictionary<string, SliderChannelRuntime> _channels = new(StringComparer.Ordinal);
        readonly List<SliderChannelRuntime> _orderedChannels = new();

        ISliderInteractionAdapter? _adapter;
        IScopeNode? _acquiredScope;
        SliderEnvironmentKind _environmentKind;
        Canvas? _canvas;
        bool _isAcquired;

        public int ChannelCount => _orderedChannels.Count;

        public SliderChannelHubService(IScopeNode owner, SliderChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquiredScope = scope;
            _environmentKind = SliderRuntimeHelpers.ResolveEnvironment(_mb.transform, out _canvas);
            _isAcquired = true;
            RebuildAdapter(scope, isReset);
            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
            ReleaseAdapter(scope, isReset);
            _acquiredScope = null;
            _canvas = null;
            _isAcquired = false;
        }

        public void Tick()
        {
            if (!_isAcquired || _acquiredScope == null)
                return;

            _adapter?.Tick();

            var blockMask = UISelectionBlockMask.None;
            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                var runtime = _orderedChannels[i];
                runtime.Interaction?.BindAdapter(_adapter);
                runtime.Player.Tick();
                runtime.Interaction?.Tick();
                runtime.Visualizer.Tick();
                if (runtime.Interaction != null)
                    blockMask |= runtime.Interaction.DesiredSelectionBlockMask;
            }

            _adapter?.SetBlockMask(blockMask);
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(SliderRuntimeHelpers.NormalizeTag(tag));
        }

        public bool TryGetOutput(string tag, out ISliderOutput? output)
        {
            output = null;
            if (!_channels.TryGetValue(SliderRuntimeHelpers.NormalizeTag(tag), out var runtime))
                return false;

            output = runtime.Player;
            return true;
        }

        public bool TryGetControl(string tag, out ISliderControlService? control)
        {
            control = null;
            if (!_channels.TryGetValue(SliderRuntimeHelpers.NormalizeTag(tag), out var runtime))
                return false;

            control = runtime.Preset;
            return true;
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _orderedChannels.Count; i++)
                output.Add(_orderedChannels[i].Tag);
        }

        void RebuildChannels(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);

            var definitions = _mb.Channels;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                var tag = SliderRuntimeHelpers.NormalizeTag(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[SliderChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var options = definition.CreateOptions(_mb.transform);
                var preset = new SliderChannelPresetRuntime(options);
                var player = new SliderChannelPlayerRuntime(_owner, options, preset);
                var visualizer = new SliderChannelVisualizerRuntime(_owner, options, player, preset, _environmentKind, _canvas);
                var interaction = CreateInteractionRuntime(options, player, preset);

                preset.OnAcquire(scope, isReset);
                player.OnAcquire(scope, isReset);
                visualizer.OnAcquire(scope, isReset);
                interaction?.OnAcquire(scope, isReset);

                var runtime = new SliderChannelRuntime
                {
                    Tag = tag,
                    Options = options,
                    Preset = preset,
                    Player = player,
                    Visualizer = visualizer,
                    Interaction = interaction,
                };

                _channels.Add(tag, runtime);
                _orderedChannels.Add(runtime);
            }
        }

        void ReleaseChannels(IScopeNode scope, bool isReset)
        {
            for (var i = _orderedChannels.Count - 1; i >= 0; i--)
            {
                var runtime = _orderedChannels[i];
                runtime.Interaction?.OnRelease(scope, isReset);
                runtime.Visualizer.OnRelease(scope, isReset);
                runtime.Player.OnRelease(scope, isReset);
                runtime.Preset.OnRelease(scope, isReset);
            }

            _orderedChannels.Clear();
            _channels.Clear();
        }

        void RebuildAdapter(IScopeNode scope, bool isReset)
        {
            ReleaseAdapter(scope, isReset);
            _adapter = CreateAdapter();
            _adapter?.OnAcquire(scope, isReset);
        }

        void ReleaseAdapter(IScopeNode scope, bool isReset)
        {
            if (_adapter == null)
                return;

            _adapter.OnRelease(scope, isReset);
            _adapter.Dispose();
            _adapter = null;
        }

        ISliderInteractionRuntime? CreateInteractionRuntime(
            ISliderOptions options,
            ISliderPlayerRuntime player,
            ISliderRuntimePresetProvider presetProvider)
        {
            if (_environmentKind == SliderEnvironmentKind.ScreenUI && _canvas != null)
                return new UISliderInteractionRuntime(_owner, options, player, presetProvider, _canvas);

            if (_environmentKind == SliderEnvironmentKind.World)
                return new WorldSpaceSliderInteractionRuntime(options, player, presetProvider);

            return null;
        }

        ISliderInteractionAdapter? CreateAdapter()
        {
            if (_environmentKind == SliderEnvironmentKind.ScreenUI)
            {
                if (TryCreateUIAdapter(out var uiAdapter) && uiAdapter != null)
                    return uiAdapter;

                return null;
            }

            if (TryCreateWorldAdapter(out var worldAdapter) && worldAdapter != null)
                return worldAdapter;

            return null;
        }

        bool TryCreateUIAdapter(out ISliderInteractionAdapter? adapter)
        {
            adapter = null;
            if (!_owner.TryResolveInAncestors<IUIInputConsumerHub>(out var consumerHub) || consumerHub == null)
                return false;
            if (!_owner.TryResolveInAncestors<IUISelectionState>(out var selectionState) || selectionState == null)
                return false;
            if (!_owner.TryResolveInAncestors<IUISelectionNavigation>(out var selectionNavigation) || selectionNavigation == null)
                return false;

            var elementState = _owner.GetUIElementState();
            if (elementState == null)
                return false;

            _owner.TryResolveInAncestors<IUISelectionBlockService>(out var selectionBlockService);
            adapter = new UISliderChannelInteractionAdapter(
                _owner,
                consumerHub,
                elementState,
                selectionState,
                selectionNavigation,
                selectionBlockService,
                DispatchSignal);
            return true;
        }

        bool TryCreateWorldAdapter(out ISliderInteractionAdapter? adapter)
        {
            adapter = null;

            var selectable = _mb.GetComponent<SelectableRuntimeMB>();
            if (selectable == null)
                selectable = _mb.GetComponentInChildren<SelectableRuntimeMB>(true);

            var pointerTarget = selectable != null
                ? selectable.ResolveTarget()
                : _mb.GetComponent<WorldPointerTargetMB>();
            if (pointerTarget == null)
                pointerTarget = _mb.GetComponentInChildren<WorldPointerTargetMB>(true);
            if (pointerTarget == null)
                return false;

            adapter = new WorldPointerSliderInteractionAdapter(_mb.transform, pointerTarget, selectable, DispatchSignal);
            return true;
        }

        bool DispatchSignal(SliderInteractionSignal signal)
        {
            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                var interaction = _orderedChannels[i].Interaction;
                if (interaction != null && interaction.HandleSignal(signal))
                    return true;
            }

            return false;
        }
    }
}
