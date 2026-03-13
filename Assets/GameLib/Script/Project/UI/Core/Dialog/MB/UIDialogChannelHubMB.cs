#nullable enable

using System;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIDialogChannelHubMB : MonoBehaviour, Game.IFeatureInstaller
    {
        [Header("Dialog Channels")]
        [SerializeField]
        DialogChannelDef[] channels = Array.Empty<DialogChannelDef>();

        public void InstallFeature(IContainerBuilder builder, Game.IScopeNode scope)
        {
            channels ??= Array.Empty<DialogChannelDef>();
            for (int i = 0; i < channels.Length; i++)
            {
                channels[i]?.EnsureIntegrity(this);
            }

            builder.Register<DialogChannelHubService>(Lifetime.Singleton)
                .As<IDialogChannelHubService>()
                .As<Game.IScopeReleaseHandler>()
                .WithParameter(channels);

            builder.Register<UIDialogService>(Lifetime.Singleton)
                .As<IUIDialogService>();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            channels ??= Array.Empty<DialogChannelDef>();
        }
#endif
    }
}

