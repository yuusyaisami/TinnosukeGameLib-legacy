#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class TransformChannelRigidbody2DCommandData : ICommandData
    {
        public int CommandId => CommandIds.TransformChannelRigidbody2D;

        public string DebugData
        {
            get
            {
                return $"Tag={NormalizedChannelTag} Rb(Sim={ApplySimulated},Grav={ApplyGravityScale},Vel={ApplyLinearVelocity}) " +
                       $"Channel(Move={ApplyMovementEnabled},Rot={ApplyRotationEnabled},Stop={ForceStopMovementNow}) " +
                       $"Block(Apply={ApplyTransformChannelMovementBlock},ForceZero={ApplyForceZeroWhenBlocked})";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Channel Tag")]
        [SerializeField]
        public string ChannelTag = "default";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [BoxGroup("Rigidbody2D")]
        [LabelText("Apply Simulated")]
        [SerializeField]
        public bool ApplySimulated;

        [BoxGroup("Rigidbody2D")]
        [ShowIf(nameof(ApplySimulated))]
        [LabelText("Simulated")]
        [SerializeField]
        public DynamicValue<bool> Simulated = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Rigidbody2D")]
        [LabelText("Apply Gravity Scale")]
        [SerializeField]
        public bool ApplyGravityScale;

        [BoxGroup("Rigidbody2D")]
        [ShowIf(nameof(ApplyGravityScale))]
        [LabelText("Gravity Scale")]
        [SerializeField]
        public DynamicValue<float> GravityScale = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Rigidbody2D")]
        [LabelText("Apply Freeze Rotation")]
        [SerializeField]
        public bool ApplyFreezeRotation;

        [BoxGroup("Rigidbody2D")]
        [ShowIf(nameof(ApplyFreezeRotation))]
        [LabelText("Freeze Rotation")]
        [SerializeField]
        public DynamicValue<bool> FreezeRotation = DynamicValueExtensions.FromLiteral(false);

        [BoxGroup("Rigidbody2D")]
        [LabelText("Apply Linear Velocity")]
        [SerializeField]
        public bool ApplyLinearVelocity;

        [BoxGroup("Rigidbody2D")]
        [ShowIf(nameof(ApplyLinearVelocity))]
        [LabelText("Linear Velocity")]
        [SerializeField]
        public DynamicValue<Vector2> LinearVelocity = DynamicValueExtensions.FromLiteral(Vector2.zero);

        [BoxGroup("Rigidbody2D")]
        [LabelText("Apply Angular Velocity")]
        [SerializeField]
        public bool ApplyAngularVelocity;

        [BoxGroup("Rigidbody2D")]
        [ShowIf(nameof(ApplyAngularVelocity))]
        [LabelText("Angular Velocity")]
        [SerializeField]
        public DynamicValue<float> AngularVelocity = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("TransformChannel")]
        [LabelText("Apply Movement Enabled")]
        [SerializeField]
        public bool ApplyMovementEnabled;

        [BoxGroup("TransformChannel")]
        [ShowIf(nameof(ApplyMovementEnabled))]
        [LabelText("Movement Enabled")]
        [SerializeField]
        public DynamicValue<bool> MovementEnabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("TransformChannel")]
        [LabelText("Apply Rotation Enabled")]
        [SerializeField]
        public bool ApplyRotationEnabled;

        [BoxGroup("TransformChannel")]
        [ShowIf(nameof(ApplyRotationEnabled))]
        [LabelText("Rotation Enabled")]
        [SerializeField]
        public DynamicValue<bool> RotationEnabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("TransformChannel")]
        [LabelText("Force Stop Movement Now")]
        [SerializeField]
        public bool ForceStopMovementNow;

        [BoxGroup("ActionBlock")]
        [LabelText("Apply Movement Block")]
        [SerializeField]
        public bool ApplyTransformChannelMovementBlock;

        [BoxGroup("ActionBlock")]
        [ShowIf(nameof(ApplyTransformChannelMovementBlock))]
        [LabelText("Blocked")]
        [SerializeField]
        public DynamicValue<bool> TransformChannelMovementBlocked = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("ActionBlock")]
        [ShowIf(nameof(ApplyTransformChannelMovementBlock))]
        [LabelText("Reason")]
        [SerializeField]
        public string BlockReason = "TransformChannelRigidbody2DCommand";

        [BoxGroup("ActionBlock")]
        [LabelText("Apply Force Zero When Blocked")]
        [SerializeField]
        public bool ApplyForceZeroWhenBlocked;

        [BoxGroup("ActionBlock")]
        [ShowIf(nameof(ApplyForceZeroWhenBlocked))]
        [LabelText("Force Zero When Blocked")]
        [SerializeField]
        public DynamicValue<bool> ForceZeroWhenBlocked = DynamicValueExtensions.FromLiteral(true);

        public string NormalizedChannelTag => string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag.Trim();
    }
}
