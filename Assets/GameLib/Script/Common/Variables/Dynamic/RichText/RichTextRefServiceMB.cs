using UnityEngine;
using VContainer;

namespace Game.Common
{
    [DisallowMultipleComponent]
    public sealed class RichTextRefServiceMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<RichTextRefService>(RuntimeLifetime.Singleton)
                .As<IRichTextRefService>();
        }
    }
}
