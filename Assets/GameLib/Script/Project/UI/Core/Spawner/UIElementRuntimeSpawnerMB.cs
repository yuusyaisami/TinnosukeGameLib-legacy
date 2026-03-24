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
        [Tooltip("Spawn parent. Null の場合はこの GameObject 直下に生成")]
        [SerializeField] Transform? root;

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            builder.RegisterInstance(this);

            var resolvedRoot = root != null ? root : transform;

            builder.Register<UIElementRuntimeSpawnerService>(Lifetime.Singleton)
                .WithParameter(resolvedRoot)
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
