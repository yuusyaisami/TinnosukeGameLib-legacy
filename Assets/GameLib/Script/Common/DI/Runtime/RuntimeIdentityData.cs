#nullable enable

using Game.Times;
using UnityEngine;
namespace Game.DI
{
    /// <summary>
    /// Lightweight identity payload passed when acquiring a KernelScopeHost from a pool.
    /// Mirrors IScopeIdentityService fields.
    /// </summary>
    public struct RuntimeIdentityData
    {
        public string Id;
        public string Category;
        public LifetimeScopeKind Kind;
        public TimeScaleBehavior TimeScaleBehavior;
        public Transform SelfTransform;
        public bool InitiallyActive;
        public float Radius;

        public static RuntimeIdentityData CreateDefault(Transform selfTransform, string id = "", string category = "Runtime")
        {
            return new RuntimeIdentityData
            {
                Id = id,
                Category = category,
                Kind = LifetimeScopeKind.Runtime,
                TimeScaleBehavior = TimeScaleBehavior.Scaled,
                InitiallyActive = true,
                SelfTransform = selfTransform,
                Radius = 0f,
            };
        }
    }
}




