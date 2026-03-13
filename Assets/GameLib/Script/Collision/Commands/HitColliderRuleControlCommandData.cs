#nullable enable
using System;
using Game.Collision;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum HitColliderRuleSelectMode
    {
        ByName = 0,
        ByIndex = 1,
        All = 2,
    }

    public enum HitColliderRuleCommandListOperation
    {
        None = 0,
        Append = 1,
        Override = 2,
        ClearOverride = 3,
        ClearAppended = 4,
        ClearRuntimeMutations = 5,
    }

    [Serializable]
    public sealed class HitColliderRuleControlCommandData : ICommandData
    {
        public int CommandId => CommandIds.HitColliderRuleControl;
        public string DebugData => $"Target={Target.Kind} Select={SelectMode} Op={CommandListOperation}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        public ActorSource Target;

        [BoxGroup("Rule Select")]
        [LabelText("Select Mode")]
        [SerializeField]
        public HitColliderRuleSelectMode SelectMode = HitColliderRuleSelectMode.ByName;

        [BoxGroup("Rule Select")]
        [ShowIf(nameof(IsByName))]
        [LabelText("Rule Name")]
        [SerializeField]
        public string RuleName = string.Empty;

        [BoxGroup("Rule Select")]
        [ShowIf(nameof(IsByIndex))]
        [LabelText("Rule Index")]
        [SerializeField]
        public DynamicValue<int> RuleIndex = DynamicValueExtensions.FromLiteral(0);

        [BoxGroup("Rule Settings")]
        [LabelText("Apply Enabled")]
        [SerializeField]
        public bool ApplyEnabled;

        [BoxGroup("Rule Settings")]
        [ShowIf(nameof(ApplyEnabled))]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> Enabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Rule Settings")]
        [LabelText("Apply Watch Flags")]
        [SerializeField]
        public bool ApplyWatchFlags;

        [BoxGroup("Rule Settings")]
        [ShowIf(nameof(ApplyWatchFlags))]
        [LabelText("Watch Flags")]
        [SerializeField]
        public HitWatchFlags WatchFlags = HitWatchFlags.SelfAndOther;

        [BoxGroup("Rule Settings")]
        [LabelText("Apply Event Mask")]
        [SerializeField]
        public bool ApplyEventMask;

        [BoxGroup("Rule Settings")]
        [ShowIf(nameof(ApplyEventMask))]
        [LabelText("Event Mask")]
        [SerializeField]
        public HitEventFlags EventMask = HitEventFlags.All;

        [BoxGroup("Rule Settings")]
        [LabelText("Apply Match Any Include")]
        [SerializeField]
        public bool ApplyMatchAnyInclude;

        [BoxGroup("Rule Settings")]
        [ShowIf(nameof(ApplyMatchAnyInclude))]
        [LabelText("Match Any Include")]
        [SerializeField]
        public DynamicValue<bool> MatchAnyInclude = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Command Runtime")]
        [LabelText("Apply Command Target")]
        [SerializeField]
        public bool ApplyCommandTarget;

        [BoxGroup("Command Runtime")]
        [ShowIf(nameof(ApplyCommandTarget))]
        [LabelText("Command Target")]
        [SerializeField]
        public HitColliderCommandTarget CommandTarget = HitColliderCommandTarget.Self;

        [BoxGroup("Command Runtime")]
        [LabelText("Apply Parallel When Both")]
        [SerializeField]
        public bool ApplyParallelWhenBoth;

        [BoxGroup("Command Runtime")]
        [ShowIf(nameof(ApplyParallelWhenBoth))]
        [LabelText("Parallel When Both")]
        [SerializeField]
        public DynamicValue<bool> ParallelWhenBoth = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Command Runtime")]
        [LabelText("Apply Stay Interval")]
        [SerializeField]
        public bool ApplyStayInterval;

        [BoxGroup("Command Runtime")]
        [ShowIf(nameof(ApplyStayInterval))]
        [LabelText("Stay Interval Seconds")]
        [SerializeField]
        public DynamicValue<float> StayIntervalSeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Filter")]
        [LabelText("Apply Use Filter")]
        [SerializeField]
        public bool ApplyUseFilter;

        [BoxGroup("Filter")]
        [ShowIf(nameof(ApplyUseFilter))]
        [LabelText("Use Filter")]
        [SerializeField]
        public DynamicValue<bool> UseFilter = DynamicValueExtensions.FromLiteral(false);

        [BoxGroup("Filter")]
        [LabelText("Apply Filter Value")]
        [SerializeField]
        public bool ApplyFilterValue;

        [BoxGroup("Filter")]
        [ShowIf(nameof(ApplyFilterValue))]
        [LabelText("Filter")]
        [SerializeField, InlineProperty]
        public HitFilter Filter;

        [BoxGroup("Stale")]
        [LabelText("Apply Use Stale Threshold")]
        [SerializeField]
        public bool ApplyUseStaleFrameThreshold;

        [BoxGroup("Stale")]
        [ShowIf(nameof(ApplyUseStaleFrameThreshold))]
        [LabelText("Use Stale Threshold")]
        [SerializeField]
        public DynamicValue<bool> UseStaleFrameThreshold = DynamicValueExtensions.FromLiteral(false);

        [BoxGroup("Stale")]
        [LabelText("Apply Stale Threshold Value")]
        [SerializeField]
        public bool ApplyStaleFrameThreshold;

        [BoxGroup("Stale")]
        [ShowIf(nameof(ApplyStaleFrameThreshold))]
        [LabelText("Stale Threshold Frames")]
        [SerializeField]
        public DynamicValue<int> StaleFrameThreshold = DynamicValueExtensions.FromLiteral(2);

        [BoxGroup("Include/Exclude")]
        [LabelText("Apply Include Static Kinds")]
        [SerializeField]
        public bool ApplyIncludeStaticKinds;

        [BoxGroup("Include/Exclude")]
        [ShowIf(nameof(ApplyIncludeStaticKinds))]
        [LabelText("Use Include Static Kinds")]
        [SerializeField]
        public bool UseIncludeStaticKinds;

        [BoxGroup("Include/Exclude")]
        [ShowIf("@ApplyIncludeStaticKinds && UseIncludeStaticKinds")]
        [LabelText("Include Static Kinds")]
        [SerializeField]
        public StaticColliderKind[] IncludeStaticKinds = Array.Empty<StaticColliderKind>();

        [BoxGroup("Include/Exclude")]
        [LabelText("Apply Include Dynamic Sets")]
        [SerializeField]
        public bool ApplyIncludeDynamicSets;

        [BoxGroup("Include/Exclude")]
        [ShowIf(nameof(ApplyIncludeDynamicSets))]
        [LabelText("Use Include Dynamic Sets")]
        [SerializeField]
        public bool UseIncludeDynamicSets;

        [BoxGroup("Include/Exclude")]
        [ShowIf("@ApplyIncludeDynamicSets && UseIncludeDynamicSets")]
        [LabelText("Include Dynamic Sets")]
        [SerializeField]
        public DynamicColliderSetId[] IncludeDynamicSets = Array.Empty<DynamicColliderSetId>();

        [BoxGroup("Include/Exclude")]
        [LabelText("Apply Exclude Static Kinds")]
        [SerializeField]
        public bool ApplyExcludeStaticKinds;

        [BoxGroup("Include/Exclude")]
        [ShowIf(nameof(ApplyExcludeStaticKinds))]
        [LabelText("Use Exclude Static Kinds")]
        [SerializeField]
        public bool UseExcludeStaticKinds;

        [BoxGroup("Include/Exclude")]
        [ShowIf("@ApplyExcludeStaticKinds && UseExcludeStaticKinds")]
        [LabelText("Exclude Static Kinds")]
        [SerializeField]
        public StaticColliderKind[] ExcludeStaticKinds = Array.Empty<StaticColliderKind>();

        [BoxGroup("Include/Exclude")]
        [LabelText("Apply Exclude Dynamic Sets")]
        [SerializeField]
        public bool ApplyExcludeDynamicSets;

        [BoxGroup("Include/Exclude")]
        [ShowIf(nameof(ApplyExcludeDynamicSets))]
        [LabelText("Use Exclude Dynamic Sets")]
        [SerializeField]
        public bool UseExcludeDynamicSets;

        [BoxGroup("Include/Exclude")]
        [ShowIf("@ApplyExcludeDynamicSets && UseExcludeDynamicSets")]
        [LabelText("Exclude Dynamic Sets")]
        [SerializeField]
        public DynamicColliderSetId[] ExcludeDynamicSets = Array.Empty<DynamicColliderSetId>();

        [BoxGroup("Command List")]
        [LabelText("Operation")]
        [SerializeField]
        public HitColliderRuleCommandListOperation CommandListOperation = HitColliderRuleCommandListOperation.None;

        [BoxGroup("Command List")]
        [ShowIf(nameof(HasCommandListSlot))]
        [LabelText("Slot")]
        [SerializeField]
        public HitColliderRuleCommandListSlot CommandListSlot = HitColliderRuleCommandListSlot.OnEnterSelf;

        [BoxGroup("Command List")]
        [ShowIf(nameof(NeedsCommandPayload))]
        [LabelText("Commands")]
        [SerializeField]
        [CommandListFunctionName("HitCollider.RuleControl.Commands")]
        public CommandListData Commands = new();

        bool IsByName => SelectMode == HitColliderRuleSelectMode.ByName;
        bool IsByIndex => SelectMode == HitColliderRuleSelectMode.ByIndex;

        bool HasCommandListSlot => CommandListOperation == HitColliderRuleCommandListOperation.Append
                                   || CommandListOperation == HitColliderRuleCommandListOperation.Override
                                   || CommandListOperation == HitColliderRuleCommandListOperation.ClearOverride
                                   || CommandListOperation == HitColliderRuleCommandListOperation.ClearAppended
                                   || CommandListOperation == HitColliderRuleCommandListOperation.ClearRuntimeMutations;

        bool NeedsCommandPayload => CommandListOperation == HitColliderRuleCommandListOperation.Append
                                    || CommandListOperation == HitColliderRuleCommandListOperation.Override;
    }
}
