#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class TraitListChannelPlayerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Player")]
        [LabelText("Refresh Mode")]
        [Tooltip("TraitHolder 変更時の更新方式。FullRebuild は全再生成、Incremental は差分更新、LayoutOnly は配置更新のみです。")]
        [SerializeField]
        TraitListChannelRefreshMode _refreshMode = TraitListChannelRefreshMode.Incremental;

        [BoxGroup("Player")]
        [LabelText("Hide Visible Placed Traits")]
        [Tooltip("true のとき placement 上で可視扱いの trait を list 表示から除外します。")]
        [SerializeField]
        bool _hideVisiblePlacedTraits;

        [BoxGroup("Player")]
        [LabelText("Merge Duplicate Trait Definitions")]
        [Tooltip("true のとき holder 内で同じ DefinitionId を持つ Trait を 1 つの Runtime としてまとめます。")]
        [SerializeField]
        bool _mergeDuplicateTraitDefinitions;

        [BoxGroup("Player")]
        [LabelText("Debounce Frames")]
        [MinValue(0)]
        [Tooltip("holder/placement の連続変化をまとめる待機 frame 数です。0 なら即時更新します。")]
        [SerializeField]
        int _debounceFrames = 1;

        public TraitListChannelRefreshMode RefreshMode => _refreshMode;
        public bool HideVisiblePlacedTraits => _hideVisiblePlacedTraits;
        public bool MergeDuplicateTraitDefinitions => _mergeDuplicateTraitDefinitions;
        public int DebounceFrames => Mathf.Max(0, _debounceFrames);

        public TraitListChannelPlayerPreset CreateRuntimeCopy()
        {
            return new TraitListChannelPlayerPreset
            {
                _refreshMode = _refreshMode,
                _hideVisiblePlacedTraits = _hideVisiblePlacedTraits,
                _mergeDuplicateTraitDefinitions = _mergeDuplicateTraitDefinitions,
                _debounceFrames = _debounceFrames,
            };
        }
    }

    [CreateAssetMenu(
        menuName = "Game/UI/TraitListChannel/Player Preset",
        fileName = "TraitListChannelPlayerPreset")]
    public sealed class TraitListChannelPlayerPresetSO : ScriptableObject, IDynamicValueAsset<TraitListChannelPlayerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("SO 内に保持する TraitListChannelPlayerPreset 本体です。")]
        TraitListChannelPlayerPreset? _preset = new();

        public TraitListChannelPlayerPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable() => EnsurePreset();
        void OnValidate() => EnsurePreset();

        void EnsurePreset()
        {
            _preset ??= new TraitListChannelPlayerPreset();
        }
    }
}
