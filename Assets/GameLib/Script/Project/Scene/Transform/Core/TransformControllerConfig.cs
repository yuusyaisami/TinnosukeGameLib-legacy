#nullable enable
using UnityEngine;
using Game.Movement;

namespace Game.TransformSystem
{
    public sealed class TransformControllerConfig
    {
        public TransformOutputTarget OutputTarget = TransformOutputTarget.Transform;
        public Transform? TargetTransform;
        public RectTransform? TargetRectTransform;
        public Rigidbody2D? TargetRigidbody2D;
        public CharacterController? TargetCharacterController;
        public Rigidbody2DVelocityApplyMode Rigidbody2DVelocityMode = Rigidbody2DVelocityApplyMode.Override;
        public Rigidbody2DAdditiveControlSettings Rigidbody2DAdditiveControl = Rigidbody2DAdditiveControlSettings.Default;
        public Rigidbody2DGravityClampSettings Rigidbody2DGravityClamp = Rigidbody2DGravityClampSettings.Default;
        public bool EnableMovement = true;
        public bool EnableRotation = false;
    }
}
