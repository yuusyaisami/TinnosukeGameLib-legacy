#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;
using VContainer.Unity;

namespace Game.Commands
{
    public sealed class CommandListChannelHubService :
        ICommandListChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
    {
        sealed class ChannelEntry
        {
            public string Tag = "default";
            public CommandListChannelOptions Options = null!;
            public CommandListChannelPresetRuntime PresetRuntime = null!;
            public CommandListChannelPlayerRuntime Runtime = null!;
        }

        readonly IScopeNode _owner;
        readonly CommandListChannelHubMB _mb;
        readonly Dictionary<string, ChannelEntry> _channels = new(StringComparer.Ordinal);
        readonly List<ChannelEntry> _orderedChannels = new();

        IScopeNode? _activeScope;
        bool _isAcquired;

        public int ChannelCount => _orderedChannels.Count;

        public CommandListChannelHubService(IScopeNode owner, CommandListChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _activeScope = scope;
            _isAcquired = true;
            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
            _activeScope = null;
            _isAcquired = false;
        }

        public void Tick()
        {
            var deltaTime = Time.deltaTime;
            for (var i = 0; i < _orderedChannels.Count; i++)
                _orderedChannels[i].Runtime.Tick(deltaTime);
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(NormalizeTag(tag));
        }

        public bool TryGetPlayer(string tag, out ICommandListChannelPlayer? player)
        {
            player = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            player = entry.Runtime;
            return true;
        }

        public bool TryGetCommand(string tag, out ICommandListChannelCommandService? command)
        {
            command = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            command = entry.Runtime;
            return true;
        }

        public bool TryGetControl(string tag, out ICommandListChannelControlService? control)
        {
            control = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var entry))
                return false;

            control = entry.Runtime;
            return true;
        }

        public bool RegisterOrReplace(string tag, CommandListChannelPreset preset)
        {
            if (preset == null)
                return false;

            var normalizedTag = NormalizeTag(tag);
            var options = new CommandListChannelOptions
            {
                PresetValue = DynamicValue<CommandListChannelPreset>.FromSource(
                    new Game.Common.ManagedRefLiteralSource<CommandListChannelPreset>(preset.CreateRuntimeCopy())),
            };

            if (_channels.TryGetValue(normalizedTag, out var existing))
            {
                if (_isAcquired && _activeScope != null)
                {
                    existing.Runtime.OnRelease(_activeScope, false);
                    existing.PresetRuntime.OnRelease(_activeScope, false);
                }

                existing.Options = options;
                existing.PresetRuntime = new CommandListChannelPresetRuntime(options);
                existing.Runtime = new CommandListChannelPlayerRuntime(_owner, normalizedTag, existing.PresetRuntime);
                if (_isAcquired && _activeScope != null)
                {
                    existing.PresetRuntime.OnAcquire(_activeScope, false);
                    existing.Runtime.OnAcquire(_activeScope, false);
                }

                return true;
            }

            var entry = CreateEntry(normalizedTag, options);
            _channels.Add(normalizedTag, entry);
            _orderedChannels.Add(entry);
            if (_isAcquired && _activeScope != null)
            {
                entry.PresetRuntime.OnAcquire(_activeScope, false);
                entry.Runtime.OnAcquire(_activeScope, false);
            }

            return true;
        }

        public bool Unregister(string tag)
        {
            var normalizedTag = NormalizeTag(tag);
            if (!_channels.TryGetValue(normalizedTag, out var entry))
                return false;

            if (_isAcquired && _activeScope != null)
            {
                entry.Runtime.OnRelease(_activeScope, false);
                entry.PresetRuntime.OnRelease(_activeScope, false);
            }

            _channels.Remove(normalizedTag);
            _orderedChannels.Remove(entry);
            return true;
        }

        public void Clear()
        {
            if (_activeScope != null)
                ReleaseChannels(_activeScope, false);
            else
            {
                _channels.Clear();
                _orderedChannels.Clear();
            }
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
                    Debug.LogWarning($"[CommandListChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var entry = CreateEntry(tag, definition.CreateOptions());
                entry.PresetRuntime.OnAcquire(scope, isReset);
                entry.Runtime.OnAcquire(scope, isReset);
                _channels.Add(tag, entry);
                _orderedChannels.Add(entry);
            }
        }

        void ReleaseChannels(IScopeNode scope, bool isReset)
        {
            for (var i = _orderedChannels.Count - 1; i >= 0; i--)
            {
                var entry = _orderedChannels[i];
                entry.Runtime.OnRelease(scope, isReset);
                entry.PresetRuntime.OnRelease(scope, isReset);
            }

            _channels.Clear();
            _orderedChannels.Clear();
        }

        ChannelEntry CreateEntry(string tag, CommandListChannelOptions options)
        {
            var presetRuntime = new CommandListChannelPresetRuntime(options);
            var runtime = new CommandListChannelPlayerRuntime(_owner, tag, presetRuntime);
            return new ChannelEntry
            {
                Tag = tag,
                Options = options,
                PresetRuntime = presetRuntime,
                Runtime = runtime,
            };
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}
