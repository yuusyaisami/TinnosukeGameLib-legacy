using UnityEngine;

namespace Game.Audio
{
    // ================================================================
    // IAudioCue - オーディオキュー共通インターフェース
    // ================================================================
    //
    // ## 概要
    //
    // 再生するサウンドの設定を定義するインターフェース。
    // ScriptableObject として実装することを想定。
    //
    // ## 実装例
    //
    // - SfxCue: 単発効果音
    // - BgmCue: ループ BGM
    // - VoiceCue: ボイス（ランダムクリップ選択など）
    //
    // ================================================================

    /// <summary>
    /// オーディオキューの共通インターフェース。
    /// </summary>
    public interface IAudioCue
    {
        /// <summary>出力先バス</summary>
        AudioBusKind Bus { get; }

        /// <summary>ループ再生するか</summary>
        bool Loop { get; }

        /// <summary>同一 Cue の多重再生を許可するか</summary>
        bool AllowMultipleInstances { get; }

        /// <summary>既に再生中の場合、最初からやり直すか</summary>
        bool RestartIfAlreadyPlaying { get; }

        /// <summary>ボリューム乗数（0 以上）</summary>
        float VolumeMultiplier { get; }

        /// <summary>基準ピッチ（0 以上）</summary>
        float BasePitch { get; }

        /// <summary>基準再生速度（0 以上）</summary>
        float BasePlaybackSpeed { get; }

        /// <summary>再生速度を AudioSource.pitch に反映するか</summary>
        bool ApplyPlaybackSpeedToPitch { get; }

        /// <summary>ピッチランダム最小値（例: 0.95）</summary>
        float PitchRandomMin { get; }

        /// <summary>ピッチランダム最大値（例: 1.05）</summary>
        float PitchRandomMax { get; }

        /// <summary>TimeScale 同期にバス既定値を使うか</summary>
        bool UseBusTimeScaleSettings { get; }

        /// <summary>TimeScale を再生速度に反映するか</summary>
        bool ApplyTimeScaleToPlaybackSpeed { get; }

        /// <summary>TimeScale をピッチに反映するか</summary>
        bool ApplyTimeScaleToPitch { get; }

        /// <summary>TimeScale がピッチへ与える影響度（0..1）</summary>
        float TimeScalePitchInfluence { get; }

        /// <summary>3D サウンドにするか</summary>
        bool Spatialize { get; }

        /// <summary>空間ブレンド（0=2D, 1=3D）</summary>
        float SpatialBlend { get; }

        /// <summary>最小距離</summary>
        float MinDistance { get; }

        /// <summary>最大距離</summary>
        float MaxDistance { get; }

        /// <summary>減衰モード</summary>
        AudioRolloffMode RolloffMode { get; }

        /// <summary>再生するクリップを選択（ランダム選択などを実装可能）</summary>
        AudioClip PickClip();

        /// <summary>BGM 等の識別用デフォルトタグ</summary>
        string DefaultTag { get; }
    }
}
