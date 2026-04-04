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
    public sealed class TraitListChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("この definition を識別する tag。command や service からこの文字列で channel を指定します。")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Channel")]
        [LabelText("Auto Build")]
        [Tooltip("true のとき scope acquire 時に既定 binding で自動 build します。")]
        [SerializeField]
        bool _autoBuild;

        [BoxGroup("Binding")]
        [LabelText("Default Binding")]
        [InlineProperty]
        [Tooltip("この channel の既定 holder binding。bind command で override されない限りこの設定を使います。")]
        [SerializeField]
        TraitListChannelBinding _defaultBinding = new();

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("追従方針や refresh 振る舞いを定義する player preset です。Literal と SO の両方を使用できます。")]
        [SerializeField]
        DynamicValue<TraitListChannelPlayerPreset> _playerPreset =
            DynamicValue<TraitListChannelPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelPlayerPreset>(new TraitListChannelPlayerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Layout Preset")]
        [Tooltip("配置、spawn 開始位置、移動演出を定義する layout preset です。Literal と SO の両方を使用できます。")]
        [SerializeField]
        DynamicValue<TraitListChannelLayoutPreset> _layoutPreset =
            DynamicValue<TraitListChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelLayoutPreset>(new TraitListChannelLayoutPreset()));

        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [Tooltip("RuntimeTemplate、pooling、spawn command を定義する visualizer preset です。Literal と SO の両方を使用できます。")]
        [SerializeField]
        DynamicValue<TraitListChannelVisualizerPreset> _visualizerPreset =
            DynamicValue<TraitListChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<TraitListChannelVisualizerPreset>(new TraitListChannelVisualizerPreset()));

        [BoxGroup("Scene")]
        [LabelText("List Root")]
        [Tooltip("生成した runtime item をぶら下げる親 Transform。未設定時はこの Hub 自身の transform を使います。")]
        [SerializeField]
        Transform? _listRoot;

        [BoxGroup("Scene")]
        [LabelText("Layout Rect")]
        [Tooltip("レイアウト計算の基準 Transform。RectTransform を指定した場合は RectTransform mode の領域として使い、world では AreaChannel の local 変換基準として使います。")]
        [SerializeField]
        Transform? _layoutRectTransform;

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();
        public bool AutoBuild => _autoBuild;
        public TraitListChannelBinding DefaultBinding => _defaultBinding;
        public DynamicValue<TraitListChannelPlayerPreset> PlayerPresetValue => _playerPreset;
        public DynamicValue<TraitListChannelLayoutPreset> LayoutPresetValue => _layoutPreset;
        public DynamicValue<TraitListChannelVisualizerPreset> VisualizerPresetValue => _visualizerPreset;
        public Transform? ListRoot => _listRoot;
        public Transform? LayoutRectTransform => _layoutRectTransform;
    }

    [DisallowMultipleComponent]
    public sealed class TraitListChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [Tooltip("この hub が管理する TraitList channel 定義一覧です。各要素は独立した list として動作します。")]
        [SerializeField]
        List<TraitListChannelDefinition> _channels = new() { new TraitListChannelDefinition() };

        public IReadOnlyList<TraitListChannelDefinition> Channels => _channels;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<TraitListChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ITraitListChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
