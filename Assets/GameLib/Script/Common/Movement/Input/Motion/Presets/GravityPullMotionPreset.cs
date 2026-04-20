#nullable enable
// Game.Movement
// ================================================================================
// GravityPullMotionPreset - 驥榊鴨縺ｧ關ｽ縺｡縺ｦ縺・￥繝｢繝ｼ繧ｷ繝ｧ繝ｳ
// ================================================================================
//
// 縲先ｦりｦ√・
// - 蛻晄悄隗貞ｺｦ・・nitialAngle・峨〒騾ｲ縺ｿ蟋九ａ縲∵凾髢薙→縺ｨ繧ゅ↓驥榊鴨縺ｧ荳九∈蠑輔▲蠑ｵ繧峨ｌ繧九・
// - GuidanceDirection 縺ｯ縲悟・譛溘・蜑肴婿蜷代阪・蝓ｺ貅悶→縺励※菴ｿ縺・√◎縺薙°繧芽ｧ貞ｺｦ繧ｪ繝輔そ繝・ヨ繧剃ｸ弱∴繧九・
// - 蜃ｺ蜉帙・ BaseVelocity・・irection*speedBase*speedMul・・ 驥榊鴨縺ｫ繧医ｋ AdditiveVelocity縲・
// ================================================================================

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Movement
{
    [Serializable]
    public sealed class GravityPullMotionPreset : MotionPreset
    {
        [Header("Direction")]
        [LabelText("Initial Angle")]
        [Tooltip("Inspector setting.")]
        public float InitialAngle = 0f;

        [Header("Gravity")]
        [LabelText("Gravity")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float Gravity = 25f;

        [LabelText("Air Drag")]
        [Tooltip("Inspector setting.")]
        [Min(0f)]
        public float AirDrag = 0.2f;

        [Header("Speed Modulation")]
        [LabelText("Speed Multiplier")]
        [Tooltip("蝓ｺ譛ｬ騾溷ｺｦ縺ｸ縺ｮ蛟咲紫")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        [Header("Debug")]
        [LabelText("Enable Debug Log")]
        public bool EnableDebugLog = false;

        [LabelText("Debug Log Interval Frames")]
        [Min(1)]
        [ShowIf(nameof(EnableDebugLog))]
        public int DebugLogIntervalFrames = 20;

        public override MotionRuntime CreateRuntime() => new GravityPullMotionRuntime(this);
    }

    public sealed class GravityPullMotionRuntime : MotionRuntime
    {
        readonly GravityPullMotionPreset _source;
        Vector2 _gravityVelocity;
        Vector2 _initialDirection;
        bool _hasInitialDirection;
        int _lastDebugLogFrame;

        public GravityPullMotionRuntime(GravityPullMotionPreset source)
        {
            _source = source;
        }

        protected override void OnInitialize()
        {
            _gravityVelocity = Vector2.zero;
            _initialDirection = Vector2.zero;
            _hasInitialDirection = false;
            _lastDebugLogFrame = -99999;
        }

        protected override void OnReset()
        {
            _gravityVelocity = Vector2.zero;
            _initialDirection = Vector2.zero;
            _hasInitialDirection = false;
            _lastDebugLogFrame = -99999;
        }

        protected override MotionOutput OnTick(in MovementGuidanceFrame frame)
        {
            float dt = Mathf.Max(0f, frame.DeltaTime);
            if (dt <= 0f)
                return MotionOutput.Default(frame.GuidanceDirection);

            // 蛻晄悄譁ｹ蜷代・譛蛻昴↓譛牙柑縺縺｣縺・GuidanceDirection 繧貞崋螳壼､縺ｨ縺励※菴ｿ縺・・
            // Guidance 縺梧悴險ｭ螳壹・蝣ｴ蜷医・ BaseDirection 繧剃ｽｿ縺・√◎繧後ｂ譛ｪ險ｭ螳壹↑繧・
            // 縲梧耳騾ｲ縺ｪ縺暦ｼ磯㍾蜉帙・縺ｿ・峨阪↓縺吶ｋ縲・
            if (!_hasInitialDirection && frame.GuidanceDirection.sqrMagnitude >= MovementMath.NormalizeEpsilon)
            {
                _initialDirection = MovementMath.NormalizeDirection(frame.GuidanceDirection);
                _hasInitialDirection = true;
            }

            if (!_hasInitialDirection && frame.BaseDirection.sqrMagnitude >= MovementMath.NormalizeEpsilon)
            {
                _initialDirection = MovementMath.NormalizeDirection(frame.BaseDirection);
                _hasInitialDirection = true;
            }

            var guide = _hasInitialDirection ? _initialDirection : Vector2.zero;

            var dir = MovementMath.RotateDirection(guide, _source.InitialAngle);
            if (dir.sqrMagnitude >= MovementMath.NormalizeEpsilon)
                dir = MovementMath.NormalizeDirection(dir);
            else
                dir = Vector2.zero;

            // 驥榊鴨騾溷ｺｦ繧堤ｩ榊・
            _gravityVelocity += Vector2.down * Mathf.Max(0f, _source.Gravity) * dt;

            // 遨ｺ豌玲慣謚暦ｼ域欠謨ｰ貂幄｡ｰ・・
            float drag = Mathf.Max(0f, _source.AirDrag);
            if (drag > 0f)
            {
                float damp = Mathf.Exp(-drag * dt);
                _gravityVelocity *= damp;
            }

            var output = new MotionOutput(
                direction: dir,
                speedMul: Mathf.Max(0f, _source.SpeedMultiplier),
                additiveVelocity: _gravityVelocity
            );

            if (_source.EnableDebugLog)
            {
                var interval = Mathf.Max(1, _source.DebugLogIntervalFrames);
                if (Time.frameCount - _lastDebugLogFrame >= interval)
                {
                    _lastDebugLogFrame = Time.frameCount;
                    Debug.Log(
                        $"[GravityPullMotion] frame={Time.frameCount} dt={dt:F4} " +
                        $"base={frame.BaseDirection} guidance={frame.GuidanceDirection} hasInit={_hasInitialDirection} init={_initialDirection} " +
                        $"dir={output.Direction} speedMul={output.SpeedMul:F3} addVel={output.AdditiveVelocity}");
                }
            }

            return output;
        }
    }
}
