#nullable enable
using System.Collections.Generic;
using Game.Movement;
using UnityEngine;

namespace Game.TransformSystem
{
    public enum TransformChannelOutputTarget
    {
        None = 0,
        Transform = 10,
        RectTransform = 20,
        BulkTransform = 30,
        Rigidbody2D = 40,
        CharacterController = 50,
    }

    public enum TransformChannelGlobalBlendMode
    {
        Override = 10,
        Additive = 20,
        Multiply = 30,
    }

    public static class TransformChannelTagUtility
    {
        public const string DefaultTag = "default";

        public static string Normalize(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? DefaultTag : tag.Trim();
        }
    }

    public interface ITransformChannelPoseReader
    {
        Transform TargetTransform { get; }
        Vector2 CurrentVelocity { get; }
        Quaternion CurrentWorldRotation { get; }
    }

    public interface ITransformChannelRigidbodyControl
    {
        void SetMovementEnabled(bool enabled);
        void SetRotationEnabled(bool enabled);
        void SetForceZeroVelocityWhenMovementBlocked(bool enabled);
        bool SetTransformChannelMovementBlocked(bool blocked, string? reason = null);
        bool TryApplyRigidbody2DSettings(
            bool applySimulated,
            bool simulated,
            bool applyGravityScale,
            float gravityScale,
            bool applyFreezeRotation,
            bool freezeRotation,
            bool applyLinearVelocity,
            Vector2 linearVelocity,
            bool applyAngularVelocity,
            float angularVelocity,
            Rigidbody2DVelocityApplyMode linearVelocityMode = Rigidbody2DVelocityApplyMode.Override);
        bool TryAddForceToRigidbody2D(Vector2 force, ForceMode2D mode = ForceMode2D.Force);
        void ForceStopMovementNow();
        void SetInitialRotation(Quaternion rotation);
    }

    public interface ITransformChannelRuntime : ITransformTeleportService, ITransformChannelPoseReader, ITransformChannelRigidbodyControl
    {
        string Tag { get; }
        TransformChannelOutputTarget OutputTarget { get; }
    }

    internal interface ITransformChannelRuntimeDebugTelemetry
    {
        bool EnableMovement { get; }
        bool EnableRotation { get; }
        bool EnableScale { get; }
        IReadOnlyList<TransformManagerChannelApplyRequest> GlobalApplyRequests { get; }
        Vector2 RigidbodyLinearVelocity { get; }
        Vector2 RigidbodyOverlayVelocity { get; }
        Vector2 LastAppliedGlobalBaseVelocity { get; }
        Vector2 LastAppliedGlobalVelocityDelta { get; }
        Vector2 LastAppliedGlobalVelocity { get; }
        int LastAppliedGlobalFrame { get; }
    }

    public interface ITransformChannelHubService
    {
        int ChannelCount { get; }
        bool Contains(string tag);
        bool TryGetRuntime(string tag, out ITransformChannelRuntime? runtime);
        void GetTags(List<string> output);
    }
}
