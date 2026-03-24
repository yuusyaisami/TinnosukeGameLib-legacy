#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Game.UI.TraitList;
using Sirenix.OdinInspector;
using UnityEngine;

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
        public string DebugData => $"UseBound={UseBoundHolder} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [LabelText("Trait")]
        [Tooltip("追加する Trait。AssetTraitDefinitionSource、Var、Blackboard などから解決する。")]
        public DynamicValue<TraitDefinitionSO> TraitDefinition;
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("TraitHolder を保有する対象 Actor。ここから TraitHolderHubService を解決して HolderKey を参照する。")]
        public ActorSource HolderActorSource;

        [ShowIf("@!UseBoundHolder")]
        public string HolderKey = string.Empty;
    }

    [Serializable]
    public sealed class RemoveTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.RemoveTraitFromHolder;
        public string DebugData => $"Selector={Selector.DebugData} UseBound={UseBoundHolder} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [InlineProperty]
        public TraitElementSelector Selector;
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("TraitHolder を保有する対象 Actor。ここから TraitHolderHubService を解決して HolderKey を参照する。")]
        public ActorSource HolderActorSource;

        [ShowIf("@!UseBoundHolder")]
        public string HolderKey = string.Empty;
    }

    [Serializable]
    public sealed class UseTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.UseTraitFromHolder;
        public string DebugData => $"Selector={Selector.DebugData} UseBound={UseBoundHolder} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [InlineProperty]
        public TraitElementSelector Selector;
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("TraitHolder を保有する対象 Actor。ここから TraitHolderHubService を解決して HolderKey を参照する。")]
        public ActorSource HolderActorSource;

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
        [LabelText("Definition")]
        public DynamicValue<TraitDefinitionSO> Definition;

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
