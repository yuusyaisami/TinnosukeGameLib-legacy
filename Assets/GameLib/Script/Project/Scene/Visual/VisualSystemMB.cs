#nullable enable

using Game;
using UnityEngine;
using VContainer;

namespace Game.Visual
{
    /// <summary>
    /// VisualSystem の FeatureInstaller。
    /// FieldLifetimeScope 推奨だが、任意の LifetimeScope 配下でも動く。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisualSystemMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            _ = scope;
            builder.Register<VisualSystemService>(Lifetime.Singleton)
                .As<IVisualSystem>()
                .AsSelf();
        }
    }
}
