#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class WriteTraitDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteTraitData;
        public string DebugData => $"Overwrite={Overwrite} Var={TraitSource.VarId}";

        [BoxGroup("Trait")]
        [LabelText("Trait")]
        public VarUnityObjectSource<TraitDefinitionSO> TraitSource = new();

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        public ActorSource TargetActorSource;

        [BoxGroup("Write")]
        [LabelText("Overwrite")]
        public bool Overwrite = true;
    }
}
