#nullable enable
using System;
using Game.Commands;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// 割り込みルール定義（SO 内に保持）
    /// </summary>
    [Serializable]
    public sealed class InterruptRule
    {
        [LabelText("Condition")]
        [Tooltip("DynamicValue<bool> ベースの条件")]
        public DynamicValue<bool> Condition;

        [LabelText("Target Clip")]
        [Tooltip("条件成立時に遷移する Clip")]
        [AssetOrInternal]
        public AIClipSO? TargetClip;

        [LabelText("Policy")]
        public InterruptPolicy Policy = InterruptPolicy.Push;

        [LabelText("Priority Override")]
        [Tooltip("-1 で TargetClip.Priority を使用")]
        public int PriorityOverride = -1;

        [LabelText("Cooldown Frames")]
        [Tooltip("発火後のクールダウン（フレーム）")]
        [MinValue(0)]
        public int CooldownFrames = 0;

        [LabelText("Min True Frames")]
        [Tooltip("条件が連続で true になる必要があるフレーム数")]
        [MinValue(1)]
        public int MinTrueFrames = 1;

        public int EffectivePriority => PriorityOverride >= 0 ? PriorityOverride : (TargetClip?.Priority ?? 0);

        public InterruptRuleRuntime CreateRuntime(in AIAgentContext ctx)
        {
            return new InterruptRuleRuntime(this, ctx);
        }
    }

    /// <summary>
    /// 割り込みルールの実行時状態
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
            // 同一フレームでの重複評価を防止
            if (ctx.FrameCount == _lastEvalFrame)
                return false;
            _lastEvalFrame = ctx.FrameCount;

            // クールダウン中
            if (_cooldownRemaining > 0)
            {
                _cooldownRemaining--;
                _trueContinuousFrames = 0;
                return false;
            }

            // 条件評価
            bool result = _rule.Condition.EvaluateBool(_evalContext);

            if (result)
            {
                _trueContinuousFrames++;
                if (_trueContinuousFrames >= _rule.MinTrueFrames)
                {
                    // 発火！クールダウン開始
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
