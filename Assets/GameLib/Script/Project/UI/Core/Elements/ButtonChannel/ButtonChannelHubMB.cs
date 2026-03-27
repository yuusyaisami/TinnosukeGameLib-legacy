#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [Serializable]
    public sealed class ButtonChannelOptions : IButtonChannelOptions
    {
        public DynamicValue<ButtonChannelPreset> PresetValue { get; set; } =
            DynamicValue<ButtonChannelPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonChannelPreset>(new ButtonChannelPreset()));

        public Transform OwnerTransform { get; set; } = null!;
    }

    [Serializable]
    public sealed class ButtonChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("ButtonChannel の識別タグです。空白の場合は default を使用します。")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Preset")]
        [Tooltip("この channel の source preset です。reset runtime override 時はここへ戻ります。")]
        [SerializeField]
        DynamicValue<ButtonChannelPreset> _presetValue =
            DynamicValue<ButtonChannelPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonChannelPreset>(new ButtonChannelPreset()));

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();

        internal ButtonChannelOptions CreateOptions(Transform ownerTransform)
        {
            return new ButtonChannelOptions
            {
                PresetValue = _presetValue,
                OwnerTransform = ownerTransform,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class ButtonChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<ButtonChannelDefinition> _channels = new() { new ButtonChannelDefinition() };

        public IReadOnlyList<ButtonChannelDefinition> Channels => _channels;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<ButtonChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IButtonChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }
    }
}
