#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.CameraSystem
{
    [Serializable]
    public sealed class CameraLocationChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField] string channelTag = "default";

        [BoxGroup("Channel")]
        [LabelText("Camera")]
        [Required]
        [SerializeField] Camera? camera;

        public string ChannelTag => string.IsNullOrWhiteSpace(channelTag) ? "default" : channelTag.Trim();
        public Camera? Camera => camera;

        internal void EnsureCameraReference()
        {
            if (camera == null)
                camera = Camera.main;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CameraLocationChannelMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<CameraLocationChannelDefinition> channels = new() { new CameraLocationChannelDefinition() };

        public IReadOnlyList<CameraLocationChannelDefinition> Channels => channels;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            EnsureChannels();

            builder.Register<CameraLocationChannelService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ICameraLocationChannelService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>()
                .AsSelf();
        }

        void Reset()
        {
            EnsureChannels();
        }

        void OnValidate()
        {
            EnsureChannels();
        }

        void EnsureChannels()
        {
            if (channels == null)
            {
                channels = new List<CameraLocationChannelDefinition> { new CameraLocationChannelDefinition() };
                return;
            }

            if (channels.Count == 0)
                channels.Add(new CameraLocationChannelDefinition());

            for (int i = 0; i < channels.Count; i++)
            {
                channels[i]?.EnsureCameraReference();
            }
        }
    }
}
