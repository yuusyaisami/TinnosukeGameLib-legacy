#nullable enable
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Trait
{
    /// <summary>
    /// EquipTraitHolder の FeatureInstaller。
    /// TraitHolderHubMB と同じ LifetimeScope に配置して使用する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EquipTraitHolderHubMB : MonoBehaviour, IFeatureInstaller
    {
        [Tooltip("Configure equip slots here. Each slot can hold one equipped trait from the corresponding TraitHolder.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeField]
        List<EquipTraitSlotSettings> _slots = new();

        IEquipTraitHolderHubService? _hub;

        public IEquipTraitHolderHubService? Hub => _hub;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var slotCount = _slots?.Count ?? 0;
            var settings = new List<EquipTraitSlotSettings>(slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                var slot = _slots?[i];
                if (slot == null)
                    continue;

                settings.Add(slot.Clone());
            }

            builder.Register<EquipTraitHolderHubService>(Lifetime.Singleton)
                .AsSelf()
                .As<IEquipTraitHolderHubService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scope)
                .WithParameter<IReadOnlyList<EquipTraitSlotSettings>>(settings);

            builder.RegisterBuildCallback(resolver =>
            {
                resolver.TryResolve(out _hub);
            });
        }
    }
}
