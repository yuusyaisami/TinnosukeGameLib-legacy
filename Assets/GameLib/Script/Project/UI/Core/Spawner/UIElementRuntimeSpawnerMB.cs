#nullable enable
using System;
using Game.Spawn;
using UnityEngine;
using VContainer;
using VContainer.Unity;


namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIElementRuntimeSpawnerMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Spawner")]
        [SerializeField] string spawnerTag = "";

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            builder.RegisterInstance(this);

            builder.Register<UIElementRuntimeSpawnerService>(Lifetime.Singleton)
                .WithParameter(spawnerTag)
                .AsSelf()
                .As<IAsyncSpawnerService>()
                .As<IUIElementRuntimeSpawnerService>();
            builder.RegisterBuildCallback(resolver =>
            {
                try
                {
                    resolver.Resolve<UIElementRuntimeSpawnerService>();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }
    }
}
