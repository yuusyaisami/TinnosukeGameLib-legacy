#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class WorldSliderChannelHubService :
        IWorldSliderChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable
    {
        sealed class ChannelRuntime
        {
            public string Tag = "default";
            public WorldSliderChannelOptions Options = null!;
            public WorldSliderChannelPresetRuntime Preset = null!;
            public WorldSliderChannelPlayerRuntime Player = null!;
            public WorldSliderChannelVisualizerRuntime Visualizer = null!;
        }

        readonly IScopeNode _owner;
        readonly WorldSliderChannelHubMB _mb;
        readonly Dictionary<string, ChannelRuntime> _channels = new(StringComparer.Ordinal);
        readonly List<ChannelRuntime> _orderedChannels = new();

        public int ChannelCount => _orderedChannels.Count;

        public WorldSliderChannelHubService(IScopeNode owner, WorldSliderChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = isReset;

            ClearChannels(scope, isReset);

            var definitions = _mb.Channels;
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                    continue;

                var tag = NormalizeTag(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[WorldSliderChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var options = definition.CreateOptions(_mb.transform);
                var preset = new WorldSliderChannelPresetRuntime(options);
                var player = new WorldSliderChannelPlayerRuntime(_owner, options, preset);
                var visualizer = new WorldSliderChannelVisualizerRuntime(_owner, options, player, preset);

                preset.OnAcquire(scope, isReset);
                player.OnAcquire(scope, isReset);
                visualizer.OnAcquire(scope, isReset);

                var runtime = new ChannelRuntime
                {
                    Tag = tag,
                    Options = options,
                    Preset = preset,
                    Player = player,
                    Visualizer = visualizer,
                };

                _channels.Add(tag, runtime);
                _orderedChannels.Add(runtime);
            }
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ClearChannels(scope, isReset);
        }

        public void Tick()
        {
            for (int i = 0; i < _orderedChannels.Count; i++)
            {
                var runtime = _orderedChannels[i];
                runtime.Player.Tick();
                runtime.Visualizer.Tick();
            }
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(NormalizeTag(tag));
        }

        public bool TryGetOutput(string tag, out IWorldSliderOutput? output)
        {
            output = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var runtime))
                return false;

            output = runtime.Player;
            return true;
        }

        public bool TryGetControl(string tag, out IWorldSliderControlService? control)
        {
            control = null;
            if (!_channels.TryGetValue(NormalizeTag(tag), out var runtime))
                return false;

            control = runtime.Preset;
            return true;
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (int i = 0; i < _orderedChannels.Count; i++)
                output.Add(_orderedChannels[i].Tag);
        }

        void ClearChannels(IScopeNode scope, bool isReset)
        {
            for (int i = _orderedChannels.Count - 1; i >= 0; i--)
            {
                var runtime = _orderedChannels[i];
                runtime.Visualizer.OnRelease(scope, isReset);
                runtime.Player.OnRelease(scope, isReset);
                runtime.Preset.OnRelease(scope, isReset);
            }

            _orderedChannels.Clear();
            _channels.Clear();
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}
