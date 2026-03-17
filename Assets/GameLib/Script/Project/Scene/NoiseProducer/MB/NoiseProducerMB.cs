#nullable enable
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.NoiseProducer
{
    [DisallowMultipleComponent]
    public sealed class NoiseProducerMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField, Required]
        [ListDrawerSettings(ShowFoldout = true)]
        List<NoiseChannelDefinitionSO> _channelDefinitions = new();

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var definitions = new List<NoiseChannelDefinition>(_channelDefinitions.Count);
            for (int i = 0; i < _channelDefinitions.Count; i++)
            {
                if (_channelDefinitions[i] != null)
                    definitions.Add(_channelDefinitions[i].Definition);
            }

            builder.Register<NoiseProducerService>(Lifetime.Singleton)
                .WithParameter("initialDefinitions", definitions)
                .As<INoiseProducerService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<VContainer.Unity.ITickable>()
                .As<System.IDisposable>();
        }
    }
}
