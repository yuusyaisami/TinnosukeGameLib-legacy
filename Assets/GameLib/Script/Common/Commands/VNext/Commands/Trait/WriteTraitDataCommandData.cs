#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum WriteTraitDataSourceMode
    {
        DirectDefinition = 10,
        HolderSelector = 20,
        VarStoreTraitData = 30,
    }

    [Serializable]
    public sealed class WriteTraitDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteTraitData;
        public string DebugData => $"Mode={SourceMode} Overwrite={Overwrite}";

        [BoxGroup("Source")]
        [EnumToggleButtons]
        [LabelText("Source Mode")]
        public WriteTraitDataSourceMode SourceMode = WriteTraitDataSourceMode.DirectDefinition;

        [BoxGroup("Source Direct Definition")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteTraitDataSourceMode.DirectDefinition")]
        [LabelText("Trait")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<TraitDefinitionSO> TraitSource;

        [BoxGroup("Source Holder Selector")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteTraitDataSourceMode.HolderSelector")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HubActorSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource HubActorSource;

        [BoxGroup("Source Holder Selector")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteTraitDataSourceMode.HolderSelector")]
        [LabelText("Holder Key")]
        public string HolderKey = string.Empty;

        [BoxGroup("Source Holder Selector")]
        [ShowIf("@SourceMode == Game.Commands.VNext.WriteTraitDataSourceMode.HolderSelector")]
        [LabelText("Selector")]
        public TraitElementSelector Selector;

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        public ActorSource TargetActorSource;

        [BoxGroup("Write")]
        [LabelText("Overwrite")]
        public bool Overwrite = true;
    }
}
