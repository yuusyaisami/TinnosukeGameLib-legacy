#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Kernel.Abstractions;
using Game.DI;
using Game.Kernel.Diagnostics;
using UnityEngine;

namespace Game.Kernel.Layers
{
    public readonly struct SceneKernelEntityLeaseHandle : IEquatable<SceneKernelEntityLeaseHandle>
    {
        public SceneKernelEntityLeaseHandle(SceneKernelHandle sceneHandle, EntityRef entityRef, int leaseId, int generation)
        {
            if (entityRef.IsEmpty)
                throw new ArgumentException("SceneKernel entity leases require a non-empty EntityRef.", nameof(entityRef));

            if (leaseId <= 0)
                throw new ArgumentOutOfRangeException(nameof(leaseId), leaseId, "SceneKernel entity leases require a positive lease id.");

            if (generation <= 0)
                throw new ArgumentOutOfRangeException(nameof(generation), generation, "SceneKernel entity leases require a positive generation.");

            SceneHandle = sceneHandle;
            EntityRef = entityRef;
            LeaseId = leaseId;
            Generation = generation;
        }

        public SceneKernelHandle SceneHandle { get; }

        public EntityRef EntityRef { get; }

        public int LeaseId { get; }

        public int Generation { get; }

        public bool Equals(SceneKernelEntityLeaseHandle other)
        {
            return SceneHandle.Equals(other.SceneHandle)
                && EntityRef.Equals(other.EntityRef)
                && LeaseId == other.LeaseId
                && Generation == other.Generation;
        }

        public override bool Equals(object? obj)
        {
            return obj is SceneKernelEntityLeaseHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SceneHandle.GetHashCode();
                hash = (hash * 397) ^ EntityRef.GetHashCode();
                hash = (hash * 397) ^ LeaseId;
                hash = (hash * 397) ^ Generation;
                return hash;
            }
        }

        public override string ToString()
        {
            return "SceneKernelEntityLeaseHandle(SceneHandle=" + SceneHandle + ", EntityRef=" + EntityRef + ", LeaseId=" + LeaseId + ", Generation=" + Generation + ")";
        }

