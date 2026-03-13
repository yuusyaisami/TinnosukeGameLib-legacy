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
    public sealed class ScrollChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("View")]
        [LabelText("World Camera")]
        [SerializeField] Camera? worldCamera;

        [BoxGroup("View")]
        [LabelText("Use Camera View")]
        [SerializeField] bool useCameraView = true;

        [BoxGroup("View")]
        [ShowIf(nameof(ShowManualViewSize))]
        [LabelText("Manual View Size")]
        [SerializeField] Vector2 manualViewSize = new(24f, 14f);

        [BoxGroup("View")]
        [LabelText("View Margin Tiles")]
        [SerializeField] Vector2Int viewMarginTiles = Vector2Int.one;

        [BoxGroup("Hub")]
        [LabelText("Run In LateUpdate")]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup("Hub")]
        [LabelText("Channels")]
        [SerializeField] ScrollChannelDefinition[] channels = Array.Empty<ScrollChannelDefinition>();

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            if (channels == null)
                channels = Array.Empty<ScrollChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
                channels[i]?.EnsureIntegrity(this);

            var forceTickInRuntime = owner != null && owner.Kind == LifetimeScopeKind.Runtime;

            builder.Register<ScrollChannelHubService>(resolver =>
                {
                    var cam = worldCamera != null ? worldCamera : Camera.main;
                    return new ScrollChannelHubService(
                        channels,
                        cam,
                        useCameraView,
                        manualViewSize,
                        viewMarginTiles,
                        runInLateUpdate,
                        forceTickInRuntime);
                }, Lifetime.Singleton)
                .As<IScrollChannelHubService>()
                .As<IChannelHubService>()
                .As<ITickable>()
                .As<ILateTickable>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (channels == null)
                channels = Array.Empty<ScrollChannelDefinition>();

            for (int i = 0; i < channels.Length; i++)
                channels[i]?.EnsureIntegrity(this);

            if (manualViewSize.x < 0f)
                manualViewSize.x = 0f;
            if (manualViewSize.y < 0f)
                manualViewSize.y = 0f;
            if (viewMarginTiles.x < 0)
                viewMarginTiles.x = 0;
            if (viewMarginTiles.y < 0)
                viewMarginTiles.y = 0;
        }
#endif

        bool ShowManualViewSize => !useCameraView;
    }
}
