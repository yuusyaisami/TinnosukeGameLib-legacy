#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum CollisionTargetKind
    {
        ColliderObject = 0,
        UnityColliderObject = 1,
        Both = 2,
    }

    [Serializable]
    public sealed class SetCollisionEnabledCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetCollisionEnabled;
        public string DebugData
        {
            get
            {
                var enabled = CommandDebugDataHelper.GetDynamicDebugData(Enabled);
                var trigger = CommandDebugDataHelper.GetDynamicDebugData(Trigger);
                return $"Kind={Kind} Enabled={enabled} Trigger={trigger}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Kind")]
        [SerializeField]
        public CollisionTargetKind Kind = CollisionTargetKind.Both;

        [BoxGroup("Enable")]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> Enabled;
        [SerializeField]
        public DynamicValue<bool> Trigger;
    }
}
