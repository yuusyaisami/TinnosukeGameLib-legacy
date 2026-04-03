#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;

namespace Game.Channel
{
    public interface IGridObjectChannelHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        void GetTags(List<string> output);
        UniTask<bool> BindAsync(string tag, GridObjectChannelBindRequest request, bool rebuild, CancellationToken ct);
        UniTask<bool> RefreshAsync(string tag, GridObjectChannelRefreshMode mode, CancellationToken ct);
        UniTask<bool> ClearAsync(string tag, bool keepBinding, CancellationToken ct);
    }

    public sealed class GridObjectChannelHubService :
        IGridObjectChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly GridObjectChannelHubMB _mb;
        readonly Dictionary<string, GridObjectChannelRuntime> _channels = new(StringComparer.Ordinal);
        readonly List<GridObjectChannelRuntime> _orderedChannels = new();

        public int ChannelCount => _orderedChannels.Count;

        public GridObjectChannelHubService(IScopeNode owner, GridObjectChannelHubMB mb)
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
            return _channels.ContainsKey(NormalizeTag(tag));
        }

        public void GetTags(List<string> output)
        {
            if (output == null)
                return;

            output.Clear();
            for (var i = 0; i < _orderedChannels.Count; i++)
                output.Add(_orderedChannels[i].Tag);
        }

        public UniTask<bool> BindAsync(string tag, GridObjectChannelBindRequest request, bool rebuild, CancellationToken ct)
        {
            if (!_channels.TryGetValue(NormalizeTag(tag), out var runtime))
                return UniTask.FromResult(false);

            return runtime.BindAsync(request, rebuild, ct);
        }

        public UniTask<bool> RefreshAsync(string tag, GridObjectChannelRefreshMode mode, CancellationToken ct)
        {
            if (!_channels.TryGetValue(NormalizeTag(tag), out var runtime))
                return UniTask.FromResult(false);

            return runtime.RefreshAsync(mode, ct);
        }

        public UniTask<bool> ClearAsync(string tag, bool keepBinding, CancellationToken ct)
        {
            if (!_channels.TryGetValue(NormalizeTag(tag), out var runtime))
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

                var tag = NormalizeTag(definition.ChannelTag);
                if (_channels.ContainsKey(tag))
                {
                    Debug.LogWarning($"[GridObjectChannel] Duplicate channel tag '{tag}' was skipped.");
                    continue;
                }

                var runtime = new GridObjectChannelRuntime(_owner, _mb, definition, tag);
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

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }
    }
}
