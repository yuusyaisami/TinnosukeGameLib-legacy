#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Kernel.Abstractions;
using Game.Kernel.Diagnostics;
using UnityEngine;

namespace Game.Kernel.Layers
{
    internal sealed class SceneKernelSpawnBoundary : ISceneKernelSpawnBoundary
    {
        readonly SceneKernelHandle sceneHandle;
        readonly SceneKernelEntityLeaseTable leaseTable;
        readonly SceneKernelSpawnRouteTable routeTable;
        readonly SceneKernelSpawnRouteHandlerTable routeHandlerTable;
        readonly SceneKernelSpawnPoolTable poolTable;
        readonly SceneKernelPrefabPoolTable prefabPoolTable;
        readonly SceneKernelSpawnTelemetry spawnTelemetry;
        readonly Dictionary<SceneKernelEntityLeaseHandle, SceneKernelSpawnPoolId> activePoolIdsByLease = new Dictionary<SceneKernelEntityLeaseHandle, SceneKernelSpawnPoolId>();
        readonly List<SceneKernelEntityLeaseHandle> leaseBuffer = new List<SceneKernelEntityLeaseHandle>(16);
        int spawnOrdinal;
        bool isOperational;

        public SceneKernelSpawnBoundary(SceneKernelHandle sceneHandle)
        {
            this.sceneHandle = sceneHandle;
            leaseTable = new SceneKernelEntityLeaseTable(sceneHandle);
            routeTable = new SceneKernelSpawnRouteTable();
            routeHandlerTable = new SceneKernelSpawnRouteHandlerTable();
            poolTable = new SceneKernelSpawnPoolTable();
            prefabPoolTable = new SceneKernelPrefabPoolTable();
            spawnTelemetry = new SceneKernelSpawnTelemetry();
        }

        public bool IsOperational => isOperational;

        public int ActiveLeaseCount => leaseTable.ActiveLeaseCount;

        public void Open()
        {
            isOperational = true;
        }

        public void Close()
        {
            isOperational = false;

            foreach (ISceneKernelPrefabPool pool in prefabPoolTable.Pools)
                pool.Shutdown(SceneKernelReleaseReason.Shutdown);

            activePoolIdsByLease.Clear();
            leaseBuffer.Clear();
            spawnOrdinal = 0;

            leaseTable.Clear();
            routeTable.Clear();
            routeHandlerTable.Clear();
            poolTable.Clear();
            prefabPoolTable.Clear();
        }

        public bool TryAcquireLease(EntityRef entityRef, out SceneKernelEntityLeaseHandle lease)
        {
            if (!isOperational)
            {
                lease = default;
                return false;
            }

            return leaseTable.TryAcquire(entityRef, out lease);
        }

        public bool TryBindSpawnRoute(SceneKernelSpawnRouteId routeId, SceneKernelSpawnPoolId poolId)
        {
            return isOperational && routeTable.TryBind(routeId, poolId);
        }

        public bool TryBindSpawnPool(ISceneKernelSpawnPool pool)
        {
            return isOperational && poolTable.TryBind(pool);
        }

        public bool TryBindSpawnPool(ISceneKernelPrefabPool pool)
        {
            return isOperational && prefabPoolTable.TryBind(pool);
        }

        public bool TryBindSpawnRouteHandler(ISceneKernelSpawnRouteHandler handler)
        {
            return isOperational && routeHandlerTable.TryBind(handler);
        }

        public bool TryResolveSpawnPool(SceneKernelSpawnRouteId routeId, out ISceneKernelSpawnPool pool)
        {
            if (!isOperational || !routeTable.TryResolve(routeId, out SceneKernelSpawnPoolId poolId))
            {
                pool = null!;
                return false;
            }

            return poolTable.TryGet(poolId, out pool);
        }

        public bool TryResolveSpawnPool(SceneKernelSpawnRouteId routeId, out ISceneKernelPrefabPool pool)
        {
            if (!isOperational || !routeTable.TryResolve(routeId, out SceneKernelSpawnPoolId poolId))
            {
                pool = null!;
                return false;
            }

            return prefabPoolTable.TryGet(poolId, out pool);
        }

        public bool TryResolveSpawnRouteHandler(SceneKernelSpawnRouteId routeId, out ISceneKernelSpawnRouteHandler handler)
        {
            if (!isOperational)
            {
                handler = null!;
                return false;
            }

            return routeHandlerTable.TryGet(routeId, out handler);
        }

