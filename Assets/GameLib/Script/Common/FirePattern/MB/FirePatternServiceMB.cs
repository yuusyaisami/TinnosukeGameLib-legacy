#nullable enable
using System;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Fire
{
    public sealed class FirePatternServiceMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Input")]
        [SerializeReference, ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        BaseFirePattern[] patterns = Array.Empty<BaseFirePattern>();

        [Header("Target Hits")]
        [SerializeField] DynamicSearchHitAcquireMode hitAcquireMode = DynamicSearchHitAcquireMode.TargetChannelHub;

        [Tooltip("Used when HitAcquireMode = TargetChannelHub")]
        [ShowIf(nameof(ShowTargetChannelFields))]
        [SerializeField] string targetChannelTag = "enemy";

        [Tooltip("Used when HitAcquireMode = DynamicSearchQuery")]
        [ShowIf(nameof(ShowQueryFields))]
        [Min(0f)]
        [SerializeField] float queryRadius = 10f;

        [ShowIf(nameof(ShowQueryFields))]
        [SerializeField] LifetimeScopeMask queryKindMask = LifetimeScopeMask.All;

        [ShowIf(nameof(ShowQueryFields))]
        [SerializeField] bool queryRequireActive = true;

        [Tooltip("Optional. Used when HitAcquireMode = DynamicSearchQuery")]
        [ShowIf(nameof(ShowQueryFields))]
        [SerializeField] string queryFilterId = "";

        [Tooltip("Optional. Used when HitAcquireMode = DynamicSearchQuery")]
        [ShowIf(nameof(ShowQueryFields))]
        [SerializeField] string queryFilterCategory = "";

        bool ShowTargetChannelFields => hitAcquireMode == DynamicSearchHitAcquireMode.TargetChannelHub;
        bool ShowQueryFields => hitAcquireMode == DynamicSearchHitAcquireMode.DynamicSearchQuery;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<FirePatternService>(Lifetime.Singleton)
                .As<IFirePatternService>();

            var query = new DynamicSearchHitQuerySettings(
                radius: queryRadius,
                kindMask: queryKindMask,
                requireActive: queryRequireActive,
                filterId: queryFilterId,
                filterCategory: queryFilterCategory);

            builder.Register<SpawnContextToFireAdapter>(Lifetime.Singleton)
                .As<ISpawnContextConsumer>()
                .As<IFirePatternOverrideReceiver>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(patterns)
                .WithParameter(hitAcquireMode)
                .WithParameter(targetChannelTag)
                .WithParameter(query);
        }
    }
}
