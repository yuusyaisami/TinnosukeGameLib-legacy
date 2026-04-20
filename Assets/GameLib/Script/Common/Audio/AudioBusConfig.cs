using System;
using UnityEngine;

namespace Game.Audio
{
    // ================================================================
    // AudioBusConfig - 繝舌せ險ｭ螳・
    // ================================================================
    //
    // ## 讎りｦ・
    //
    // 蜷・が繝ｼ繝・ぅ繧ｪ繝舌せ縺ｮ險ｭ螳壹ｒ螳夂ｾｩ縺吶ｋ縲・
    // AudioInstallerMB 縺ｮ Inspector 縺ｧ險ｭ螳壼庄閭ｽ縲・
    //
    // ================================================================

    /// <summary>
    /// 繧ｪ繝ｼ繝・ぅ繧ｪ繝舌せ縺ｮ險ｭ螳壹・
    /// </summary>
    [Serializable]
    public struct AudioBusConfig
    {
        /// <summary>蟇ｾ雎｡繝舌せ</summary>
        public AudioBusKind Bus;

        // ----------------------------------------------------------------
        // 繝励・繝ｫ險ｭ螳・
        // ----------------------------------------------------------------

        [Header("Pooling")]
        [Tooltip("蛻晄悄繝励・繝ｫ繧ｵ繧､繧ｺ")]
        public int InitialPoolSize;

        // ----------------------------------------------------------------
        // 蜷梧凾蜀咲函蛻ｶ蠕｡
        // ----------------------------------------------------------------

        [Header("Concurrency")]
        [Tooltip("Inspector setting.")]
        public bool SingleInstanceByTag;

        // ----------------------------------------------------------------
        // 繝輔ぉ繝ｼ繝芽ｨｭ螳・
        // ----------------------------------------------------------------

        [Header("Fade Defaults")]
        [Tooltip("Inspector setting.")]
        public float DefaultFadeOutSeconds;

        // ----------------------------------------------------------------
        // TimeScale 騾｣謳ｺ
        // ----------------------------------------------------------------

        [Header("TimeScale Sync")]
        [Tooltip("TimeService / Time.timeScale 螟牙喧譎ゅ↓蜀咲函騾溷ｺｦ縺ｸ蠖ｱ髻ｿ繧剃ｸ弱∴繧九°")]
        public bool ApplyTimeScaleToPlaybackSpeed;

        [Tooltip("TimeService / Time.timeScale 螟牙喧譎ゅ↓繝斐ャ繝√↓蠖ｱ髻ｿ繧剃ｸ弱∴繧九°")]
        public bool AffectedByTimeScale;

        [Tooltip("Inspector setting.")]
        [Range(0f, 1f)]
        public float PitchInfluence;

        // ----------------------------------------------------------------
        // 繝・ヵ繧ｩ繝ｫ繝亥､
        // ----------------------------------------------------------------

        /// <summary>
        /// 繝舌せ遞ｮ蛻･縺ｫ蠢懊§縺溘ョ繝輔か繝ｫ繝郁ｨｭ螳壹ｒ逕滓・縲・
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
                PitchInfluence = 0f, // 譌｢螳夲ｼ夂┌蠖ｱ髻ｿ
            };
        }
    }
}
