#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;
using VContainer.Unity;

namespace Game.Dialogue
{
    public interface IDialogueChannelService
    {
        string Tag { get; }
        DialogueChannelSnapshot Snapshot { get; }

        event Action<DialogueChannelSnapshot>? OnStateChanged;

        UniTask<bool> SetupAsync(DialogueSetupRequest request, CancellationToken ct);
        UniTask<DialogueMessageResult> ShowMessageAsync(DialogueMessageRequest request, CancellationToken ct);
        UniTask<DialogueChoiceResult> ShowChoiceAndWaitAsync(DialogueChoiceRequest request, CancellationToken ct);
        UniTask<bool> ApplyCharactersAsync(DialogueCharacterFrameRequest request, CancellationToken ct);
        UniTask<bool> RefreshLayoutAsync(DialogueLayoutRefreshRequest request, CancellationToken ct);
        UniTask<bool> EndAsync(DialogueEndRequest request, CancellationToken ct);

        bool SetVisible(bool visible);
        bool SetActive(bool active);
        bool SetInputEnabled(bool enabled);
        bool TryRequestAdvance();
        bool TryCancelChoice(string reason = "");
    }

    public interface IDialogueChannelHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        void GetTags(List<string> output);
        IDialogueChannelService GetChannel(string tag);
        bool TryGetChannel(string tag, out IDialogueChannelService? channel);
        bool RegisterOrReplace(string tag, DialogueChannelPreset preset);
        bool Unregister(string tag);
    }

    public sealed class DialogueChannelHubService :
        IDialogueChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        IScopeTickHandler
    {
        sealed class ChannelEntry
        {
            public string Tag = "default";
            public DialogueChannelDefinition Definition = null!;
            public DialogueChannelRuntime Runtime = null!;
        }

        readonly IScopeNode _owner;
        readonly DialogueChannelHubMB _mb;
        readonly Dictionary<string, ChannelEntry> _channels = new(StringComparer.Ordinal);
        readonly List<ChannelEntry> _orderedChannels = new();

        IScopeNode? _activeScope;
        bool _isAcquired;

        public int ChannelCount => _orderedChannels.Count;

        public DialogueChannelHubService(IScopeNode owner, DialogueChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _activeScope = scope;
            _isAcquired = true;
            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            ReleaseChannels(scope, isReset);
            _activeScope = null;
            _isAcquired = false;
        }

        public void Tick()
        {
            for (var i = 0; i < _orderedChannels.Count; i++)
                _orderedChannels[i].Runtime.Tick();
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(DialogueTagUtility.Normalize(tag));
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _orderedChannels.Count; i++)
                output.Add(_orderedChannels[i].Tag);
        }

        public IDialogueChannelService GetChannel(string tag)
        {
            var normalized = DialogueTagUtility.Normalize(tag);
            if (_channels.TryGetValue(normalized, out var entry) && entry?.Runtime != null)
                return entry.Runtime;

            throw new KeyNotFoundException($"DialogueChannel '{normalized}' was not found.");
        }

        public bool TryGetChannel(string tag, out IDialogueChannelService? channel)
        {
            channel = null;
            var normalized = DialogueTagUtility.Normalize(tag);
            if (!_channels.TryGetValue(normalized, out var entry) || entry?.Runtime == null)
                return false;

            channel = entry.Runtime;
            return true;
        }

        public bool RegisterOrReplace(string tag, DialogueChannelPreset preset)
        {
            if (preset == null)
                return false;

            var normalized = DialogueTagUtility.Normalize(tag);
            var definition = new DialogueChannelDefinition();
            var runtimePreset = preset.CreateRuntimeCopy();
            var options = DynamicValue<DialogueChannelPreset>.FromSource(
                new ManagedRefLiteralSource<DialogueChannelPreset>(runtimePreset));

            if (_channels.TryGetValue(normalized, out var existing) && existing != null)
            {
                existing.Definition = definition;
                existing.Runtime.SetSourcePreset(options);
                if (_isAcquired && _activeScope != null)
                    existing.Runtime.RebuildPreset(_activeScope);
                return true;
            }

            var runtime = new DialogueChannelRuntime(_owner, normalized, options, _mb.EnableDebugLog);
            var entry = new ChannelEntry
            {
                Tag = normalized,
                Definition = definition,
                Runtime = runtime,
            };

            _channels[normalized] = entry;
            _orderedChannels.Add(entry);

            if (_isAcquired && _activeScope != null)
                runtime.OnAcquire(_activeScope, false);

            return true;
        }

        public bool Unregister(string tag)
        {
            var normalized = DialogueTagUtility.Normalize(tag);
            if (!_channels.TryGetValue(normalized, out var entry) || entry == null)
                return false;

            if (_isAcquired && _activeScope != null)
                entry.Runtime.OnRelease(_activeScope, false);

            _channels.Remove(normalized);
            _orderedChannels.Remove(entry);
            return true;
        }

        void RebuildChannels(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);

            var defs = _mb.Channels;
            for (var i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null)
                    continue;

                var tag = DialogueTagUtility.Normalize(def.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[DialogueChannelHub] Duplicate channel tag was skipped. tag='{tag}'");
                    continue;
                }

                var runtime = new DialogueChannelRuntime(_owner, tag, def.PresetValue, _mb.EnableDebugLog);
                runtime.OnAcquire(scope, isReset);

                var entry = new ChannelEntry
                {
                    Tag = tag,
                    Definition = def,
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

            _orderedChannels.Clear();
            _channels.Clear();
        }
    }
}
