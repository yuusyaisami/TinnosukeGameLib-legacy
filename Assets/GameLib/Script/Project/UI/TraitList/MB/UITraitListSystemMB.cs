#nullable enable
using System;
using Game;
using Game.Commands.VNext;
using Game.Trait;
using Game.UI;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.UI.TraitList
{
    public interface IUITraitListSystemOptions
    {
        UITraitListProfileSO? DefaultProfile { get; }
        bool AutoBuildOnAcquire { get; }
        ActorSource HolderHubSource { get; }
        string HolderKey { get; }
        UITraitListRange AutoBuildRange { get; }
        RectTransform? LayoutRectTransform { get; }
        Transform? DefaultParentTransform { get; }
        int AutoBuildRetryCount { get; }
        int AutoBuildRetryFrameInterval { get; }
        bool HideVisiblePlacedTraits { get; }
    }

    [Serializable]
    public sealed class UITraitListBuildSettings
    {
        [InlineProperty]
        public UITraitListRange Range;

        [LabelText("Retry Count")]
        [Tooltip("Number of retry attempts when holder is not found.")]
        [MinValue(0)]
        public int AutoBuildRetryCount = 5;

        [LabelText("Retry Frame Interval")]
        [Tooltip("Frames to wait between retry attempts.")]
        [MinValue(1)]
        public int AutoBuildRetryFrameInterval = 1;
    }

    [DisallowMultipleComponent]
    public sealed class UITraitListSystemMB : MonoBehaviour, IFeatureInstaller, IUITraitListSystemOptions
    {
        [BoxGroup("Profile")]
        [SerializeField]
        UITraitListProfileSO? _defaultProfile;

        [BoxGroup("Runtime")]
        [SerializeField]
        Transform? _defaultParentTransform;

        [BoxGroup("Runtime")]
        [LabelText("Layout Rect")]
        [Tooltip("UITraitList の配置範囲として使用する RectTransform。指定時はこの Rect 内の座標系で並べます。")]
        [SerializeField]
        RectTransform? _layoutRectTransform;

        [BoxGroup("Runtime")]
        [SerializeField]
        bool _autoBuildOnAcquire = false;

        [BoxGroup("Runtime")]
        [LabelText("Hide Visible Placed Traits")]
        [SerializeField]
        bool _hideVisiblePlacedTraits;

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(_holderHubSource)")]
        [SerializeField]
        ActorSource _holderHubSource;

        [BoxGroup("Target")]
        [LabelText("Holder Key")]
        [SerializeField]
        string _holderKey = string.Empty;

        [BoxGroup("Auto Build")]
        [ShowIf(nameof(_autoBuildOnAcquire))]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        UITraitListBuildSettings _autoBuildSettings = new();

        public UITraitListProfileSO? DefaultProfile => _defaultProfile;
        public bool AutoBuildOnAcquire => _autoBuildOnAcquire;
        public ActorSource HolderHubSource => _holderHubSource;
        public string HolderKey => _holderKey;
        public UITraitListRange AutoBuildRange => _autoBuildSettings != null ? _autoBuildSettings.Range : default;
        public RectTransform? LayoutRectTransform => _layoutRectTransform != null
            ? _layoutRectTransform
            : _defaultParentTransform as RectTransform;
        public Transform? DefaultParentTransform => _defaultParentTransform != null ? _defaultParentTransform : transform;
        public int AutoBuildRetryCount => _autoBuildSettings != null ? _autoBuildSettings.AutoBuildRetryCount : 0;
        public int AutoBuildRetryFrameInterval => _autoBuildSettings != null ? _autoBuildSettings.AutoBuildRetryFrameInterval : 1;
        public bool HideVisiblePlacedTraits => _hideVisiblePlacedTraits;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance<IUITraitListSystemOptions>(this);

            builder.Register<UITraitListLayoutService>(Lifetime.Singleton)
                .As<IUITraitListLayoutService>()
                .As<IUITransformListLayoutService<ITraitInstance, UITraitListSlot, UITraitListLayoutProfileSO>>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<UITraitListVisualizerService>(Lifetime.Singleton)
                .As<IUITraitListVisualizerService>()
                .As<IUITransformListVisualizerService<UITraitListSlot, UITraitListVisualInstance, UITraitListLayoutProfileSO, UITraitListVisualizerProfileSO>>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<UITransformListBuilderService<ITraitInstance, UITraitListSlot, UITraitListVisualInstance, UITraitListLayoutProfileSO, UITraitListVisualizerProfileSO>>(Lifetime.Singleton)
                .As<IUITransformListBuilderService<ITraitInstance, UITraitListSlot, UITraitListVisualInstance, UITraitListLayoutProfileSO, UITraitListVisualizerProfileSO>>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<UITraitListBuilderService>(Lifetime.Singleton)
                .As<IUITraitListBuilderService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<UITraitListPlayerService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IUITraitListPlayerService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
