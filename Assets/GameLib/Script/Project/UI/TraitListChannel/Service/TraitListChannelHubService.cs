#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;

namespace Game.UI
{
    public interface ITraitListChannelHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        void GetTags(List<string> output);
        bool TryGetBinding(string tag, out TraitListChannelBinding? binding);
        UniTask<bool> BindAsync(string tag, TraitListChannelBindRequest request, bool rebuild, CancellationToken ct);
        UniTask<bool> RefreshAsync(string tag, TraitListChannelRefreshMode mode, CancellationToken ct);
        UniTask<bool> SetRangeAsync(string tag, bool useRange, TraitListChannelRange range, bool rebuild, CancellationToken ct);
        UniTask<bool> ClearAsync(string tag, bool keepBinding, CancellationToken ct);
    }

    public sealed class TraitListChannelHubService :
        ITraitListChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly TraitListChannelHubMB _mb;
        readonly Dictionary<string, TraitListChannelRuntime> _channels = new(StringComparer.Ordinal);
        readonly List<TraitListChannelRuntime> _orderedChannels = new();

        public int ChannelCount => _orderedChannels.Count;

        public TraitListChannelHubService(IScopeNode owner, TraitListChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            ReleaseChannels(scope, isReset);
        }

        public bool Contains(string tag)
        {
            return _channels.ContainsKey(TraitListChannelRuntimeHelpers.NormalizeTag(tag));
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _orderedChannels.Count; i++)
                output.Add(_orderedChannels[i].Tag);
        }

        public bool TryGetBinding(string tag, out TraitListChannelBinding? binding)
        {
            binding = null;
            if (!_channels.TryGetValue(TraitListChannelRuntimeHelpers.NormalizeTag(tag), out var runtime))
                return false;

            return runtime.TryGetBinding(out binding);
        }

        public UniTask<bool> BindAsync(string tag, TraitListChannelBindRequest request, bool rebuild, CancellationToken ct)
        {
            if (!_channels.TryGetValue(TraitListChannelRuntimeHelpers.NormalizeTag(tag), out var runtime))
                return UniTask.FromResult(false);

            return runtime.BindAsync(request, rebuild, ct);
        }

        public UniTask<bool> RefreshAsync(string tag, TraitListChannelRefreshMode mode, CancellationToken ct)
        {
            if (!_channels.TryGetValue(TraitListChannelRuntimeHelpers.NormalizeTag(tag), out var runtime))
                return UniTask.FromResult(false);

            return runtime.RefreshAsync(mode, ct);
        }

        public UniTask<bool> SetRangeAsync(string tag, bool useRange, TraitListChannelRange range, bool rebuild, CancellationToken ct)
        {
            if (!_channels.TryGetValue(TraitListChannelRuntimeHelpers.NormalizeTag(tag), out var runtime))
                return UniTask.FromResult(false);

            return runtime.SetRangeAsync(useRange, range, rebuild, ct);
        }

        public UniTask<bool> ClearAsync(string tag, bool keepBinding, CancellationToken ct)
        {
            if (!_channels.TryGetValue(TraitListChannelRuntimeHelpers.NormalizeTag(tag), out var runtime))
                return UniTask.FromResult(false);

            return runtime.ClearAsync(keepBinding, ct);
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

                var tag = TraitListChannelRuntimeHelpers.NormalizeTag(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[TraitListChannelHub] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var runtime = new TraitListChannelRuntime(_owner, _mb, definition, tag);
                runtime.OnAcquire(scope, isReset);
                _channels.Add(tag, runtime);
                _orderedChannels.Add(runtime);
            }
        }

        void ReleaseChannels(IScopeNode scope, bool isReset)
        {
            for (var i = _orderedChannels.Count - 1; i >= 0; i--)
                _orderedChannels[i].OnRelease(scope, isReset);

            _orderedChannels.Clear();
            _channels.Clear();
        }
    }
}
