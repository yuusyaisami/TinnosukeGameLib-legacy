// Game.Health.HealthModifierProfileSO
// Health Modifier 用 薄い asset wrapper。実データは HealthModifierPreset に保持。

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Profile;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Health
{
    [CreateAssetMenu(menuName = "Game/Health/HealthModifierProfile", fileName = "HealthModifierProfile")]
    public sealed class HealthModifierProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<HealthModifierPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        HealthModifierPreset _preset = new();

        public HealthModifierPreset Preset => _preset;

        public Type ProfileType => typeof(HealthModifierPreset);

        public IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            if (_preset == null) yield break;
            foreach (var b in _preset.EnumerateBindings())
                yield return b;
        }

        public void CollectBindings(List<IProfileValueBinding> output)
        {
            _preset?.CollectBindings(output);
        }

        public int GetBindingCount() => _preset?.GetBindingCount() ?? 0;
    }
}
