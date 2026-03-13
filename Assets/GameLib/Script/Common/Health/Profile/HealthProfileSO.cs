// Game.Health.HealthProfileSO.cs
//
// Health 関連の設定を保持する薄い asset wrapper。

#nullable enable

using System;
using System.Collections.Generic;
using Game.Profile;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Health
{
    public enum HealthInitialHPMode
    {
        InitialHPRatio = 10,
        CustomValue = 20,
    }

    [CreateAssetMenu(menuName = "Game/Health/HealthProfile", fileName = "HealthProfile")]
    public sealed class HealthProfileSO : ScriptableObject, IProfileDefinition
    {
        [SerializeReference, InlineProperty, HideLabel]
        HealthPreset? preset = new();

        public Type ProfileType => typeof(HealthProfileSO);

        public HealthPreset? Preset
        {
            get
            {
                EnsurePreset();
                return preset;
            }
        }

        public float MaxHPFallback => Preset?.MaxHPFallback ?? 100f;
        public HealthInitialHPMode InitialHPMode => Preset?.InitialHPMode ?? HealthInitialHPMode.InitialHPRatio;
        public float InitialHPRatio => Preset?.InitialHPRatio ?? 1f;
        public float InitialHPValue => Preset?.InitialHPValue ?? 100f;
        public float InvincibleDurationOnSpawn => Preset?.InvincibleDurationOnSpawn ?? 0f;
        public bool EnableInvincibleOnDamaged => Preset?.EnableInvincibleOnDamaged ?? false;
        public float InvincibleDurationOnDamaged => Preset?.InvincibleDurationOnDamaged ?? 0.1f;
        public float DeathDelay => Preset?.DeathDelay ?? 0f;

        public IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            var resolvedPreset = Preset;
            if (resolvedPreset == null)
                yield break;

            foreach (var binding in resolvedPreset.EnumerateBindings())
                yield return binding;
        }

        public void CollectBindings(List<IProfileValueBinding> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            Preset?.CollectBindings(output);
        }

        public int GetBindingCount()
        {
            return Preset?.GetBindingCount() ?? 0;
        }

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            if (preset == null)
                preset = new HealthPreset();
        }
    }
}
