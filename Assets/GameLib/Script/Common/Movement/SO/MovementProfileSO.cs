// Game.Movement
// MovementProfileSO - Movement 系 Profile の薄い asset wrapper

using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Common;
using Game.Profile;

namespace Game.Movement
{
    [CreateAssetMenu(menuName = "Game/Movement/MovementProfile", fileName = "MovementProfile")]
    public sealed class MovementProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<MovementPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MovementPreset _preset = new();

        public MovementPreset Preset => _preset;

        public float DefaultSpeedFallback => _preset?.DefaultSpeedFallback ?? 4f;
        public float DefaultMultiplierFallback => _preset?.DefaultMultiplierFallback ?? 1f;
        public float AgentRadius => _preset?.AgentRadius ?? 0.35f;

        public Type ProfileType => typeof(MovementPreset);

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

        public int GetBindingCount()
        {
            return _preset?.GetBindingCount() ?? 0;
        }
    }
}
