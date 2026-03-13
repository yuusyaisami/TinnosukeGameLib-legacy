using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Rotation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RotateChannelHubMB))]
    public sealed class VelocityDrivenRotationMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        [InlineProperty, HideLabel]
        VelocityRotationSettings settings = VelocityRotationSettings.Default;

        [BoxGroup("Debug")]
        [SerializeField]
        bool enableDebugView = true;

        [BoxGroup("Debug")]
        [ShowIf(nameof(enableDebugView))]
        [ShowInInspector, ReadOnly, InlineProperty, HideLabel]
        VelocityDrivenRotationDebugView debugView = new VelocityDrivenRotationDebugView();

        public void InstallFeature(IContainerBuilder builder, IScopeNode _)
        {
            builder.Register<VelocityDrivenRotationService>(Lifetime.Singleton)
                .AsSelf()
                .As<IVelocityRotationSettingsAdapter>()
                .As<IVelocityDrivenRotationTelemetry>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<ITickable>()
                .WithParameter(settings);

            if (!enableDebugView)
                return;

            if (debugView == null)
                debugView = new VelocityDrivenRotationDebugView();

            builder.RegisterInstance(debugView);
            builder.RegisterBuildCallback(container =>
            {
                if (debugView != null && container.TryResolve<IVelocityDrivenRotationTelemetry>(out var telemetry))
                {
                    debugView.Bind(telemetry);
                }
            });
        }
    }
}
