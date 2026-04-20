// Game.Channel.AnimationSpriteHubMB.cs

using System;
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.SharedTexture;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class AnimationSpriteHubMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        AnimationSpriteChannelDef[] channels = Array.Empty<AnimationSpriteChannelDef>();

        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool replaceWithTransparentOnRelease = false;

        [SerializeField]
        [Tooltip("VisualSystem selector 逕ｨ縺ｮ HubTag")]
        string hubTag = "default";

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            //Debug.Log($"[AnimationSpriteHub] Installing AnimationSpriteHubService. {channels.Length} channels.");
            if (channels != null)
            {
                for (int i = 0; i < channels.Length; i++)
                {
                    channels[i]?.EnsureIntegrity(this);
                }
            }

            builder.Register<AnimationSpriteHubService>(RuntimeLifetime.Singleton)
                    .As<IAnimationSpriteHubService>()
                    .As<ITaggedMaterialFxProvider>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>()
                    .As<IScopeTickHandler>()
                    .AsSelf()
                    .WithParameter(channels)
                    .WithParameter(scope)
                    .WithParameter(replaceWithTransparentOnRelease)
                    .WithParameter(hubTag);

        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<AnimationSpriteChannelDef>();

            if (string.IsNullOrWhiteSpace(hubTag))
                hubTag = "default";
        }
#endif
    }
}
