#nullable enable
using System;
using UnityEngine;
using Game.Times;

namespace Game.DI
{
    public sealed class RuntimeScopeIdentityService : ILTSIdentityService
    {
        public LifetimeScopeKind Kind { get; private set; } = LifetimeScopeKind.Runtime;
        public string Id { get; private set; } = "";
        public string Category { get; private set; } = "Runtime";
        public bool IsActive { get; set; } = true;
        public Transform SelfTransform { get; private set; } = null!;
        public TimeScaleBehavior TimeScaleBehavior { get; private set; } = TimeScaleBehavior.Scaled;
        public float Radius { get; private set; } = 0f;

        public void Apply(RuntimeIdentityData data)
        {
            Kind = data.Kind;
            Id = data.Id ?? "";
            Category = data.Category ?? "Runtime";
            IsActive = data.InitiallyActive;
            TimeScaleBehavior = data.TimeScaleBehavior;
            SelfTransform = data.SelfTransform;
            Radius = data.Radius;
        }
    }
}
