#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Input;
using Game.SelectRuntime;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class ButtonChannelHubService :
        IButtonChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
    {
        sealed class ChannelEntry
        {
            public string Tag = "default";
            public ButtonChannelOptions Options = null!;
            public ButtonChannelRuntime Runtime = null!;
        }

        readonly IScopeNode _owner;
        readonly ButtonChannelHubMB _mb;
        readonly Dictionary<string, ChannelEntry> _channels = new(StringComparer.Ordinal);
        readonly List<ChannelEntry> _orderedChannels = new();

        IButtonChannelInteractionAdapter? _adapter;
        IScopeNode? _acquiredScope;
        UISelectionBlockMask _lastBlockMask;
        bool _isAcquired;

        public int ChannelCount => _orderedChannels.Count;

        public ButtonChannelHubService(IScopeNode owner, ButtonChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquiredScope = scope;
            _isAcquired = true;
            _lastBlockMask = UISelectionBlockMask.None;
            RebuildAdapter(scope, isReset);
            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
            ReleaseAdapter(scope, isReset);
            _acquiredScope = null;
            _lastBlockMask = UISelectionBlockMask.None;
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
                var runtime = _orderedChannels[i].Runtime;
                runtime.BindAdapter(_adapter);
                runtime.Tick();
                blockMask |= runtime.DesiredSelectionBlockMask;
            }

            if (_lastBlockMask == blockMask)
                return;

            _lastBlockMask = blockMask;
            _adapter?.SetBlockMask(blockMask);
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(NormalizeTag(tag));
        }

        public bool TryGetOutput(string tag, out IButtonChannelOutput? output)
        {
            output = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            output = entry.Runtime;
            return true;
        }

        public bool TryGetControl(string tag, out IButtonChannelControlService? control)
        {
            control = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            control = entry.Runtime;
            return true;
        }

        public bool RegisterOrReplace(string tag, ButtonChannelPreset preset)
        {
            if (preset == null)
                return false;

            var normalizedTag = NormalizeTag(tag);
            var options = new ButtonChannelOptions
            {
                OwnerTransform = _mb.transform,
                PresetValue = DynamicValue<ButtonChannelPreset>.FromSource(
                    new ManagedRefLiteralSource<ButtonChannelPreset>(preset.CreateRuntimeCopy())),
            };

            if (_channels.TryGetValue(normalizedTag, out var existing))
            {
                if (_isAcquired && _acquiredScope != null)
                    existing.Runtime.OnRelease(_acquiredScope, false);

                existing.Options = options;
                existing.Runtime = new ButtonChannelRuntime(_owner, normalizedTag, options);
                if (_isAcquired && _acquiredScope != null)
                    existing.Runtime.OnAcquire(_acquiredScope, false, _adapter);
                return true;
            }

            var entry = new ChannelEntry
            {
                Tag = normalizedTag,
                Options = options,
                Runtime = new ButtonChannelRuntime(_owner, normalizedTag, options),
            };

            _channels.Add(normalizedTag, entry);
            _orderedChannels.Add(entry);
            if (_isAcquired && _acquiredScope != null)
                entry.Runtime.OnAcquire(_acquiredScope, false, _adapter);
            return true;
        }

        public bool Unregister(string tag)
        {
            var normalizedTag = NormalizeTag(tag);
            if (!_channels.TryGetValue(normalizedTag, out var entry))
                return false;

            if (_isAcquired && _acquiredScope != null)
                entry.Runtime.OnRelease(_acquiredScope, false);

            _channels.Remove(normalizedTag);
            _orderedChannels.Remove(entry);
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

                var tag = NormalizeTag(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[ButtonChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var options = definition.CreateOptions(_mb.transform);
                var runtime = new ButtonChannelRuntime(_owner, tag, options);
                runtime.OnAcquire(scope, isReset, _adapter);

                var entry = new ChannelEntry
                {
                    Tag = tag,
                    Options = options,
                    Runtime = runtime,
                };

                _channels.Add(tag, entry);
                _orderedChannels.Add(entry);
            }
        }

        void ReleaseChannels(IScopeNode scope, bool isReset)
        {
            for (var i = _orderedChannels.Count - 1; i >= 0; i--)
                _orderedChannels[i].Runtime.OnRelease(scope, isReset);

            _channels.Clear();
            _orderedChannels.Clear();
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

        IButtonChannelInteractionAdapter? CreateAdapter()
        {
            if (TryCreateUIAdapter(out var uiAdapter) && uiAdapter != null)
                return uiAdapter;

            if (TryCreateWorldAdapter(out var worldAdapter) && worldAdapter != null)
                return worldAdapter;

            if (TryCreateGameRootAdapter(out var gameRootAdapter) && gameRootAdapter != null)
                return gameRootAdapter;

            return null;
        }

        bool TryCreateUIAdapter(out IButtonChannelInteractionAdapter? adapter)
        {
            adapter = null;
            if (!_owner.TryResolveInAncestors<IUIInputConsumerHub>(out var consumerHub) || consumerHub == null)
                return false;

            if (!_owner.TryResolveInAncestors<IUISelectionState>(out var selectionState) || selectionState == null)
                return false;

            var elementState = _owner.GetUIElementState();
            if (elementState == null)
                return false;

            _owner.TryResolveInAncestors<IUISelectionBlockService>(out var selectionBlockService);
            adapter = new UIButtonChannelInteractionAdapter(
                _owner,
                consumerHub,
                elementState,
                selectionState,
                selectionBlockService,
                DispatchSignal);
            return true;
        }

        bool TryCreateWorldAdapter(out IButtonChannelInteractionAdapter? adapter)
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

            adapter = new WorldButtonChannelInteractionAdapter(_mb.transform, pointerTarget, selectable, DispatchSignal);
            return true;
        }

        bool TryCreateGameRootAdapter(out IButtonChannelInteractionAdapter? adapter)
        {
            adapter = null;
            if (!_owner.TryResolveInAncestors<IInputRouter>(out var inputRouter) || inputRouter == null)
                return false;

            adapter = new GameRootButtonChannelInteractionAdapter(inputRouter, DispatchSignal);
            return true;
        }

        bool DispatchSignal(ButtonChannelInteractionSignal signal)
        {
            var handled = false;
            for (var i = 0; i < _orderedChannels.Count; i++)
            {
                if (_orderedChannels[i].Runtime.HandleSignal(signal))
                    handled = true;
            }

            return handled;
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}
