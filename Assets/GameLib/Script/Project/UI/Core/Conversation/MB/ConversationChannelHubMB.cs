#nullable enable

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Conversation
{
    [DisallowMultipleComponent]
    public sealed class ConversationChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [SerializeField]
        List<ConversationChannelDefinition> _channels = new() { new ConversationChannelDefinition() };

        [FoldoutGroup("Debug")]
        [LabelText("Enable Debug Log")]
        [SerializeField]
        bool _enableDebugLog;

        IConversationChannelHubService? _hub;

        public IReadOnlyList<ConversationChannelDefinition> Channels => _channels;
        public bool EnableDebugLog => _enableDebugLog;
        public IConversationChannelHubService? Hub => _hub;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<ConversationChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IConversationChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterBuildCallback(resolver =>
            {
                if (resolver.TryResolve<IConversationChannelHubService>(out var hub) && hub != null)
                    _hub = hub;
            });
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _channels ??= new List<ConversationChannelDefinition>();
            if (_channels.Count == 0)
                _channels.Add(new ConversationChannelDefinition());
        }
#endif
    }
}
