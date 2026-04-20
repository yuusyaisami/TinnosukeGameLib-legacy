// Game.Movement.MovementPreset
using System;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    [Serializable]
    public sealed class MovementPreset : BaseProfileData
    {
        [BoxGroup("Default Speed")]
        [LabelText("Default Speed")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ProfileFloatValue _defaultSpeed = new()
        {
            Value = 4f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Movement.DefaultSpeed),
            ScalarPolicyValue = ScalarBindPolicy.SkipIfExists,
            UseEffectMod = false,
            UseClampMod = false
        };

        [BoxGroup("Default Multiplier")]
        [LabelText("Default Multiplier")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        ProfileFloatValue _defaultMultiplier = new()
        {
            Value = 1f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Movement.SpeedMultiplier),
            ScalarPolicyValue = ScalarBindPolicy.SkipIfExists,
            UseEffectMod = false,
            UseClampMod = false
        };

        [BoxGroup("Agent")]
        [LabelText("Agent Radius")]
        [MinValue(0.001f)]
        [SerializeField]
        float _agentRadius = 0.35f;

        public override Type ProfileType => typeof(MovementPreset);

        public float DefaultSpeedFallback => _defaultSpeed.Value;
        public float DefaultMultiplierFallback => _defaultMultiplier.Value;
        public float AgentRadius => _agentRadius;
    }
}
