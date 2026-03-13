// Game.StatusEffect.EffectState.cs
//
// StatusEffect の現在状態（UI 表示用等）

using Game.Health;
using UnityEngine;

namespace Game.StatusEffect
{
    /// <summary>
    /// StatusEffect の現在状態（UI 表示用等）
    /// </summary>
    public readonly struct EffectState
    {
        /// <summary>効果 ID</summary>
        public readonly string EffectId;

        /// <summary>表示名</summary>
        public readonly string DisplayName;

        /// <summary>アイコン（オプション）</summary>
        public readonly Sprite Icon;

        /// <summary>効果タイプ（Buff/Debuff）</summary>
        public readonly EffectType Type;

        /// <summary>残り時間（秒）。-1 で永続。</summary>
        public readonly float RemainingTime;

        /// <summary>総時間（秒）</summary>
        public readonly float TotalDuration;

        /// <summary>現在の強度（スタック数等）</summary>
        public readonly float Intensity;

        /// <summary>スタック数</summary>
        public readonly int StackCount;

        public EffectState(
            string effectId,
            string displayName,
            Sprite icon,
            EffectType type,
            float remainingTime,
            float totalDuration,
            float intensity,
            int stackCount)
        {
            EffectId = effectId;
            DisplayName = displayName;
            Icon = icon;
            Type = type;
            RemainingTime = remainingTime;
            TotalDuration = totalDuration;
            Intensity = intensity;
            StackCount = stackCount;
        }
    }
}
