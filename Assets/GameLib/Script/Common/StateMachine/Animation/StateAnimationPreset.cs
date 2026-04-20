#nullable enable

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StateMachine
{
    [System.Serializable]
    public sealed class StateAnimationPreset
    {
        [Title("Animation Rules")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = false,
            DraggableItems = true,
            ShowPaging = true,
            NumberOfItemsPerPage = 10,
            ListElementLabelName = nameof(StateAnimationRule.RuleHeader))]
        [SerializeField]
        List<StateAnimationRule> rules = new();

        List<StateAnimationRule>? _sortedRulesCache;
        bool _sortedDirty = true;

        public IReadOnlyList<StateAnimationRule> Rules => rules;
        public bool HasRules => rules.Count > 0;

        public IReadOnlyList<StateAnimationRule> GetRulesByPriority()
        {
            if (_sortedDirty || _sortedRulesCache == null)
            {
                _sortedRulesCache = new List<StateAnimationRule>(rules);
                var originalOrder = new Dictionary<StateAnimationRule, int>();
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule == null || originalOrder.ContainsKey(rule))
                        continue;

                    originalOrder[rule] = i;
                }

                _sortedRulesCache.Sort((a, b) =>
                {
                    if (ReferenceEquals(a, b))
                        return 0;
                    if (a == null)
                        return 1;
                    if (b == null)
                        return -1;

                    var byPriority = b.Priority.CompareTo(a.Priority);
                    if (byPriority != 0)
                        return byPriority;

                    var ai = originalOrder.TryGetValue(a, out var aIndex) ? aIndex : int.MaxValue;
                    var bi = originalOrder.TryGetValue(b, out var bIndex) ? bIndex : int.MaxValue;
                    return ai.CompareTo(bi);
                });
                _sortedDirty = false;
            }

            return _sortedRulesCache;
        }

        public void MarkDirty()
        {
            _sortedDirty = true;
        }

        internal void CopyFromLegacy(List<StateAnimationRule>? legacyRules)
        {
            rules = legacyRules ?? new List<StateAnimationRule>();
            _sortedDirty = true;
        }

#if UNITY_EDITOR
        [Button("Sort by Priority", ButtonSizes.Medium)]
        void SortByPriority()
        {
            rules.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            _sortedDirty = true;
        }
#endif
    }
}
