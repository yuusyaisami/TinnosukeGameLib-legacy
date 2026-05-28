#nullable enable

using System;
using Game.Kernel.Layers;
using Game.Spawn;
using UnityEngine;

namespace Game.Kernel.Authoring
{
    [Serializable]
    public sealed class SceneKernelSpawnRouteDeclaration
    {
        [SerializeField]
        SpawnerKind kind = SpawnerKind.RuntimeEntity;

        [SerializeField]
        string tag = string.Empty;

        [SerializeField]
        Transform? root;

        [SerializeField]
        Transform? parkingRoot;

        [SerializeField]
        string routeId = string.Empty;

        public SceneKernelSpawnRouteDeclaration()
        {
        }

        public SceneKernelSpawnRouteDeclaration(
            SpawnerKind kind,
            string? tag = null,
            Transform? root = null,
            Transform? parkingRoot = null,
            string? routeId = null)
        {
            this.kind = kind;
            this.tag = tag ?? string.Empty;
            this.root = root;
            this.parkingRoot = parkingRoot;
            this.routeId = routeId ?? string.Empty;
            Normalize();
        }

        public SpawnerKind Kind => kind;

        public string Tag => NormalizeTag(tag);

        public Transform? Root => root;

        public Transform? ParkingRoot => parkingRoot;

        public string StableRouteKey => string.IsNullOrWhiteSpace(routeId) ? KernelRouteId.Value : routeId.Trim();

        public bool IsRuntimeKind => kind == SpawnerKind.RuntimeEntity || kind == SpawnerKind.RuntimeUIElement;

        public SceneKernelSpawnRouteId KernelRouteId => SceneKernelSpawnRouteId.FromParts(kind.ToString(), Tag);

        public SceneKernelSpawnPoolId PoolId => SceneKernelSpawnPoolId.FromParts(kind.ToString(), Tag);

        public bool TryValidate(out string failureReason)
        {
            if (!IsRuntimeKind)
            {
                failureReason = "SceneKernel spawn routes only support RuntimeEntity or RuntimeUIElement kinds in the new path.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        internal void Normalize()
        {
            tag = NormalizeTag(tag);
            routeId = NormalizeOptional(routeId);
        }

        static string NormalizeTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
                return string.Empty;

            string normalizedTag = rawTag.Trim();
            return string.Equals(normalizedTag, "default", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalizedTag;
        }

        static string NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}