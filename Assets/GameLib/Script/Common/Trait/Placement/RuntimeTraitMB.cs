#nullable enable
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Trait
{
    [DisallowMultipleComponent]
    public sealed class RuntimeTraitMB : MonoBehaviour, IFeatureInstaller
    {
        TraitRuntimeLinkData? _linkData;

        [ShowInInspector, ReadOnly]
        public string SourceScopeId => _linkData?.SourceScopeId ?? string.Empty;

        [ShowInInspector, ReadOnly]
        public string HolderKey => _linkData?.HolderKey ?? string.Empty;

        [ShowInInspector, ReadOnly]
        public string TraitKey => _linkData?.TraitKey ?? string.Empty;

        [ShowInInspector, ReadOnly]
        public string TraitDefinitionId => _linkData?.TraitDefinitionId ?? string.Empty;

        public TraitRuntimeLinkData? LinkData => _linkData?.Clone();

        public void SetLinkData(TraitRuntimeLinkData linkData)
        {
            _linkData = linkData?.Clone();
        }

        public void ClearLinkData()
        {
            _linkData = null;
        }

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<RuntimeTraitBridgeService>(Lifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .WithParameter(this);
        }
    }
}
