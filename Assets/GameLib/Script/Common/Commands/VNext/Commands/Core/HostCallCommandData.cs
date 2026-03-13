#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class HostCallCommandData : ICommandData
    {
        public int CommandId => CommandIds.HostCall;
        public string DebugData
        {
            get
            {
                var argsCount = Args?.Length ?? 0;
                var resultKey = !string.IsNullOrEmpty(ResultVarKey.StableKey)
                    ? ResultVarKey.StableKey
                    : ResultVarKey.VarId > 0 ? ResultVarKey.VarId.ToString() : "<none>";
                return $"Sys={SysId} Args={argsCount} Result={resultKey}";
            }
        }

        [LabelText("SysId")]
        public int SysId;

        [LabelText("Args")]
        public DynamicValue[] Args = Array.Empty<DynamicValue>();

        [LabelText("Result Var")]
        public VarKeyRef ResultVarKey;
    }
}
