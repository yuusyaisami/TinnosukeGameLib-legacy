#nullable enable
using System;
using Game.Collision;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum UnityColliderShapeMode
    {
        KeepCurrent = 0,
        Circle = 1,
        Box = 2,
        Capsule = 3,
    }

    [Serializable]
    public sealed class SetUnityColliderCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetUnityCollider;

        public string DebugData
        {
            get
            {
                var enabled = ApplyEnabled
                    ? CommandDebugDataHelper.GetDynamicDebugData(Enabled)
                    : "(keep)";
                var trigger = ApplyIsTrigger
                    ? CommandDebugDataHelper.GetDynamicDebugData(IsTrigger)
                    : "(keep)";
                return $"Mode={Mode} Enabled={enabled} Trigger={trigger}";
            }
        }

        [BoxGroup("Shape")]
        [LabelText("Mode")]
        [SerializeField]
        public UnityColliderShapeMode Mode = UnityColliderShapeMode.KeepCurrent;

        [BoxGroup("Common")]
        [LabelText("Apply Enabled")]
        [SerializeField]
        public bool ApplyEnabled = true;

        [BoxGroup("Common")]
        [ShowIf(nameof(ApplyEnabled))]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> Enabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Common")]
        [LabelText("Apply IsTrigger")]
        [SerializeField]
        public bool ApplyIsTrigger;

        [BoxGroup("Common")]
        [ShowIf(nameof(ApplyIsTrigger))]
        [LabelText("Is Trigger")]
        [SerializeField]
        public DynamicValue<bool> IsTrigger = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Common")]
        [LabelText("Apply Shared Material")]
        [SerializeField]
        public bool ApplySharedMaterial;

        [BoxGroup("Common")]
        [ShowIf(nameof(ApplySharedMaterial))]
        [LabelText("Shared Material")]
        [SerializeField]
        public PhysicsMaterial2D? SharedMaterial;

        [BoxGroup("Common")]
        [LabelText("Apply Offset")]
        [SerializeField]
        public bool ApplyOffset;

        [BoxGroup("Common")]
        [ShowIf(nameof(ApplyOffset))]
        [LabelText("Offset")]
        [SerializeField]
        public DynamicValue<Vector2> Offset = DynamicValueExtensions.FromLiteral(Vector2.zero);

        [BoxGroup("Common")]
        [LabelText("Apply Size")]
        [SerializeField]
        public bool ApplySize;

        [BoxGroup("Common")]
        [ShowIf(nameof(ApplySize))]
        [LabelText("Size")]
        [SerializeField]
        public DynamicValue<Vector2> Size = DynamicValueExtensions.FromLiteral(Vector2.one);

        [BoxGroup("Circle")]
        [ShowIf(nameof(IsCircleMode))]
        [LabelText("Radius")]
        [SerializeField]
        public DynamicValue<float> CircleRadius = DynamicValueExtensions.FromLiteral(0.5f);

        [BoxGroup("Box")]
        [ShowIf(nameof(IsBoxMode))]
        [LabelText("Edge Radius")]
        [SerializeField]
        public DynamicValue<float> BoxEdgeRadius = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Capsule")]
        [ShowIf(nameof(IsCapsuleMode))]
        [LabelText("Direction")]
        [SerializeField]
        public CapsuleDirection2D CapsuleDirection = CapsuleDirection2D.Vertical;

        [BoxGroup("Dynamic Settings")]
        [LabelText("Apply Layer Id")]
        [SerializeField]
        public bool ApplyLayerId;

        [BoxGroup("Dynamic Settings")]
        [ShowIf(nameof(ApplyLayerId))]
        [LabelText("Layer Id")]
        [SerializeField]
        public DynamicValue<int> LayerId = DynamicValueExtensions.FromLiteral(0);

        [BoxGroup("Dynamic Settings")]
        [LabelText("Apply Hit Mask")]
        [SerializeField]
        public bool ApplyHitMask;

        [BoxGroup("Dynamic Settings")]
        [ShowIf(nameof(ApplyHitMask))]
        [LabelText("Hit Mask (int bits)")]
        [SerializeField]
        public DynamicValue<int> HitMask = DynamicValueExtensions.FromLiteral(unchecked((int)~0u));

        [BoxGroup("Dynamic Settings")]
        [LabelText("Apply Set Id")]
        [SerializeField]
        public bool ApplySetId;

        [BoxGroup("Dynamic Settings")]
        [ShowIf(nameof(ApplySetId))]
        [LabelText("Set Id (enum int)")]
        [SerializeField]
        public DynamicValue<int> SetId = DynamicValueExtensions.FromLiteral((int)DynamicColliderSetId.EnemyBullet);

        [BoxGroup("Dynamic Settings")]
        [LabelText("Apply User Data")]
        [SerializeField]
        public bool ApplyUserData;

        [BoxGroup("Dynamic Settings")]
        [ShowIf(nameof(ApplyUserData))]
        [LabelText("User Data")]
        [SerializeField]
        public DynamicValue<int> UserData = DynamicValueExtensions.FromLiteral(0);

        bool IsCircleMode => Mode == UnityColliderShapeMode.Circle;
        bool IsBoxMode => Mode == UnityColliderShapeMode.Box;
        bool IsCapsuleMode => Mode == UnityColliderShapeMode.Capsule;
    }
}
