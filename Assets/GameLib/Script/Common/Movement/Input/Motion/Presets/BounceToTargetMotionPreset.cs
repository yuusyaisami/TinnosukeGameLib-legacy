#nullable enable
// Game.Movement
// ================================================================================
// BounceToTargetMotionPreset - 繝舌え繝ｳ繝峨＠縺ｦ繧ｿ繝ｼ繧ｲ繝・ヨ菴咲ｽｮ縺ｧ蛛懈ｭ｢縺吶ｋ繝｢繝ｼ繧ｷ繝ｧ繝ｳ
// ================================================================================
//
// 縲先ｦりｦ√・
// - 繧ｿ繝ｼ繧ｲ繝・ヨ蠎ｧ讓吶∈蜷代°縺｣縺ｦ謫ｬ莨ｼ2D繝舌え繝ｳ繝峨＠縲∵怙邨ら噪縺ｫ蛻ｶ豁｢縺吶ｋ縲・
// - 驥榊鴨縺ｯ荳区婿蜷代・
// - InitialAngle 縺ｯ GuidanceDirection 縺ｫ蟇ｾ縺吶ｋ蛻晄悄逋ｺ蟆・ｧ偵・繧ｪ繝輔そ繝・ヨ・亥ｺｦ・峨・
//
// 縲仙ｮ溯｣・婿驥昴・
// - 騾溷ｺｦ繧貞・驛ｨ迥ｶ諷九→縺励※遨榊・縺励∝・蜉帙・ AdditiveVelocity 縺ｧ窶懈怙邨る溷ｺｦ縺昴・繧ゅ・窶昴ｒ霑斐☆縲・
// - 繝偵ャ繝亥ｺ翫・ TargetPosition.y 繧貞渕貅悶↓縺励◆豌ｴ蟷ｳ髱｢縺ｨ縺励※謇ｱ縺・∝渚逋ｺ菫よ焚縺ｧ霍ｳ縺ｭ霑斐ｋ縲・
// - X 譁ｹ蜷代・繧ｿ繝ｼ繧ｲ繝・ヨ縺ｫ蜷ｸ縺・ｯ・○繧具ｼ医ヰ繧ｦ繝ｳ繝我ｸｭ縺ｫ閾ｪ辟ｶ縺ｫ蟇・ｋ・・
// - 霑大ｍ + 菴朱溘〒蛛懈ｭ｢縲・
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    [Serializable]
    public sealed class BounceToTargetMotionPreset : MotionPreset
    {
        [Header("Launch")]
        [LabelText("Initial Angle")]
        [Tooltip("Inspector setting.")]
        public float InitialAngle = 0f;

        [LabelText("Initial Speed Multiplier")]
        [Tooltip("蛻晞・= SpeedBase * 縺薙・蛟咲紫")]
        [Min(0f)]
        public float InitialSpeedMultiplier = 1f;

        [Header("Gravity")]
        [LabelText("Gravity")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float Gravity = 30f;

        [LabelText("Air Drag")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float AirDrag = 0.5f;

        [Header("Bounce")]
        [LabelText("Restitution")]
        [Tooltip("Inspector setting.")]
        [Range(0f, 1f)]
        public float Restitution = 0.6f;

        [LabelText("Restitution Decay")]
        [Tooltip("Inspector setting.")]
        [Range(0f, 1f)]
        public float RestitutionDecay = 0.85f;

        [LabelText("Bounce Friction")]
        [Tooltip("Inspector setting.")]
        [Range(0f, 1f)]
        public float BounceFriction = 0.15f;

        [Header("Attraction (Horizontal)")]
        [LabelText("Attraction")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float HorizontalAttraction = 20f;

        [LabelText("Damping")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float HorizontalDamping = 6f;

        [Header("Stop")]
        [LabelText("Stop Distance")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float StopDistance = 0.05f;

        [LabelText("Stop Speed")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float StopSpeed = 0.05f;

        public override MotionRuntime CreateRuntime() => new BounceToTargetMotionRuntime(this);
    }

    public sealed class BounceToTargetMotionRuntime : MotionRuntime
    {
        readonly BounceToTargetMotionPreset _source;

        Vector2 _velocity;
        bool _hasInitialVelocity;
        float _restitution;

        public BounceToTargetMotionRuntime(BounceToTargetMotionPreset source)
        {
            _source = source;
        }

        protected override void OnInitialize()
        {
            _velocity = Vector2.zero;
            _hasInitialVelocity = false;
            _restitution = Mathf.Clamp01(_source.Restitution);
        }

        protected override void OnReset()
        {
            _velocity = Vector2.zero;
            _hasInitialVelocity = false;
            _restitution = Mathf.Clamp01(_source.Restitution);
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            if (!frame.Target.HasTarget)
                return MotionOutput.Default(frame.GuidanceDirection);

            float dt = Mathf.Max(0f, frame.DeltaTime);
            if (dt <= 0f)
                return MotionOutput.Default(frame.GuidanceDirection);

            Vector2 ownerPos = frame.Target.OwnerPosition;
            Vector2 targetPos = frame.Target.TargetPosition;

            // 蛻晞・
            if (!_hasInitialVelocity)
            {
                var guide = frame.GuidanceDirection;
                if (guide.sqrMagnitude < MovementMath.NormalizeEpsilon)
                    guide = Vector2.up;

                var dir = MovementMath.RotateDirection(guide, _source.InitialAngle);
                dir = MovementMath.NormalizeDirection(dir);

                float initialSpeed = Mathf.Max(0f, frame.SpeedBase) * Mathf.Max(0f, _source.InitialSpeedMultiplier);
                _velocity = dir * initialSpeed;
                _hasInitialVelocity = true;
            }

            // X 譁ｹ蜷代・繧ｿ繝ｼ繧ｲ繝・ヨ縺ｸ蜷ｸ縺・ｯ・○繧具ｼ郁・辟ｶ縺ｫ窶懷ｯ・▲縺ｦ縺・￥窶昴◆繧・ｼ・
            float dx = targetPos.x - ownerPos.x;
            float ax = _source.HorizontalAttraction * dx - _source.HorizontalDamping * _velocity.x;

            // 驥榊鴨
            Vector2 accel = new(ax, -_source.Gravity);
            _velocity += accel * dt;

            // 遨ｺ豌玲慣謚暦ｼ域欠謨ｰ貂幄｡ｰ縺ｧ dt 縺ｫ螳牙ｮ夲ｼ・
            float drag = Mathf.Max(0f, _source.AirDrag);
            if (drag > 0f)
            {
                float damp = Mathf.Exp(-drag * dt);
                _velocity *= damp;
            }

            // 謫ｬ莨ｼ蠎奇ｼ・argetY・峨〒繝舌え繝ｳ繝・
            float groundY = targetPos.y;
            float nextY = ownerPos.y + _velocity.y * dt;
            bool willCrossGround = ownerPos.y > groundY && nextY <= groundY;
            bool alreadyBelow = ownerPos.y <= groundY;

            if ((_velocity.y < 0f) && (willCrossGround || alreadyBelow))
            {
                // 蜿榊ｰ・
                _velocity.y = -_velocity.y * _restitution;

                // 鞫ｩ謫ｦ
                float friction = Mathf.Clamp01(_source.BounceFriction);
                _velocity.x *= (1f - friction);

                // 繝舌え繝ｳ繝峨＃縺ｨ縺ｫ蠑ｱ縺・
                _restitution *= Mathf.Clamp01(_source.RestitutionDecay);
            }

            // 蛛懈ｭ｢蛻､螳夲ｼ郁ｿ大ｍ + 菴朱滂ｼ・
            float stopDist = Mathf.Max(0f, _source.StopDistance);
            float stopSpeed = Mathf.Max(0f, _source.StopSpeed);

            if ((ownerPos - targetPos).sqrMagnitude <= stopDist * stopDist &&
                _velocity.sqrMagnitude <= stopSpeed * stopSpeed)
            {
                _velocity = Vector2.zero;
                return MotionOutput.Zero;
            }

            // AdditiveVelocity 縺ｨ縺励※窶懈怙邨る溷ｺｦ縺昴・繧ゅ・窶昴ｒ蜃ｺ縺・
            return new MotionOutput(
                direction: Vector2.zero,
                speedMul: 0f,
                additiveVelocity: _velocity
            );
        }
    }
}
