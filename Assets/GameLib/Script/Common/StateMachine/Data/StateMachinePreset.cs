#nullable enable

using System;
using System.Collections.Generic;
using Game.StateMachine.Editor;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StateMachine
{
    [Serializable]
    public sealed class StateMachinePreset
    {
        [TitleGroup("Default Priorities")]
        [Tooltip("譛ｪ逋ｻ骭ｲ Layer 縺ｮ繝・ヵ繧ｩ繝ｫ繝亥━蜈亥ｺｦ")]
        [SerializeField]
        int defaultLayerPriority = 0;

        [TitleGroup("Default Priorities")]
        [Tooltip("譛ｪ逋ｻ骭ｲ State 縺ｮ繝・ヵ繧ｩ繝ｫ繝亥━蜈亥ｺｦ")]
        [SerializeField]
        int defaultStatePriority = 0;

        [TitleGroup("Layer Priority Overrides")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [SerializeField]
        List<LayerPriorityEntry> layerPriorityOverrides = new();

        [TitleGroup("State Priority Overrides")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [SerializeField]
        List<StatePriorityEntry> statePriorityOverrides = new();

        [TitleGroup("Global Options")]
        [Tooltip("GlobalOption 縺ｮ繝・ヵ繧ｩ繝ｫ繝亥､")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [SerializeField]
        List<GlobalOptionDefault> globalOptionDefaults = new();

        public int DefaultLayerPriority => defaultLayerPriority;
        public int DefaultStatePriority => defaultStatePriority;
        public IReadOnlyList<LayerPriorityEntry> LayerPriorityOverrides => layerPriorityOverrides;
        public IReadOnlyList<StatePriorityEntry> StatePriorityOverrides => statePriorityOverrides;
        public IReadOnlyList<GlobalOptionDefault> GlobalOptionDefaults => globalOptionDefaults;

        public bool HasMeaningfulData()
        {
            return defaultLayerPriority != 0 ||
                   defaultStatePriority != 0 ||
                   layerPriorityOverrides.Count > 0 ||
                   statePriorityOverrides.Count > 0 ||
                   globalOptionDefaults.Count > 0;
        }

        public int GetLayerPriority(string? layerKey)
        {
            if (string.IsNullOrEmpty(layerKey))
                return defaultLayerPriority;

            for (int i = 0; i < layerPriorityOverrides.Count; i++)
            {
                var entry = layerPriorityOverrides[i];
                if (entry != null && string.Equals(entry.LayerKey, layerKey, StringComparison.Ordinal))
                    return entry.Priority;
            }

            return defaultLayerPriority;
        }

        public int GetStatePriority(string? stateKey)
        {
            if (string.IsNullOrEmpty(stateKey))
                return defaultStatePriority;

            for (int i = 0; i < statePriorityOverrides.Count; i++)
            {
                var entry = statePriorityOverrides[i];
                if (entry != null && string.Equals(entry.StateKey, stateKey, StringComparison.Ordinal))
                    return entry.Priority;
            }

            return defaultStatePriority;
        }

        public string? GetGlobalOptionDefault(string? optionKey)
        {
            if (string.IsNullOrEmpty(optionKey))
                return null;

            for (int i = 0; i < globalOptionDefaults.Count; i++)
            {
                var entry = globalOptionDefaults[i];
                if (entry != null && string.Equals(entry.OptionKey, optionKey, StringComparison.Ordinal))
                    return entry.DefaultValue;
            }

            return null;
        }

        internal void CopyFromLegacy(
            int layerPriority,
            int statePriority,
            List<LayerPriorityEntry>? layerOverrides,
            List<StatePriorityEntry>? stateOverrides,
            List<GlobalOptionDefault>? optionDefaults)
        {
            defaultLayerPriority = layerPriority;
            defaultStatePriority = statePriority;
            layerPriorityOverrides = layerOverrides ?? new List<LayerPriorityEntry>();
            statePriorityOverrides = stateOverrides ?? new List<StatePriorityEntry>();
            globalOptionDefaults = optionDefaults ?? new List<GlobalOptionDefault>();
        }
    }

    [Serializable]
    public sealed class LayerPriorityEntry
    {
        [HorizontalGroup, LabelWidth(80)]
        [Tooltip("LayerKey (e.g. Movement, Combat, UI.Button)")]
        public string LayerKey = string.Empty;

        [HorizontalGroup, LabelWidth(60)]
        [Tooltip("Inspector setting.")]
        public int Priority;
    }

    [Serializable]
    public sealed class StatePriorityEntry
    {
        [HorizontalGroup, LabelWidth(80)]
        [Tooltip("StateKey (e.g. Movement.Idle, Combat.Attack)")]
        [StateKeyPicker]
        public string StateKey = string.Empty;

        [HorizontalGroup, LabelWidth(60)]
        [Tooltip("Inspector setting.")]
        public int Priority;
    }

    [Serializable]
    public sealed class GlobalOptionDefault
    {
        [HorizontalGroup, LabelWidth(80)]
        [Tooltip("OptionKey (e.g. Movement.Direction)")]
        [OptionKeyPicker]
        public string OptionKey = string.Empty;

        [HorizontalGroup, LabelWidth(100)]
        [Tooltip("繝・ヵ繧ｩ繝ｫ繝・OptionValue (e.g. Movement.Direction.Right)")]
        public string DefaultValue = string.Empty;
    }
}
