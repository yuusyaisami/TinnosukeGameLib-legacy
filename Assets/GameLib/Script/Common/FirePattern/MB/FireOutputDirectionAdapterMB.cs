#nullable enable
using Game.Movement;
using UnityEngine;
using VContainer;

namespace Game.Fire
{
    public sealed class FireOutputDirectionAdapterMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField] int directionPriority = InputDirectionAdapterPriority.Dynamic;
        [SerializeField] bool enableDebugLog = false;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<FireOutputDirectionAdapter>(Lifetime.Singleton)
                .As<IOutputFirePattern>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(directionPriority)
                .WithParameter(enableDebugLog);
        }
    }
}
