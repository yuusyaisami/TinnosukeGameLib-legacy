#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum WriteServiceScope
    {
        Scope = 0,
        Actor = 1,
    }

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
        public DynamicValue Value;

        [ShowIf("@Op == ScalarWriteOpKind.LocalAdd || Op == ScalarWriteOpKind.GlobalAdd || Op == ScalarWriteOpKind.LocalMul || Op == ScalarWriteOpKind.GlobalMul")]
        [LabelText("Duration (sec)")]
        public DynamicValue DurationSeconds;

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
                return $"Scope={ServiceScope} Vars={varCount} Scalars={scalarCount}";
            }
        }

        [EnumToggleButtons]
        public WriteServiceScope ServiceScope = WriteServiceScope.Actor;

        [FoldoutGroup("Vars")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<VarWriteOp> VarOps = new();

        [FoldoutGroup("Scalar")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<ScalarWriteOp> ScalarOps = new();
    }
}
