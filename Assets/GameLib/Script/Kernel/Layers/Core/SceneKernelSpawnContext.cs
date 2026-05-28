#nullable enable

using Game.DI;
using Game.Kernel.Abstractions;
using UnityEngine;

namespace Game.Kernel.Layers
{
    public readonly struct SceneKernelSpawnContext
    {
        public SceneKernelSpawnContext(
            SceneKernelHandle sceneHandle,
            SceneKernelSpawnPoolId poolId,
            SceneKernelSpawnRequest request,
            SceneKernelEntityLeaseHandle lease,
            SceneKernelSpawnTelemetry telemetry)
        {
            SceneHandle = sceneHandle;
            PoolId = poolId;
            Request = request;
            Lease = lease;
            Telemetry = telemetry ?? throw new System.ArgumentNullException(nameof(telemetry));
        }

        public SceneKernelHandle SceneHandle { get; }

        public SceneKernelSpawnPoolId PoolId { get; }

        public SceneKernelSpawnRequest Request { get; }

        public SceneKernelEntityLeaseHandle Lease { get; }

        public SceneKernelSpawnTelemetry Telemetry { get; }

        public SceneKernelSpawnRouteId RouteId => Request.RouteId;

        public BaseRuntimeTemplateSO Template => Request.Template;

        public Game.Kernel.Abstractions.EntityRef EntityRef => Request.EntityRef;

        public Transform? Parent => Request.Parent;

        public Transform? ParkingRoot => Request.ParkingRoot;

        public Vector3 Position => Request.Position;

        public Quaternion Rotation => Request.Rotation;

        public Vector3 Scale => Request.Scale;

        public bool WorldSpace => Request.WorldSpace;

        public bool AllowPooling => Request.AllowPooling;
    }
}