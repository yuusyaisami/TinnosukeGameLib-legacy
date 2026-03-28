#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class Light2DPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Player")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        Light2DPlayerPreset _playerPreset = new();

        [BoxGroup("Effects")]
        [LabelText("Default Effects")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<Light2DEffectEntry> _defaultEffects = new();

        public Light2DPlayerPreset PlayerPreset => _playerPreset;
        public IReadOnlyList<Light2DEffectEntry> DefaultEffects => _defaultEffects;

        public Light2DPreset CreateRuntimeCopy()
        {
            var effects = new List<Light2DEffectEntry>(_defaultEffects.Count);
            for (var i = 0; i < _defaultEffects.Count; i++)
            {
                var entry = _defaultEffects[i];
                if (entry == null)
                    continue;

                var copy = entry.CreateRuntimeCopy();
                copy.Order = effects.Count;
                effects.Add(copy);
            }

            return new Light2DPreset
            {
                _playerPreset = _playerPreset?.CreateRuntimeCopy() ?? new Light2DPlayerPreset(),
                _defaultEffects = effects,
            };
        }
    }
}
