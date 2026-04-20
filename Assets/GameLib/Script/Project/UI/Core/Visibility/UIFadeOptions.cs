#nullable enable
using System;
using DG.Tweening;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// UI陦ｨ遉ｺ/髱櫁｡ｨ遉ｺ縺ｮFade險ｭ螳壹・
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

        [Tooltip("Inspector setting.")]
        public bool UseUnscaledTime;

        [Tooltip("Inspector setting.")]
        public bool DisableInteractionDuringFade;

        [Tooltip("Inspector setting.")]
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
