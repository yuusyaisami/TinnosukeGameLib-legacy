// Game.Movement
// ================================================================================
// MovementProfileSO - Movement 系で共通的に使う設定をまとめた ScriptableObject
// ================================================================================
//
// - 共通で扱う設定（速度キー、AgentRadius など）はここに保持する。
// - 速度は ProfileFloatValue + ScalarKey で保持し、各システムは Scalar Service から値を読み取る。
// - Profile Registration System により、このプロファイルが登録されると
//   DefaultSpeed の値が自動的に Scalar に登録される。
// - その他 float 以外の設定は SO 上に直接保持する（例: AgentRadius）。
// ================================================================================

using UnityEngine;
using Sirenix.OdinInspector;
using Game.Profile;
using Game.Scalar;
using Game.Scalar.Generated;

namespace Game.Movement
{
    [CreateAssetMenu(menuName = "Game/Movement/MovementProfile", fileName = "MovementProfile")]
    public sealed class MovementProfileSO : BaseProfileSO
    {
        [BoxGroup("Default Speed")]
        [LabelText("Default Speed")]
        [Tooltip("デフォルト移動速度。Scalar に自動登録される。")]
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
        [Tooltip("デフォルト速度に乗算される倍率。Scalar に自動登録される。")]
        [SerializeField]
        ProfileFloatValue _defaultMultiplier = new()
        {
            Value = 1f,
            ScalarKeyValue = new ScalarKey(ScalarKeys.GameLib.Movement.SpeedMultiplier),
            ScalarPolicyValue = ScalarBindPolicy.SkipIfExists,
            UseEffectMod = false,
            UseClampMod = false
        };

        /// <summary>
        /// デフォルト速度のフォールバック値（Scalar が利用できない場合に使用）。
        /// </summary>
        public float DefaultSpeedFallback => _defaultSpeed.Value;
        /// <summary>
        /// デフォルト速度倍率のフォールバック値（Scalar が利用できない場合に使用）。
        /// </summary>
        public float DefaultMultiplierFallback => _defaultMultiplier.Value;

        [BoxGroup("Agent")]
        [LabelText("Agent Radius")]
        [MinValue(0.001f)]
        public float AgentRadius = 0.35f;
    }
}
