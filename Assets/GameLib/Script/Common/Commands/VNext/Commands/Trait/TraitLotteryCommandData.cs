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
        public string DebugData => $"Key={HolderKey} Count={DrawCount.GetOrDefaultWithoutContext(1)} Pool={Candidates.Count}+{ConditionalCandidates.Count} Apply={ApplyMode}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(HolderHubSource)")]
        [Tooltip("TraitHolderHubService を持つ対象 LTS。ここから HolderKey を使って TraitHolder を解決する。")]
        public ActorSource HolderHubSource;

        [BoxGroup("Target")]
        [LabelText("Holder Key")]
        [Tooltip("抽選結果を適用する TraitHolder のキー。")]
        public string HolderKey = string.Empty;

        [BoxGroup("Candidates")]
        [LabelText("Traits")]
        [Tooltip("抽選候補にする TraitDefinitionSO 一覧。各 Trait の Weight を使って抽選する。")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ShowIndexLabels = true)]
        public List<TraitDefinitionSO> Candidates = new();

        [BoxGroup("Candidates")]
        [LabelText("Conditional Traits")]
        [Tooltip("Condition が true のときだけ、Traits 内の候補を追加で抽選プールへ足す。アンロックや進行度による開放用。")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, ShowIndexLabels = true)]
        public List<ConditionalTraitLotteryCandidateGroup> ConditionalCandidates = new();

        [BoxGroup("Draw")]
        [LabelText("Count")]
        [Tooltip("抽選する個数。DynamicValue<int> で実行時に変化させられる。")]
        public DynamicValue<int> DrawCount = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Draw")]
        [LabelText("Allow Duplicates")]
        [Tooltip("同じ Trait を複数回選んでよいか。true の場合は同一抽選内でも重複しうる。")]
        public bool AllowDuplicates;

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates")]
        [LabelText("Exclude Holder Traits")]
        [Tooltip("重複禁止時のみ有効。すでに対象 TraitHolder に入っている TraitDefinition とも重複しないように除外する。")]
        public bool ExcludeExistingHolderTraits;

        [BoxGroup("Draw")]
        [ShowIf("@!AllowDuplicates")]
        [LabelText("If Short")]
        [Tooltip("重複禁止や holder 除外の結果、必要数に足りないときの挙動。OutputLess は不足分をそのまま捨てる。AllowDuplicates は残りだけ重複許可で埋める。")]
        [EnumToggleButtons]
        public TraitLotteryShortageMode ShortageMode = TraitLotteryShortageMode.OutputLess;

        [BoxGroup("Apply")]
        [LabelText("Apply Mode")]
        [Tooltip("抽選後に TraitHolder へどう反映するか。Append は追加、Replace は一度全削除してから追加する。")]
        [EnumToggleButtons]
        public TraitLotteryApplyMode ApplyMode = TraitLotteryApplyMode.Append;
    }

    [Serializable]
    public sealed class ConditionalTraitLotteryCandidateGroup
    {
        [LabelText("Condition")]
        [Tooltip("true のときだけ、このグループの Trait を抽選候補へ追加する。")]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(false);

        [LabelText("Traits")]
        [Tooltip("Condition が true のときに追加される Trait 群。各要素は Asset / Var / Blackboard から解決できる。")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, ShowIndexLabels = true)]
        public List<DynamicValue<TraitDefinitionSO>> Traits = new();
    }
}
