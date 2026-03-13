#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    // Minimal compatibility wrapper for legacy project reference.
    // Prefer using UISelectionDebugView registered from `UISelectionMB`.
    public sealed class UISelectionDebugViewerMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField]
        UISelectionDebugView _debug = new UISelectionDebugView();

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance(_debug);
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IUISelectionTelemetry>(out var telemetry))
                {
                    _debug.Bind(telemetry);
                }
            });
        }
    }
}
