using System;

namespace Game.Times
{
    // ================================================================
    // TimeScaleBehavior - LTS 単位の TimeScale 適用設定
    // ================================================================
    //
    // ## 概要
    //
    // 外部ライブラリ（DOTween, UniTask, TextAnimator）は
    // Scaled / Unscaled の 2 択しかサポートしない。
    // LTS 単位でこの設定を行い、配下のコンポーネントが参照する。
    //
    // ================================================================

    /// <summary>
    /// LTS 配下のコンポーネントが TimeScale の影響を受けるかの設定。
    /// </summary>
    public enum TimeScaleBehavior
    {
        /// <summary>
        /// Time.timeScale の影響を受ける（通常のゲームプレイ）。
        /// DOTween: SetUpdate(false)
        /// UniTask: DelayType.DeltaTime
        /// TextAnimator: TimeScale.Scaled
        /// </summary>
        Scaled = 0,

        /// <summary>
        /// Time.timeScale を無視する（UI、ポーズメニュー等）。
        /// DOTween: SetUpdate(true)
        /// UniTask: DelayType.UnscaledDeltaTime
        /// TextAnimator: TimeScale.Unscaled
        /// </summary>
        Unscaled = 1,
    }

    // ================================================================
    // TimeScaleKind - タイムスケール種別（内部システム用）
    // ================================================================
    //
    // ## 概要
    //
    // 複数の TimeScale を独立管理するための種別定義。
    // 例えば Pause と GamePlay を分離することで、
    // ポーズ中でも UI アニメーションだけ動かすといった制御が可能。
    //
    // ## 合成ルール
    //
    // - Kind ごとに基準スケールは 1 つだけ保持
    // - Unity の Time.timeScale は全 Kind の min で決定
    //
    // ================================================================

    /// <summary>
    /// タイムスケールの種別。
    /// 値は 0 からの連番を推奨（Mask に使用するため）。
    /// </summary>
    public enum TimeScaleKind
    {
        GamePlay = 0,
        Pause = 1,
    }

    /// <summary>
    /// TimeScaleKind のビットマスク。
    /// 複数の Kind を組み合わせて指定する際に使用。
    /// </summary>
    [Flags]
    public enum TimeScaleKindMask : ulong
    {
        None = 0,
        GamePlay = 1UL << (int)TimeScaleKind.GamePlay,
        Pause = 1UL << (int)TimeScaleKind.Pause,

        All = GamePlay | Pause,
    }

    /// <summary>
    /// TimeScaleKindMask のユーティリティ。
    /// </summary>
    public static class TimeScaleKindMaskUtil
    {
        /// <summary>
        /// マスクに指定した Kind が含まれているか判定。
        /// </summary>
        public static bool Contains(this TimeScaleKindMask mask, TimeScaleKind kind)
            => ((ulong)mask & (1UL << (int)kind)) != 0;

        /// <summary>
        /// 単一の Kind からマスクを生成。
        /// </summary>
        public static TimeScaleKindMask Of(TimeScaleKind kind)
            => (TimeScaleKindMask)(1UL << (int)kind);
    }
}