        public bool TryReleaseAll(SceneKernelSpawnRouteId routeId, object filter, out int releasedCount)
        {
            if (!isOperational)
            {
                releasedCount = 0;
                return false;
            }

            if (!routeTable.TryResolve(routeId, out SceneKernelSpawnPoolId poolId))
            {
                releasedCount = 0;
                return false;
            }

            if (prefabPoolTable.TryGet(poolId, out ISceneKernelPrefabPool typedPool))
            {
                SceneKernelBulkReleaseQuery query = filter is SceneKernelBulkReleaseQuery typedQuery
                    ? new SceneKernelBulkReleaseQuery(routeId, typedQuery.EntityRef, typedQuery.Reason, typedQuery.IncludeInactive)
                    : new SceneKernelBulkReleaseQuery(routeId, reason: SceneKernelReleaseReason.BulkRelease, includeInactive: true);

                releasedCount = ReleaseAll(query).ReleasedCount;
                return true;
            }

            if (poolTable.TryGet(poolId, out ISceneKernelSpawnPool legacyPool))
            {
                releasedCount = legacyPool.ReleaseAll(filter);
                return true;
            }

            releasedCount = 0;
            return false;
        }

        public ValueTask<SceneKernelSpawnResult> SpawnAsync(SceneKernelSpawnRequest request, CancellationToken cancellationToken)
        {
            if (!isOperational)
                return new ValueTask<SceneKernelSpawnResult>(CreateSpawnFailure(request.RouteId, request.EntityRef, "SceneKernel spawn boundary is not operational."));

            if (cancellationToken.IsCancellationRequested)
                return new ValueTask<SceneKernelSpawnResult>(CreateSpawnFailure(request.RouteId, request.EntityRef, "SceneKernel spawn request was canceled."));

            if (!routeTable.TryResolve(request.RouteId, out SceneKernelSpawnPoolId poolId))
                return new ValueTask<SceneKernelSpawnResult>(CreateSpawnFailure(request.RouteId, request.EntityRef, "SceneKernel spawn boundary could not resolve the requested route."));

            if (!TryGetOrCreatePrefabPool(poolId, request.ParkingRoot, out ISceneKernelPrefabPool pool, out KernelDiagnostic? diagnostic))
                return new ValueTask<SceneKernelSpawnResult>(new SceneKernelSpawnResult(diagnostic!));

            EntityRef entityRef = request.EntityRef.IsEmpty
                ? SceneKernelEntityRefFactory.Create(sceneHandle, request.RouteId, request.Template, ++spawnOrdinal)
                : request.EntityRef;

            if (!leaseTable.TryAcquire(entityRef, out SceneKernelEntityLeaseHandle lease))
                return new ValueTask<SceneKernelSpawnResult>(CreateSpawnFailure(request.RouteId, entityRef, "SceneKernel spawn boundary could not acquire a lease for the requested entity ref."));

            SceneKernelSpawnRequest resolvedRequest = request.EntityRef == entityRef
                ? request
                : new SceneKernelSpawnRequest(
                    request.RouteId,
                    request.Template,
                    request.Position,
                    request.Rotation,
                    request.Scale,
                    entityRef,
                    request.Parent,
                    request.ParkingRoot,
                    request.WorldSpace,
                    request.AllowPooling);

            SceneKernelSpawnContext context = new SceneKernelSpawnContext(sceneHandle, poolId, resolvedRequest, lease, spawnTelemetry);
            SceneKernelSpawnResult result = pool.Spawn(in context);
            if (!result.Succeeded)
            {
                leaseTable.TryRelease(lease);
                return new ValueTask<SceneKernelSpawnResult>(result);
            }

            activePoolIdsByLease[lease] = poolId;
            return new ValueTask<SceneKernelSpawnResult>(result);
        }

        public ValueTask<SceneKernelWarmupResult> WarmupAsync(SceneKernelWarmupRequest request, CancellationToken cancellationToken)
        {
            if (!isOperational)
                return new ValueTask<SceneKernelWarmupResult>(CreateWarmupFailure(request.RouteId, "SceneKernel spawn boundary is not operational."));

            if (cancellationToken.IsCancellationRequested)
                return new ValueTask<SceneKernelWarmupResult>(CreateWarmupFailure(request.RouteId, "SceneKernel warmup request was canceled."));

            if (!routeTable.TryResolve(request.RouteId, out SceneKernelSpawnPoolId poolId))
                return new ValueTask<SceneKernelWarmupResult>(CreateWarmupFailure(request.RouteId, "SceneKernel spawn boundary could not resolve the requested route."));

            if (!TryGetOrCreatePrefabPool(poolId, request.ParkingRoot, out ISceneKernelPrefabPool pool, out KernelDiagnostic? diagnostic))
                return new ValueTask<SceneKernelWarmupResult>(new SceneKernelWarmupResult(diagnostic!));

            return new ValueTask<SceneKernelWarmupResult>(pool.Warmup(request));
        }

