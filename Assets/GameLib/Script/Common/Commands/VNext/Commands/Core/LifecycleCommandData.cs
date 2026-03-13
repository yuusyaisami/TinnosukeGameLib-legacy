#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class SelfDespawnCommandData : ICommandData
    {
        public int CommandId => CommandIds.SelfDespawn;
        public string DebugData
        {
            get
            {
                var delay = CommandDebugDataHelper.GetDynamicDebugData(DelaySeconds);
                var beforeCount = BeforeDespawnCommands?.Count ?? 0;
                var onReacquireCount = OnReacquireCommands?.Count ?? 0;
                return $"Delay={delay} Before={beforeCount} OnReacquire={onReacquireCount}";
            }
        }

        [BoxGroup("Delay")]
        [LabelText("Delay Seconds")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        public DynamicValue<float> DelaySeconds;

        [FoldoutGroup("Before Despawn")]
        [HideLabel]
        [CommandListFunctionName("Core.SelfDespawn.Before")]
        [SerializeField]
        public CommandListData BeforeDespawnCommands = new();

        [FoldoutGroup("On Reacquire (Runtime Only)")]
        [HideLabel]
        [CommandListFunctionName("Core.SelfDespawn.OnReacquire")]
        [SerializeField]
        public CommandListData OnReacquireCommands = new();

    }
}
