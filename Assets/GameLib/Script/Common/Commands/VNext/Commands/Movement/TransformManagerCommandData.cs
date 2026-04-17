#nullable enable
using System;
using Game.Common;
using Game.TransformSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    public enum TransformManagerEntryOperation
    {
        Upsert = 10,
        Remove = 20,
    }

    [Serializable]
    public sealed class TransformManagerMovementCommandData : ICommandData
    {
        public int CommandId => CommandIds.TransformManagerMovement;
        public string DebugData => $"TransformManagerMovement Op={Operation} Entry={NormalizedEntryId}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [BoxGroup("Entry")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        [SerializeField]
        public TransformManagerEntryOperation Operation = TransformManagerEntryOperation.Upsert;

        [BoxGroup("Entry")]
        [LabelText("Entry Id")]
        [Tooltip("このグローバルエントリ専用のスロットキーです。同じエントリIDでUpsertを行うと、そのスロットが上書きされます。チャネルタグはターゲットのフィルタリングを制御するものであり、識別子ではありません。")]
        [SerializeField]
        public string EntryId = string.Empty;

        [BoxGroup("Entry")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Apply All Channels")]
        [SerializeField]
        public bool ApplyAllChannels = true;

        [BoxGroup("Entry")]
        [ShowIf(nameof(ShowChannelTag))]
        [LabelText("Channel Tag")]
        [SerializeField]
        public string ChannelTag = TransformChannelTagUtility.DefaultTag;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Priority")]
        [SerializeField]
        public int Priority;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Blend Mode")]
        [SerializeField]
        public TransformChannelGlobalBlendMode BlendMode = TransformChannelGlobalBlendMode.Additive;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Weight")]
        [MinValue(0f)]
        [SerializeField]
        public float Weight = 1f;

        [BoxGroup("Entry")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Condition")]
        [SerializeField]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Lifetime")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("One Shot")]
        [SerializeField]
        public bool OneShot;

        [BoxGroup("Lifetime")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Duration Seconds")]
        [MinValue(0f)]
        [SerializeField]
        public float DurationSeconds;

        [BoxGroup("Value")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Velocity")]
        [SerializeField]
        public DynamicValue<Vector2> Velocity = DynamicValueExtensions.FromLiteral(Vector2.zero);

        bool UseEntrySettings => Operation == TransformManagerEntryOperation.Upsert;
        bool ShowChannelTag => UseEntrySettings && !ApplyAllChannels;

        public string NormalizedEntryId => string.IsNullOrWhiteSpace(EntryId) ? string.Empty : EntryId.Trim();

        public bool TryBuildSettings(out TransformManagerEntrySettings settings)
        {
            settings = default;
            if (!UseEntrySettings)
                return false;

            if (string.IsNullOrWhiteSpace(NormalizedEntryId))
                return false;

            settings = new TransformManagerEntrySettings(
                NormalizedEntryId,
                ApplyAllChannels,
                ChannelTag,
                Priority,
                BlendMode,
                Weight,
                Condition,
                0,
                OneShot,
                DurationSeconds);
            return true;
        }
    }

    [Serializable]
    public sealed class TransformManagerRotateCommandData : ICommandData
    {
        public int CommandId => CommandIds.TransformManagerRotate;
        public string DebugData => $"TransformManagerRotate Op={Operation} Entry={NormalizedEntryId}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [BoxGroup("Entry")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        [SerializeField]
        public TransformManagerEntryOperation Operation = TransformManagerEntryOperation.Upsert;

        [BoxGroup("Entry")]
        [LabelText("Entry Id")]
        [Tooltip("Unique slot key for this global entry. Upsert with the same Entry Id replaces that slot. Channel Tag controls target filtering, not identity.")]
        [SerializeField]
        public string EntryId = string.Empty;

        [BoxGroup("Entry")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Apply All Channels")]
        [SerializeField]
        public bool ApplyAllChannels = true;

        [BoxGroup("Entry")]
        [ShowIf(nameof(ShowChannelTag))]
        [LabelText("Channel Tag")]
        [SerializeField]
        public string ChannelTag = TransformChannelTagUtility.DefaultTag;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Priority")]
        [SerializeField]
        public int Priority;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Blend Mode")]
        [SerializeField]
        public TransformChannelGlobalBlendMode BlendMode = TransformChannelGlobalBlendMode.Additive;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Weight")]
        [MinValue(0f)]
        [SerializeField]
        public float Weight = 1f;

        [BoxGroup("Entry")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Condition")]
        [SerializeField]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Lifetime")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("One Shot")]
        [SerializeField]
        public bool OneShot;

        [BoxGroup("Lifetime")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Duration Seconds")]
        [MinValue(0f)]
        [SerializeField]
        public float DurationSeconds;

        [BoxGroup("Value")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Offset Degrees")]
        [SerializeField]
        public DynamicValue<float> OffsetDegrees = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Value")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Angular Velocity")]
        [SerializeField]
        public DynamicValue<float> AngularVelocity = DynamicValueExtensions.FromLiteral(0f);

        bool UseEntrySettings => Operation == TransformManagerEntryOperation.Upsert;
        bool ShowChannelTag => UseEntrySettings && !ApplyAllChannels;

        public string NormalizedEntryId => string.IsNullOrWhiteSpace(EntryId) ? string.Empty : EntryId.Trim();

        public bool TryBuildSettings(out TransformManagerEntrySettings settings)
        {
            settings = default;
            if (!UseEntrySettings)
                return false;

            if (string.IsNullOrWhiteSpace(NormalizedEntryId))
                return false;

            settings = new TransformManagerEntrySettings(
                NormalizedEntryId,
                ApplyAllChannels,
                ChannelTag,
                Priority,
                BlendMode,
                Weight,
                Condition,
                0,
                OneShot,
                DurationSeconds);
            return true;
        }
    }

    [Serializable]
    public sealed class TransformManagerScaleCommandData : ICommandData
    {
        public int CommandId => CommandIds.TransformManagerScale;
        public string DebugData => $"TransformManagerScale Op={Operation} Entry={NormalizedEntryId}";

        [BoxGroup("Target")]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(Target)")]
        [SerializeField]
        public ActorSource Target;

        [BoxGroup("Entry")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        [SerializeField]
        public TransformManagerEntryOperation Operation = TransformManagerEntryOperation.Upsert;

        [BoxGroup("Entry")]
        [LabelText("Entry Id")]
        [Tooltip("Unique slot key for this global entry. Upsert with the same Entry Id replaces that slot. Channel Tag controls target filtering, not identity.")]
        [SerializeField]
        public string EntryId = string.Empty;

        [BoxGroup("Entry")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Apply All Channels")]
        [SerializeField]
        public bool ApplyAllChannels = true;

        [BoxGroup("Entry")]
        [ShowIf(nameof(ShowChannelTag))]
        [LabelText("Channel Tag")]
        [SerializeField]
        public string ChannelTag = TransformChannelTagUtility.DefaultTag;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Priority")]
        [SerializeField]
        public int Priority;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Blend Mode")]
        [SerializeField]
        public TransformChannelGlobalBlendMode BlendMode = TransformChannelGlobalBlendMode.Additive;

        [BoxGroup("Blend")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Weight")]
        [MinValue(0f)]
        [SerializeField]
        public float Weight = 1f;

        [BoxGroup("Entry")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Condition")]
        [SerializeField]
        public DynamicValue<bool> Condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Lifetime")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("One Shot")]
        [SerializeField]
        public bool OneShot;

        [BoxGroup("Lifetime")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Duration Seconds")]
        [MinValue(0f)]
        [SerializeField]
        public float DurationSeconds;

        [BoxGroup("Value")]
        [ShowIf(nameof(UseEntrySettings))]
        [LabelText("Local Scale")]
        [SerializeField]
        public DynamicValue<Vector3> LocalScale = DynamicValueExtensions.FromLiteral(Vector3.one);

        bool UseEntrySettings => Operation == TransformManagerEntryOperation.Upsert;
        bool ShowChannelTag => UseEntrySettings && !ApplyAllChannels;

        public string NormalizedEntryId => string.IsNullOrWhiteSpace(EntryId) ? string.Empty : EntryId.Trim();

        public bool TryBuildSettings(out TransformManagerEntrySettings settings)
        {
            settings = default;
            if (!UseEntrySettings)
                return false;

            if (string.IsNullOrWhiteSpace(NormalizedEntryId))
                return false;

            settings = new TransformManagerEntrySettings(
                NormalizedEntryId,
                ApplyAllChannels,
                ChannelTag,
                Priority,
                BlendMode,
                Weight,
                Condition,
                0,
                OneShot,
                DurationSeconds);
            return true;
        }
    }
}
