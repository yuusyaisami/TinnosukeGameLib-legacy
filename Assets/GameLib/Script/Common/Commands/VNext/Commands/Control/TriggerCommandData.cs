#nullable enable

using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class TriggerCommandData : ICommandData
    {
        public int CommandId => CommandIds.Trigger;

        public string DebugData
        {
            get
            {
                var key = DescribeKey(Key);
                var thenCount = ThenCommands?.Count ?? 0;
                var elseCount = ElseCommands?.Count ?? 0;
                return $"Target={Target} Key={key} Then={thenCount} Else={elseCount}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Store")]
        [EnumToggleButtons]
        [SerializeField]
        public VarStoreTarget Target = VarStoreTarget.CommandVars;

        [BoxGroup("Target")]
        [LabelText("Key")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        public VarKeyRef Key;

        [FoldoutGroup("Then")]
        [HideLabel]
        [CommandListFunctionName("Control.Trigger.Then")]
        [SerializeField]
        public CommandListData ThenCommands = new();

        [FoldoutGroup("Else")]
        [HideLabel]
        [CommandListFunctionName("Control.Trigger.Else")]
        [SerializeField]
        public CommandListData ElseCommands = new();

        static string DescribeKey(in VarKeyRef key)
        {
            if (key.VarId != 0)
            {
                var stableKey = VarIdResolver.TryGetIdToStable(key.VarId);
                if (!string.IsNullOrWhiteSpace(stableKey))
                    return stableKey;

                if (!string.IsNullOrWhiteSpace(key.StableKey))
                    return key.StableKey;

                return $"VarId={key.VarId}";
            }

            if (!string.IsNullOrWhiteSpace(key.StableKey))
                return key.StableKey;

            return "<none>";
        }
    }
}