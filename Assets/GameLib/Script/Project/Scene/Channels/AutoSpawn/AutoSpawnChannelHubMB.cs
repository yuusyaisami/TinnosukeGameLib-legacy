#nullable enable
using System;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AreaChannelHubMB))]
    public sealed class AutoSpawnChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Hub")]
        [LabelText("Run In LateUpdate")]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup("Hub")]
        [LabelText("Channels")]
        [SerializeField] AutoSpawnChannelDefinition[] channels = Array.Empty<AutoSpawnChannelDefinition>();

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            if (channels == null)
                channels = Array.Empty<AutoSpawnChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
                channels[i]?.EnsureIntegrity(this);

            var forceTickInRuntime = owner != null && owner.Kind == LifetimeScopeKind.Runtime;

            builder.Register<AutoSpawnChannelHubService>(resolver =>
                {
                    IAreaChannelHubService? areaHub = null;
                    resolver.TryResolve(out areaHub);
                    return new AutoSpawnChannelHubService(channels, areaHub, runInLateUpdate, forceTickInRuntime);
                }, RuntimeLifetime.Singleton)
                .As<IAutoSpawnChannelHubService>()
                .As<IChannelHubService>()
                .As<IScopeTickHandler>()
                .As<IScopeLateTickHandler>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<AutoSpawnChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
                channels[i]?.EnsureIntegrity(this);
        }
#endif
    }
}