        public SceneKernelReleaseResult Release(SceneKernelEntityLeaseHandle lease, SceneKernelReleaseReason reason)
        {
            if (!isOperational)
                return CreateReleaseFailure(lease, reason, "SceneKernel spawn boundary is not operational.");

            if (!leaseTable.ValidateLease(lease))
                return CreateReleaseFailure(lease, reason, "SceneKernel spawn boundary could not validate the requested lease.");

            if (activePoolIdsByLease.TryGetValue(lease, out SceneKernelSpawnPoolId poolId))
            {
                if (!prefabPoolTable.TryGet(poolId, out ISceneKernelPrefabPool pool))
                    return CreateReleaseFailure(lease, reason, "SceneKernel spawn boundary could not resolve the active prefab pool for the requested lease.");

                SceneKernelReleaseResult result = pool.Release(lease, reason);
                if (!result.Succeeded)
                    return result;

                if (!leaseTable.TryRelease(lease))
                    return CreateReleaseFailure(lease, reason, "SceneKernel spawn boundary could not release the lease table entry for the requested lease.");

                activePoolIdsByLease.Remove(lease);
                return result;
            }

            if (!leaseTable.TryRelease(lease))
                return CreateReleaseFailure(lease, reason, "SceneKernel spawn boundary could not release the requested lease.");

            return new SceneKernelReleaseResult(1);
        }

        public SceneKernelBulkReleaseResult ReleaseAll(SceneKernelBulkReleaseQuery query)
        {
            if (!isOperational)
                return CreateBulkReleaseFailure("SceneKernel spawn boundary is not operational.");

            if (query.HasRouteFilter)
            {
                SceneKernelSpawnRouteId routeId = query.RouteId!.Value;
                if (!routeTable.TryResolve(routeId, out SceneKernelSpawnPoolId poolId))
                    return CreateBulkReleaseFailure("SceneKernel spawn boundary could not resolve the requested route.");

                if (prefabPoolTable.TryGet(poolId, out ISceneKernelPrefabPool typedPool))
                    return ReleaseAllFromTypedPool(poolId, typedPool, query);

                if (poolTable.TryGet(poolId, out ISceneKernelSpawnPool legacyPool))
                    return new SceneKernelBulkReleaseResult(legacyPool.ReleaseAll(query));

                return CreateBulkReleaseFailure("SceneKernel spawn boundary could not resolve the requested spawn pool.");
            }

            int releasedCount = 0;
            foreach (ISceneKernelPrefabPool typedPool in prefabPoolTable.Pools)
            {
                SceneKernelBulkReleaseResult result = ReleaseAllFromTypedPool(typedPool.PoolId, typedPool, query);
                if (!result.Succeeded)
                    return result;

                releasedCount += result.ReleasedCount;
            }

            return new SceneKernelBulkReleaseResult(releasedCount);
        }

        public bool TryGetLease(EntityRef entityRef, out SceneKernelEntityLeaseHandle lease)
        {
            if (!isOperational)
            {
                lease = default;
                return false;
            }

            return leaseTable.TryGetLease(entityRef, out lease);
        }

        public bool ValidateLease(SceneKernelEntityLeaseHandle lease)
        {
            return isOperational && leaseTable.ValidateLease(lease);
        }

        public bool TryReleaseLease(SceneKernelEntityLeaseHandle lease)
        {
            if (!isOperational)
                return false;

            if (activePoolIdsByLease.ContainsKey(lease))
                return Release(lease, SceneKernelReleaseReason.Despawn).Succeeded;

            return leaseTable.TryRelease(lease);
        }

        bool TryGetOrCreatePrefabPool(SceneKernelSpawnPoolId poolId, Transform? parkingRoot, out ISceneKernelPrefabPool pool, out KernelDiagnostic? diagnostic)
        {
            if (prefabPoolTable.TryGet(poolId, out pool))
            {
                diagnostic = null;
                return true;
            }

            SceneKernelPrefabPool createdPool = new SceneKernelPrefabPool(sceneHandle, poolId, parkingRoot, spawnTelemetry);
            if (!prefabPoolTable.TryBind(createdPool) && !prefabPoolTable.TryGet(poolId, out pool))
            {
                pool = null!;
                diagnostic = CreateDiagnostic(
                    "Game.Kernel.Spawn.PoolBindFailed",
                    "SceneKernel spawn boundary could not bind the requested prefab pool.");
                return false;
            }

            pool = createdPool;
            diagnostic = null;
            return true;
        }

