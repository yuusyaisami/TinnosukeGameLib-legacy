// Game.StateMachine.StateAnimationProfileSO.cs

#nullable enable

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StateMachine
{
    /// <summary>
    /// StateAnimationPreset を保持する薄いアセットラッパ。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/StateMachine/Animation Profile")]
    public sealed class StateAnimationProfileSO : ScriptableObject
    {
        [SerializeReference, InlineProperty, HideLabel]
        StateAnimationPreset? preset = new();

        // Legacy migration field
        [SerializeField, HideInInspector]
        List<StateAnimationRule> rules = new();

        public StateAnimationPreset? Preset
        {
            get
            {
                EnsurePresetMigrated();
                return preset;
            }
        }

        public IReadOnlyList<StateAnimationRule> Rules => Preset?.Rules ?? System.Array.Empty<StateAnimationRule>();

        public IReadOnlyList<StateAnimationRule> GetRulesByPriority()
            => Preset?.GetRulesByPriority() ?? System.Array.Empty<StateAnimationRule>();

        void OnEnable()
        {
            EnsurePresetMigrated();
        }

        void OnValidate()
        {
            EnsurePresetMigrated();
            preset?.MarkDirty();
        }

        void EnsurePresetMigrated()
        {
            preset ??= new StateAnimationPreset();
            if (preset.HasRules)
                return;

            if (rules.Count == 0)
                return;

            preset.CopyFromLegacy(rules);
            rules = new List<StateAnimationRule>();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
