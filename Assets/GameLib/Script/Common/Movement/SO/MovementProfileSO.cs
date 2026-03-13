// Game.Movement
// MovementProfileSO - Movement 系 Profile の薄い asset wrapper
//
// 実データは MovementPreset に保持。
// BaseProfileSO 継承は TryResolve<MovementProfileSO>() 互換のために維持。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;
using Game.Common;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;

namespace Game.Movement
{
    [CreateAssetMenu(menuName = "Game/Movement/MovementProfile", fileName = "MovementProfile")]
    public sealed class MovementProfileSO : ScriptableObject, IProfileDefinition, IDynamicValueAsset<MovementPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        MovementPreset _preset;

        // Legacy fields — kept for migration of existing assets
        [HideInInspector, SerializeField]
        ProfileFloatValue _defaultSpeed = new()
        {
            Value = 4f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Movement.DefaultSpeed),
            ScalarPolicyValue = ScalarBindPolicy.SkipIfExists,
            UseEffectMod = false,
            UseClampMod = false
        };
        [HideInInspector, SerializeField]
        ProfileFloatValue _defaultMultiplier = new()
        {
            Value = 1f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Movement.SpeedMultiplier),
            ScalarPolicyValue = ScalarBindPolicy.SkipIfExists,
            UseEffectMod = false,
            UseClampMod = false
        };
        [HideInInspector, FormerlySerializedAs("AgentRadius")]
        public float AgentRadius_Legacy = 0.35f;

        public MovementPreset Preset
        {
            get
            {
                EnsurePresetMigrated();
                return _preset;
            }
        }

        public float DefaultSpeedFallback => Preset?.DefaultSpeedFallback ?? 4f;
        public float DefaultMultiplierFallback => Preset?.DefaultMultiplierFallback ?? 1f;
        public float AgentRadius => Preset?.AgentRadius ?? 0.35f;

        public Type ProfileType => typeof(MovementProfileSO);

        public IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            var p = Preset;
            if (p == null) yield break;
            foreach (var b in p.EnumerateBindings())
                yield return b;
        }

        public void CollectBindings(List<IProfileValueBinding> output)
        {
            Preset?.CollectBindings(output);
        }

        public int GetBindingCount()
        {
            return Preset?.GetBindingCount() ?? 0;
        }

        void OnEnable() => EnsurePresetMigrated();
        void OnValidate() => EnsurePresetMigrated();

        void EnsurePresetMigrated()
        {
            if (_preset != null) return;
            _preset = MovementPreset.CreateFromLegacy(_defaultSpeed, _defaultMultiplier, AgentRadius_Legacy);
        }
    }
}
