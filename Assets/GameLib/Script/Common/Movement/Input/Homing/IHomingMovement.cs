#nullable enable
// Game.Movement
// ================================================================================
// IHomingMovement - ホーミング（ターゲット追尾）インターフェース
// ================================================================================
//
// 【概要】
// ターゲット方向と入力方向を角度補間し、GuidanceDirection を生成する。
// BoolLayer で ON/OFF を制御し、OFF 時は計算を停止して状態を保持する。
//
// 【責務】
// - TargetChannelHub からターゲット取得
// - BaseDirection と TargetDirection の角度補間
// - GuidanceDirection の出力
// - BoolLayer による有効/無効制御
//
// 【重要な挙動】
// - HomingEnabled == false: 停止（Target/補間なし）、GuidanceDirection は直前保持
// - HasTarget == false: GuidanceDirection = BaseDirection
// - BaseDirection == zero && HasTarget: GuidanceDirection = TargetDirection（完全追従）
// - BaseDirection != zero && HasTarget: 角度補間で合成
// ================================================================================

using System;
using UnityEngine;
using Game.Common;

namespace Game.Movement
{
    /// <summary>
    /// ホーミング（ターゲット追尾）を管理するモジュール。
    /// </summary>
    public interface IHomingMovement
    {
        /// <summary>Homing の有効/無効を制御する BoolLayer</summary>
        BoolLayer HomingLayer { get; }

        /// <summary>Homing が有効か（BoolLayer の合成結果）</summary>
        bool HomingEnabled { get; }

        /// <summary>現在の GuidanceDirection（読み取り専用）</summary>
        Vector2 GuidanceDirection { get; }

        /// <summary>現在のターゲット情報（読み取り専用）</summary>
        TargetSnapshot CurrentTarget { get; }

        /// <summary>
        /// Homing を更新し、GuidanceDirection を算出。
        /// </summary>
        /// <param name="baseDirection">入力方向（正規化済み or zero）</param>
        /// <param name="ownerPosition">Owner のワールド座標</param>
        /// <param name="deltaTime">デルタタイム</param>
        /// <returns>合成後の GuidanceDirection</returns>
        Vector2 Tick(Vector2 baseDirection, Vector2 ownerPosition, float deltaTime);

        /// <summary>
        /// 合成状態をリセット（ホーミング影響を減らす）。
        /// </summary>
        /// <param name="resetAlpha">0=何もしない, 1=完全リセット</param>
        void ResetBlend(float resetAlpha);

        /// <summary>内部状態を完全にクリア</summary>
        void Clear();
    }

    /// <summary>
    /// Homing 補間パラメータ。
    /// </summary>
    [Serializable]
    public sealed class HomingBlendParams
    {
        [Tooltip("1秒あたりの追従速度（α/秒）")]
        [Min(0.01f)]
        public float BlendSpeed = 2f;

        [Tooltip("時間→α のカーブ（null なら線形）")]
        public AnimationCurve? BlendCurve;

        [Tooltip("最大到達値（通常 1）")]
        [Range(0f, 1f)]
        public float MaxAlpha = 1f;

        /// <summary>デフォルトパラメータを作成</summary>
        public static HomingBlendParams Default => new()
        {
            BlendSpeed = 2f,
            BlendCurve = null,
            MaxAlpha = 1f
        };
    }

    /// <summary>
    /// Homing モジュールのオプション（DI 登録用）。
    /// </summary>
    public sealed class HomingMovementOptions
    {
        /// <summary>ターゲットチャネルのタグ</summary>
        public string TargetChannelTag { get; set; } = "enemy";

        /// <summary>補間パラメータ</summary>
        public HomingBlendParams BlendParams { get; set; } = HomingBlendParams.Default;

        /// <summary>初期状態で Homing を有効にするか</summary>
        public bool EnabledByDefault { get; set; } = true;

        /// <summary>BoolLayer のデフォルトキー</summary>
        public string DefaultLayerKey { get; set; } = "default";

        /// <summary>パラメータ指定コンストラクタ。</summary>
        public HomingMovementOptions(
            string? targetChannelTag = null,
            HomingBlendParams? blendParams = null,
            bool? enabledByDefault = null,
            string? defaultLayerKey = null)
        {
            if (!string.IsNullOrEmpty(targetChannelTag))
                TargetChannelTag = targetChannelTag;
            if (blendParams != null)
                BlendParams = blendParams;
            if (enabledByDefault.HasValue)
                EnabledByDefault = enabledByDefault.Value;
            if (!string.IsNullOrEmpty(defaultLayerKey))
                DefaultLayerKey = defaultLayerKey;
        }

        public HomingMovementOptions()
        {
        }
    }
}
