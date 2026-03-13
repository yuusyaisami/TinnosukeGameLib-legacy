#nullable enable

using System;
using System.Collections.Generic;
using Game.Spawn;
using UnityEngine;

namespace Game.UI
{
    public interface IDialogChannelHubService
    {
        int ChannelCount { get; }

        void RegisterChannel(string key, DialogChannelDef def);
        bool TryGetChannel(string key, out DialogChannelRuntime? channel);
        void UnregisterChannel(string key);

        void SetSubscribeBindings(string key, DialogEventBinding[] bindings);
        void AddSubscribeBindings(string key, DialogEventBinding[] bindings);
    }

    public sealed class DialogChannelHubService : IDialogChannelHubService, Game.IScopeReleaseHandler, IDisposable
    {
        readonly Dictionary<string, DialogChannelRuntime> _channels = new(StringComparer.Ordinal);
        readonly DialogChannelDef[] _defs;
        readonly ISceneSpawnerRegistry _spawnerRegistry;
        readonly IUIModalStackService? _modalStackService;
        readonly IUIModalStackTelemetry? _modalTelemetry;

        public int ChannelCount => _channels.Count;

        public DialogChannelHubService(
            ISceneSpawnerRegistry spawnerRegistry,
            DialogChannelDef[] defs,
            IUIModalStackService? modalStackService = null,
            IUIModalStackTelemetry? modalTelemetry = null)
        {
            _spawnerRegistry = spawnerRegistry ?? throw new ArgumentNullException(nameof(spawnerRegistry));
            _modalStackService = modalStackService;
            _modalTelemetry = modalTelemetry;
            _defs = defs ?? Array.Empty<DialogChannelDef>();

            for (int i = 0; i < _defs.Length; i++)
            {
                var def = _defs[i];
                if (def == null)
                    continue;

                RegisterChannel(def.Tag, def);
            }
        }

        public void RegisterChannel(string key, DialogChannelDef def)
        {
            if (string.IsNullOrWhiteSpace(key) || def == null)
                return;

            if (_channels.TryGetValue(key, out var existing))
            {
                existing.Hide(DialogCloseReason.Replaced);
                existing.Dispose();
                _channels.Remove(key);
            }

            var runtime = new DialogChannelRuntime(
                channelKey: key,
                def: def,
                spawnerRegistry: _spawnerRegistry,
                modalStackService: _modalStackService,
                modalTelemetry: _modalTelemetry);

            _channels[key] = runtime;
        }

        public bool TryGetChannel(string key, out DialogChannelRuntime? channel)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                channel = null;
                return false;
            }

            return _channels.TryGetValue(key, out channel);
        }

        public void UnregisterChannel(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (!_channels.TryGetValue(key, out var runtime))
                return;

            runtime.Hide(DialogCloseReason.Explicit);
            runtime.Dispose();
            _channels.Remove(key);
        }

        public void SetSubscribeBindings(string key, DialogEventBinding[] bindings)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (!_channels.TryGetValue(key, out var runtime) || runtime == null)
                return;

            runtime.SetSubscribeBindings(bindings);
        }

        public void AddSubscribeBindings(string key, DialogEventBinding[] bindings)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            if (!_channels.TryGetValue(key, out var runtime) || runtime == null)
                return;

            runtime.AddSubscribeBindings(bindings);
        }

        public void OnRelease(Game.IScopeNode scope, bool isReset)
        {
            Dispose();
        }

        public void Dispose()
        {
            foreach (var kv in _channels)
            {
                try
                {
                    kv.Value.Hide(DialogCloseReason.Explicit);
                    kv.Value.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            _channels.Clear();
        }
    }
}
