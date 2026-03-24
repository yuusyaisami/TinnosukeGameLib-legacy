#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum LotteryMode
    {
        ActorSource = 10,
        Scalar = 20,
        Blackboard = 30,
        VarStore = 40,
    }

    public enum LotteryShortagePolicy
    {
        ClampToCandidates = 10,
        ContinueWithReplacement = 20,
    }

    public enum LotteryVarOpKind
    {
        None = 0,
        Set = 10,
        Unset = 20,
        Add = 30,
        Mul = 40,
    }

    public enum LotteryScalarOpKind
    {
        None = 0,
        SetLocalBase = 10,
        SetGlobalBase = 20,
        LocalAdd = 30,
        GlobalAdd = 40,
        LocalMul = 50,
        GlobalMul = 60,
        ClearKey = 70,
        ClearAll = 80,
    }

    public enum LotteryBlackboardWriteScope
    {
        Local = 10,
        Global = 20,
    }

    [Serializable]
    public sealed class LotteryActorItem
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [SerializeField]
        public bool Enabled = true;

        [FoldoutGroup("Selected")]
        [HideLabel]
        [CommandListFunctionName("Lottery.Actor.Selected")]
        [SerializeField]
        public CommandListData OnSelectedCommands = new();

        [FoldoutGroup("Unselected")]
        [HideLabel]
        [CommandListFunctionName("Lottery.Actor.Unselected")]
        [SerializeField]
        public CommandListData OnUnselectedCommands = new();
    }

    [Serializable]
    public sealed class LotteryActorEntry
    {
        [Min(0f)]
        [SerializeField]
        public float Weight = 1f;

        [SerializeField]
        public bool Enabled = true;

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryActorItem> Items = new();
    }

    [Serializable]
    public sealed class LotteryScalarItem
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [LabelText("Key")]
        [SerializeField]
        public ScalarKey Key;

        [SerializeField]
        public bool Enabled = true;

        [FoldoutGroup("Selected")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryScalarOpKind SelectedOp = LotteryScalarOpKind.SetLocalBase;

        [FoldoutGroup("Selected")]
        [ShowIf(nameof(NeedsSelectedScalarValue))]
        [SerializeField]
        public DynamicValue<float> SelectedValue;

        [FoldoutGroup("Selected")]
        [ShowIf(nameof(NeedsSelectedLayer))]
        [SerializeField]
        public string SelectedLayer = string.Empty;

        [FoldoutGroup("Selected")]
        [ShowIf(nameof(NeedsSelectedMulPhase))]
        [SerializeField]
        public ScalarMulPhase SelectedMulPhase = ScalarMulPhase.PostAdd;

        [FoldoutGroup("Selected")]
        [ShowIf(nameof(NeedsSelectedDuration))]
        [LabelText("Duration (sec)")]
        [SerializeField]
        public DynamicValue<float> SelectedDurationSeconds;

        [FoldoutGroup("Selected")]
        [ShowIf(nameof(NeedsSelectedDuration))]
        [SerializeField]
        public string SelectedTag = string.Empty;

        [FoldoutGroup("Unselected")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryScalarOpKind UnselectedOp = LotteryScalarOpKind.None;

        [FoldoutGroup("Unselected")]
        [ShowIf(nameof(NeedsUnselectedScalarValue))]
        [SerializeField]
        public DynamicValue<float> UnselectedValue;

        [FoldoutGroup("Unselected")]
        [ShowIf(nameof(NeedsUnselectedLayer))]
        [SerializeField]
        public string UnselectedLayer = string.Empty;

        [FoldoutGroup("Unselected")]
        [ShowIf(nameof(NeedsUnselectedMulPhase))]
        [SerializeField]
        public ScalarMulPhase UnselectedMulPhase = ScalarMulPhase.PostAdd;

        [FoldoutGroup("Unselected")]
        [ShowIf(nameof(NeedsUnselectedDuration))]
        [LabelText("Duration (sec)")]
        [SerializeField]
        public DynamicValue<float> UnselectedDurationSeconds;

        [FoldoutGroup("Unselected")]
        [ShowIf(nameof(NeedsUnselectedDuration))]
        [SerializeField]
        public string UnselectedTag = string.Empty;

        bool NeedsSelectedScalarValue() => NeedsScalarValue(SelectedOp);
        bool NeedsSelectedLayer() => NeedsScalarLayer(SelectedOp);
        bool NeedsSelectedMulPhase() => NeedsScalarMulPhase(SelectedOp);
        bool NeedsSelectedDuration() => NeedsScalarDuration(SelectedOp);

        bool NeedsUnselectedScalarValue() => NeedsScalarValue(UnselectedOp);
        bool NeedsUnselectedLayer() => NeedsScalarLayer(UnselectedOp);
        bool NeedsUnselectedMulPhase() => NeedsScalarMulPhase(UnselectedOp);
        bool NeedsUnselectedDuration() => NeedsScalarDuration(UnselectedOp);

        static bool NeedsScalarValue(LotteryScalarOpKind op)
        {
            switch (op)
            {
                case LotteryScalarOpKind.SetLocalBase:
                case LotteryScalarOpKind.SetGlobalBase:
                case LotteryScalarOpKind.LocalAdd:
                case LotteryScalarOpKind.GlobalAdd:
                case LotteryScalarOpKind.LocalMul:
                case LotteryScalarOpKind.GlobalMul:
                    return true;
                default:
                    return false;
            }
        }

        static bool NeedsScalarLayer(LotteryScalarOpKind op)
        {
            switch (op)
            {
                case LotteryScalarOpKind.LocalAdd:
                case LotteryScalarOpKind.GlobalAdd:
                case LotteryScalarOpKind.LocalMul:
                case LotteryScalarOpKind.GlobalMul:
                    return true;
                default:
                    return false;
            }
        }

        static bool NeedsScalarMulPhase(LotteryScalarOpKind op)
        {
            switch (op)
            {
                case LotteryScalarOpKind.LocalMul:
                case LotteryScalarOpKind.GlobalMul:
                    return true;
                default:
                    return false;
            }
        }

        static bool NeedsScalarDuration(LotteryScalarOpKind op)
        {
            switch (op)
            {
                case LotteryScalarOpKind.LocalAdd:
                case LotteryScalarOpKind.GlobalAdd:
                case LotteryScalarOpKind.LocalMul:
                case LotteryScalarOpKind.GlobalMul:
                    return true;
                default:
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class LotteryScalarEntry
    {
        [Min(0f)]
        [SerializeField]
        public float Weight = 1f;

        [SerializeField]
        public bool Enabled = true;

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryScalarItem> Items = new();
    }

    [Serializable]
    public sealed class LotteryBlackboardItem
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [EnumToggleButtons]
        [SerializeField]
        public LotteryBlackboardWriteScope WriteScope = LotteryBlackboardWriteScope.Local;

        [LabelText("Key")]
        [SerializeField]
        public VarKeyRef Key;

        [SerializeField]
        public bool Enabled = true;

        [FoldoutGroup("Selected")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryVarOpKind SelectedOp = LotteryVarOpKind.Set;

        [FoldoutGroup("Selected")]
        [ShowIf(nameof(NeedsSelectedValue))]
        [SerializeField]
        public DynamicValue SelectedValue;

        [FoldoutGroup("Unselected")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryVarOpKind UnselectedOp = LotteryVarOpKind.None;

        [FoldoutGroup("Unselected")]
        [ShowIf(nameof(NeedsUnselectedValue))]
        [SerializeField]
        public DynamicValue UnselectedValue;

        bool NeedsSelectedValue() => SelectedOp == LotteryVarOpKind.Set || SelectedOp == LotteryVarOpKind.Add || SelectedOp == LotteryVarOpKind.Mul;
        bool NeedsUnselectedValue() => UnselectedOp == LotteryVarOpKind.Set || UnselectedOp == LotteryVarOpKind.Add || UnselectedOp == LotteryVarOpKind.Mul;
    }

    [Serializable]
    public sealed class LotteryBlackboardEntry
    {
        [Min(0f)]
        [SerializeField]
        public float Weight = 1f;

        [SerializeField]
        public bool Enabled = true;

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryBlackboardItem> Items = new();
    }

    [Serializable]
    public sealed class LotteryVarStoreItem
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [LabelText("Key")]
        [SerializeField]
        public VarKeyRef Key;

        [SerializeField]
        public bool Enabled = true;

        [FoldoutGroup("Selected")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryVarOpKind SelectedOp = LotteryVarOpKind.Set;

        [FoldoutGroup("Selected")]
        [ShowIf(nameof(NeedsSelectedValue))]
        [SerializeField]
        public DynamicValue SelectedValue;

        [FoldoutGroup("Unselected")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryVarOpKind UnselectedOp = LotteryVarOpKind.None;

        [FoldoutGroup("Unselected")]
        [ShowIf(nameof(NeedsUnselectedValue))]
        [SerializeField]
        public DynamicValue UnselectedValue;

        bool NeedsSelectedValue() => SelectedOp == LotteryVarOpKind.Set || SelectedOp == LotteryVarOpKind.Add || SelectedOp == LotteryVarOpKind.Mul;
        bool NeedsUnselectedValue() => UnselectedOp == LotteryVarOpKind.Set || UnselectedOp == LotteryVarOpKind.Add || UnselectedOp == LotteryVarOpKind.Mul;
    }

    [Serializable]
    public sealed class LotteryVarStoreEntry
    {
        [Min(0f)]
        [SerializeField]
        public float Weight = 1f;

        [SerializeField]
        public bool Enabled = true;

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryVarStoreItem> Items = new();
    }

    [Serializable]
    public sealed class LotteryCommandData : ICommandData
    {
        public int CommandId => CommandIds.Lottery;
        public string DebugData
        {
            get
            {
                var modeCount = Mode switch
                {
                    LotteryMode.ActorSource => ActorEntries?.Count ?? 0,
                    LotteryMode.Scalar => ScalarEntries?.Count ?? 0,
                    LotteryMode.Blackboard => BlackboardEntries?.Count ?? 0,
                    LotteryMode.VarStore => VarStoreEntries?.Count ?? 0,
                    _ => 0,
                };
                return $"Mode={Mode} Count={DrawCount.GetOrDefault(default, 0)} Entries={modeCount}";
            }
        }

        [BoxGroup("Settings")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryMode Mode = LotteryMode.ActorSource;

        [BoxGroup("Settings")]
        [LabelText("Draw Count")]
        [SerializeField]
        public DynamicValue<int> DrawCount = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Settings")]
        [LabelText("With Replacement")]
        [SerializeField]
        public bool WithReplacement;

        [BoxGroup("Settings")]
        [EnumToggleButtons]
        [SerializeField]
        public LotteryShortagePolicy ShortagePolicy = LotteryShortagePolicy.ContinueWithReplacement;

        [FoldoutGroup("Actor Entries")]
        [ShowIf("@Mode == LotteryMode.ActorSource")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryActorEntry> ActorEntries = new();

        [FoldoutGroup("Scalar Entries")]
        [ShowIf("@Mode == LotteryMode.Scalar")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryScalarEntry> ScalarEntries = new();

        [FoldoutGroup("Blackboard Entries")]
        [ShowIf("@Mode == LotteryMode.Blackboard")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryBlackboardEntry> BlackboardEntries = new();

        [FoldoutGroup("VarStore Entries")]
        [ShowIf("@Mode == LotteryMode.VarStore")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        public List<LotteryVarStoreEntry> VarStoreEntries = new();
    }
}
