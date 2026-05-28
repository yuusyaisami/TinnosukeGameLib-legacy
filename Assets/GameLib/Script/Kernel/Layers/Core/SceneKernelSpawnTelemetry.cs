#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;

namespace Game.Kernel.Layers
{
    public enum SceneKernelSpawnTelemetryKind
    {
        Unknown = 0,
        SpawnRequested = 10,
        SpawnSucceeded = 20,
        WarmupRequested = 30,
        WarmupSucceeded = 40,
        ReleaseSucceeded = 50,
        BulkReleaseSucceeded = 60,
        Shutdown = 70,
        Failure = 80,
    }

    public readonly struct SceneKernelSpawnTelemetryRecord
    {
        public SceneKernelSpawnTelemetryRecord(
            int sequence,
            SceneKernelSpawnTelemetryKind kind,
            SceneKernelSpawnRouteId routeId,
            SceneKernelSpawnPoolId poolId,
            EntityRef entityRef,
            SceneKernelReleaseReason reason,
            int count,
            bool reused,
            string message)
        {
            Sequence = sequence;
            Kind = kind;
            RouteId = routeId;
            PoolId = poolId;
            EntityRef = entityRef;
            Reason = reason;
            Count = count;
            Reused = reused;
            Message = message ?? string.Empty;
        }

        public int Sequence { get; }

        public SceneKernelSpawnTelemetryKind Kind { get; }

        public SceneKernelSpawnRouteId RouteId { get; }

        public SceneKernelSpawnPoolId PoolId { get; }

        public EntityRef EntityRef { get; }

        public SceneKernelReleaseReason Reason { get; }

        public int Count { get; }

        public bool Reused { get; }

        public string Message { get; }
    }

    public sealed class SceneKernelSpawnTelemetry
    {
        readonly List<SceneKernelSpawnTelemetryRecord> recentRecords = new List<SceneKernelSpawnTelemetryRecord>(32);
        readonly int recentRecordCapacity;
        int sequence;

        public SceneKernelSpawnTelemetry(int recentRecordCapacity = 32)
        {
            if (recentRecordCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(recentRecordCapacity), recentRecordCapacity, "SceneKernel spawn telemetry requires a positive record capacity.");

            this.recentRecordCapacity = recentRecordCapacity;
        }

        public int RecentRecordCapacity => recentRecordCapacity;

        public int SpawnRequestCount { get; private set; }

        public int SpawnSuccessCount { get; private set; }

        public int WarmupRequestCount { get; private set; }

        public int WarmupSuccessCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public int DestroyCount { get; private set; }

        public int FailureCount { get; private set; }

        public IReadOnlyList<SceneKernelSpawnTelemetryRecord> RecentRecords => recentRecords;

        public void RecordSpawnRequested(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId, EntityRef entityRef)
        {
            SpawnRequestCount++;
            Append(SceneKernelSpawnTelemetryKind.SpawnRequested, routeId, poolId, entityRef, SceneKernelReleaseReason.Unknown, 1, reused: false, string.Empty);
        }

        public void RecordSpawnSucceeded(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId, EntityRef entityRef, bool reused)
        {
            SpawnSuccessCount++;
            Append(SceneKernelSpawnTelemetryKind.SpawnSucceeded, routeId, poolId, entityRef, SceneKernelReleaseReason.Unknown, 1, reused, string.Empty);
        }

        public void RecordWarmupRequested(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId, int count)
        {
            WarmupRequestCount += Math.Max(0, count);
            Append(SceneKernelSpawnTelemetryKind.WarmupRequested, routeId, poolId, default, SceneKernelReleaseReason.Unknown, count, reused: false, string.Empty);
        }

        public void RecordWarmupSucceeded(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId, int count)
        {
            WarmupSuccessCount += Math.Max(0, count);
            Append(SceneKernelSpawnTelemetryKind.WarmupSucceeded, routeId, poolId, default, SceneKernelReleaseReason.Unknown, count, reused: false, string.Empty);
        }

        public void RecordRelease(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId, EntityRef entityRef, SceneKernelReleaseReason reason, bool destroyed)
        {
            ReleaseCount++;
            if (destroyed)
                DestroyCount++;

            Append(SceneKernelSpawnTelemetryKind.ReleaseSucceeded, routeId, poolId, entityRef, reason, 1, reused: !destroyed, destroyed ? "destroy" : string.Empty);
        }

        public void RecordBulkRelease(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId, int count, SceneKernelReleaseReason reason)
        {
            ReleaseCount += Math.Max(0, count);
            if (reason == SceneKernelReleaseReason.Shutdown || reason == SceneKernelReleaseReason.BulkRelease)
                DestroyCount += Math.Max(0, count);

            Append(SceneKernelSpawnTelemetryKind.BulkReleaseSucceeded, routeId, poolId, default, reason, count, reused: false, string.Empty);
        }

        public void RecordShutdown(SceneKernelSpawnPoolId poolId, int destroyedCount)
        {
            DestroyCount += Math.Max(0, destroyedCount);
            Append(SceneKernelSpawnTelemetryKind.Shutdown, default, poolId, default, SceneKernelReleaseReason.Shutdown, destroyedCount, reused: false, string.Empty);
        }

        public void RecordFailure(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId, EntityRef entityRef, string message)
        {
            FailureCount++;
            Append(SceneKernelSpawnTelemetryKind.Failure, routeId, poolId, entityRef, SceneKernelReleaseReason.Unknown, 0, reused: false, message);
        }

        void Append(
            SceneKernelSpawnTelemetryKind kind,
            SceneKernelSpawnRouteId routeId,
            SceneKernelSpawnPoolId poolId,
            EntityRef entityRef,
            SceneKernelReleaseReason reason,
            int count,
            bool reused,
            string message)
        {
            sequence++;
            recentRecords.Add(new SceneKernelSpawnTelemetryRecord(sequence, kind, routeId, poolId, entityRef, reason, count, reused, message));

            if (recentRecords.Count > recentRecordCapacity)
                recentRecords.RemoveAt(0);
        }
    }
}