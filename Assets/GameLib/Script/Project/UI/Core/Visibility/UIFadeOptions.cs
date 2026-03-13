#nullable enable
using System;
using DG.Tweening;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// UI表示/非表示のFade設定。
    /// </summary>
    [Serializable]
    public struct UIFadeOptions
    {
        [Min(0f)]
        public float FadeInSeconds;

        [Min(0f)]
        public float FadeOutSeconds;

        public Ease FadeInEase;
        public Ease FadeOutEase;

        [Tooltip("UI用途では true 推奨（Time.timeScale の影響を受けない）。")]
        public bool UseUnscaledTime;

        [Tooltip("Fade中は interactable/blocksRaycasts を false にする。")]
        public bool DisableInteractionDuringFade;

        [Tooltip("非表示完了時に描画を停止する（Graphic.enabled等）。")]
        public bool DisableRenderWhenHidden;

        public static UIFadeOptions Default => new()
        {
            FadeInSeconds = 0.25f,
            FadeOutSeconds = 0.25f,
            FadeInEase = Ease.OutQuad,
            FadeOutEase = Ease.InQuad,
            UseUnscaledTime = true,
            DisableInteractionDuringFade = true,
            DisableRenderWhenHidden = false,
        };
    }
}
