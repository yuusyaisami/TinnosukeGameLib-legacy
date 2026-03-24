#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class WriteTraitDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteTraitData;
        public string DebugData => $"Overwrite={Overwrite} Trait={TraitSource.SourceDebugData}";

        [BoxGroup("Trait")]
        [LabelText("Trait")]
        [Tooltip("書き出す Trait。AssetTraitDefinitionSource、Var、Blackboard などから解決する。")]
        public DynamicValue<TraitDefinitionSO> TraitSource;

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        public ActorSource TargetActorSource;

        [BoxGroup("Write")]
        [LabelText("Overwrite")]
        public bool Overwrite = true;
    }
}
