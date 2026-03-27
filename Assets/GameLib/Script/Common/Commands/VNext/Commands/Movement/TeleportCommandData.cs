#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class TeleportCommandData : ICommandData
    {
        public int CommandId => CommandIds.Teleport;
        public string DebugData => $"Target={TargetActorSource.Kind} Relative={Relative} ResetVel={ResetVelocity} StopAnim={StopTransformAnimations}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(TargetActorSource)")]
        [SerializeField]
        public ActorSource TargetActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Destination")]
        [LabelText("Position")]
        [SerializeField]
        public DynamicValue<Vector3> Position;

        [BoxGroup("Options")]
        [LabelText("Relative")]
        public bool Relative = false;

        [BoxGroup("Options")]
        [LabelText("Reset Velocity")]
        public bool ResetVelocity = true;

        [BoxGroup("Options")]
        [LabelText("Stop Transform Anim")]
        public bool StopTransformAnimations = true;
    }
}
