using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Input
{
    /// <summary>
    /// Registers <see cref="ActionBlockService"/> into the lifetime scope.
    /// </summary>
    public sealed class ActionBlockMB : MonoBehaviour
    {
        public void InstallActionBlockRuntime(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ = builder ?? throw new System.ArgumentNullException(nameof(builder));
            _ = scope ?? throw new System.ArgumentNullException(nameof(scope));

            builder.Register<ActionBlockService>(RuntimeLifetime.Singleton)
                .As<IActionBlockService>();
        }
    }
}
