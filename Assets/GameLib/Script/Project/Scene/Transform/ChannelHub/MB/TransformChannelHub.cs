#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.TransformSystem
{
    [Serializable]
    public sealed class TransformChannelOptions
    {
        public DynamicValue<TransformChannelOutputPreset> OutputPresetValue { get; set; } =
            DynamicValue<TransformChannelOutputPreset>.FromSource(
                new ManagedRefLiteralSource<TransformChannelOutputPreset>(new TransformChannelTransformOutputPreset()));

        public DynamicValue<TransformChannelFeaturePreset> FeaturePresetValue { get; set; } =
            DynamicValue<TransformChannelFeaturePreset>.FromSource(
                new ManagedRefLiteralSource<TransformChannelFeaturePreset>(new TransformChannelFeaturePreset()));

        public DynamicValue<TransformChannelEffectPreset> EffectPresetValue { get; set; } =
            DynamicValue<TransformChannelEffectPreset>.FromSource(
                new ManagedRefLiteralSource<TransformChannelEffectPreset>(new TransformChannelEffectPreset()));

        public Transform OwnerTransform { get; set; } = null!;

        public bool DebugGlobalApplyLogs { get; set; }
    }

    [Serializable]
    public sealed class TransformChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [SerializeField]
        string _channelTag = TransformChannelTagUtility.DefaultTag;

        [BoxGroup("Preset")]
        [LabelText("Output Preset")]
        [SerializeField]
        DynamicValue<TransformChannelOutputPreset> _outputPreset =
            DynamicValue<TransformChannelOutputPreset>.FromSource(
                new ManagedRefLiteralSource<TransformChannelOutputPreset>(new TransformChannelTransformOutputPreset()));

        [BoxGroup("Preset")]
        [LabelText("Feature Preset")]
        [SerializeField]
        DynamicValue<TransformChannelFeaturePreset> _featurePreset =
            DynamicValue<TransformChannelFeaturePreset>.FromSource(
                new ManagedRefLiteralSource<TransformChannelFeaturePreset>(new TransformChannelFeaturePreset()));

        [BoxGroup("Preset")]
        [LabelText("Effect Preset")]
        [SerializeField]
        DynamicValue<TransformChannelEffectPreset> _effectPreset =
            DynamicValue<TransformChannelEffectPreset>.FromSource(
                new ManagedRefLiteralSource<TransformChannelEffectPreset>(new TransformChannelEffectPreset()));

        public string ChannelTag => TransformChannelTagUtility.Normalize(_channelTag);

        internal TransformChannelOptions CreateOptions(Transform ownerTransform)
        {
            return new TransformChannelOptions
            {
                OutputPresetValue = _outputPreset,
                FeaturePresetValue = _featurePreset,
                EffectPresetValue = _effectPreset,
                OwnerTransform = ownerTransform,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class TransformChannelHub : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel]
        TransformChannelHubDebugViewer _debugViewer = new();

        [BoxGroup("Debug")]
        [LabelText("Debug Global Apply Logs")]
        [SerializeField]
        bool _debugGlobalApplyLogs;

        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<TransformChannelDefinition> _channels = new() { new TransformChannelDefinition() };

        public IReadOnlyList<TransformChannelDefinition> Channels => _channels;

        internal bool DebugGlobalApplyLogs => _debugGlobalApplyLogs;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<TransformChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ITransformChannelHubService>()
                .As<ITransformTeleportService>()
                .As<ITransformChannelPoseReader>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();

            builder.RegisterInstance(_debugViewer);
            builder.RegisterBuildCallback(container =>
            {
                if (_debugViewer != null && container.TryResolve<ITransformChannelHubService>(out var hub) && hub != null)
                    _debugViewer.Bind(hub);
            });
        }
    }
}
