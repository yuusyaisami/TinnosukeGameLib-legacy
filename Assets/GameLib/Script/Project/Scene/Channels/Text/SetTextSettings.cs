#nullable enable
using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public enum NumberRoundingMode
    {
        Round = 0,
        Floor = 1,
        Ceil = 2,
    }

    /// <summary>
    /// SetText に渡す設定構造体。カウント表現・桁・小数・符号・速度・Easing を統合する。
    /// </summary>
    [Serializable]
    public struct SetTextSettings
    {
        [BoxGroup("Counter")]
        [LabelText("Use Counter")]
        [Tooltip("Enable counter animation for numeric values.")]
        public bool UseCounter;

        [BoxGroup("Counter")]
        [ShowIf(nameof(UseCounter))]
        [LabelText("Ease")]
        public Ease CounterEase;

        [BoxGroup("Counter")]
        [ShowIf(nameof(UseCounter))]
        [LabelText("Duration (sec)")]
        [Min(0.01f)]
        public float CounterDurationSeconds;

        [BoxGroup("Counter")]
        [ShowIf(nameof(UseCounter))]
        [LabelText("Use Unscaled Time")]
        public bool CounterUseUnscaledTime;

        [BoxGroup("Formatting")]
        [LabelText("Fixed Integer Digits")]
        [Tooltip("0 = no fixed padding. e.g., 3 -> 007")]
        [Min(0)]
        public int FixedIntegerDigits;

        [BoxGroup("Formatting")]
        [LabelText("Decimal Digits")]
        [Tooltip("Number of decimal places to display.")]
        [Min(0)]
        public int DecimalDigits;

        [BoxGroup("Formatting")]
        [LabelText("Show Plus Sign")]
        [Tooltip("Prepend + for positive numbers.")]
        public bool ShowPlusSign;

        [BoxGroup("Formatting")]
        [LabelText("Use Thousands Separator")]
        public bool UseThousandsSeparator;

        [BoxGroup("Formatting")]
        [LabelText("Rounding Mode")]
        public NumberRoundingMode RoundingMode;

        public static SetTextSettings Default => new()
        {
            UseCounter = false,
            CounterEase = Ease.OutQuad,
            CounterDurationSeconds = 0.5f,
            CounterUseUnscaledTime = false,
            FixedIntegerDigits = 0,
            DecimalDigits = 0,
            ShowPlusSign = false,
            UseThousandsSeparator = false,
            RoundingMode = NumberRoundingMode.Round,
        };
    }
}
