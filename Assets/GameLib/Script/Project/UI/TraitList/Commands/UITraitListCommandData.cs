#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Game.UI.TraitList;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class BuildUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.BuildUITraitList;
        public string DebugData
        {
            get
            {
                var assetName = ProfileSource.Asset != null ? ProfileSource.Asset.name : "null";
                if (ProfileSource.PreferVar && ProfileSource.VarId != 0)
                    return $"VarId={ProfileSource.VarId} Asset={assetName} Key={HolderKey}";
                return $"Asset={assetName} Key={HolderKey}";
            }
        }

        public VarUnityObjectSource<UITraitListProfileSO> ProfileSource = new();
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        public ActorSource HolderHubSource;
        public string HolderKey = string.Empty;
        [InlineProperty]
        public UITraitListRange Range;
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(BuildScope)")]
        public ActorSource BuildScope;
    }

    [Serializable]
    public sealed class RefreshUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshUITraitList;
        public string DebugData => $"Mode={RefreshMode}";

        public UITraitListRefreshMode RefreshMode = UITraitListRefreshMode.Incremental;
    }

    [Serializable]
    public sealed class SetUITraitListRangeCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetUITraitListRange;
        public string DebugData => $"Start={Range.StartIndex} Count={Range.Count} Rebuild={Rebuild}";

        [InlineProperty]
        public UITraitListRange Range;
        public bool Rebuild = true;
    }

    [Serializable]
    public sealed class ClearUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearUITraitList;
        public string DebugData => $"KeepBinding={KeepBinding}";

        public bool KeepBinding = false;
    }

    [Serializable]
    public sealed class AddTraitToHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.AddTraitToHolder;
        public string DebugData => $"UseBound={UseBoundHolder} Key={HolderKey}";

        public VarUnityObjectSource<TraitDefinitionSO> TraitDefinitionSource = new();
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        public ActorSource HolderHubSource;

        [ShowIf("@!UseBoundHolder")]
        public string HolderKey = string.Empty;
    }

    [Serializable]
    public sealed class RemoveTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.RemoveTraitFromHolder;
        public string DebugData => $"Target={Target.Kind} UseBound={UseBoundHolder} Key={HolderKey}";

        [InlineProperty]
        public UITraitTarget Target;
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        public ActorSource HolderHubSource;

        [ShowIf("@!UseBoundHolder")]
        public string HolderKey = string.Empty;
    }

    [Serializable]
    public sealed class UseTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.UseTraitFromHolder;
        public string DebugData => $"Target={Target.Kind} UseBound={UseBoundHolder} Key={HolderKey}";

        [InlineProperty]
        public UITraitTarget Target;
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        public ActorSource HolderHubSource;

        [ShowIf("@!UseBoundHolder")]
        public string HolderKey = string.Empty;
    }

    public enum UITraitTargetKind
    {
        ByDefinition = 0,
        ByInstanceId = 1,
        ByIndex = 2,
        ByRowAndColumn = 3,
    }

    [Serializable]
    public struct UITraitTarget
    {
        [EnumToggleButtons]
        public UITraitTargetKind Kind;

        [ShowIf("@Kind == UITraitTargetKind.ByDefinition")]
        public VarUnityObjectSource<TraitDefinitionSO> DefinitionSource;

        [ShowIf("@Kind == UITraitTargetKind.ByInstanceId")]
        public string InstanceId;

        [ShowIf("@Kind == UITraitTargetKind.ByIndex")]
        public DynamicValue<int> TraitIndex;

        [ShowIf("@Kind == UITraitTargetKind.ByRowAndColumn")]
        public DynamicValue<int> Row;

        [ShowIf("@Kind == UITraitTargetKind.ByRowAndColumn")]
        public DynamicValue<int> Column;
    }
}
