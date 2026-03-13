using System;
using DG.Tweening;

namespace Game.Times
{
    // ================================================================
    // ITimeService - タイムスケール管理サービス
    // ================================================================
    //
    // ## 概要
    //
    // ゲーム全体のタイムスケールを Kind ごとに管理する。
    // 全 Kind の min を Unity の Time.timeScale に反映する。
    //
    // ## 合成ルール
    //
    // - Kind ごとに base を参照
    // - Unity への適用は全 Kind の min で決定
    //
    // ## 外部ライブラリとの連携
    //
    // DOTween, UniTask, TextAnimator 等は Scaled/Unscaled の 2 択のみ。
    // これらは LTS の TimeScaleBehavior で制御する。
    // TimeScaleExtensions を参照。
    //
    // ## 使用例
    //
    // ```csharp
    // // スローモーション
    // timeService.SetBaseScale(TimeScaleKind.GamePlay, 0.5f);
    //
    // // なめらかに戻す
    // timeService.AnimateBaseScale(TimeScaleKind.GamePlay, 1f, 0.3f, Ease.OutQuad);
    // ```
    //
    // ================================================================

    /// <summary>
    /// タイムスケール管理サービス。
    /// </summary>
    public interface ITimeService
    {
        // ----------------------------------------------------------------
        // 実効値取得
        // ----------------------------------------------------------------

        /// <summary>
        /// Unity の Time.timeScale に適用される実効値。
        /// 全 Kind の min 合成。
        /// </summary>
        float UnityTimeScale { get; }

        /// <summary>
        /// 指定 Kind の実効スケール（base）。
        /// </summary>
        float GetEffectiveScale(TimeScaleKind kind);

        /// <summary>
        /// マスクに含まれる Kind の min 合成。
        /// </summary>
        float GetCompositeScale(TimeScaleKindMask mask);

        // ----------------------------------------------------------------
        // 基準値操作
        // ----------------------------------------------------------------

        /// <summary>
        /// 指定 Kind の基準スケールを取得。
        /// </summary>
        float GetBaseScale(TimeScaleKind kind);

        /// <summary>
        /// 指定 Kind の基準スケールを設定。
        /// </summary>
        void SetBaseScale(TimeScaleKind kind, float scale);

        /// <summary>
        /// 指定 Kind の基準スケールをアニメーションで変更。
        /// </summary>
        /// <param name="kind">対象の Kind</param>
        /// <param name="scale">目標スケール（0 以上）</param>
        /// <param name="duration">アニメーション時間（0 以下で即時適用）</param>
        /// <param name="ease">Easing</param>
        void AnimateBaseScale(TimeScaleKind kind, float scale, float duration, Ease ease);

        // ----------------------------------------------------------------
        // イベント
        // ----------------------------------------------------------------

        /// <summary>
        /// Unity の Time.timeScale が変更されたときに発火。
        /// </summary>
        event System.Action UnityTimeScaleChanged;

        // ----------------------------------------------------------------
        // 操作系の追加 API はここにまとめていく
    }
}
