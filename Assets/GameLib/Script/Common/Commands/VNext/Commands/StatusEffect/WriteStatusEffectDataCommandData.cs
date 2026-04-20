#nullable enable

using System;
using Game.Common;
using Game.StatusEffect;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum WriteStatusEffectDataSourceMode
    {
        Definition = 10,
        Runtime = 20,
        StackPreset = 30,
    }

    [Serializable]
    public sealed class WriteStatusEffectDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteStatusEffectData;
        public string DebugData => $"Mode={SourceMode} Target={Target} Overwrite={Overwrite}";

        [BoxGroup("Source")]
        [EnumToggleButtons]
        [LabelText("Source Mode")]
        public WriteStatusEffectDataSourceMode SourceMode = WriteStatusEffectDataSourceMode.Definition;

        [BoxGroup("Source Definition")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteStatusEffectDataSourceMode.Definition")]
        [LabelText("Definition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<BaseStatusEffectDefinitionData> DefinitionSource;

        [BoxGroup("Source Runtime")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteStatusEffectDataSourceMode.Runtime")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(ServiceActorSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource ServiceActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Source Runtime")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteStatusEffectDataSourceMode.Runtime")]
        [LabelText("Filter")]
        public StatusEffectRuntimeFilter Filter = StatusEffectRuntimeFilter.All;

        [BoxGroup("Source StackPreset")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteStatusEffectDataSourceMode.StackPreset")]
        [LabelText("Stack Preset")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<StatusEffectStackPreset> StackPresetSource;

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        public ActorSource TargetActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Write")]
        [LabelText("Target")]
        public VarStoreTarget Target = VarStoreTarget.CommandVars;

        [BoxGroup("Write")]
        [LabelText("Overwrite")]
        public bool Overwrite = true;
    }
}
