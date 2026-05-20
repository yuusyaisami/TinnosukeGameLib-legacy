#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum ForLoopMode
    {
        Count = 0,
        While = 1,
        Until = 2,
        Random = 3,
    }

    public enum SwitchEvaluateOrder
    {
        TopToBottom = 10,
        BottomToTop = 20,
    }

    public enum SwitchCaseMatchMode
    {
        Exact = 10,
        Compare = 20,
        Condition = 30,
    }

    public enum SwitchNumericCompareOp
    {
        [InspectorName("S == T")]
        Equal = 10,
        [InspectorName("S != T")]
        NotEqual = 20,
        [InspectorName("S < T")]
        LessThan = 30,
        [InspectorName("S <= T")]
        LessOrEqual = 40,
        [InspectorName("S > T")]
        GreaterThan = 50,
        [InspectorName("S >= T")]
        GreaterOrEqual = 60,
    }

    [Serializable]
    public sealed class WaitCommandData : ICommandData
    {
        public int CommandId => CommandIds.Wait;
        public string DebugData
        {
            get
            {
                var wait = CommandDebugDataHelper.GetDynamicDebugData(WaitTime);
                return $"Wait={wait}";
            }
        }

        [LabelText("Wait Time")]
        [SerializeField]
        public DynamicValue<float> WaitTime;
    }

    [Serializable]
    public sealed class BreakCommandData : ICommandData
    {
        public int CommandId => CommandIds.Break;
        public string DebugData => "Break";
    }

    [Serializable]
    public sealed class CancelCommandData : ICommandData
    {
        public int CommandId => CommandIds.Cancel;
        public string DebugData => "Cancel";
    }

    [Serializable]
    public sealed class IfCommandData : ICommandData
    {
        public int CommandId => CommandIds.If;
        public string DebugData
        {
            get
            {
                var condition = CommandDebugDataHelper.GetDynamicDebugData(Condition);
                var thenCount = ThenCommands?.Count ?? 0;
                var elseCount = ElseCommands?.Count ?? 0;
                return $"Cond={condition} Then={thenCount} Else={elseCount}";
            }
        }

        [BoxGroup("Condition")]
        [HideLabel]
        [SerializeField]
        public DynamicValue<bool> Condition;

        [FoldoutGroup("Then")]
        [HideLabel]
        [CommandListFunctionName("Control.If.Then")]
        [SerializeField]
        public CommandListData ThenCommands = new();

        [FoldoutGroup("Else")]
        [HideLabel]
        [CommandListFunctionName("Control.If.Else")]
        [SerializeField]
        public CommandListData ElseCommands = new();

        [FoldoutGroup("OnCanceled")]
        [HideLabel]
        [CommandListFunctionName("Control.If.OnCanceled")]
        [SerializeField]
        public CommandListData OnCanceledCommands = new();
    }

    [Serializable]
    public sealed class SwitchCase
    {
        public string ListLabel
        {
            get
            {
                var caseValue = MatchMode switch
                {
                    SwitchCaseMatchMode.Exact => CommandDebugDataHelper.GetDynamicDebugData(CaseValue),
                    SwitchCaseMatchMode.Compare => $"{CompareOp} {CommandDebugDataHelper.GetDynamicDebugData(CompareTarget)}",
                    SwitchCaseMatchMode.Condition => CommandDebugDataHelper.GetDynamicDebugData(Condition),
                    _ => "<unknown>",
                };
                var commandCount = Commands?.Count ?? 0;
                return $"Mode={MatchMode} Case={caseValue} Cmds={commandCount}";
            }
        }

        [LabelText("Match Mode")]
        [EnumToggleButtons]
        [SerializeField]
        public SwitchCaseMatchMode MatchMode = SwitchCaseMatchMode.Exact;

        [LabelText("Case Value")]
        [ShowIf(nameof(IsExactMode))]
        [SerializeField]
        public DynamicValue CaseValue;

        [LabelText("Compare Operator")]
        [ShowIf(nameof(IsCompareMode))]
        [SerializeField]
        public SwitchNumericCompareOp CompareOp = SwitchNumericCompareOp.GreaterOrEqual;

        [LabelText("Compare Target")]
        [ShowIf(nameof(IsCompareMode))]
        [SerializeField]
        public DynamicValue CompareTarget;

        [LabelText("Condition")]
        [ShowIf(nameof(IsConditionMode))]
        [SerializeField]
        public DynamicValue<bool> Condition;

        [LabelText("Commands")]
        [CommandListFunctionName("Control.Switch.Case")]
        [SerializeField]
        public CommandListData Commands = new();

        bool IsExactMode() => MatchMode == SwitchCaseMatchMode.Exact;
        bool IsCompareMode() => MatchMode == SwitchCaseMatchMode.Compare;
        bool IsConditionMode() => MatchMode == SwitchCaseMatchMode.Condition;
    }

    [Serializable]
    public sealed class SwitchCommandData : ICommandData
    {
        public int CommandId => CommandIds.Switch;
        public string DebugData
        {
            get
            {
                var switchValue = CommandDebugDataHelper.GetDynamicDebugData(SwitchValue);
                var cases = Cases?.Count ?? 0;
                var casePreview = BuildCasePreview(Cases);
                var defaultCount = DefaultCommands?.Count ?? 0;
                return $"Switch={switchValue} Cases={cases} [{casePreview}] Default={defaultCount} Debug={DebugMode}";
            }
        }

        [LabelText("Switch Value")]
        [SerializeField]
        public DynamicValue SwitchValue;

        [LabelText("Evaluate Order")]
        [EnumToggleButtons]
        [SerializeField]
        public SwitchEvaluateOrder EvaluateOrder = SwitchEvaluateOrder.TopToBottom;

        [ListDrawerSettings(ShowFoldout = true, ListElementLabelName = nameof(SwitchCase.ListLabel))]
        [SerializeField]
        public List<SwitchCase> Cases = new();

        [FoldoutGroup("Default")]
        [HideLabel]
        [CommandListFunctionName("Control.Switch.Default")]
        [SerializeField]
        public CommandListData DefaultCommands = new();

        [FoldoutGroup("OnCanceled")]
        [HideLabel]
        [CommandListFunctionName("Control.Switch.OnCanceled")]
        [SerializeField]
        public CommandListData OnCanceledCommands = new();

        [FoldoutGroup("Debug")]
        [LabelText("Debug Mode")]
        [SerializeField]
        public bool DebugMode;

        static string BuildCasePreview(List<SwitchCase>? cases, int maxPreviewCount = 3)
        {
            if (cases == null || cases.Count == 0)
                return "<none>";

            var takeCount = Math.Min(maxPreviewCount, cases.Count);
            var preview = string.Empty;
            for (var i = 0; i < takeCount; i++)
            {
                var value = CommandDebugDataHelper.GetDynamicDebugData(cases[i].CaseValue);
                preview = i == 0 ? value : $"{preview},{value}";
            }

            var rest = cases.Count - takeCount;
            if (rest <= 0)
                return preview;
            return $"{preview},+{rest}";
        }
    }

    [Serializable]
    public sealed class ForCommandData : ICommandData
    {
        public int CommandId => CommandIds.For;
        public string DebugData
        {
            get
            {
                var bodyCount = BodyCommands?.Count ?? 0;
                if (Mode == ForLoopMode.Count || Mode == ForLoopMode.Random)
                {
                    var count = CommandDebugDataHelper.GetDynamicDebugData(Count);
                    if (Mode == ForLoopMode.Random)
                    {
                        var min = CommandDebugDataHelper.GetDynamicDebugData(RandomMinNoExecuteSeconds);
                        var jitter = CommandDebugDataHelper.GetDynamicDebugData(RandomJitterSeconds);
                        return $"Mode=Random Count={count} Min={min} Jitter={jitter} Wait={WaitForCompletion} Body={bodyCount}";
                    }

                    return $"Mode=Count Count={count} Wait={WaitForCompletion} Body={bodyCount}";
                }
                var condition = CommandDebugDataHelper.GetDynamicDebugData(Condition);
                return $"Mode={Mode} Cond={condition} Wait={WaitForCompletion} Body={bodyCount}";
            }
        }

        [BoxGroup("Loop Settings")]
        [EnumToggleButtons]
        [SerializeField]
        public ForLoopMode Mode = ForLoopMode.Count;

        [BoxGroup("Loop Settings")]
        [ShowIf("@Mode == ForLoopMode.Count || Mode == ForLoopMode.Random")]
        [LabelText("Count")]
        [SerializeField]
        public DynamicValue<int> Count = DynamicValueExtensions.FromLiteral(1);

        [BoxGroup("Loop Settings")]
        [ShowIf("@Mode == ForLoopMode.While || Mode == ForLoopMode.Until")]
        [LabelText("Condition")]
        [SerializeField]
        public DynamicValue<bool> Condition;

        [BoxGroup("Loop Settings")]
        [ShowIf("@Mode == ForLoopMode.Random")]
        [LabelText("Min No Execute Sec")]
        [SerializeField]
        public DynamicValue<float> RandomMinNoExecuteSeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Loop Settings")]
        [ShowIf("@Mode == ForLoopMode.Random")]
        [LabelText("Random Jitter Sec")]
        [SerializeField]
        public DynamicValue<float> RandomJitterSeconds = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Loop Settings")]
        [Tooltip("Max iterations to prevent infinite loop.")]
        [SerializeField]
        public int MaxIterations = 1000;

        [BoxGroup("Loop Settings")]
        [LabelText("Wait For Completion")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        public bool WaitForCompletion = true;

        [BoxGroup("Loop Settings")]
        [LabelText("Counter Var")]
        [SerializeField]
        public VarKeyRef CounterVar = new(VarIds.GameLib.Base.CommandVar.i, "GameLib.Base.CommandVar.i");

        [BoxGroup("Loop Settings")]
        [LabelText("Break Switch Var")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        public VarKeyRef BreakSwitchVar;

        [FoldoutGroup("Body")]
        [HideLabel]
        [CommandListFunctionName("Control.For.Body")]
        [SerializeField]
        public CommandListData BodyCommands = new();

        [FoldoutGroup("OnCanceled")]
        [HideLabel]
        [CommandListFunctionName("Control.For.OnCanceled")]
        [SerializeField]
        public CommandListData OnCanceledCommands = new();
    }

    [Serializable]
    public sealed class SequenceCommandData : ICommandData
    {
        public int CommandId => CommandIds.Sequence;
        public string DebugData
        {
            get
            {
                var bodyCount = BodyCommands?.Count ?? 0;
                return $"Body={bodyCount}";
            }
        }

        [FoldoutGroup("Body")]
        [HideLabel]
        [CommandListFunctionName("Control.Sequence.Body")]
        [SerializeField]
        public CommandListData BodyCommands = new();

        [FoldoutGroup("OnCanceled")]
        [HideLabel]
        [CommandListFunctionName("Control.Sequence.OnCanceled")]
        [SerializeField]
        public CommandListData OnCanceledCommands = new();
    }

    public enum ActionBlockMode
    {
        Disposable = 0,
        Tag = 1,
    }

    [Serializable]
    public sealed class ActionBlockCommandData : ICommandData
    {
        public int CommandId => CommandIds.ActionBlock;
        public string DebugData
        {
            get
            {
                var kinds = string.IsNullOrEmpty(Kinds) ? "<none>" : Kinds;
                var tag = string.IsNullOrEmpty(Tag) ? "<none>" : Tag;
                if (Mode == ActionBlockMode.Tag)
                    return $"Mode=Tag Tag={tag} Block={TagShouldBlock}";
                return $"Mode=Disposable Kinds={kinds}";
            }
        }

        [BoxGroup("Block")]
        [EnumToggleButtons]
        [LabelText("Mode")]
        [SerializeField]
        public ActionBlockMode Mode = ActionBlockMode.Disposable;

        [BoxGroup("Block")]
        [LabelText("Kinds")]
        [SerializeField]
        [ActionBlockKeyDropdown]
        public string Kinds = string.Empty;

        [BoxGroup("Block")]
        [LabelText("Reason")]
        [ShowIf("@Mode == ActionBlockMode.Disposable")]
        [SerializeField]
        public string Reason = string.Empty;

        [BoxGroup("Block")]
        [LabelText("Tag")]
        [ShowIf("@Mode == ActionBlockMode.Tag")]
        [SerializeField]
        public string Tag = string.Empty;

        [BoxGroup("Block")]
        [LabelText("Apply Block")]
        [ShowIf("@Mode == ActionBlockMode.Tag")]
        [SerializeField]
        public bool TagShouldBlock = true;

        [BoxGroup("Block")]
        [LabelText("Persistent")]
        [ShowIf("@Mode == ActionBlockMode.Tag")]
        [SerializeField]
        public bool TagPersistent = true;

        [BoxGroup("Execution")]
        [LabelText("Fire & Forget")]
        [SerializeField]
        public bool FireAndForget;

        [FoldoutGroup("Body")]
        [HideLabel]
        [CommandListFunctionName("Control.ActionBlock.Body")]
        [SerializeField]
        public CommandListData BodyCommands = new();

        [FoldoutGroup("OnCanceled")]
        [HideLabel]
        [CommandListFunctionName("Control.ActionBlock.OnCanceled")]
        [SerializeField]
        public CommandListData OnCanceledCommands = new();
    }

    [Serializable]
    public sealed class ForgetCommandData : ICommandData
    {
        public int CommandId => CommandIds.Forget;
        public string DebugData
        {
            get
            {
                var count = Commands?.Count ?? 0;
                return $"Commands={count}";
            }
        }

        [FoldoutGroup("Commands")]
        [HideLabel]
        [CommandListFunctionName("Control.Forget.Commands")]
        [SerializeField]
        public CommandListData Commands = new();

        [FoldoutGroup("OnCanceled")]
        [HideLabel]
        [CommandListFunctionName("Control.Forget.OnCanceled")]
        [SerializeField]
        public CommandListData OnCanceledCommands = new();
    }

    [Serializable]
    public sealed class DelayExecutorCommandData : ICommandData
    {
        public int CommandId => CommandIds.DelayExecutor;
        public string DebugData
        {
            get
            {
                var delay = CommandDebugDataHelper.GetDynamicDebugData(DelaySeconds);
                var firstCount = FirstCommands?.Count ?? 0;
                var secondCount = SecondCommands?.Count ?? 0;
                return $"Delay={delay} First={firstCount} Second={secondCount}";
            }
        }

        [BoxGroup("Delay")]
        [LabelText("Delay Seconds")]
        [SerializeField]
        public DynamicValue<float> DelaySeconds;

        [FoldoutGroup("First (Fire & Forget)")]
        [HideLabel]
        [CommandListFunctionName("Control.DelayExecutor.First")]
        [SerializeField]
        public CommandListData FirstCommands = new();

        [FoldoutGroup("Second (After Delay)")]
        [HideLabel]
        [CommandListFunctionName("Control.DelayExecutor.Second")]
        [SerializeField]
        public CommandListData SecondCommands = new();

        [FoldoutGroup("OnCanceled")]
        [HideLabel]
        [CommandListFunctionName("Control.DelayExecutor.OnCanceled")]
        [SerializeField]
        public CommandListData OnCanceledCommands = new();
    }
}
