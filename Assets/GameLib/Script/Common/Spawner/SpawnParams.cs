#nullable enable

using Game.DI;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// Unified spawn parameter struct for all spawners.
    /// LTS spawners use Prefab; Runtime spawners use Template/Identity.
    /// </summary>
    public struct SpawnParams
    {
        public GameObject? Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        /// <summary>
        /// Parent transform in the Unity hierarchy.
        /// - When null, Runtime spawners should parent under their configured root.
        /// - When specified and different from the configured root, pooling must be bypassed.
        /// </summary>
        public Transform? TransformParent;

        /// <summary>
        /// Parent scope for DI/LifetimeScope build.
        /// This is independent from <see cref="TransformParent"/>.
        /// When specified, RuntimeLifetimeScope should be built under this scope even if the transform
        /// parent is the spawner root.
        /// </summary>
        public IScopeNode? LifetimeScopeParent;
        public bool WorldSpace;

        /// <summary>
        /// Whether the spawned instance may be pooled. When false, spawners should instantiate/destroy
        /// directly and mark the created RuntimeLifetimeScope so it will not be returned to the pool.
        /// Default: true
        /// </summary>
        public bool AllowPooling;

        public BaseRuntimeTemplateSO? Template;
        public RuntimeIdentityData? Identity;

        public static SpawnParams Default => new()
        {
            Prefab = null,
            Position = Vector3.zero,
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = null,
            Identity = null
        };

        public static SpawnParams ForLTS(GameObject prefab, Vector3 position) => new()
        {
            Prefab = prefab,
            Position = position,
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = null,
            Identity = null
        };

        public static SpawnParams ForLTS(GameObject prefab, Vector3 position, Quaternion rotation) => new()
        {
            Prefab = prefab,
            Position = position,
            Rotation = rotation,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = null,
            Identity = null
        };
        public static SpawnParams ForLTS(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Transform? transformParent = null,
            IScopeNode? lifetimeScopeParent = null,
            bool worldSpace = true,
            bool allowPooling = true) => new()
            {
                Prefab = prefab,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                TransformParent = transformParent,
                LifetimeScopeParent = lifetimeScopeParent,
                WorldSpace = worldSpace,
                AllowPooling = allowPooling,
                Template = null,
                Identity = null
            };

        public static SpawnParams ForRuntime(BaseRuntimeObjectTemplate template, Vector3 position, RuntimeIdentityData? identity = null) => new()
        {
            Prefab = null,
            Position = position,
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = template,
            Identity = identity
        };

        public static SpawnParams ForRuntime(BaseRuntimeTemplateSO template, Vector3 position, Quaternion rotation, RuntimeIdentityData? identity = null) => new()
        {
            Prefab = null,
            Position = position,
            Rotation = rotation,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = template,
            Identity = identity
        };

        public static SpawnParams ForRuntime(
            BaseRuntimeTemplateSO template,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            RuntimeIdentityData? identity = null,
            Transform? transformParent = null,
            IScopeNode? lifetimeScopeParent = null,
            bool worldSpace = true,
            bool allowPooling = true) => new()
            {
                Prefab = null,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                TransformParent = transformParent,
                LifetimeScopeParent = lifetimeScopeParent,
                WorldSpace = worldSpace,
                AllowPooling = allowPooling,
                Template = template,
                Identity = identity
            };
    }
}

