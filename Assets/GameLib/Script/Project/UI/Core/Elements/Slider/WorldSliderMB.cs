#nullable enable
using Game.Common;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class WorldSliderMB : MonoBehaviour, IFeatureInstaller, IWorldSliderOptions
    {
        [BoxGroup("Preset")]
        [LabelText("Visualizer Preset")]
        [Tooltip("WorldSlider の見た目側の基準設定です。runtime control で上書きしても、Reset Runtime Overrides ではこの source preset に戻ります。")]
        [SerializeField]
        DynamicValue<WorldSliderVisualizerPreset> _visualizerPreset =
            DynamicValue<WorldSliderVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderVisualizerPreset>(new WorldSliderVisualizerPreset()));

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("WorldSlider の値バインディング、範囲、遷移、値変化コマンドの基準設定です。runtime control の reset 時はこの source preset が再適用されます。")]
        [SerializeField]
        DynamicValue<WorldSliderPlayerPreset> _playerPreset =
            DynamicValue<WorldSliderPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<WorldSliderPlayerPreset>(new WorldSliderPlayerPreset()));

        [BoxGroup("Scene")]
        [LabelText("Simple Bar Renderer")]
        [Tooltip("Simple mode かつ SceneSpriteRenderer backend のときに使う SpriteRenderer です。RuntimeGeneratedBar backend では使用しません。")]
        [SerializeField]
        SpriteRenderer? _simpleBarRenderer;

        [BoxGroup("Scene")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Area Source\", _areaActorSource)")]
        [Tooltip("Slider の描画範囲を決める AreaChannel を持つ actor / scope です。")]
        [SerializeField]
        ActorSource _areaActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Scene")]
        [LabelText("Area Channel Tag")]
        [Tooltip("取得対象の AreaChannel tag です。空白の場合は default を使用します。")]
        [SerializeField]
        string _areaChannelTag = "default";

        [BoxGroup("Scene")]
        [LabelText("Segment Bars Root")]
        [Tooltip("Segmented mode の segment bar を生成する親 Transform です。描画範囲そのものは AreaChannel から決まります。")]
        [SerializeField]
        Transform? _segmentBarsRoot;

        [BoxGroup("Scene")]
        [LabelText("Segment Markers Root")]
        [Tooltip("Segmented mode の marker を生成する親 Transform です。描画範囲そのものは AreaChannel から決まります。")]
        [SerializeField]
        Transform? _segmentMarkersRoot;

        public DynamicValue<WorldSliderVisualizerPreset> VisualizerPresetValue => _visualizerPreset;
        public DynamicValue<WorldSliderPlayerPreset> PlayerPresetValue => _playerPreset;
        public SpriteRenderer? SimpleBarRenderer => _simpleBarRenderer;
        public Transform? SegmentBarsRoot => _segmentBarsRoot;
        public Transform? SegmentMarkersRoot => _segmentMarkersRoot;
        public ActorSource AreaActorSource => _areaActorSource;
        public string AreaChannelTag => string.IsNullOrWhiteSpace(_areaChannelTag) ? "default" : _areaChannelTag.Trim();
        public Transform OwnerTransform => transform;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<WorldSliderRuntimePresetService>(Lifetime.Singleton)
                .WithParameter<IWorldSliderOptions>(this)
                .As<IWorldSliderRuntimePresetProvider>()
                .As<IWorldSliderControlService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<WorldSliderPlayerService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IWorldSliderOptions>(this)
                .As<IWorldSliderPlayerService>()
                .As<IWorldSliderOutput>()
                .As<ITickable>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<WorldSliderVisualizerService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter<IWorldSliderOptions>(this)
                .As<IWorldSliderVisualizerService>()
                .As<ITickable>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
