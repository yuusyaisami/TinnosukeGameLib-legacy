#nullable enable
using System;
using Game.Channel;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class Light2DChannelHubControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.Light2DChannelHubControl;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [BoxGroup("Operation")]
        [LabelText("Operation")]
        public Light2DChannelHubControlOperation Operation = Light2DChannelHubControlOperation.SwapSourcePreset;

        [BoxGroup("Preset")]
        [ShowIf(nameof(UsesSourcePreset))]
        [LabelText("Source Preset")]
        public DynamicValue<Light2DPreset> SourcePreset =
            DynamicValue<Light2DPreset>.FromSource(
                new ManagedRefLiteralSource<Light2DPreset>(new Light2DPreset()));

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();

        bool UsesSourcePreset => Operation == Light2DChannelHubControlOperation.SwapSourcePreset;
    }

    [Serializable]
    public sealed class Light2DChannelPlayerControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.Light2DChannelPlayerControl;
        public string DebugData => $"Target={Target.Kind} Tag={NormalizedChannelTag} Op={Operation} EffectId={NormalizedEffectId}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        public string ChannelTag = "default";

        [BoxGroup("Operation")]
        [LabelText("Operation")]
        public Light2DChannelPlayerControlOperation Operation = Light2DChannelPlayerControlOperation.MutatePlayerPreset;

        [BoxGroup("Player Preset")]
        [ShowIf(nameof(UsesPlayerPresetSwap))]
        [LabelText("Player Preset")]
        public DynamicValue<Light2DPlayerPreset> PlayerPreset =
            DynamicValue<Light2DPlayerPreset>.FromSource(
                new ManagedRefLiteralSource<Light2DPlayerPreset>(new Light2DPlayerPreset()));

        [BoxGroup("Player Mutation")]
        [ShowIf(nameof(UsesPlayerMutation))]
        [InlineProperty]
        [HideLabel]
        public Light2DPlayerRuntimeMutation PlayerMutation = new();

        [BoxGroup("Global")]
        [ShowIf(nameof(UsesGlobalIntensity))]
        [LabelText("Global Intensity")]
        public DynamicValue<float> GlobalIntensity = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Effect")]
        [ShowIf(nameof(UsesEffectId))]
        [LabelText("Effect Id")]
        public string EffectId = "default";

        [BoxGroup("Effect")]
        [ShowIf(nameof(UsesEffectPreset))]
        [LabelText("Effect Preset")]
        public DynamicValue<Light2DEffectPresetBase> EffectPreset =
            DynamicValue<Light2DEffectPresetBase>.FromSource(
                new ManagedRefLiteralSource<Light2DEffectPresetBase>(new Light2DIntensityFlickerEffectPreset()));

        [BoxGroup("Effect")]
        [ShowIf(nameof(UsesEffectPreset))]
        [LabelText("Effect Priority")]
        public int EffectPriority;

        [BoxGroup("Effect")]
        [ShowIf(nameof(UsesEffectPreset))]
        [LabelText("Effect Blend Mode")]
        public Light2DEffectBlendMode EffectBlendMode = Light2DEffectBlendMode.Override;

        [BoxGroup("Effect")]
        [ShowIf(nameof(UsesEffectPreset))]
        [LabelText("Effect Enabled")]
        public bool EffectEnabled = true;

        [BoxGroup("Effect Mutation")]
        [ShowIf(nameof(UsesEffectMutation))]
        [SerializeReference]
        [InlineProperty]
        [HideLabel]
        public Light2DEffectRuntimeMutationBase EffectMutation = new Light2DIntensityFlickerEffectMutation();

        [BoxGroup("Effect Toggle")]
        [ShowIf(nameof(UsesEffectEnabled))]
        [LabelText("Enabled")]
        public bool SetEffectEnabled = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesResetRuntimeOverrides))]
        [LabelText("Reset Player Preset")]
        public bool ResetPlayerPreset = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesResetRuntimeOverrides))]
        [LabelText("Reset Effects")]
        public bool ResetEffects = true;

        [BoxGroup("Reset")]
        [ShowIf(nameof(UsesResetRuntimeOverrides))]
        [LabelText("Reset Global Intensity")]
        public bool ResetGlobalIntensity = true;

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();
        public string NormalizedEffectId => string.IsNullOrWhiteSpace(EffectId) ? "default" : EffectId.Trim();

        bool UsesPlayerPresetSwap => Operation == Light2DChannelPlayerControlOperation.SwapPlayerPreset;
        bool UsesPlayerMutation => Operation == Light2DChannelPlayerControlOperation.MutatePlayerPreset;
        bool UsesGlobalIntensity =>
            Operation == Light2DChannelPlayerControlOperation.SetGlobalIntensity;
        bool UsesEffectId =>
            Operation == Light2DChannelPlayerControlOperation.ReplaceEffect ||
            Operation == Light2DChannelPlayerControlOperation.MutateEffect ||
            Operation == Light2DChannelPlayerControlOperation.SetEffectEnabled ||
            Operation == Light2DChannelPlayerControlOperation.RemoveEffect;
        bool UsesEffectPreset => Operation == Light2DChannelPlayerControlOperation.ReplaceEffect;
        bool UsesEffectMutation => Operation == Light2DChannelPlayerControlOperation.MutateEffect;
        bool UsesEffectEnabled => Operation == Light2DChannelPlayerControlOperation.SetEffectEnabled;
        bool UsesResetRuntimeOverrides => Operation == Light2DChannelPlayerControlOperation.ResetRuntimeOverrides;
    }
}
