using UnityEngine;
using VContainer;

namespace Game.Common
{
    [DisallowMultipleComponent]
    public sealed class RichTextRefServiceMB : MonoBehaviour, IScopeInstaller
    {
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<RichTextRefService>(RuntimeLifetime.Singleton)
                .As<IRichTextRefService>();
        }
    }
}

