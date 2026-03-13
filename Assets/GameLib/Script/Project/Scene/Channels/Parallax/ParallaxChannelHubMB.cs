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
    public sealed class ParallaxChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Hub")]
        [LabelText("Run In LateUpdate")]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup("Hub")]
        [LabelText("Channels")]
        [SerializeField] ParallaxChannelDef[] channels = Array.Empty<ParallaxChannelDef>();

        [BoxGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel]
        ParallaxChannelHubDebugViewer debugViewer = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            if (channels == null)
                channels = Array.Empty<ParallaxChannelDef>();

            var forceTickInRuntime = scope != null && scope.Kind == LifetimeScopeKind.Runtime;

            for (int i = 0; i < channels.Length; i++)
            {
                channels[i]?.EnsureIntegrity(this);
            }

            builder.Register<ParallaxChannelHubService>(Lifetime.Singleton)
                .As<IParallaxChannelHubService>()
                .As<IChannelHubService>()
                .As<ITickable>()
                .As<ILateTickable>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf()
                .WithParameter(channels)
                .WithParameter(scope)
                .WithParameter(runInLateUpdate)
                .WithParameter(forceTickInRuntime);

            builder.RegisterBuildCallback(container =>
            {
                if (debugViewer != null && container.TryResolve<IParallaxChannelHubService>(out var hub) && hub != null)
                    debugViewer.Bind(hub);
            });
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<ParallaxChannelDef>();
        }
#endif
    }
}
