#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum VarStoreTarget
    {
        CommandVars = 0,
        BlackboardLocal = 1,
        BlackboardGlobal = 2,
    }

    public enum VarWriteOpKind
    {
        Set = 0,
        Unset = 1,
        Add = 2,
        Mul = 3,
    }

    [Serializable]
    public sealed class VarWriteOp
    {
        [EnumToggleButtons]
        public VarStoreTarget Target = VarStoreTarget.CommandVars;

        [LabelText("Key")]
        public VarKeyRef Key;

        [EnumToggleButtons]
        public VarWriteOpKind Op = VarWriteOpKind.Set;

        [ShowIf("@Op != VarWriteOpKind.Unset")]
        public DynamicValue Value;
    }

    public enum ScalarWriteOpKind
    {
        SetLocalBase = 0,
        SetGlobalBase = 1,
        LocalAdd = 2,
        GlobalAdd = 3,
        LocalMul = 4,
        GlobalMul = 5,
        ClearKey = 6,
        ClearAll = 7,

        DisposeHandleVar = 20,
    }

    public enum WriteDataDebugLogLevel
    {
        Info = 10,
        Warning = 20,
        Error = 30,
    }

    [Serializable]
    public sealed class WriteDataVarDebugSettings
    {
        [LabelText("Enabled")]
        public bool Enabled = true;

        [LabelText("Log Success")]
        public bool LogSuccess = true;

        [LabelText("Log Failure")]
        public bool LogFailure = true;

        [LabelText("Include Input Value")]
        public bool IncludeInputValue = true;

        [LabelText("Include Before Value")]
        public bool IncludeBeforeValue = true;

        [LabelText("Include After Value")]
        public bool IncludeAfterValue = true;

        [LabelText("Include Var Key")]
        public bool IncludeVarKey = true;

        [LabelText("Include Scope")]
        public bool IncludeScope = true;
    }

    [Serializable]
    public sealed class WriteDataScalarDebugSettings
    {
        [LabelText("Enabled")]
        public bool Enabled = true;

        [LabelText("Log Success")]
        public bool LogSuccess = true;

        [LabelText("Log Failure")]
        public bool LogFailure = true;

        [LabelText("Include Input Value")]
        public bool IncludeInputValue = true;

        [LabelText("Include Before Value")]
        public bool IncludeBeforeValue = true;

        [LabelText("Include After Value")]
        public bool IncludeAfterValue = true;

        [LabelText("Include Scalar Key")]
        public bool IncludeScalarKey = true;

        [LabelText("Include Scope")]
        public bool IncludeScope = true;

        [LabelText("Include Layer")]
        public bool IncludeLayer = true;

        [LabelText("Include Duration")]
        public bool IncludeDuration = true;

        [LabelText("Include Tag")]
        public bool IncludeTag = true;
    }

    [Serializable]
    public sealed class WriteDataDebugSettings
    {
        [LabelText("Log Level")]
        [EnumToggleButtons]
        public WriteDataDebugLogLevel LogLevel = WriteDataDebugLogLevel.Info;

        [LabelText("Log Prefix")]
        public string Prefix = "[WriteDataDebug]";

        [LabelText("Include Command Summary")]
        public bool IncludeCommandSummary = true;

        [LabelText("Include Op Index")]
        public bool IncludeOpIndex = true;

        [LabelText("Vars")]
        [InlineProperty]
        [HideLabel]
        public WriteDataVarDebugSettings Vars = new();

        [LabelText("Scalars")]
        [InlineProperty]
        [HideLabel]
        public WriteDataScalarDebugSettings Scalars = new();
    }

    [Serializable]
    public sealed class ScalarWriteOp
    {
        [EnumToggleButtons]
        public ScalarWriteOpKind Op;

        [ShowIf("@Op != ScalarWriteOpKind.ClearAll && Op != ScalarWriteOpKind.DisposeHandleVar")]
        public ScalarKey Key;

        [ShowIf("@Op == ScalarWriteOpKind.LocalAdd || Op == ScalarWriteOpKind.GlobalAdd || Op == ScalarWriteOpKind.LocalMul || Op == ScalarWriteOpKind.GlobalMul")]
        public string Layer = "";

        [ShowIf("@Op == ScalarWriteOpKind.LocalMul || Op == ScalarWriteOpKind.GlobalMul")]
        public ScalarMulPhase MulPhase = ScalarMulPhase.PostAdd;

        [ShowIf("@Op == ScalarWriteOpKind.SetLocalBase || Op == ScalarWriteOpKind.SetGlobalBase || Op == ScalarWriteOpKind.LocalAdd || Op == ScalarWriteOpKind.GlobalAdd || Op == ScalarWriteOpKind.LocalMul || Op == ScalarWriteOpKind.GlobalMul")]
        public DynamicValue<float> Value;

        [ShowIf("@Op == ScalarWriteOpKind.LocalAdd || Op == ScalarWriteOpKind.GlobalAdd || Op == ScalarWriteOpKind.LocalMul || Op == ScalarWriteOpKind.GlobalMul")]
        [LabelText("Duration (sec)")]
        public DynamicValue<float> DurationSeconds;

        [ShowIf("@Op == ScalarWriteOpKind.LocalAdd || Op == ScalarWriteOpKind.GlobalAdd || Op == ScalarWriteOpKind.LocalMul || Op == ScalarWriteOpKind.GlobalMul")]
        public string Tag = "";

        [ShowIf("@Op == ScalarWriteOpKind.LocalAdd || Op == ScalarWriteOpKind.GlobalAdd || Op == ScalarWriteOpKind.LocalMul || Op == ScalarWriteOpKind.GlobalMul")]
        [LabelText("Store Handle Var (optional)")]
        public VarKeyRef StoreHandleVar;

        [ShowIf("@Op == ScalarWriteOpKind.DisposeHandleVar")]
        [LabelText("Handle Var")]
        public VarKeyRef HandleVar;

        [ShowIf("@Op == ScalarWriteOpKind.DisposeHandleVar")]
        public bool UnsetAfterDispose = true;
    }

    [Serializable]
    public sealed class WriteDataCommandData : ICommandData
    {
        public int CommandId => CommandIds.WriteData;
        public string DebugData
        {
            get
            {
                var varCount = VarOps?.Count ?? 0;
                var scalarCount = ScalarOps?.Count ?? 0;
                return $"Source={Source.Kind} Target={Target.Kind} Vars={varCount} Scalars={scalarCount}";
            }
        }

        [BoxGroup("Source")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Source)")]
        [SerializeField]
        public ActorSource Source = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [FoldoutGroup("Vars")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<VarWriteOp> VarOps = new();

        [FoldoutGroup("Scalar")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<ScalarWriteOp> ScalarOps = new();

        [FoldoutGroup("Debug")]
        [LabelText("Debug Mode")]
        public bool DebugMode;

        [FoldoutGroup("Debug")]
        [ShowIf(nameof(DebugMode))]
        [InlineProperty]
        [HideLabel]
        public WriteDataDebugSettings Debug = new();
    }
}
