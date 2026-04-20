#nullable enable

using System;
using System.Collections.Generic;
using Game.Dialogue;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class DialogueChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowFoldout = true, DraggableItems = true)]
        [SerializeField]
        List<DialogueChannelDefinition> _channels = new() { new DialogueChannelDefinition() };

        [FoldoutGroup("Debug")]
        [LabelText("Enable Debug Log")]
        [SerializeField]
        bool _enableDebugLog;

        IDialogueChannelHubService? _hub;

        public IReadOnlyList<DialogueChannelDefinition> Channels => _channels;
        public bool EnableDebugLog => _enableDebugLog;
        public IDialogueChannelHubService? Hub => _hub;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<DialogueChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IDialogueChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();

            builder.Register<DialogueService>(RuntimeLifetime.Singleton)
                .As<IDialogueService>();

            builder.RegisterBuildCallback(resolver =>
            {
                if (resolver.TryResolve<IDialogueChannelHubService>(out var hub) && hub != null)
                    _hub = hub;
            });
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _channels ??= new List<DialogueChannelDefinition>();
            if (_channels.Count == 0)
                _channels.Add(new DialogueChannelDefinition());
        }
#endif
    }
}

