#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using UnityEngine;

namespace Game.UI
{
    public static class UIDialogVarKeys
    {
        public const string DialogScope = "DialogScope";
        public const string DialogOwner = "DialogOwner";
        public const string DialogChannelKey = "DialogChannelKey";
    }

    public enum DialogCloseReason
    {
        Explicit = 0,
        Replaced = 1,
        ActionInvoked = 2,
        ModalStackChanged = 3,
    }

    public readonly struct UIDialogRequest
    {
        public IScopeNode Owner { get; }
        public IVarStore? InitialVariables { get; }
        public Transform? PrefabTransformParentOverride { get; }
        public IScopeNode? LifetimeScopeParentOverride { get; }

        /// <summary>
        /// 表示中に購読するバインディングを上書きする（null なら ChannelDef の設定を使用）。
        /// </summary>
        public DialogEventBinding[]? SubscribeBindingsOverride { get; }

        /// <summary>
        /// 表示中に追加で購読するバインディング（null/空なら追加なし）。
        /// </summary>
        public DialogEventBinding[]? AdditionalSubscribeBindings { get; }

        public UIDialogRequest(
            IScopeNode owner,
            IVarStore? initialVariables = null,
            Transform? prefabTransformParentOverride = null,
            IScopeNode? lifetimeScopeParentOverride = null,
            DialogEventBinding[]? subscribeBindingsOverride = null,
            DialogEventBinding[]? additionalSubscribeBindings = null)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            InitialVariables = initialVariables;
            PrefabTransformParentOverride = prefabTransformParentOverride;
            LifetimeScopeParentOverride = lifetimeScopeParentOverride;
            SubscribeBindingsOverride = subscribeBindingsOverride;
            AdditionalSubscribeBindings = additionalSubscribeBindings;
        }
    }

    public interface IUIDialogService
    {
        void Show(string channelKey, UIDialogRequest request);
        UniTask<bool> ShowAsync(string channelKey, UIDialogRequest request, CancellationToken ct = default);
        void Hide(string channelKey, DialogCloseReason reason = DialogCloseReason.Explicit);
        bool IsVisible(string channelKey);

        void SetSubscribeBindings(string channelKey, DialogEventBinding[] bindings);
        void AddSubscribeBindings(string channelKey, DialogEventBinding[] bindings);
    }

    public sealed class UIDialogService : IUIDialogService
    {
        readonly IDialogChannelHubService _hub;

        public UIDialogService(IDialogChannelHubService hub)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        public void Show(string channelKey, UIDialogRequest request)
        {
            if (!_hub.TryGetChannel(channelKey, out var channel) || channel == null)
                return;

            channel.Show(request);
        }

        public async UniTask<bool> ShowAsync(string channelKey, UIDialogRequest request, CancellationToken ct = default)
        {
            if (!_hub.TryGetChannel(channelKey, out var channel) || channel == null)
                return false;

            await channel.ShowAsync(request, ct);
            return channel.IsVisible;
        }

        public void Hide(string channelKey, DialogCloseReason reason = DialogCloseReason.Explicit)
        {
            if (!_hub.TryGetChannel(channelKey, out var channel) || channel == null)
                return;

            channel.Hide(reason);
        }

        public bool IsVisible(string channelKey)
        {
            if (!_hub.TryGetChannel(channelKey, out var channel) || channel == null)
                return false;

            return channel.IsVisible;
        }

        public void SetSubscribeBindings(string channelKey, DialogEventBinding[] bindings)
        {
            _hub.SetSubscribeBindings(channelKey, bindings);
        }

        public void AddSubscribeBindings(string channelKey, DialogEventBinding[] bindings)
        {
            _hub.AddSubscribeBindings(channelKey, bindings);
        }
    }
}