        SceneKernelBulkReleaseResult ReleaseAllFromTypedPool(SceneKernelSpawnPoolId poolId, ISceneKernelPrefabPool pool, SceneKernelBulkReleaseQuery query)
        {
            if (!TryReleaseActiveLeasesForPool(poolId, pool, query, out int activeReleasedCount, out KernelDiagnostic? diagnostic))
                return diagnostic != null ? new SceneKernelBulkReleaseResult(diagnostic) : new SceneKernelBulkReleaseResult(activeReleasedCount);

            int parkedReleasedCount = 0;
            if (query.IncludeInactive && !query.HasEntityFilter)
            {
                SceneKernelBulkReleaseResult parkedResult = pool.ReleaseAll(query);
                if (!parkedResult.Succeeded)
                    return parkedResult;

                parkedReleasedCount = parkedResult.ReleasedCount;
            }

            return new SceneKernelBulkReleaseResult(activeReleasedCount + parkedReleasedCount);
        }

        bool TryReleaseActiveLeasesForPool(
            SceneKernelSpawnPoolId poolId,
            ISceneKernelPrefabPool pool,
            in SceneKernelBulkReleaseQuery query,
            out int releasedCount,
            out KernelDiagnostic? diagnostic)
        {
            releasedCount = 0;
            diagnostic = null;
            leaseBuffer.Clear();

            foreach (KeyValuePair<SceneKernelEntityLeaseHandle, SceneKernelSpawnPoolId> entry in activePoolIdsByLease)
            {
                if (entry.Value != poolId)
                    continue;

                if (query.HasEntityFilter && entry.Key.EntityRef != query.EntityRef)
                    continue;

                leaseBuffer.Add(entry.Key);
            }

            for (int index = 0; index < leaseBuffer.Count; index++)
            {
                SceneKernelEntityLeaseHandle lease = leaseBuffer[index];
                if (!leaseTable.ValidateLease(lease))
                {
                    diagnostic = CreateDiagnostic(
                        "Game.Kernel.Spawn.ReleaseAllLeaseValidationFailed",
                        "SceneKernel spawn boundary could not validate an active lease during bulk release.");
                    leaseBuffer.Clear();
                    return false;
                }

                SceneKernelReleaseResult releaseResult = pool.Release(lease, query.Reason);
                if (!releaseResult.Succeeded)
                {
                    diagnostic = releaseResult.Diagnostic;
                    leaseBuffer.Clear();
                    return false;
                }

                if (!leaseTable.TryRelease(lease))
                {
                    diagnostic = CreateDiagnostic(
                        "Game.Kernel.Spawn.ReleaseAllLeaseTableReleaseFailed",
                        "SceneKernel spawn boundary could not release the lease table entry during bulk release.");
                    leaseBuffer.Clear();
                    return false;
                }

                activePoolIdsByLease.Remove(lease);
                releasedCount += releaseResult.ReleasedCount;
            }

            leaseBuffer.Clear();
            return true;
        }

        static SceneKernelSpawnResult CreateSpawnFailure(SceneKernelSpawnRouteId routeId, EntityRef entityRef, string message)
        {
            return new SceneKernelSpawnResult(CreateDiagnostic(message));
        }

        static SceneKernelWarmupResult CreateWarmupFailure(SceneKernelSpawnRouteId routeId, string message)
        {
            return new SceneKernelWarmupResult(CreateDiagnostic(message));
        }

        static SceneKernelReleaseResult CreateReleaseFailure(SceneKernelEntityLeaseHandle lease, SceneKernelReleaseReason reason, string message)
        {
            return new SceneKernelReleaseResult(CreateDiagnostic(message));
        }

        static SceneKernelBulkReleaseResult CreateBulkReleaseFailure(string message)
        {
            return new SceneKernelBulkReleaseResult(CreateDiagnostic(message));
        }

        static KernelDiagnostic CreateDiagnostic(string message)
        {
            return new KernelDiagnostic(
                new DiagnosticCode("Game.Kernel.Spawn.PoolFailure"),
                DiagnosticSeverity.Error,
                DiagnosticDomain.Kernel,
                DiagnosticFailureBoundary.Scene,
                message);
        }
    }
}