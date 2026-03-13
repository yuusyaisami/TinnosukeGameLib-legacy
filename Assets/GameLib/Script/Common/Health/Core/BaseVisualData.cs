// Game.Health.BaseVisualData.cs
//
// 外部に見せるための表示用データの基底クラス

using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Animation;

namespace Game.Health
{
    /// <summary>
    /// 外部に見せるための表示用データの基底クラス。
    /// UI、ログ、デバッグ表示等で使用される共通データを提供する。
    /// HealthSystem と StatusEffectSystem で共通使用。
    /// </summary>
    [Serializable]
    public abstract class BaseVisualData
    {
        /// <summary>表示名（ローカライズキー or 直接文字列）</summary>
        [LabelText("Display Name")]
        [Tooltip("UI 等で表示される名前")]
        public string DisplayName;

        /// <summary>アイコン用 AnimationData（スプライトシート対応）</summary>
        [LabelText("Icon Animation")]
        [Tooltip("アイコン表示用の AnimationData。静止画の場合は 1 フレームのみ設定。")]
        [AssetSelector, AssetOrInternal]
        public AnimationData IconAnimation;

        /// <summary>説明文（ローカライズキー or 直接文字列）</summary>
        [LabelText("Description")]
        [TextArea(2, 4)]
        public string Description;

        /// <summary>
        /// IconAnimation の最初のフレームのスプライトを取得。
        /// IconAnimation が null の場合は null を返す。
        /// </summary>
        public Sprite Icon => IconAnimation?.frames?.Count > 0
            ? IconAnimation.frames[0].sprite
            : null;
    }

    /// <summary>
    /// Health Modifier 用の表示データ。
    /// </summary>
    [Serializable]
    public sealed class HealthModifierVisualData : BaseVisualData
    {
        /// <summary>Buff/Debuff の種別</summary>
        [LabelText("Effect Type")]
        public EffectType EffectType;

        /// <summary>優先度表示用のソート順</summary>
        [LabelText("Sort Order")]
        public int SortOrder;
    }

    /// <summary>
    /// 効果の種類（UI 分類用）
    /// </summary>
    public enum EffectType
    {
        /// <summary>バフ（有利な効果）</summary>
        Buff,

        /// <summary>デバフ（不利な効果）</summary>
        Debuff,

        /// <summary>中立（有利でも不利でもない）</summary>
        Neutral,
    }
}
