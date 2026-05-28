#nullable enable

using Game.Times;
using UnityEngine;

namespace Game
{
    public interface ILTSIdentityService
    {
        LifetimeScopeKind Kind { get; }

        string Id { get; }

        string Category { get; }

        bool IsActive { get; set; }

        TimeScaleBehavior TimeScaleBehavior { get; }

        Transform SelfTransform { get; }

        float Radius { get; }
    }
}