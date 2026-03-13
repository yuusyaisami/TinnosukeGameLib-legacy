#nullable enable
using System;
using Game.Channel;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum TextChannelCommandAction
    {
        SetText = 0,
        Append = 1,
        Clear = 2,
        Skip = 3,
        SetVisible = 4,
    }

    public enum TextChannelCommandMode
    {
        Single = 0,
        Preset = 1,
    }

    [Serializable]
    public sealed class TextChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.TextChannel;
        public string DebugData
        {
            get
            {
                var tag = string.IsNullOrEmpty(ChannelTag) ? "<none>" : ChannelTag;
                if (Mode == TextChannelCommandMode.Preset)
                    return $"Tag={tag} Mode=Preset";
                return $"Tag={tag} Action={Action}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [BoxGroup("Mode")]
        [LabelText("Mode")]
        public TextChannelCommandMode Mode = TextChannelCommandMode.Single;

        [BoxGroup("Mode")]
        [LabelText("Await Mode")]
        [ShowIf("@Mode == TextChannelCommandMode.Preset")]
        public FlowRunAwaitMode AwaitMode = FlowRunAwaitMode.WaitForCompletion;

        [BoxGroup("Action")]
        [LabelText("Action")]
        [ShowIf("@Mode == TextChannelCommandMode.Single")]
        public TextChannelCommandAction Action = TextChannelCommandAction.SetText;

        [BoxGroup("Action")]
        [LabelText("Apply Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single")]
        public bool ApplyText = true;

        [BoxGroup("Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && (Action == TextChannelCommandAction.SetText || Action == TextChannelCommandAction.Append)")]
        public DynamicValue<string> Text;

        [BoxGroup("Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && (Action == TextChannelCommandAction.SetText || Action == TextChannelCommandAction.Append)")]
        [LabelText("Play Mode")]
        public TextPlayMode PlayMode = TextPlayMode.Instant;

        [BoxGroup("Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && (Action == TextChannelCommandAction.SetText || Action == TextChannelCommandAction.Append)")]
        [LabelText("Style Command")]
        [InlineProperty]
        public TextStyleCommandOptions StyleCommand;

        [BoxGroup("Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && (Action == TextChannelCommandAction.SetText || Action == TextChannelCommandAction.Append) && PlayMode == TextPlayMode.Typewriter")]
        [LabelText("Wait Typewriter Complete")]
        public bool WaitForTypewriterComplete;

        [BoxGroup("Typewriter Events")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && (Action == TextChannelCommandAction.SetText || Action == TextChannelCommandAction.Append) && PlayMode == TextPlayMode.Typewriter")]
        [LabelText("Apply Typewriter Events")]
        public bool ApplyTypewriterEventCommands;

        [BoxGroup("Typewriter Events")]
        [ShowIf(nameof(ShouldShowTypewriterEventCommands))]
        [LabelText("Typewriter Event Commands")]
        [InlineProperty]
        public TypewriterEventCommandMutations TypewriterEventCommands = new();

        [BoxGroup("Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && PlayMode == TextPlayMode.Count")]
        [LabelText("Text Settings")]
        public SetTextSettings TextSettings = SetTextSettings.Default;

        [BoxGroup("Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && PlayMode == TextPlayMode.Count")]
        [LabelText("Override Start Value")]
        public bool OverrideCountStartValue;

        [BoxGroup("Text")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyText && PlayMode == TextPlayMode.Count && OverrideCountStartValue")]
        [LabelText("Start Value")]
        public DynamicValue<float> CountStartValue;

        [BoxGroup("Visibility")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && Action == TextChannelCommandAction.SetVisible")]
        [LabelText("Visible")]
        public bool Visible = true;

        [BoxGroup("Preset")]
        [ShowIf("@Mode == TextChannelCommandMode.Preset")]
        [SerializeReference, HideLabel, InlineProperty]
        public ITextAnimationPreset AnimationPreset = new TextAnimationPreset();

        [BoxGroup("MaterialFx")]
        [LabelText("Apply MaterialFx")]
        public bool ApplyMaterialFx;

        [BoxGroup("MaterialFx")]
        [LabelText("MaterialFx Source")]
        [ShowIf(nameof(ApplyMaterialFx))]
        public DynamicValue<MaterialFxPayload> MaterialFxSource;

        [BoxGroup("Font")]
        [LabelText("Apply Font Size")]
        [ShowIf("@Mode == TextChannelCommandMode.Single")]
        public bool ApplyFontSize;

        [BoxGroup("Font")]
        [LabelText("Font Size")]
        [ShowIf("@Mode == TextChannelCommandMode.Single && ApplyFontSize")]
        public DynamicValue<float> FontSize;

        bool ShouldShowTypewriterEventCommands()
        {
            return Mode == TextChannelCommandMode.Single &&
                   ApplyText &&
                   (Action == TextChannelCommandAction.SetText || Action == TextChannelCommandAction.Append) &&
                   PlayMode == TextPlayMode.Typewriter &&
                   ApplyTypewriterEventCommands;
        }
    }
}
