// Game.StatusEffect.EffectVisualData.cs
//
// StatusEffect 用の表示データ

using System;
using Game.Animation;
using Game.Health;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.StatusEffect
{
    /// <summary>
    /// StatusEffect 用の表示データ。
    /// </summary>
    [Serializable]
    public sealed class EffectVisualData : BaseVisualData
    {
        /// <summary>Buff/Debuff の種別</summary>
        [LabelText("Effect Type")]
        public EffectType EffectType;

        /// <summary>優先度表示用のソート順</summary>
        [LabelText("Sort Order")]
        public int SortOrder;

        /// <summary>効果発動時のアニメーション</summary>
        [LabelText("Apply Anim")]
        [AssetSelector, AssetOrInternal]
        public AnimationData ApplyAnim;

        /// <summary>効果終了時のアニメーション</summary>
        [LabelText("Remove Anim")]
        [AssetSelector, AssetOrInternal]
        public AnimationData RemoveAnim;
    }
}
