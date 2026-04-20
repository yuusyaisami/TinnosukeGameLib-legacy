#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    public interface ITextAnimationPreset
    {
        bool Loop { get; }
        int LoopCount { get; }
        IReadOnlyList<ITextAnimationStep> Steps { get; }
    }

    public interface ITextAnimationStep
    {
        TextChannelCommandAction Action { get; }
        DynamicValue<float> DelaySeconds { get; }
        DynamicValue<string> Text { get; }
        TextPlayMode PlayMode { get; }
        bool ApplyText { get; }
        bool Visible { get; }
    }

    [Serializable]
    public sealed class TextAnimationPreset : ITextAnimationPreset
    {
        [Header("Loop")]
        [Tooltip("繧ｷ繝ｼ繧ｱ繝ｳ繧ｹ蜈ｨ菴薙ｒ繝ｫ繝ｼ繝励☆繧九°")]
        public bool loop;

        [ShowIf(nameof(loop))]
        [Tooltip("Inspector setting.")]
        [MinValue(-1)]
        public int loopCount = -1;

        [Header("Steps")]
        public List<TextAnimationStep> steps = new();
        public List<TextAnimationStep> Steps => steps;

        bool ITextAnimationPreset.Loop => loop;
        int ITextAnimationPreset.LoopCount => loopCount;
        IReadOnlyList<ITextAnimationStep> ITextAnimationPreset.Steps => steps;
    }

    [Serializable]
    public sealed class TextAnimationStep : ITextAnimationStep
    {
        [TableColumnWidth(120)]
        [LabelText("Action")]
        public TextChannelCommandAction action = TextChannelCommandAction.SetText;

        [LabelText("Delay (sec)")]
        public DynamicValue<float> delaySeconds = new();

        [LabelText("Apply Text")]
        public bool applyText = true;

        [ShowIf(nameof(UsesText))]
        [LabelText("Text")]
        public DynamicValue<string> text = new();

        [ShowIf(nameof(UsesText))]
        [LabelText("Play Mode")]
        public TextPlayMode playMode = TextPlayMode.Instant;

        [ShowIf(nameof(UsesVisible))]
        [LabelText("Visible")]
        public bool visible = true;

        bool UsesText =>
            action == TextChannelCommandAction.SetText ||
            action == TextChannelCommandAction.Append;

        bool UsesVisible => action == TextChannelCommandAction.SetVisible;

        TextChannelCommandAction ITextAnimationStep.Action => action;
        DynamicValue<float> ITextAnimationStep.DelaySeconds => delaySeconds;
        DynamicValue<string> ITextAnimationStep.Text => text;
        TextPlayMode ITextAnimationStep.PlayMode => playMode;
        bool ITextAnimationStep.ApplyText => applyText;
        bool ITextAnimationStep.Visible => visible;
    }
}
