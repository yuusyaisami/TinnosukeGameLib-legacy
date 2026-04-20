#nullable enable
using System;
using Game.Common;
using Game.Trait;
using Game.UI.TraitList;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("Inspector setting.")]
        public VarUnityObjectSource<UITraitListProfileSO> ProfileSource = new();
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource HolderHubSource;
        [Tooltip("Inspector setting.")]
        public string HolderKey = string.Empty;
        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public UITraitListRange Range;
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(BuildScope)")]
        [Tooltip("Inspector setting.")]
        public ActorSource BuildScope;
    }

    [Serializable]
    public sealed class RefreshUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.RefreshUITraitList;
        public string DebugData => $"Mode={RefreshMode}";

        [Tooltip("Inspector setting.")]
        public UITraitListRefreshMode RefreshMode = UITraitListRefreshMode.Incremental;
    }

    [Serializable]
    public sealed class SetUITraitListRangeCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetUITraitListRange;
        public string DebugData => $"Start={Range.StartIndex} Count={Range.Count} Rebuild={Rebuild}";

        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public UITraitListRange Range;
        [Tooltip("Inspector setting.")]
        public bool Rebuild = true;
    }

    [Serializable]
    public sealed class ClearUITraitListCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearUITraitList;
        public string DebugData => $"KeepBinding={KeepBinding}";

        [Tooltip("Inspector setting.")]
        public bool KeepBinding = false;
    }

    [Serializable]
    public sealed class AddTraitToHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.AddTraitToHolder;
        public string DebugData => $"UseBound={UseBoundHolder} AutoUseAfterAdd={AutoUseAfterAdd} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [LabelText("Trait")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<TraitDefinitionSO> TraitDefinition;
        [Tooltip("Inspector setting.")]
        public bool UseBoundHolder = true;
        [Tooltip("Inspector setting.")]
        public bool AutoUseAfterAdd = false;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource HolderActorSource;

        [ShowIf("@!UseBoundHolder")]
        [Tooltip("Inspector setting.")]
        public string HolderKey = string.Empty;
    }

    [Serializable]
    public sealed class RemoveTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.RemoveTraitFromHolder;
        public string DebugData => $"Selector={Selector.DebugData} UseBound={UseBoundHolder} Holder={TargetHolder.Kind} Key={TargetHolderKey}";

        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public TraitElementSelector Selector;
        [Tooltip("Inspector setting.")]
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [FormerlySerializedAs("TargetHolderActorSource")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetHolder)")]
        [Tooltip("Inspector setting.")]
        public ActorSource TargetHolder;

        [ShowIf("@!UseBoundHolder")]
        [FormerlySerializedAs("HolderKey")]
        [Tooltip("Inspector setting.")]
        public string TargetHolderKey = string.Empty;
    }

    [Serializable]
    public sealed class UseTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.UseTraitFromHolder;
        public string DebugData => $"Selector={Selector.DebugData} UseBound={UseBoundHolder} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [InlineProperty]
        [Tooltip("Inspector setting.")]
        public TraitElementSelector Selector;
        [Tooltip("Inspector setting.")]
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource HolderActorSource;

        [ShowIf("@!UseBoundHolder")]
        [Tooltip("Inspector setting.")]
        public string HolderKey = string.Empty;
    }

    [Serializable]
    public sealed class ClearTraitFromHolderCommandData : ICommandData
    {
        public int CommandId => CommandIds.ClearTraitFromHolder;
        public string DebugData => $"UseBound={UseBoundHolder} RunOnRemove={RunOnRemove} Holder={HolderActorSource.Kind} Key={HolderKey}";

        [Tooltip("Inspector setting.")]
        public bool UseBoundHolder = true;

        [ShowIf("@!UseBoundHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderActorSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource HolderActorSource;

        [ShowIf("@!UseBoundHolder")]
        [Tooltip("Inspector setting.")]
        public string HolderKey = string.Empty;

        [Tooltip("Inspector setting.")]
        public bool RunOnRemove = true;
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
        [Tooltip("Inspector setting.")]
        public UITraitTargetKind Kind;

        [ShowIf("@Kind == UITraitTargetKind.ByDefinition")]
        [LabelText("Definition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<TraitDefinitionSO> Definition;

        [ShowIf("@Kind == UITraitTargetKind.ByInstanceId")]
        [Tooltip("Inspector setting.")]
        public string InstanceId;

        [ShowIf("@Kind == UITraitTargetKind.ByIndex")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<int> TraitIndex;

        [ShowIf("@Kind == UITraitTargetKind.ByRowAndColumn")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<int> Row;

        [ShowIf("@Kind == UITraitTargetKind.ByRowAndColumn")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<int> Column;
    }
}
