// Game.Channel.AnimationSpriteHubMB.cs

using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.SharedTexture;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class AnimationSpriteHubMB : AnimationSpriteHubAuthoring, IScopeInstaller
    {

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            ValidateOrThrow();

            builder.Register<AnimationSpriteHubService>(RuntimeLifetime.Singleton)
                    .As<IAnimationSpriteHubService>()
                    .As<ITaggedMaterialFxProvider>()
                    .As<IScopeAcquireHandler>()
                    .As<IScopeReleaseHandler>()
                    .As<IScopeTickHandler>()
                    .AsSelf()
                    .WithParameter(Channels)
                    .WithParameter(scope)
                    .WithParameter(ReplaceWithTransparentOnRelease)
                    .WithParameter(HubTag);

        }
    }
}

