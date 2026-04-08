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
    public sealed class TooltipChannelOptions : ITooltipChannelOptions
    {
        public DynamicValue<TooltipPlayerPreset> PresetValue { get; set; } =
            DynamicValue<TooltipPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipPlayerPreset>(new TooltipPlayerPreset()));
    }

    [Serializable]
    public sealed class TooltipChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("TooltipChannel の識別タグです。空白の場合は default を使用します。")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Player Preset")]
        [Tooltip("この channel の source player preset です。runtime override reset 時はここへ戻ります。")]
        [SerializeField]
        DynamicValue<TooltipPlayerPreset> _presetValue =
            DynamicValue<TooltipPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipPlayerPreset>(new TooltipPlayerPreset()));

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();
        internal DynamicValue<TooltipPlayerPreset> PresetValue => _presetValue;

        internal TooltipChannelOptions CreateOptions()
        {
            return new TooltipChannelOptions
            {
                PresetValue = _presetValue,
            };
        }

        internal TooltipPlayerPreset? ResolveEditorPreset()
        {
            return _presetValue.GetOrDefaultWithoutContext();
        }
    }

    [DisallowMultipleComponent]
    public sealed class TooltipChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        const string RootsGroup = "Root Override";
        const string SpaceGroup = "Space";
        const string PresetGroup = "Preset";
        const string ChannelsGroup = "Channels";
        const string DebugGroup = "Debug";

        [BoxGroup(SpaceGroup)]
        [LabelText("Space Kind")]
        [Tooltip("この hub が扱う tooltip 空間です。Editor では初期値を自動推論しますが、runtime 中はこの値を固定で使います。")]
        [SerializeField]
        TooltipChannelSpaceKind _spaceKind = TooltipChannelSpaceKind.Unknown;

        [BoxGroup(RootsGroup)]
        [LabelText("Apply Tooltip Root Override")]
        [Tooltip("true のときだけ TooltipSystem の TooltipRoot を上書きして、この hub 専用の UI spawn parent を使います。")]
        [SerializeField]
        bool _applyTooltipRootOverride;

        [BoxGroup(RootsGroup)]
        [ShowIf(nameof(ShowsTooltipRootOverride))]
        [LabelText("Tooltip Root Override")]
        [Tooltip("UI hub のときにだけ使う個別 TooltipRoot override です。未指定時はこの GameObject の RectTransform を使います。")]
        [SerializeField]
        RectTransform? _tooltipRootOverride;

        [BoxGroup(PresetGroup)]
        [LabelText("Apply Hub Preset Override")]
        [Tooltip("true のときだけ TooltipSystem の shared hub preset を上書きします。false のときは TooltipSystem 側の default を使用します。")]
        [SerializeField]
        bool _applyHubPresetOverride;

        [BoxGroup(PresetGroup)]
        [ShowIf(nameof(_applyHubPresetOverride))]
        [LabelText("Hub Preset")]
        [Tooltip("camera tag、default hit test、stack/clamp 設定など hub 共通設定です。")]
        [SerializeField]
        DynamicValue<TooltipHubPreset> _hubPresetValue =
            DynamicValue<TooltipHubPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipHubPreset>(new TooltipHubPreset()));

        [BoxGroup(DebugGroup)]
        [LabelText("Enable Debug Log")]
        [Tooltip("true のとき TooltipChannel の acquire/hit test/spawn/close ログを出力します。")]
        [SerializeField]
        bool _enableDebugLog;

        [BoxGroup(ChannelsGroup)]
        [LabelText("Channels")]
        [Tooltip("必要になったタイミングで動的追加できます。0件のままでも問題ありません。")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<TooltipChannelDefinition> _channels = new();

        public DynamicValue<TooltipHubPreset> HubPresetValue => _hubPresetValue;
        public bool ApplyHubPresetOverride => _applyHubPresetOverride;
        public IReadOnlyList<TooltipChannelDefinition> Channels => _channels;
        public TooltipChannelSpaceKind SpaceKind => NormalizeSpaceKind(_spaceKind);
        public bool ApplyTooltipRootOverride => _applyTooltipRootOverride;
        public RectTransform? TooltipRootOverride => _tooltipRootOverride != null ? _tooltipRootOverride : GetComponent<RectTransform>();
        public bool EnableDebugLog => _enableDebugLog;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            EnsureDefaults();

            builder.Register<TooltipChannelHubService>(Lifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<ITooltipChannelHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>();
        }

        void Reset()
        {
            EnsureDefaults();
        }

        void OnValidate()
        {
            EnsureDefaults();
        }

        void EnsureDefaults()
        {
            if (_channels == null)
                _channels = new List<TooltipChannelDefinition>();

            if (_tooltipRootOverride == null && _applyTooltipRootOverride)
                _tooltipRootOverride = GetComponent<RectTransform>();

            if (_spaceKind == TooltipChannelSpaceKind.Unknown)
                _spaceKind = InferSpaceKind();
        }

        TooltipChannelSpaceKind InferSpaceKind()
        {
            if (_applyTooltipRootOverride && (_tooltipRootOverride != null || GetComponent<RectTransform>() != null))
                return TooltipChannelSpaceKind.UIScreen;

            if (GetComponent<RectTransform>() != null)
                return TooltipChannelSpaceKind.UIScreen;

            for (var i = 0; i < _channels.Count; i++)
            {
                var preset = _channels[i]?.ResolveEditorPreset();
                if (preset == null)
                    continue;

                var hitTest = preset.HitTestValue.GetOrDefaultWithoutContext();
                if (hitTest == null || !hitTest.HasAnyTarget)
                    continue;

                for (var j = 0; j < hitTest.Targets.Count; j++)
                {
                    var target = hitTest.Targets[j];
                    if (target == null)
                        continue;

                    switch (target.Kind)
                    {
                        case TooltipHitTestTargetKind.OwnerRectTransform:
                        case TooltipHitTestTargetKind.ActorRectTransform:
                            return TooltipChannelSpaceKind.UIScreen;

                        case TooltipHitTestTargetKind.OwnerSpriteRenderer:
                        case TooltipHitTestTargetKind.ActorSpriteRenderer:
                            return TooltipChannelSpaceKind.World;
                    }
                }
            }

            return TooltipChannelSpaceKind.World;
        }

        static TooltipChannelSpaceKind NormalizeSpaceKind(TooltipChannelSpaceKind spaceKind)
        {
            return spaceKind == TooltipChannelSpaceKind.Unknown
                ? TooltipChannelSpaceKind.World
                : spaceKind;
        }

        bool ShowsTooltipRootOverride => _applyTooltipRootOverride && NormalizeSpaceKind(_spaceKind) == TooltipChannelSpaceKind.UIScreen;
    }
}
