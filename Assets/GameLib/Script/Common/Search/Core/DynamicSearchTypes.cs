#nullable enable
using System;
using System.Collections.Generic;
using Unity.Mathematics;
namespace Game.Search
{
    public readonly struct DynamicSearchQuery
    {
        public readonly float2 Origin;
        public readonly float Radius;
        public readonly float2 Forward;
        public readonly float CosHalfAngle;
        public readonly string? FilterId;
        public readonly string? FilterCategory;
        public readonly LifetimeScopeMask KindMask;
        public readonly bool RequireActive;

        public DynamicSearchQuery(
            float2 origin,
            float radius,
            LifetimeScopeMask kindMask = LifetimeScopeMask.All,
            bool requireActive = true,
            string? filterId = null,
            string? filterCategory = null)
        {
            Origin = origin;
            Radius = radius;
            Forward = float2.zero;
            CosHalfAngle = -1f;
            KindMask = kindMask;
            RequireActive = requireActive;
            FilterId = filterId;
            FilterCategory = filterCategory;
        }

        public DynamicSearchQuery(
            float2 origin,
            float radius,
            float2 forward,
            float cosHalfAngle,
            LifetimeScopeMask kindMask = LifetimeScopeMask.All,
            bool requireActive = true,
            string? filterId = null,
            string? filterCategory = null)
        {
            Origin = origin;
            Radius = radius;
            Forward = forward;
            CosHalfAngle = cosHalfAngle;
            KindMask = kindMask;
            RequireActive = requireActive;
            FilterId = filterId;
            FilterCategory = filterCategory;
        }

        public bool HasConeFilter => CosHalfAngle > -0.99f;
        public bool HasIdFilter => !string.IsNullOrEmpty(FilterId);
        public bool HasCategoryFilter => !string.IsNullOrEmpty(FilterCategory);
    }

    public readonly struct DynamicSearchHit
    {
        public readonly IScopeNode Scope;
        public readonly ILTSIdentityService Identity;
        public readonly float DistanceSq;
        public readonly float2 Position;

        public DynamicSearchHit(IScopeNode scope, ILTSIdentityService identity, float distanceSq, float2 position)
        {
            Scope = scope;
            Identity = identity;
            DistanceSq = distanceSq;
            Position = position;
        }
    }

    public interface IDynamicObjectRegistryService
    {
        void Register(IScopeNode scope, ILTSIdentityService identity);
        void Unregister(IScopeNode scope);
        void Update(IScopeNode scope);
        int Count { get; }
    }

    public interface IDynamicSearchService : IDynamicObjectRegistryService
    {
        void Query(in DynamicSearchQuery query, List<DynamicSearchHit> results);
        void Query(float2 origin, float radius, List<DynamicSearchHit> results, LifetimeScopeMask kindMask = LifetimeScopeMask.All);
        void Query(float2 origin, float radius, float2 forward, float cosHalfAngle, List<DynamicSearchHit> results, LifetimeScopeMask kindMask = LifetimeScopeMask.All);
    }
}
