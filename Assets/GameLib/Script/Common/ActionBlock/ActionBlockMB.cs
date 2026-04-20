using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Input
{
    /// <summary>
    /// Registers <see cref="ActionBlockService"/> into the lifetime scope.
    /// </summary>
    public sealed class ActionBlockMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<ActionBlockService>(RuntimeLifetime.Singleton)
                .As<IActionBlockService>();
        }
    }
}
