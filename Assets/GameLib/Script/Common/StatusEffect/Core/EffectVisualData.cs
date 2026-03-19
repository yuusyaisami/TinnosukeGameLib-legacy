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
        [Tooltip("UI 上でこの効果をバフ・デバフ・中立のどれとして扱うかを指定します。")]
        public EffectType EffectType;

        /// <summary>優先度表示用のソート順</summary>
        [LabelText("Sort Order")]
        [Tooltip("一覧表示時の並び順です。小さい値ほど前に表示されます。")]
        public int SortOrder;

        /// <summary>効果発動時のアニメーション</summary>
        [LabelText("Apply Anim")]
        [AssetSelector, AssetOrInternal]
        [Tooltip("付与時に再生したい演出アニメーションを設定します。不要なら空で構いません。")]
        public AnimationData ApplyAnim;

        /// <summary>効果終了時のアニメーション</summary>
        [LabelText("Remove Anim")]
        [AssetSelector, AssetOrInternal]
        [Tooltip("削除時に再生したい演出アニメーションを設定します。不要なら空で構いません。")]
        public AnimationData RemoveAnim;
    }
}
