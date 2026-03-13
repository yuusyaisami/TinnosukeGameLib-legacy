#nullable enable
using System;
using Game.Collision;
using Game.MaterialFx;
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    public sealed class MeshFxHubMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        MeshFxChannelDef[] channels = Array.Empty<MeshFxChannelDef>();

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            if (channels != null)
            {
                for (int i = 0; i < channels.Length; i++)
                {
                    channels[i]?.EnsureIntegrity(this);
                }
            }

            builder.Register<MeshFxChannelHubService>(resolver =>
                {
                    IMaterialFxServiceFactory? materialFxFactory = null;
                    ICollisionService? collisionService = null;
                    IHitColliderScopeRegistry? hitScopeRegistry = null;
                    IHitColliderChannelHub? hitChannelHub = null;

                    resolver.TryResolve(out materialFxFactory);
                    resolver.TryResolve(out collisionService);
                    resolver.TryResolve(out hitScopeRegistry);
                    resolver.TryResolve(out hitChannelHub);

                    return new MeshFxChannelHubService(
                        channels ?? Array.Empty<MeshFxChannelDef>(),
                        scope,
                        materialFxFactory,
                        collisionService,
                        hitScopeRegistry,
                        hitChannelHub);
                }, Lifetime.Singleton)
                .As<IMeshFxChannelHubService>()
                .As<IChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .AsSelf();

            builder.Register<MeshFxAnimationService>(Lifetime.Singleton)
                .As<IMeshFxAnimationService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .AsSelf();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<MeshFxChannelDef>();
        }
#endif
    }
}
