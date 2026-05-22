#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    // Minimal compatibility wrapper for legacy project reference.
    // Prefer using UISelectionDebugView registered from `UISelectionMB`.
    public sealed class UISelectionDebugViewerMB : MonoBehaviour, IScopeInstaller
    {
        [SerializeField]
        UISelectionDebugView _debug = new UISelectionDebugView();

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
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

