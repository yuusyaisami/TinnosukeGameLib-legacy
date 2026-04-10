#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Dialogue
{
    public interface IDialogueService
    {
        bool TryGetSnapshot(string channelTag, out DialogueChannelSnapshot snapshot);
        bool SetVisible(string channelTag, bool visible);
        bool SetActive(string channelTag, bool active);
        bool SetInputEnabled(string channelTag, bool enabled);
        bool TryRequestAdvance(string channelTag);
        bool TryCancelChoice(string channelTag, string reason = "");

        UniTask<bool> SetupAsync(string channelTag, DialogueSetupRequest request, CancellationToken ct = default);
        UniTask<DialogueMessageResult> ShowMessageAsync(string channelTag, DialogueMessageRequest request, CancellationToken ct = default);
        UniTask<DialogueChoiceResult> ShowChoiceAndWaitAsync(string channelTag, DialogueChoiceRequest request, CancellationToken ct = default);
        UniTask<bool> ApplyCharactersAsync(string channelTag, DialogueCharacterFrameRequest request, CancellationToken ct = default);
        UniTask<bool> RefreshLayoutAsync(string channelTag, DialogueLayoutRefreshRequest request, CancellationToken ct = default);
        UniTask<bool> EndAsync(string channelTag, DialogueEndRequest request, CancellationToken ct = default);
    }

    public sealed class DialogueService : IDialogueService
    {
        readonly IDialogueChannelHubService _hub;

        public DialogueService(IDialogueChannelHubService hub)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        public bool TryGetSnapshot(string channelTag, out DialogueChannelSnapshot snapshot)
        {
            if (!TryGetChannel(channelTag, out var channel) || channel == null)
            {
                snapshot = default;
                return false;
            }

            snapshot = channel.Snapshot;
            return true;
        }

        public bool SetVisible(string channelTag, bool visible)
        {
            return TryGetChannel(channelTag, out var channel) && channel != null && channel.SetVisible(visible);
        }

        public bool SetActive(string channelTag, bool active)
        {
            return TryGetChannel(channelTag, out var channel) && channel != null && channel.SetActive(active);
        }

        public bool SetInputEnabled(string channelTag, bool enabled)
        {
            return TryGetChannel(channelTag, out var channel) && channel != null && channel.SetInputEnabled(enabled);
        }

        public bool TryRequestAdvance(string channelTag)
        {
            return TryGetChannel(channelTag, out var channel) && channel != null && channel.TryRequestAdvance();
        }

        public bool TryCancelChoice(string channelTag, string reason = "")
        {
            return TryGetChannel(channelTag, out var channel) && channel != null && channel.TryCancelChoice(reason);
        }

        public UniTask<bool> SetupAsync(string channelTag, DialogueSetupRequest request, CancellationToken ct = default)
        {
            if (!TryGetChannel(channelTag, out var channel) || channel == null)
                return UniTask.FromResult(false);

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueSetupRequest();
            return channel.SetupAsync(runtimeRequest, ct);
        }

        public UniTask<DialogueMessageResult> ShowMessageAsync(string channelTag, DialogueMessageRequest request, CancellationToken ct = default)
        {
            if (!TryGetChannel(channelTag, out var channel) || channel == null)
                return UniTask.FromResult(DialogueMessageResult.Failed($"[DIALOGUE-300] Channel not found. tag='{DialogueTagUtility.Normalize(channelTag)}'"));

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueMessageRequest();
            return channel.ShowMessageAsync(runtimeRequest, ct);
        }

        public UniTask<DialogueChoiceResult> ShowChoiceAndWaitAsync(string channelTag, DialogueChoiceRequest request, CancellationToken ct = default)
        {
            if (!TryGetChannel(channelTag, out var channel) || channel == null)
                return UniTask.FromResult(DialogueChoiceResult.Failed($"[DIALOGUE-301] Channel not found. tag='{DialogueTagUtility.Normalize(channelTag)}'"));

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueChoiceRequest();
            return channel.ShowChoiceAndWaitAsync(runtimeRequest, ct);
        }

        public UniTask<bool> ApplyCharactersAsync(string channelTag, DialogueCharacterFrameRequest request, CancellationToken ct = default)
        {
            if (!TryGetChannel(channelTag, out var channel) || channel == null)
                return UniTask.FromResult(false);

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueCharacterFrameRequest();
            return channel.ApplyCharactersAsync(runtimeRequest, ct);
        }

        public UniTask<bool> RefreshLayoutAsync(string channelTag, DialogueLayoutRefreshRequest request, CancellationToken ct = default)
        {
            if (!TryGetChannel(channelTag, out var channel) || channel == null)
                return UniTask.FromResult(false);

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueLayoutRefreshRequest();
            return channel.RefreshLayoutAsync(runtimeRequest, ct);
        }

        public UniTask<bool> EndAsync(string channelTag, DialogueEndRequest request, CancellationToken ct = default)
        {
            if (!TryGetChannel(channelTag, out var channel) || channel == null)
                return UniTask.FromResult(false);

            var runtimeRequest = request?.CreateRuntimeCopy() ?? new DialogueEndRequest();
            return channel.EndAsync(runtimeRequest, ct);
        }

        bool TryGetChannel(string channelTag, out IDialogueChannelService? channel)
        {
            var normalizedTag = DialogueTagUtility.Normalize(channelTag);
            return _hub.TryGetChannel(normalizedTag, out channel) && channel != null;
        }
    }
}
