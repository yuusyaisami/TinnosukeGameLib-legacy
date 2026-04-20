#nullable enable
using System;
using Game.Commands;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// 蜑ｲ繧願ｾｼ縺ｿ繝ｫ繝ｼ繝ｫ螳夂ｾｩ・・O 蜀・↓菫晄戟・・
    /// </summary>
    [Serializable]
    public sealed class InterruptRule
    {
        [LabelText("Condition")]
        [Tooltip("DynamicValue<bool> 繝吶・繧ｹ縺ｮ譚｡莉ｶ")]
        public DynamicValue<bool> Condition;

        [LabelText("Target Clip")]
        [Tooltip("譚｡莉ｶ謌千ｫ区凾縺ｫ驕ｷ遘ｻ縺吶ｋ Clip")]
        [AssetOrInternal]
        public AIClipSO? TargetClip;

        [LabelText("Policy")]
        public InterruptPolicy Policy = InterruptPolicy.Push;

        [LabelText("Priority Override")]
        [Tooltip("-1 縺ｧ TargetClip.Priority 繧剃ｽｿ逕ｨ")]
        public int PriorityOverride = -1;

        [LabelText("Cooldown Frames")]
        [Tooltip("Inspector setting.")]
        [MinValue(0)]
        public int CooldownFrames = 0;

        [LabelText("Min True Frames")]
        [Tooltip("譚｡莉ｶ縺碁｣邯壹〒 true 縺ｫ縺ｪ繧句ｿ・ｦ√′縺ゅｋ繝輔Ξ繝ｼ繝謨ｰ")]
        [MinValue(1)]
        public int MinTrueFrames = 1;

        public int EffectivePriority => PriorityOverride >= 0 ? PriorityOverride : (TargetClip?.Priority ?? 0);

        public InterruptRuleRuntime CreateRuntime(in AIAgentContext ctx)
        {
            return new InterruptRuleRuntime(this, ctx);
        }
    }

    /// <summary>
    /// 蜑ｲ繧願ｾｼ縺ｿ繝ｫ繝ｼ繝ｫ縺ｮ螳溯｡梧凾迥ｶ諷・
    /// </summary>
    public sealed class InterruptRuleRuntime
    {
        readonly InterruptRule _rule;
        readonly Game.Commands.VNext.CommandContext _evalContext;

        int _trueContinuousFrames;
        int _cooldownRemaining;
        int _lastEvalFrame = -1;

        public InterruptRule Rule => _rule;
        public AIClipSO? TargetClip => _rule.TargetClip;
        public InterruptPolicy Policy => _rule.Policy;
        public int EffectivePriority => _rule.EffectivePriority;

        public InterruptRuleRuntime(InterruptRule rule, in AIAgentContext ctx)
        {
            _rule = rule;
            _evalContext = ctx.ToCommandContext();
        }

        public bool Evaluate(in AIAgentContext ctx)
        {
            // 蜷御ｸ繝輔Ξ繝ｼ繝縺ｧ縺ｮ驥崎､・ｩ穂ｾ｡繧帝亟豁｢
            if (ctx.FrameCount == _lastEvalFrame)
                return false;
            _lastEvalFrame = ctx.FrameCount;

            // 繧ｯ繝ｼ繝ｫ繝繧ｦ繝ｳ荳ｭ
            if (_cooldownRemaining > 0)
            {
                _cooldownRemaining--;
                _trueContinuousFrames = 0;
                return false;
            }

            // 譚｡莉ｶ隧穂ｾ｡
            bool result = _rule.Condition.EvaluateBool(_evalContext);

            if (result)
            {
                _trueContinuousFrames++;
                if (_trueContinuousFrames >= _rule.MinTrueFrames)
                {
                    // 逋ｺ轣ｫ・√け繝ｼ繝ｫ繝繧ｦ繝ｳ髢句ｧ・
                    _cooldownRemaining = _rule.CooldownFrames;
                    _trueContinuousFrames = 0;
                    return true;
                }
            }
            else
            {
                _trueContinuousFrames = 0;
            }

            return false;
        }

        public void Reset()
        {
            _trueContinuousFrames = 0;
            _cooldownRemaining = 0;
            _lastEvalFrame = -1;
        }
    }
}
