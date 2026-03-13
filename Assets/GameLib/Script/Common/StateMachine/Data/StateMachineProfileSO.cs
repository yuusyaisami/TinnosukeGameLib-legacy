// Game.StateMachine.StateMachineProfileSO.cs

#nullable enable

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StateMachine
{
    /// <summary>
    /// StateMachinePreset を保持する薄いアセットラッパ。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/StateMachine/Profile")]
    public sealed class StateMachineProfileSO : ScriptableObject
    {
        [SerializeReference, InlineProperty, HideLabel]
        StateMachinePreset? preset = new();

        // Legacy migration fields
        [SerializeField, HideInInspector] int defaultLayerPriority = 0;
        [SerializeField, HideInInspector] int defaultStatePriority = 0;
        [SerializeField, HideInInspector] List<LayerPriorityEntry> layerPriorityOverrides = new();
        [SerializeField, HideInInspector] List<StatePriorityEntry> statePriorityOverrides = new();
        [SerializeField, HideInInspector] List<GlobalOptionDefault> globalOptionDefaults = new();

        public StateMachinePreset? Preset
        {
            get
            {
                EnsurePresetMigrated();
                return preset;
            }
        }

        public int DefaultLayerPriority => Preset?.DefaultLayerPriority ?? 0;
        public int DefaultStatePriority => Preset?.DefaultStatePriority ?? 0;
        public IReadOnlyList<LayerPriorityEntry> LayerPriorityOverrides => Preset?.LayerPriorityOverrides ?? System.Array.Empty<LayerPriorityEntry>();
        public IReadOnlyList<StatePriorityEntry> StatePriorityOverrides => Preset?.StatePriorityOverrides ?? System.Array.Empty<StatePriorityEntry>();
        public IReadOnlyList<GlobalOptionDefault> GlobalOptionDefaults => Preset?.GlobalOptionDefaults ?? System.Array.Empty<GlobalOptionDefault>();

        public int GetLayerPriority(string layerKey) => Preset?.GetLayerPriority(layerKey) ?? 0;
        public int GetStatePriority(string stateKey) => Preset?.GetStatePriority(stateKey) ?? 0;
        public string? GetGlobalOptionDefault(string optionKey) => Preset?.GetGlobalOptionDefault(optionKey);

        void OnEnable()
        {
            EnsurePresetMigrated();
        }

        void OnValidate()
        {
            EnsurePresetMigrated();
        }

        void EnsurePresetMigrated()
        {
            preset ??= new StateMachinePreset();
            if (preset.HasMeaningfulData())
                return;

            if (!HasLegacyData())
                return;

            preset.CopyFromLegacy(
                defaultLayerPriority,
                defaultStatePriority,
                layerPriorityOverrides,
                statePriorityOverrides,
                globalOptionDefaults);

            defaultLayerPriority = 0;
            defaultStatePriority = 0;
            layerPriorityOverrides = new List<LayerPriorityEntry>();
            statePriorityOverrides = new List<StatePriorityEntry>();
            globalOptionDefaults = new List<GlobalOptionDefault>();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        bool HasLegacyData()
        {
            return defaultLayerPriority != 0 ||
                   defaultStatePriority != 0 ||
                   layerPriorityOverrides.Count > 0 ||
                   statePriorityOverrides.Count > 0 ||
                   globalOptionDefaults.Count > 0;
        }
    }
}
