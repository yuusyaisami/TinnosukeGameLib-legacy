#nullable enable
using System;
using Unity.Mathematics;

namespace Game.TransformSystem
{
    public enum TickPhase
    {
        Pre = 0,
        Default = 1,
        Late = 2,
    }

    public interface ITickPhase
    {
        TickPhase Phase { get; }
    }

    /// <summary>
    /// Transform の stable ハンドル。外部はこれを保持する。
    /// index は swap-remove で変わるため、外部に公開しない。
    /// </summary>
    public readonly struct TransformHandle : IEquatable<TransformHandle>
    {
        public readonly int Id;
        public readonly int Generation;

        public TransformHandle(int id, int generation)
        {
            Id = id;
            Generation = generation;
        }

        public bool IsValid => Id >= 0;
        public static TransformHandle Invalid => new(-1, 0);

        public bool Equals(TransformHandle other) => Id == other.Id && Generation == other.Generation;
        public override bool Equals(object? obj) => obj is TransformHandle h && Equals(h);
        public override int GetHashCode() => HashCode.Combine(Id, Generation);
        public static bool operator ==(TransformHandle a, TransformHandle b) => a.Equals(b);
        public static bool operator !=(TransformHandle a, TransformHandle b) => !a.Equals(b);
        public override string ToString() => $"TransformHandle({Id}:{Generation})";
    }

    /// <summary>
    /// swap 時に dense index を更新する通知用。
    /// </summary>
    public interface ITransformIndexReceiver
    {
        int DenseIndex { get; set; }
    }

    public interface IBulkTransformManager : VContainer.Unity.ITickable
    {
        int Count { get; }
        int MaxCapacity { get; }

        TransformHandle Register(
            UnityEngine.Transform transform,
            float3 initialVelocity = default,
            float initialAngularVelocity = 0f,
            ITransformIndexReceiver? receiver = null);

        bool Unregister(TransformHandle handle);
        bool IsValid(TransformHandle handle);

        void SetVelocity(TransformHandle handle, float3 velocity);
        bool TryGetVelocity(TransformHandle handle, out float3 velocity);
        void Teleport(TransformHandle handle, float3 position);
        bool TryGetPosition(TransformHandle handle, out float3 position);

        void SetAngularVelocity(TransformHandle handle, float angularVelocity);
        bool TryGetAngularVelocity(TransformHandle handle, out float angularVelocity);
        void SetRotation(TransformHandle handle, float rotation);
        bool TryGetRotation(TransformHandle handle, out float rotation);

        void SetAllVelocities(float3 velocity);
        void SetAllAngularVelocities(float angularVelocity);

        void Tick(float deltaTime);
        void TickAsync(float deltaTime);

        void CleanupDestroyed();
    }

    /// <summary>
    /// Optional bridge API for systems that need to interact with BulkTransform by Transform reference
    /// (e.g., teleport fallback that directly writes Transform.position).
    ///
    /// BulkTransform is authoritative once a Transform is registered; if callers only write Transform.position,
    /// BulkTransform may snap it back on the next tick. Implementations can use this bridge to sync their
    /// internal buffers.
    /// </summary>
    public interface IBulkTransformTransformBridge
    {
        /// <summary>
        /// Returns true if the given transform is currently managed by BulkTransform.
        /// </summary>
        bool IsManaged(UnityEngine.Transform transform);

        /// <summary>
        /// If managed, updates the internal position buffer for the given Transform.
        /// Does not throw; returns false when not managed.
        /// </summary>
        bool TryTeleportTransform(UnityEngine.Transform transform, float3 position);

        /// <summary>
        /// If managed, reads the internal position buffer for the given Transform.
        /// </summary>
        bool TryGetManagedPosition(UnityEngine.Transform transform, out float3 position);
    }
}