        public static bool operator ==(SceneKernelEntityLeaseHandle left, SceneKernelEntityLeaseHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SceneKernelEntityLeaseHandle left, SceneKernelEntityLeaseHandle right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct SceneKernelSpawnRouteId : IEquatable<SceneKernelSpawnRouteId>
    {
        readonly string? value;

        public SceneKernelSpawnRouteId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("SceneKernel spawn route ids must be non-empty.", nameof(value));

            this.value = value.Trim();
        }

        public string Value => value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(value);

        public static SceneKernelSpawnRouteId FromParts(string routeKind, string tag)
        {
            if (string.IsNullOrWhiteSpace(routeKind))
                throw new ArgumentException("SceneKernel spawn route ids require a non-empty route kind.", nameof(routeKind));

            string normalizedKind = routeKind.Trim();
            string normalizedTag = NormalizeTag(tag);
            return string.IsNullOrEmpty(normalizedTag)
                ? new SceneKernelSpawnRouteId(normalizedKind)
                : new SceneKernelSpawnRouteId(normalizedKind + ":" + normalizedTag);
        }

        public bool Equals(SceneKernelSpawnRouteId other)
        {
            return StringComparer.Ordinal.Equals(Value, other.Value);
        }

        public override bool Equals(object? obj)
        {
            return obj is SceneKernelSpawnRouteId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(SceneKernelSpawnRouteId left, SceneKernelSpawnRouteId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SceneKernelSpawnRouteId left, SceneKernelSpawnRouteId right)
        {
            return !left.Equals(right);
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return string.Empty;

            string normalizedTag = tag.Trim();
            return string.Equals(normalizedTag, "default", StringComparison.OrdinalIgnoreCase) ? string.Empty : normalizedTag;
        }
    }

    public readonly struct SceneKernelSpawnPoolId : IEquatable<SceneKernelSpawnPoolId>
    {
        readonly string? value;

        public SceneKernelSpawnPoolId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("SceneKernel spawn pool ids must be non-empty.", nameof(value));

            this.value = value.Trim();
        }

        public string Value => value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(value);

        public static SceneKernelSpawnPoolId FromParts(string routeKind, string tag)
        {
            if (string.IsNullOrWhiteSpace(routeKind))
                throw new ArgumentException("SceneKernel spawn pool ids require a non-empty route kind.", nameof(routeKind));

            string normalizedKind = routeKind.Trim();
            string normalizedTag = NormalizeTag(tag);
            return string.IsNullOrEmpty(normalizedTag)
                ? new SceneKernelSpawnPoolId(normalizedKind)
                : new SceneKernelSpawnPoolId(normalizedKind + ":" + normalizedTag);
        }

        public bool Equals(SceneKernelSpawnPoolId other)
        {
            return StringComparer.Ordinal.Equals(Value, other.Value);
        }

        public override bool Equals(object? obj)
        {
            return obj is SceneKernelSpawnPoolId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(SceneKernelSpawnPoolId left, SceneKernelSpawnPoolId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SceneKernelSpawnPoolId left, SceneKernelSpawnPoolId right)
        {
            return !left.Equals(right);
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return string.Empty;

            string normalizedTag = tag.Trim();
            return string.Equals(normalizedTag, "default", StringComparison.OrdinalIgnoreCase) ? string.Empty : normalizedTag;
        }
    }

    public enum SceneKernelReleaseReason
    {
        Unknown = 0,
        Despawn = 10,
        BulkRelease = 20,
        Shutdown = 30,
        SpawnFailure = 40,
    }

    public readonly struct SceneKernelSpawnRequest
    {
        public SceneKernelSpawnRequest(
            SceneKernelSpawnRouteId routeId,
            BaseRuntimeTemplateSO template,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            EntityRef entityRef = default,
            Transform? parent = null,
            Transform? parkingRoot = null,
            bool worldSpace = true,
            bool allowPooling = true)
        {
            if (routeId.IsEmpty)
                throw new ArgumentException("SceneKernel spawn requests require a non-empty route id.", nameof(routeId));

            Template = template ?? throw new ArgumentNullException(nameof(template));
            RouteId = routeId;
            EntityRef = entityRef;
            Parent = parent;
            ParkingRoot = parkingRoot;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            WorldSpace = worldSpace;
            AllowPooling = allowPooling;
        }

        public SceneKernelSpawnRouteId RouteId { get; }

        public BaseRuntimeTemplateSO Template { get; }

        public EntityRef EntityRef { get; }

        public Transform? Parent { get; }

        public Transform? ParkingRoot { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public Vector3 Scale { get; }

        public bool WorldSpace { get; }

        public bool AllowPooling { get; }
    }

    public readonly struct SceneKernelWarmupRequest
    {
        public SceneKernelWarmupRequest(
            SceneKernelSpawnRouteId routeId,
            BaseRuntimeTemplateSO template,
            int count,
            Transform? parkingRoot = null)
        {
            if (routeId.IsEmpty)
                throw new ArgumentException("SceneKernel warmup requests require a non-empty route id.", nameof(routeId));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "SceneKernel warmup requests require a non-negative count.");

            RouteId = routeId;
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Count = count;
            ParkingRoot = parkingRoot;
        }

        public SceneKernelSpawnRouteId RouteId { get; }

        public BaseRuntimeTemplateSO Template { get; }

        public int Count { get; }

        public Transform? ParkingRoot { get; }
    }

    public readonly struct SceneKernelBulkReleaseQuery
    {
        public SceneKernelBulkReleaseQuery(
            SceneKernelSpawnRouteId? routeId = null,
            EntityRef entityRef = default,
            SceneKernelReleaseReason reason = SceneKernelReleaseReason.BulkRelease,
            bool includeInactive = true)
        {
            RouteId = routeId;
            EntityRef = entityRef;
            Reason = reason;
            IncludeInactive = includeInactive;
        }

        public SceneKernelSpawnRouteId? RouteId { get; }

        public EntityRef EntityRef { get; }

        public SceneKernelReleaseReason Reason { get; }

        public bool IncludeInactive { get; }

        public bool HasRouteFilter => RouteId.HasValue && !RouteId.Value.IsEmpty;

        public bool HasEntityFilter => !EntityRef.IsEmpty;
    }

    public readonly struct SceneKernelSpawnResult
    {
        public SceneKernelSpawnResult(SceneKernelEntityLeaseHandle lease, GameObject? spawnedRoot)
        {
            Succeeded = true;
            Lease = lease;
            SpawnedRoot = spawnedRoot;
            Diagnostic = null;
        }

        public SceneKernelSpawnResult(KernelDiagnostic diagnostic)
        {
            Succeeded = false;
            Lease = default;
            SpawnedRoot = null;
            Diagnostic = diagnostic;
        }

        public bool Succeeded { get; }

        public SceneKernelEntityLeaseHandle Lease { get; }

        public GameObject? SpawnedRoot { get; }

        public KernelDiagnostic? Diagnostic { get; }
    }

    public readonly struct SceneKernelWarmupResult
    {
        public SceneKernelWarmupResult(int warmedCount)
        {
            Succeeded = true;
            WarmedCount = warmedCount;
            Diagnostic = null;
        }

        public SceneKernelWarmupResult(KernelDiagnostic diagnostic)
        {
            Succeeded = false;
            WarmedCount = 0;
            Diagnostic = diagnostic;
        }

        public bool Succeeded { get; }

        public int WarmedCount { get; }

        public KernelDiagnostic? Diagnostic { get; }
    }

    public readonly struct SceneKernelReleaseResult
    {
        public SceneKernelReleaseResult(int releasedCount)
        {
            Succeeded = true;
            ReleasedCount = releasedCount;
            Diagnostic = null;
        }

        public SceneKernelReleaseResult(KernelDiagnostic diagnostic)
        {
            Succeeded = false;
            ReleasedCount = 0;
            Diagnostic = diagnostic;
        }

        public bool Succeeded { get; }

        public int ReleasedCount { get; }

        public KernelDiagnostic? Diagnostic { get; }
    }

    public readonly struct SceneKernelBulkReleaseResult
    {
        public SceneKernelBulkReleaseResult(int releasedCount)
        {
            Succeeded = true;
            ReleasedCount = releasedCount;
            Diagnostic = null;
        }

        public SceneKernelBulkReleaseResult(KernelDiagnostic diagnostic)
        {
            Succeeded = false;
            ReleasedCount = 0;
            Diagnostic = diagnostic;
        }

        public bool Succeeded { get; }

        public int ReleasedCount { get; }

        public KernelDiagnostic? Diagnostic { get; }
    }

    public interface ISceneKernelPrefabPool
    {
        SceneKernelSpawnPoolId PoolId { get; }

        SceneKernelSpawnResult Spawn(in SceneKernelSpawnContext context);

        SceneKernelWarmupResult Warmup(in SceneKernelWarmupRequest request);

        SceneKernelReleaseResult Release(SceneKernelEntityLeaseHandle lease, SceneKernelReleaseReason reason);

        SceneKernelBulkReleaseResult ReleaseAll(in SceneKernelBulkReleaseQuery query);

        void Shutdown(SceneKernelReleaseReason reason);
    }

    public interface ISceneKernelSpawnPool
    {
        SceneKernelSpawnPoolId PoolId { get; }

        int ReleaseAll(object filter);
    }

    public interface ISceneKernelSpawnRouteHandler
    {
        SceneKernelSpawnRouteId RouteId { get; }

        ValueTask<object?> SpawnAsync(object spawnRequest, CancellationToken cancellationToken);

        ValueTask WarmupAsync(object template, int count, CancellationToken cancellationToken);
    }

    public interface ISceneKernelSpawnBoundary
    {
        bool IsOperational { get; }

        int ActiveLeaseCount { get; }

        bool TryAcquireLease(EntityRef entityRef, out SceneKernelEntityLeaseHandle lease);

        bool TryBindSpawnRoute(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId);

        bool TryBindSpawnPool(ISceneKernelSpawnPool pool);

        bool TryBindSpawnPool(ISceneKernelPrefabPool pool);

        bool TryBindSpawnRouteHandler(ISceneKernelSpawnRouteHandler handler);

        bool TryResolveSpawnPool(SceneKernelSpawnRouteId routeId, out ISceneKernelSpawnPool pool);

        bool TryResolveSpawnPool(SceneKernelSpawnRouteId routeId, out ISceneKernelPrefabPool pool);

        bool TryResolveSpawnRouteHandler(SceneKernelSpawnRouteId routeId, out ISceneKernelSpawnRouteHandler handler);

        bool TryReleaseAll(SceneKernelSpawnRouteId routeId, object filter, out int releasedCount);

        ValueTask<SceneKernelSpawnResult> SpawnAsync(SceneKernelSpawnRequest request, CancellationToken cancellationToken);

        ValueTask<SceneKernelWarmupResult> WarmupAsync(SceneKernelWarmupRequest request, CancellationToken cancellationToken);

        SceneKernelReleaseResult Release(SceneKernelEntityLeaseHandle lease, SceneKernelReleaseReason reason);

        SceneKernelBulkReleaseResult ReleaseAll(SceneKernelBulkReleaseQuery query);

        bool TryGetLease(EntityRef entityRef, out SceneKernelEntityLeaseHandle lease);

        bool ValidateLease(SceneKernelEntityLeaseHandle lease);

        bool TryReleaseLease(SceneKernelEntityLeaseHandle lease);
    }
}