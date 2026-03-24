#nullable enable
using System.Collections.Generic;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Trait
{
    [DisallowMultipleComponent]
    public sealed class TraitHolderHubMB : MonoBehaviour, IFeatureInstaller
    {
        [Tooltip("Configure holders here. External systems must obtain ITraitHolderService via the hub with a key.")]
        [SerializeField]
        List<TraitHolderSettings> _holders = new();

        [SerializeField]
        global::Game.Trait.TraitHolderDebugViewer _debugViewer = new();

        ITraitHolderHubService? _hub;

        public ITraitHolderHubService? Hub => _hub;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var holderCount = _holders?.Count ?? 0;
            var settings = new List<TraitHolderSettings>(holderCount);
            for (int i = 0; i < holderCount; i++)
            {
                var holder = _holders?[i];
                if (holder == null)
                    continue;

                settings.Add(holder.Clone());
            }

            builder.Register<TraitHolderHubService>(Lifetime.Singleton)
                .AsSelf()
                .As<ITraitHolderHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scope)
                // NOTE: Explicit interface typing avoids settings injection being missed by VContainer.
                .WithParameter<IReadOnlyList<TraitHolderSettings>>(settings);

            builder.Register<TraitPlacementService>(Lifetime.Singleton)
                .AsSelf()
                .As<ITraitPlacementService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scope);

            builder.RegisterBuildCallback(resolver =>
            {
                resolver.TryResolve(out _hub);
                if (_debugViewer != null && resolver.TryResolve<ITraitHolderHubService>(out var hub))
                    _debugViewer.Bind(hub);
            });
        }
    }
}
