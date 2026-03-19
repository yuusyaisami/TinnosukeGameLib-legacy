#nullable enable
using System;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    [Serializable]
    public sealed class SetColliderSharedMaterialCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetColliderSharedMaterial;
        public string DebugData => $"Target={Target.Kind} Material={(SharedMaterial != null ? SharedMaterial.name : "(null)")}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Material")]
        [LabelText("Shared Material")]
        [SerializeField]
        public PhysicsMaterial2D? SharedMaterial;
    }

    [Serializable]
    public sealed class SetColliderPhysicsMaterialValuesCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetColliderPhysicsMaterialValues;
        public string DebugData => $"Target={Target.Kind} Friction={FormatApply(ApplyFriction, Friction)} Bounce={FormatApply(ApplyBounciness, Bounciness)}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Material")]
        [LabelText("Template Material (Optional)")]
        [PropertyTooltip("Runtime material is cloned from this template first. If empty, current sharedMaterial is cloned, otherwise a new runtime material is created.")]
        [SerializeField]
        public PhysicsMaterial2D? TemplateMaterial;

        [BoxGroup("Material")]
        [LabelText("Apply Friction")]
        [SerializeField]
        public bool ApplyFriction = true;

        [BoxGroup("Material")]
        [ShowIf(nameof(ApplyFriction))]
        [LabelText("Friction")]
        [SerializeField]
        public DynamicValue<float> Friction = DynamicValueExtensions.FromLiteral(0.4f);

        [BoxGroup("Material")]
        [LabelText("Apply Bounciness")]
        [SerializeField]
        public bool ApplyBounciness = true;

        [BoxGroup("Material")]
        [ShowIf(nameof(ApplyBounciness))]
        [LabelText("Bounciness")]
        [SerializeField]
        public DynamicValue<float> Bounciness = DynamicValueExtensions.FromLiteral(0f);

        static string FormatApply(bool apply, DynamicValue<float> value)
        {
            return apply ? CommandDebugDataHelper.GetDynamicDebugData(value) : "(keep)";
        }
    }

    [Serializable]
    public sealed class SetGlobalPhysics2DCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetGlobalPhysics2D;
        public string DebugData
        {
            get
            {
                return $"Gravity={ApplyGravity} SimMode={ApplySimulationMode} QueryTrig={ApplyQueriesHitTriggers} QueryStart={ApplyQueriesStartInColliders}";
            }
        }

        [BoxGroup("World")]
        [LabelText("Apply Gravity")]
        [SerializeField]
        public bool ApplyGravity;

        [BoxGroup("World")]
        [ShowIf(nameof(ApplyGravity))]
        [LabelText("Gravity")]
        [SerializeField]
        public DynamicValue<Vector2> Gravity = DynamicValueExtensions.FromLiteral(new Vector2(0f, -9.81f));

        [BoxGroup("Simulation")]
        [LabelText("Apply Simulation Mode")]
        [SerializeField]
        public bool ApplySimulationMode;

        [BoxGroup("Simulation")]
        [ShowIf(nameof(ApplySimulationMode))]
        [LabelText("Simulation Mode")]
        [SerializeField]
        public SimulationMode2D SimulationMode = SimulationMode2D.FixedUpdate;

        [BoxGroup("Solver")]
        [LabelText("Apply Velocity Iterations")]
        [SerializeField]
        public bool ApplyVelocityIterations;

        [BoxGroup("Solver")]
        [ShowIf(nameof(ApplyVelocityIterations))]
        [LabelText("Velocity Iterations")]
        [SerializeField]
        public DynamicValue<int> VelocityIterations = DynamicValueExtensions.FromLiteral(8);

        [BoxGroup("Solver")]
        [LabelText("Apply Position Iterations")]
        [SerializeField]
        public bool ApplyPositionIterations;

        [BoxGroup("Solver")]
        [ShowIf(nameof(ApplyPositionIterations))]
        [LabelText("Position Iterations")]
        [SerializeField]
        public DynamicValue<int> PositionIterations = DynamicValueExtensions.FromLiteral(3);

        [BoxGroup("Solver")]
        [LabelText("Apply Default Contact Offset")]
        [SerializeField]
        public bool ApplyDefaultContactOffset;

        [BoxGroup("Solver")]
        [ShowIf(nameof(ApplyDefaultContactOffset))]
        [LabelText("Default Contact Offset")]
        [SerializeField]
        public DynamicValue<float> DefaultContactOffset = DynamicValueExtensions.FromLiteral(0.01f);

        [BoxGroup("Solver")]
        [LabelText("Apply Baumgarte Scale")]
        [SerializeField]
        public bool ApplyBaumgarteScale;

        [BoxGroup("Solver")]
        [ShowIf(nameof(ApplyBaumgarteScale))]
        [LabelText("Baumgarte Scale")]
        [SerializeField]
        public DynamicValue<float> BaumgarteScale = DynamicValueExtensions.FromLiteral(0.2f);

        [BoxGroup("Solver")]
        [LabelText("Apply Baumgarte TOI Scale")]
        [SerializeField]
        public bool ApplyBaumgarteTOIScale;

        [BoxGroup("Solver")]
        [ShowIf(nameof(ApplyBaumgarteTOIScale))]
        [LabelText("Baumgarte TOI Scale")]
        [SerializeField]
        public DynamicValue<float> BaumgarteTOIScale = DynamicValueExtensions.FromLiteral(0.75f);

        [BoxGroup("Query")]
        [LabelText("Apply Queries Hit Triggers")]
        [SerializeField]
        public bool ApplyQueriesHitTriggers;

        [BoxGroup("Query")]
        [ShowIf(nameof(ApplyQueriesHitTriggers))]
        [LabelText("Queries Hit Triggers")]
        [SerializeField]
        public DynamicValue<bool> QueriesHitTriggers = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Query")]
        [LabelText("Apply Queries Start In Colliders")]
        [SerializeField]
        public bool ApplyQueriesStartInColliders;

        [BoxGroup("Query")]
        [ShowIf(nameof(ApplyQueriesStartInColliders))]
        [LabelText("Queries Start In Colliders")]
        [SerializeField]
        public DynamicValue<bool> QueriesStartInColliders = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Callback")]
        [LabelText("Apply Callbacks On Disable")]
        [SerializeField]
        public bool ApplyCallbacksOnDisable;

        [BoxGroup("Callback")]
        [ShowIf(nameof(ApplyCallbacksOnDisable))]
        [LabelText("Callbacks On Disable")]
        [SerializeField]
        public DynamicValue<bool> CallbacksOnDisable = DynamicValueExtensions.FromLiteral(false);

        [BoxGroup("Callback")]
        [LabelText("Apply Reuse Collision Callbacks")]
        [SerializeField]
        public bool ApplyReuseCollisionCallbacks;

        [BoxGroup("Callback")]
        [ShowIf(nameof(ApplyReuseCollisionCallbacks))]
        [LabelText("Reuse Collision Callbacks")]
        [SerializeField]
        public DynamicValue<bool> ReuseCollisionCallbacks = DynamicValueExtensions.FromLiteral(false);
    }
}
