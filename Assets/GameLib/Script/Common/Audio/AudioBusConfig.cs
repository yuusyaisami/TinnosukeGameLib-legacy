using System;
using UnityEngine;

namespace Game.Audio
{
    // ================================================================
    // AudioBusConfig - バス設定
    // ================================================================
    //
    // ## 概要
    //
    // 各オーディオバスの設定を定義する。
    // AudioInstallerMB の Inspector で設定可能。
    //
    // ================================================================

    /// <summary>
    /// オーディオバスの設定。
    /// </summary>
    [Serializable]
    public struct AudioBusConfig
    {
        /// <summary>対象バス</summary>
        public AudioBusKind Bus;

        // ----------------------------------------------------------------
        // プール設定
        // ----------------------------------------------------------------

        [Header("Pooling")]
        [Tooltip("初期プールサイズ")]
        public int InitialPoolSize;

        // ----------------------------------------------------------------
        // 同時再生制御
        // ----------------------------------------------------------------

        [Header("Concurrency")]
        [Tooltip("タグ単位で1つに制限（BGM向け）")]
        public bool SingleInstanceByTag;

        // ----------------------------------------------------------------
        // フェード設定
        // ----------------------------------------------------------------

        [Header("Fade Defaults")]
        [Tooltip("デフォルトのフェードアウト時間（秒）")]
        public float DefaultFadeOutSeconds;

        // ----------------------------------------------------------------
        // TimeScale 連携
        // ----------------------------------------------------------------

        [Header("TimeScale Sync")]
        [Tooltip("TimeService / Time.timeScale 変化時に再生速度へ影響を与えるか")]
        public bool ApplyTimeScaleToPlaybackSpeed;

        [Tooltip("TimeService / Time.timeScale 変化時にピッチに影響を与えるか")]
        public bool AffectedByTimeScale;

        [Tooltip("ピッチへの影響度（0=無影響, 1=フル影響）")]
        [Range(0f, 1f)]
        public float PitchInfluence;

        // ----------------------------------------------------------------
        // デフォルト値
        // ----------------------------------------------------------------

        /// <summary>
        /// バス種別に応じたデフォルト設定を生成。
        /// </summary>
        public static AudioBusConfig Default(AudioBusKind bus)
        {
            return new AudioBusConfig
            {
                Bus = bus,
                InitialPoolSize = bus == AudioBusKind.Bgm ? 2 : 8,
                SingleInstanceByTag = (bus == AudioBusKind.Bgm),
                DefaultFadeOutSeconds = bus == AudioBusKind.Bgm ? 0.5f : 0.1f,
                ApplyTimeScaleToPlaybackSpeed = false,
                AffectedByTimeScale = true,
                PitchInfluence = 0f, // 既定：無影響
            };
        }
    }
}
