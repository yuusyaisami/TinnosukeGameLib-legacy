#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    public interface IChoiceSessionHandle
    {
        string ChannelTag { get; }
        bool IsCompleted { get; }
        bool Cancel(string reason = "");
        UniTask<GridObjectChoiceSessionResult> WaitAsync(CancellationToken ct);
    }

    public interface IChoiceChannelHubService
    {
        bool IsChoiceSessionActive(string tag);
        bool TryGetChoiceSession(string tag, out IChoiceSessionHandle? session);
        UniTask<GridObjectChoiceSessionResult> ShowChoiceAndWaitAsync(string tag, GridObjectChoiceRequest request, CancellationToken ct);
        bool CancelChoice(string tag, string reason = "");
    }

    public sealed class GridObjectChannelHubService :
        IGridObjectChannelHubService,
        IChoiceChannelHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        sealed class ChoiceSessionHandle : IChoiceSessionHandle
        {
            readonly GridObjectChannelRuntime _runtime;
            readonly Task<GridObjectChoiceSessionResult> _task;
            bool _isCompleted;

            public ChoiceSessionHandle(string channelTag, GridObjectChannelRuntime runtime, Task<GridObjectChoiceSessionResult> task)
            {
                ChannelTag = channelTag;
                _runtime = runtime;
                _task = task;

                _task.ContinueWith(_ => _isCompleted = true, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            public string ChannelTag { get; }
            public bool IsCompleted => _isCompleted;

            public bool Cancel(string reason = "")
            {
                return _runtime.TryCancelActiveChoice(reason);
            }

            public UniTask<GridObjectChoiceSessionResult> WaitAsync(CancellationToken ct)
            {
                return ct.CanBeCanceled ? _task.AsUniTask().AttachExternalCancellation(ct) : _task.AsUniTask();
            }
        }

        readonly IScopeNode _owner;
        readonly GridObjectChannelHubMB _mb;
        readonly Dictionary<string, GridObjectChannelRuntime> _channels = new(StringComparer.Ordinal);
        readonly Dictionary<string, IChoiceSessionHandle> _choiceSessions = new(StringComparer.Ordinal);
        readonly List<GridObjectChannelRuntime> _orderedChannels = new();

        public int ChannelCount => _orderedChannels.Count;

        public GridObjectChannelHubService(IScopeNode owner, GridObjectChannelHubMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            RebuildChannels(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

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

        public bool IsChoiceSessionActive(string tag)
        {
            if (!_channels.TryGetValue(NormalizeTag(tag), out var runtime))
                return false;

            return runtime.IsChoiceSessionActive;
        }

        public bool TryGetChoiceSession(string tag, out IChoiceSessionHandle? session)
        {
            session = null;
            var normalizedTag = NormalizeTag(tag);
            if (!_choiceSessions.TryGetValue(normalizedTag, out var handle))
                return false;

            if (handle == null || handle.IsCompleted)
            {
                _choiceSessions.Remove(normalizedTag);
                return false;
            }

            session = handle;
            return true;
        }

        public async UniTask<GridObjectChoiceSessionResult> ShowChoiceAndWaitAsync(
            string tag,
            GridObjectChoiceRequest request,
            CancellationToken ct)
        {
            if (request == null)
                return GridObjectChoiceSessionResult.Failed("[GOC-CHOICE-100] Choice request is null.");

            var normalizedTag = NormalizeTag(tag);
            if (!_channels.TryGetValue(normalizedTag, out var runtime) || runtime == null)
                return GridObjectChoiceSessionResult.Failed($"[GOC-CHOICE-101] GridObjectChannel '{normalizedTag}' was not found.");

            var task = runtime.ShowChoiceAndWaitAsync(request, ct).AsTask();
            var handle = new ChoiceSessionHandle(normalizedTag, runtime, task);
            _choiceSessions[normalizedTag] = handle;

            try
            {
                return await task;
            }
            finally
            {
                if (_choiceSessions.TryGetValue(normalizedTag, out var current) && ReferenceEquals(current, handle))
                    _choiceSessions.Remove(normalizedTag);
            }
        }

        public bool CancelChoice(string tag, string reason = "")
        {
            var normalizedTag = NormalizeTag(tag);
            if (_choiceSessions.TryGetValue(normalizedTag, out var handle) && handle != null && !handle.IsCompleted)
                return handle.Cancel(reason);

            if (!_channels.TryGetValue(normalizedTag, out var runtime) || runtime == null)
                return false;

            return runtime.TryCancelActiveChoice(reason);
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
            if (_choiceSessions.Count > 0)
            {
                foreach (var pair in _choiceSessions)
                    pair.Value?.Cancel($"[GOC-CHOICE-102] Hub released. tag='{pair.Key}'");

                _choiceSessions.Clear();
            }

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
