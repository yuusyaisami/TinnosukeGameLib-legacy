#nullable enable
using System;
using Game.Common;
using Game.Movement;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Commands.VNext
{
    public enum AddForceTargetKind
    {
        MovementChannel = 10,
        TransformChannelRigidbody2D = 20,
    }

    public enum AddForceWriteMode
    {
        AddForce = 10,
        OverrideVelocity = 20,
    }

    [Serializable]
    public sealed class MovementChannelCreateSettings
    {
        [LabelText("Tag (empty = Channel Key)")]
        [SerializeField]
        public string Tag = string.Empty;

        [LabelText("Priority")]
        [SerializeField]
        public int Priority = 0;

        [LabelText("Blend Op")]
        [SerializeField]
        public MovementBlendOp BlendOp = MovementBlendOp.Add;

        [LabelText("Influence")]
        [SerializeField, Range(0f, 1f)]
        public float Influence = 1f;

        [LabelText("Enabled By Default")]
        [SerializeField]
        public bool EnabledByDefault = true;

        [LabelText("Smoothing Lambda")]
        [SerializeField, MinValue(0f)]
        public float SmoothingLambda = 0f;

        [LabelText("Deceleration Lambda")]
        [SerializeField, MinValue(0f)]
        public float DecelerationLambda = 0f;
    }

    [Serializable]
    public sealed class SetVelocityCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetVelocity;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(ChannelKey) ? "<none>" : ChannelKey;
                return $"Channel={key}";
            }
        }

        [BoxGroup("Channel")]
        [LabelText("Channel Key")]
        [SerializeField]
        public string ChannelKey = "input";

        [BoxGroup("Velocity")]
        [LabelText("Velocity")]
        [SerializeField]
        public DynamicValue<Vector2> Velocity;

        [BoxGroup("Velocity")]
        [LabelText("Immediate")]
        [SerializeField]
        public bool Immediate;

        [BoxGroup("Channel Auto Create")]
        [LabelText("Auto Create Channel If Missing")]
        [SerializeField]
        public bool AutoCreateChannelIfMissing;

        [BoxGroup("Channel Auto Create")]
        [ShowIf(nameof(AutoCreateChannelIfMissing))]
        [LabelText("Create Settings")]
        [SerializeField, InlineProperty, HideLabel]
        public MovementChannelCreateSettings AutoCreateSettings = new();
    }

    [Serializable]
    public sealed class AddForceCommandData : ICommandData
    {
        public int CommandId => CommandIds.AddForce;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(ChannelKey) ? "<none>" : ChannelKey;
                return $"Target={TargetKind} Channel={key} WriteMode={WriteMode} ForceMode={ForceMode}";
            }
        }

        [BoxGroup("Target")]
        [LabelText("Target Kind")]
        [SerializeField]
        public AddForceTargetKind TargetKind = AddForceTargetKind.MovementChannel;

        [BoxGroup("Target")]
        [ShowIf(nameof(UseTransformChannelTarget))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Target")]
        [ShowIf(nameof(UseTransformChannelTarget))]
        [LabelText("Transform Channel Tag")]
        [SerializeField]
        public string TransformChannelTag = "default";

        [BoxGroup("Channel")]
        [ShowIf(nameof(UseMovementChannel))]
        [LabelText("Channel Key")]
        [SerializeField]
        public string ChannelKey = "knockback";

        [BoxGroup("Force")]
        [LabelText("Write Mode")]
        [SerializeField]
        public AddForceWriteMode WriteMode = AddForceWriteMode.AddForce;

        [BoxGroup("Force")]
        [LabelText("Force")]
        [SerializeField]
        public DynamicValue<Vector2> Force;

        [BoxGroup("Force")]
        [ShowIf(nameof(UseRigidbodyForceMode))]
        [LabelText("Force Mode")]
        [SerializeField]
        public ForceMode2D ForceMode = ForceMode2D.Force;

        bool UseMovementChannel() => TargetKind == AddForceTargetKind.MovementChannel;
        bool UseTransformChannelTarget() => TargetKind == AddForceTargetKind.TransformChannelRigidbody2D;
        bool UseRigidbodyForceMode() => UseTransformChannelTarget() && WriteMode == AddForceWriteMode.AddForce;
    }

    [Serializable]
    public sealed class SetChannelEnabledCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetChannelEnabled;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(ChannelKey) ? "<none>" : ChannelKey;
                var layer = string.IsNullOrEmpty(LayerKey) ? "<none>" : LayerKey;
                return $"Channel={key} Layer={layer}";
            }
        }

        [BoxGroup("Channel")]
        [LabelText("Channel Key")]
        [SerializeField]
        public string ChannelKey = "input";

        [BoxGroup("Enable")]
        [LabelText("Layer Key")]
        [SerializeField]
        public string LayerKey = "stun";

        [BoxGroup("Enable")]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> Enabled;
    }

    [Serializable]
    public sealed class SetAllChannelsEnabledCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetAllChannelsEnabled;
        public string DebugData
        {
            get
            {
                var layer = string.IsNullOrEmpty(LayerKey) ? "<none>" : LayerKey;
                return $"Layer={layer}";
            }
        }

        [BoxGroup("Enable")]
        [LabelText("Layer Key")]
        [SerializeField]
        public string LayerKey = "stun";

        [BoxGroup("Enable")]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> Enabled;
    }

    [Serializable]
    public sealed class SetChannelInfluenceCommandData : ICommandData
    {
        public int CommandId => CommandIds.SetChannelInfluence;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(ChannelKey) ? "<none>" : ChannelKey;
                return $"Channel={key}";
            }
        }

        [BoxGroup("Channel")]
        [LabelText("Channel Key")]
        [SerializeField]
        public string ChannelKey = "input";

        [BoxGroup("Influence")]
        [LabelText("Influence (0-1)")]
        [SerializeField]
        public DynamicValue<float> Influence;
    }

    [Serializable]
    public sealed class ResetAllVelocitiesCommandData : ICommandData
    {
        public int CommandId => CommandIds.ResetAllVelocities;
        public string DebugData => "All";
    }

    [Serializable]
    public sealed class CreateMovementChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.CreateMovementChannel;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(ChannelKey) ? "<none>" : ChannelKey;
                return $"Channel={key}";
            }
        }

        [BoxGroup("Channel")]
        [LabelText("Channel Key")]
        [SerializeField]
        public string ChannelKey = "runtime";

        [BoxGroup("Create")]
        [LabelText("Skip If Exists")]
        [SerializeField]
        public bool SkipIfExists = true;

        [BoxGroup("Create")]
        [LabelText("Settings")]
        [SerializeField, InlineProperty, HideLabel]
        public MovementChannelCreateSettings Settings = new();
    }

    [Serializable]
    public sealed class RemoveMovementChannelCommandData : ICommandData
    {
        public int CommandId => CommandIds.RemoveMovementChannel;
        public string DebugData
        {
            get
            {
                var key = string.IsNullOrEmpty(ChannelKey) ? "<none>" : ChannelKey;
                return $"Channel={key}";
            }
        }

        [BoxGroup("Channel")]
        [LabelText("Channel Key")]
        [SerializeField]
        public string ChannelKey = "runtime";
    }

    public enum MovementModuleTarget
    {
        Motion = 0,
        Homing = 1,
    }

    public enum InlineMotionPresetType
    {
        None = 0,
        GravityPull = 1,
        Arc = 2,
        BounceToTarget = 3,
        OrbitApproach = 4,
        Spiral = 5,
        Wave = 6,
    }

    [Serializable]
    public sealed class SetMovementModuleCommandData : ICommandData, ISerializationCallbackReceiver
    {
        public int CommandId => CommandIds.SetMovementModule;
        public string DebugData => Target.ToString();

        [BoxGroup("Target")]
        [LabelText("Module Target")]
        [EnumToggleButtons]
        [SerializeField]
        public MovementModuleTarget Target = MovementModuleTarget.Motion;

        [BoxGroup("Motion"), ShowIf(nameof(IsMotionTarget))]
        [LabelText("Preset Type")]
        [EnumToggleButtons]
        [SerializeField]
        public InlineMotionPresetType MotionPresetType = InlineMotionPresetType.None;

        [BoxGroup("Motion"), ShowIf(nameof(IsGravityPullPresetVisible))]
        [LabelText("Gravity Pull")]
        [SerializeField, InlineProperty, HideLabel]
        public GravityPullMotionPreset GravityPullPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsArcPresetVisible))]
        [LabelText("Arc")]
        [SerializeField, InlineProperty, HideLabel]
        public ArcMotionPreset ArcPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsBounceToTargetPresetVisible))]
        [LabelText("Bounce To Target")]
        [SerializeField, InlineProperty, HideLabel]
        public BounceToTargetMotionPreset BounceToTargetPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsOrbitApproachPresetVisible))]
        [LabelText("Orbit Approach")]
        [SerializeField, InlineProperty, HideLabel]
        public OrbitApproachMotionPreset OrbitApproachPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsSpiralPresetVisible))]
        [LabelText("Spiral")]
        [SerializeField, InlineProperty, HideLabel]
        public SpiralMotionPreset SpiralPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsWavePresetVisible))]
        [LabelText("Wave")]
        [SerializeField, InlineProperty, HideLabel]
        public WaveMotionPreset WavePreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsMotionTarget))]
        [LabelText("Clear If Null")]
        [SerializeField]
        public bool ClearMotionIfNull = true;

        [BoxGroup("Homing"), ShowIf(nameof(IsHomingTarget))]
        [LabelText("Layer Key")]
        [SerializeField]
        public string HomingLayerKey = "command";

        [BoxGroup("Homing"), ShowIf(nameof(IsHomingTarget))]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> HomingEnabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Homing"), ShowIf(nameof(IsHomingTarget))]
        [LabelText("Apply Blend Params")]
        [SerializeField]
        public bool ApplyBlendParams = false;

        [BoxGroup("Homing"), ShowIf(nameof(IsBlendParamsVisible))]
        [LabelText("Blend Params")]
        [SerializeField, InlineProperty, HideLabel]
        public HomingBlendParams BlendParams = HomingBlendParams.Default;

        public MotionPreset? ResolveMotionPreset()
        {
            EnsureInlineMotionPresetsInitialized();
            return MotionPresetType switch
            {
                InlineMotionPresetType.GravityPull => GravityPullPreset,
                InlineMotionPresetType.Arc => ArcPreset,
                InlineMotionPresetType.BounceToTarget => BounceToTargetPreset,
                InlineMotionPresetType.OrbitApproach => OrbitApproachPreset,
                InlineMotionPresetType.Spiral => SpiralPreset,
                InlineMotionPresetType.Wave => WavePreset,
                _ => null
            };
        }

        public void EnsureInlineMotionPresetsInitialized()
        {
            GravityPullPreset ??= new();
            ArcPreset ??= new();
            BounceToTargetPreset ??= new();
            OrbitApproachPreset ??= new();
            SpiralPreset ??= new();
            WavePreset ??= new();
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            EnsureInlineMotionPresetsInitialized();
        }

        bool IsMotionTarget() => Target == MovementModuleTarget.Motion;
        bool IsHomingTarget() => Target == MovementModuleTarget.Homing;
        bool IsGravityPullPresetVisible() => IsMotionTarget() && MotionPresetType == InlineMotionPresetType.GravityPull;
        bool IsArcPresetVisible() => IsMotionTarget() && MotionPresetType == InlineMotionPresetType.Arc;
        bool IsBounceToTargetPresetVisible() => IsMotionTarget() && MotionPresetType == InlineMotionPresetType.BounceToTarget;
        bool IsOrbitApproachPresetVisible() => IsMotionTarget() && MotionPresetType == InlineMotionPresetType.OrbitApproach;
        bool IsSpiralPresetVisible() => IsMotionTarget() && MotionPresetType == InlineMotionPresetType.Spiral;
        bool IsWavePresetVisible() => IsMotionTarget() && MotionPresetType == InlineMotionPresetType.Wave;
        bool IsBlendParamsVisible() => IsHomingTarget() && ApplyBlendParams;

    }

    [Serializable]
    public sealed class SetInputMovementCommandData : ICommandData, ISerializationCallbackReceiver
    {
        public int CommandId => CommandIds.SetInputMovement;
        public string DebugData
        {
            get
            {
                var hasSpeed = ApplySpeed ? "S" : "-";
                var hasEnabled = ApplyEnabled ? "E" : "-";
                var hasMotion = ApplyMotion ? "M" : "-";
                var hasHoming = ApplyHoming ? "H" : "-";
                var hasAcceleration = ApplyAcceleration ? "A" : "-";
                return $"{hasSpeed}{hasEnabled}{hasMotion}{hasHoming}{hasAcceleration}";
            }
        }

        [BoxGroup("Apply")]
        [LabelText("Apply Speed")]
        [FormerlySerializedAs("ApplyVelocity")]
        [SerializeField]
        public bool ApplySpeed;

        [BoxGroup("Speed"), ShowIf(nameof(ShowSpeedFields))]
        [LabelText("Speed Multiplier")]
        [SerializeField]
        public DynamicValue<float> SpeedMultiplier = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Apply")]
        [LabelText("Apply Input Enabled")]
        [SerializeField]
        public bool ApplyEnabled;

        [BoxGroup("Input"), ShowIf(nameof(ShowEnabledFields))]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> Enabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Apply")]
        [LabelText("Apply Motion")]
        [SerializeField]
        public bool ApplyMotion;

        [BoxGroup("Motion"), ShowIf(nameof(ShowMotionFields))]
        [LabelText("Preset Type")]
        [EnumToggleButtons]
        [SerializeField]
        public InlineMotionPresetType MotionPresetType = InlineMotionPresetType.None;

        [BoxGroup("Motion"), ShowIf(nameof(IsGravityPullPresetVisible))]
        [LabelText("Gravity Pull")]
        [SerializeField, InlineProperty, HideLabel]
        public GravityPullMotionPreset GravityPullPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsArcPresetVisible))]
        [LabelText("Arc")]
        [SerializeField, InlineProperty, HideLabel]
        public ArcMotionPreset ArcPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsBounceToTargetPresetVisible))]
        [LabelText("Bounce To Target")]
        [SerializeField, InlineProperty, HideLabel]
        public BounceToTargetMotionPreset BounceToTargetPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsOrbitApproachPresetVisible))]
        [LabelText("Orbit Approach")]
        [SerializeField, InlineProperty, HideLabel]
        public OrbitApproachMotionPreset OrbitApproachPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsSpiralPresetVisible))]
        [LabelText("Spiral")]
        [SerializeField, InlineProperty, HideLabel]
        public SpiralMotionPreset SpiralPreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(IsWavePresetVisible))]
        [LabelText("Wave")]
        [SerializeField, InlineProperty, HideLabel]
        public WaveMotionPreset WavePreset = new();

        [BoxGroup("Motion"), ShowIf(nameof(ShowMotionFields))]
        [LabelText("Clear Motion If Null")]
        [SerializeField]
        public bool ClearMotionIfNull = true;

        [BoxGroup("Apply")]
        [LabelText("Apply Homing")]
        [SerializeField]
        public bool ApplyHoming;

        [BoxGroup("Homing"), ShowIf(nameof(ShowHomingFields))]
        [LabelText("Layer Key")]
        [SerializeField]
        public string HomingLayerKey = "command";

        [BoxGroup("Homing"), ShowIf(nameof(ShowHomingFields))]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> HomingEnabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Homing"), ShowIf(nameof(ShowHomingFields))]
        [LabelText("Apply Blend Params")]
        [SerializeField]
        public bool ApplyBlendParams;

        [BoxGroup("Homing"), ShowIf(nameof(ShowHomingBlendFields))]
        [LabelText("Blend Params")]
        [SerializeField, InlineProperty, HideLabel]
        public HomingBlendParams BlendParams = HomingBlendParams.Default;

        [BoxGroup("Apply")]
        [LabelText("Apply Acceleration")]
        [SerializeField]
        public bool ApplyAcceleration;

        [BoxGroup("Acceleration"), ShowIf(nameof(ShowAccelerationFields))]
        [LabelText("Enabled")]
        [SerializeField]
        public DynamicValue<bool> AccelerationEnabled = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Acceleration"), ShowIf(nameof(ShowAccelerationFields))]
        [LabelText("Accel")]
        [SerializeField]
        public DynamicValue<float> Accel = DynamicValueExtensions.FromLiteral(8f);

        [BoxGroup("Acceleration"), ShowIf(nameof(ShowAccelerationFields))]
        [LabelText("Decel")]
        [SerializeField]
        public DynamicValue<float> Decel = DynamicValueExtensions.FromLiteral(8f);

        public MotionPreset? ResolveMotionPreset()
        {
            EnsureInlineMotionPresetsInitialized();
            return MotionPresetType switch
            {
                InlineMotionPresetType.GravityPull => GravityPullPreset,
                InlineMotionPresetType.Arc => ArcPreset,
                InlineMotionPresetType.BounceToTarget => BounceToTargetPreset,
                InlineMotionPresetType.OrbitApproach => OrbitApproachPreset,
                InlineMotionPresetType.Spiral => SpiralPreset,
                InlineMotionPresetType.Wave => WavePreset,
                _ => null
            };
        }

        public void EnsureInlineMotionPresetsInitialized()
        {
            GravityPullPreset ??= new();
            ArcPreset ??= new();
            BounceToTargetPreset ??= new();
            OrbitApproachPreset ??= new();
            SpiralPreset ??= new();
            WavePreset ??= new();
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            EnsureInlineMotionPresetsInitialized();
        }

        bool ShowSpeedFields() => ApplySpeed;
        bool ShowEnabledFields() => ApplyEnabled;
        bool ShowMotionFields() => ApplyMotion;
        bool ShowHomingFields() => ApplyHoming;
        bool ShowHomingBlendFields() => ApplyHoming && ApplyBlendParams;
        bool ShowAccelerationFields() => ApplyAcceleration;
        bool IsGravityPullPresetVisible() => ApplyMotion && MotionPresetType == InlineMotionPresetType.GravityPull;
        bool IsArcPresetVisible() => ApplyMotion && MotionPresetType == InlineMotionPresetType.Arc;
        bool IsBounceToTargetPresetVisible() => ApplyMotion && MotionPresetType == InlineMotionPresetType.BounceToTarget;
        bool IsOrbitApproachPresetVisible() => ApplyMotion && MotionPresetType == InlineMotionPresetType.OrbitApproach;
        bool IsSpiralPresetVisible() => ApplyMotion && MotionPresetType == InlineMotionPresetType.Spiral;
        bool IsWavePresetVisible() => ApplyMotion && MotionPresetType == InlineMotionPresetType.Wave;
    }
}
