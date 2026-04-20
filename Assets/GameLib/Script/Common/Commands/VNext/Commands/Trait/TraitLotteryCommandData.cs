#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Trait;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class TraitLotteryCommandData : ICommandData
    {
        public int CommandId => CommandIds.TraitLottery;
        public string DebugData =>
            $"Key={HolderKey} Count={DrawCount.GetOrDefaultWithoutContext(1)} Pool={Candidates.Count}+{ConditionalCandidates.Count} Apply={ApplyMode} ExcludeHolder={ExcludeExistingHolderTraits} DupCheckOverride={UseDuplicateCheckHolder} DupExceptions={DuplicateAllowedTraits.Count}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource HolderHubSource;

        [BoxGroup("Target")]
        [LabelText("Holder Key")]
        [Tooltip("Inspector setting.")]
        public string HolderKey = string.Empty;

        [BoxGroup("Candidates")]
        [LabelText("Traits")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ShowIndexLabels = true)]
        public List<TraitDefinitionSO> Candidates = new();

        [BoxGroup("Candidates")]
        [LabelText("Conditional Traits")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, ShowIndexLabels = true)]
        public List<ConditionalTraitLotteryCandidateGroup> ConditionalCandidates = new();

        [BoxGroup("Draw")]
        [LabelText("Count")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<int> DrawCount = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Draw")]
        [LabelText("Allow Duplicates")]
        [Tooltip("Inspector setting.")]
        public bool AllowDuplicates;

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates")]
        [LabelText("Exclude Holder Traits")]
        [Tooltip("Inspector setting.")]
        public bool ExcludeExistingHolderTraits;

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates && ExcludeExistingHolderTraits")]
        [LabelText("Use Duplicate Check Holder")]
        [Tooltip("Inspector setting.")]
        public bool UseDuplicateCheckHolder;

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates && ExcludeExistingHolderTraits && UseDuplicateCheckHolder")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(DuplicateCheckHolderHubSource)")]
        [Tooltip("Inspector setting.")]
        public ActorSource DuplicateCheckHolderHubSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates && ExcludeExistingHolderTraits && UseDuplicateCheckHolder")]
        [LabelText("Duplicate Check Holder Key")]
        [Tooltip("Inspector setting.")]
        public string DuplicateCheckHolderKey = string.Empty;

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates && ExcludeExistingHolderTraits")]
        [LabelText("Duplicate Exception Traits")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, ShowIndexLabels = true)]
        public List<TraitDefinitionSO> DuplicateAllowedTraits = new();

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates")]
        [LabelText("If Short")]
        [Tooltip("Inspector setting.")]
        [EnumToggleButtons]
        public TraitLotteryShortageMode ShortageMode = TraitLotteryShortageMode.OutputLess;

        [BoxGroup("Apply")]
        [LabelText("Apply Mode")]
        [Tooltip("Inspector setting.")]
        [EnumToggleButtons]
        public TraitLotteryApplyMode ApplyMode = TraitLotteryApplyMode.Append;
    }

    [Serializable]
    public sealed class ConditionalTraitLotteryCandidateGroup
    {
        [LabelText("Condition")]
        [Tooltip("Inspector setting.")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(false);

        [LabelText("Traits")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ShowIndexLabels = true)]
        public List<DynamicValue<TraitDefinitionSO>> Traits = new();
    }
}
