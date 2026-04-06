#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Channel
{
    [Serializable]
    public sealed class GridObjectChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("hub 内でこの channel を識別する tag です。command からもこの値で指定します。")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Channel")]
        [LabelText("Auto Build")]
        [Tooltip("scope acquire 時に自動で bind + full rebuild を行います。")]
        [SerializeField]
        bool _autoBuild;

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("item 群をどう生成するかを決める player preset です。")]
        [SerializeField]
        DynamicValue<GridObjectChannelPlayerPresetBase> _playerPreset =
            DynamicValue<GridObjectChannelPlayerPresetBase>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelPlayerPresetBase>(new GridObjectChannelStandalonePlayerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Layout Preset")]
        [Tooltip("item の row/column と target 座標をどう計算するかを決める layout preset です。")]
        [SerializeField]
        DynamicValue<GridObjectChannelLayoutPreset> _layoutPreset =
            DynamicValue<GridObjectChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelLayoutPreset>(new GridObjectChannelLayoutPreset()));

        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [Tooltip("spawn 対象 runtime template や command 実行方法を決める visualizer preset です。")]
        [SerializeField]
        DynamicValue<GridObjectChannelVisualizerPreset> _visualizerPreset =
            DynamicValue<GridObjectChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelVisualizerPreset>(new GridObjectChannelVisualizerPreset()));

        [BoxGroup("Scene")]
        [LabelText("List Root")]
        [Tooltip("spawn した item の親 Transform です。未設定時は Hub 自身を使います。")]
        [SerializeField]
        Transform? _listRoot;

        [BoxGroup("Scene")]
        [LabelText("Layout Rect")]
        [Tooltip("レイアウト計算の基準 Transform。RectTransform を指定した場合は RectTransform mode の領域として使い、world では AreaChannel の local 変換基準として使います。")]
        [SerializeField]
        Transform? _layoutRectTransform;

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();
        public bool AutoBuild => _autoBuild;
        public DynamicValue<GridObjectChannelPlayerPresetBase> PlayerPresetValue => _playerPreset;
        public DynamicValue<GridObjectChannelLayoutPreset> LayoutPresetValue => _layoutPreset;
        public DynamicValue<GridObjectChannelVisualizerPreset> VisualizerPresetValue => _visualizerPreset;
        public Transform? ListRoot => _listRoot;
        public Transform? LayoutRectTransform => _layoutRectTransform;
    }

    [DisallowMultipleComponent]
    public sealed class GridObjectChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [Tooltip("この hub が管理する GridObjectChannel 定義一覧です。")]
        [SerializeField]
        List<GridObjectChannelDefinition> _channels = new() { new GridObjectChannelDefinition() };

        public IReadOnlyList<GridObjectChannelDefinition> Channels => _channels;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<GridObjectChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IGridObjectChannelHubService>()
                .As<IChoiceChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
