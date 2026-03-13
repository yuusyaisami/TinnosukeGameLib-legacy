using UnityEngine;
using VContainer;

namespace Game.Common
{
    [DisallowMultipleComponent]
    public sealed class RichTextRefServiceMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<RichTextRefService>(Lifetime.Singleton)
                .As<IRichTextRefService>();
        }
    }
}
