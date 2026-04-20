using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Direction
{
    [DisallowMultipleComponent]
    public sealed class DirectionChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        [Tooltip("Initial direction layers that will be registered with the hub.")]
        List<DirectionLayerDef> layerDefs = new();

        [BoxGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel]
        DirectionChannelHubDebugViewer debugViewer = new();

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<IDirectionChannelHub, DirectionChannelHubService>(RuntimeLifetime.Singleton)
                .As<IScopeTickHandler>()
                .As<IDirectionChannelHubTelemetry>()
                .WithParameter("layerDefs", layerDefs);

            builder.RegisterBuildCallback(container =>
            {
                if (debugViewer != null && container.TryResolve<IDirectionChannelHubTelemetry>(out var telemetry) && telemetry != null)
                    debugViewer.Bind(telemetry);
            });
        }
    }
}
