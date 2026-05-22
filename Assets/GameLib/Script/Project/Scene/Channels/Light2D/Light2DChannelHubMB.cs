#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class Light2DChannelHubMB : MonoBehaviour, IScopeInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        Light2DChannelDef[] _channels = Array.Empty<Light2DChannelDef>();

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            if (_channels != null)
            {
                for (var i = 0; i < _channels.Length; i++)
                    _channels[i]?.EnsureIntegrity(this);
            }

            builder.Register<Light2DChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(_channels)
                .WithParameter(scope)
                .As<ILight2DChannelHubService>()
                .As<IChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_channels == null)
                _channels = Array.Empty<Light2DChannelDef>();
        }
#endif
    }
}

