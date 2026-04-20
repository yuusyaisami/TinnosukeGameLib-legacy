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
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitListChannelRefreshMode _refreshMode = TraitListChannelRefreshMode.Incremental;

        [BoxGroup("Player")]
        [LabelText("Hide Visible Placed Traits")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _hideVisiblePlacedTraits;

        [BoxGroup("Player")]
        [LabelText("Merge Duplicate Trait Definitions")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _mergeDuplicateTraitDefinitions;

        [BoxGroup("Player")]
        [LabelText("Debounce Frames")]
        [MinValue(0)]
        [Tooltip("Inspector setting.")]
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
        [Tooltip("Inspector setting.")]
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
