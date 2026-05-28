#nullable enable

using System;
using System.Collections.Generic;
using Game.DI;
using Game.Kernel.Abstractions;
using Game.Kernel.Layers.Unity;
using UnityEngine;

namespace Game.Kernel.Layers
{
    internal sealed class SceneKernelPrefabPool : ISceneKernelPrefabPool
    {
        readonly SceneKernelHandle sceneHandle;
        readonly SceneKernelSpawnPoolId poolId;
        readonly Transform? defaultParkingRoot;
        readonly SceneKernelSpawnTelemetry telemetry;
        readonly Dictionary<BaseRuntimeTemplateSO, TemplateBucket> bucketsByTemplate = new Dictionary<BaseRuntimeTemplateSO, TemplateBucket>(ReferenceEqualityComparer<BaseRuntimeTemplateSO>.Instance);
        readonly Dictionary<SceneKernelEntityLeaseHandle, ActiveEntry> activeEntriesByLease = new Dictionary<SceneKernelEntityLeaseHandle, ActiveEntry>();
        readonly List<SceneKernelEntityLeaseHandle> activeLeaseBuffer = new List<SceneKernelEntityLeaseHandle>(16);
        bool isShutdown;

        public SceneKernelPrefabPool(
            SceneKernelHandle sceneHandle,
            SceneKernelSpawnPoolId poolId,
            Transform? defaultParkingRoot,
            SceneKernelSpawnTelemetry telemetry)
        {
            if (sceneHandle.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(sceneHandle), sceneHandle, "SceneKernel prefab pools require a positive scene handle.");

            if (poolId.IsEmpty)
                throw new ArgumentException("SceneKernel prefab pools require a non-empty pool id.", nameof(poolId));

            this.sceneHandle = sceneHandle;
            this.poolId = poolId;
            this.defaultParkingRoot = defaultParkingRoot;
            this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public SceneKernelSpawnPoolId PoolId => poolId;

        public SceneKernelSpawnResult Spawn(in SceneKernelSpawnContext context)
        {
            if (isShutdown)
                return Fail(context.RouteId, context.EntityRef, "SceneKernel prefab pool has been shut down.");

            if (context.Template == null)
                return Fail(context.RouteId, context.EntityRef, "SceneKernel spawn requests require a runtime template.");

            GameObject? prefab = context.Template.Prefab;
            if (prefab == null)
                return Fail(context.RouteId, context.EntityRef, "SceneKernel spawn requests require a template with a prefab.");

            telemetry.RecordSpawnRequested(context.RouteId, poolId, context.EntityRef);

            TemplateBucket bucket = EnsureBucket(context.Template, context.ParkingRoot);
            bool canReuse = context.AllowPooling && context.Template.UsePooling && bucket.TryPop(out GameObject? root);
            if (!canReuse)
                root = UnityEngine.Object.Instantiate(prefab);

            if (root == null)
                return Fail(context.RouteId, context.EntityRef, "SceneKernel prefab pool could not create a spawned root.");

            ConfigureSpawnedRoot(root.transform, context);

            SceneKernelEntityInstanceMB anchor = GetOrCreateAnchor(root);
            anchor.BindLease(context.Lease, context.RouteId);

            activeEntriesByLease[context.Lease] = new ActiveEntry(context.Template.PoolKey, root, context.ParkingRoot ?? defaultParkingRoot, context.AllowPooling);
            telemetry.RecordSpawnSucceeded(context.RouteId, poolId, context.EntityRef, canReuse);

            return new SceneKernelSpawnResult(context.Lease, root);
        }

        public SceneKernelWarmupResult Warmup(in SceneKernelWarmupRequest request)
        {
            if (isShutdown)
                return FailWarmup(request.RouteId, "SceneKernel prefab pool has been shut down.");

            if (request.Template == null)
                return FailWarmup(request.RouteId, "SceneKernel warmup requests require a runtime template.");

            if (request.Template.Prefab == null)
                return FailWarmup(request.RouteId, "SceneKernel warmup requests require a template with a prefab.");

            if (!request.Template.UsePooling)
                return FailWarmup(request.RouteId, "SceneKernel warmup requests require a poolable template.");

            telemetry.RecordWarmupRequested(request.RouteId, poolId, request.Count);

            if (request.Count == 0)
                return new SceneKernelWarmupResult(0);

            TemplateBucket bucket = EnsureBucket(request.Template, request.ParkingRoot);
            int warmedCount = 0;
            for (int index = 0; index < request.Count; index++)
            {
                GameObject spawnedRoot = UnityEngine.Object.Instantiate(request.Template.Prefab);
                if (spawnedRoot == null)
                    return FailWarmup(request.RouteId, "SceneKernel prefab pool could not warm up a spawn root.");

                PrepareForParking(spawnedRoot.transform, request.ParkingRoot ?? defaultParkingRoot);
                GetOrCreateAnchor(spawnedRoot).ClearLease();
                bucket.Park(spawnedRoot);
                warmedCount++;
            }

            telemetry.RecordWarmupSucceeded(request.RouteId, poolId, warmedCount);
            return new SceneKernelWarmupResult(warmedCount);
        }

        public SceneKernelReleaseResult Release(SceneKernelEntityLeaseHandle lease, SceneKernelReleaseReason reason)
        {
            if (isShutdown)
                return new SceneKernelReleaseResult(0);

            if (!activeEntriesByLease.TryGetValue(lease, out ActiveEntry entry))
                return FailRelease(lease, reason, "SceneKernel prefab pool could not find an active entry for the requested lease.");

            activeEntriesByLease.Remove(lease);

            SceneKernelEntityInstanceMB? anchor = entry.Root != null ? entry.Root.GetComponent<SceneKernelEntityInstanceMB>() : null;
            anchor?.ClearLease();

            bool destroy = ShouldDestroyOnRelease(reason, entry);
            if (destroy)
            {
                DestroyRoot(entry.Root);
                telemetry.RecordRelease(default, poolId, lease.EntityRef, reason, destroyed: true);
                return new SceneKernelReleaseResult(1);
            }

            TemplateBucket bucket = EnsureBucket(entry.Template, entry.ParkingRoot);
            PrepareForParking(entry.Root.transform, entry.ParkingRoot ?? defaultParkingRoot);
            bucket.Park(entry.Root);
            telemetry.RecordRelease(default, poolId, lease.EntityRef, reason, destroyed: false);
            return new SceneKernelReleaseResult(1);
        }

        public SceneKernelBulkReleaseResult ReleaseAll(in SceneKernelBulkReleaseQuery query)
        {
            if (isShutdown)
                return new SceneKernelBulkReleaseResult(0);

            if (!query.IncludeInactive)
                return new SceneKernelBulkReleaseResult(0);

            int releasedCount = 0;

            foreach (TemplateBucket bucket in bucketsByTemplate.Values)
                releasedCount += bucket.DestroyParkedRoots();

            telemetry.RecordBulkRelease(default, poolId, releasedCount, query.Reason);
            return new SceneKernelBulkReleaseResult(releasedCount);
        }

        public void Shutdown(SceneKernelReleaseReason reason)
        {
            if (isShutdown)
                return;

            isShutdown = true;

            int destroyedCount = 0;
            foreach (ActiveEntry entry in activeEntriesByLease.Values)
            {
                SceneKernelEntityInstanceMB? anchor = entry.Root != null ? entry.Root.GetComponent<SceneKernelEntityInstanceMB>() : null;
                anchor?.ClearLease();
                DestroyRoot(entry.Root);
                destroyedCount++;
            }

            activeEntriesByLease.Clear();

            foreach (TemplateBucket bucket in bucketsByTemplate.Values)
                destroyedCount += bucket.DestroyParkedRoots();

            bucketsByTemplate.Clear();
            telemetry.RecordShutdown(poolId, destroyedCount);
        }

        TemplateBucket EnsureBucket(BaseRuntimeTemplateSO template, Transform? parkingRoot)
        {
            BaseRuntimeTemplateSO key = template.PoolKey;
            if (!bucketsByTemplate.TryGetValue(key, out TemplateBucket bucket))
            {
                bucket = new TemplateBucket(template, parkingRoot ?? defaultParkingRoot);
                bucketsByTemplate.Add(key, bucket);
                return bucket;
            }

            if (parkingRoot != null)
                bucket.ParkingRoot = parkingRoot;

            return bucket;
        }

        static SceneKernelEntityInstanceMB GetOrCreateAnchor(GameObject root)
        {
            SceneKernelEntityInstanceMB? anchor = root.GetComponent<SceneKernelEntityInstanceMB>();
            if (anchor == null)
                anchor = root.AddComponent<SceneKernelEntityInstanceMB>();

            return anchor;
        }

        static void ConfigureSpawnedRoot(Transform root, in SceneKernelSpawnContext context)
        {
            if (context.Parent != null)
                root.SetParent(context.Parent, context.WorldSpace);
            else if (root.parent != null)
                root.SetParent(null, context.WorldSpace);

            if (context.WorldSpace)
            {
                root.SetPositionAndRotation(context.Position, context.Rotation);
            }
            else
            {
                root.localPosition = context.Position;
                root.localRotation = context.Rotation;
            }

            root.localScale = context.Scale;
            root.gameObject.name = context.EntityRef.IsEmpty ? context.Template.TemplateId : context.EntityRef.Value;
            root.gameObject.SetActive(true);
        }

        static void PrepareForParking(Transform root, Transform? parkingRoot)
        {
            root.SetParent(parkingRoot, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            root.gameObject.SetActive(false);
        }

        static void DestroyRoot(GameObject root)
        {
            if (root == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }

        SceneKernelSpawnResult Fail(SceneKernelSpawnRouteId routeId, EntityRef entityRef, string message)
        {
            telemetry.RecordFailure(routeId, poolId, entityRef, message);
            return new SceneKernelSpawnResult(CreateDiagnostic(message));
        }

        SceneKernelWarmupResult FailWarmup(SceneKernelSpawnRouteId routeId, string message)
        {
            telemetry.RecordFailure(routeId, poolId, default, message);
            return new SceneKernelWarmupResult(CreateDiagnostic(message));
        }

        SceneKernelReleaseResult FailRelease(SceneKernelEntityLeaseHandle lease, SceneKernelReleaseReason reason, string message)
        {
            telemetry.RecordFailure(default, poolId, lease.EntityRef, message);
            return new SceneKernelReleaseResult(CreateDiagnostic(message));
        }

        bool ShouldDestroyOnRelease(SceneKernelReleaseReason reason, in ActiveEntry entry)
        {
            return reason != SceneKernelReleaseReason.Despawn || !entry.AllowPooling || !entry.Template.UsePooling;
        }

        static KernelDiagnostic CreateDiagnostic(string message)
        {
            return new KernelDiagnostic(
                new Game.Kernel.Diagnostics.DiagnosticCode("Game.Kernel.Spawn.PoolFailure"),
                Game.Kernel.Diagnostics.DiagnosticSeverity.Error,
                Game.Kernel.Diagnostics.DiagnosticDomain.Kernel,
                Game.Kernel.Diagnostics.DiagnosticFailureBoundary.Scene,
                message);
        }

        sealed class TemplateBucket
        {
            readonly Stack<GameObject> parkedRoots = new Stack<GameObject>();

            public TemplateBucket(BaseRuntimeTemplateSO template, Transform? parkingRoot)
            {
                Template = template;
                ParkingRoot = parkingRoot;
            }

            public BaseRuntimeTemplateSO Template { get; }

            public Transform? ParkingRoot { get; set; }

            public bool TryPop(out GameObject? root)
            {
                while (parkedRoots.Count > 0)
                {
                    root = parkedRoots.Pop();
                    if (root != null)
                        return true;
                }

                root = null;
                return false;
            }

            public void Park(GameObject root)
            {
                if (root == null)
                    return;

                parkedRoots.Push(root);
            }

            public int DestroyParkedRoots()
            {
                int destroyedCount = 0;
                while (parkedRoots.Count > 0)
                {
                    GameObject? root = parkedRoots.Pop();
                    if (root == null)
                        continue;

                    DestroyRoot(root);
                    destroyedCount++;
                }

                return destroyedCount;
            }
        }

        readonly struct ActiveEntry
        {
            public ActiveEntry(BaseRuntimeTemplateSO template, GameObject root, Transform? parkingRoot, bool allowPooling)
            {
                Template = template;
                Root = root;
                ParkingRoot = parkingRoot;
                AllowPooling = allowPooling;
            }

            public BaseRuntimeTemplateSO Template { get; }

            public GameObject Root { get; }

            public Transform? ParkingRoot { get; }

            public bool AllowPooling { get; }
        }

        sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public static readonly ReferenceEqualityComparer<T> Instance = new ReferenceEqualityComparer<T>();

            public bool Equals(T? x, T? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}