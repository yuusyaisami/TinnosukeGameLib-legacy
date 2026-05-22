#nullable enable
using System;
using UnityEngine;
using VContainer;
using Game.Spawn;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIElementSpawnerMB : MonoBehaviour, IScopeInstaller
    {
        [Header("Spawner")]
        [SerializeField] string spawnerTag = "";

        [Tooltip("Spawn parent. Null の場合�Eこ�E GameObject 直下に生�E")]
        [SerializeField] Transform? root;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            builder.RegisterInstance(this);

            var resolvedRoot = root != null ? root : transform;

            builder.Register<UIElementSpawnerService>(RuntimeLifetime.Singleton)
                .WithParameter(resolvedRoot)
                .WithParameter(spawnerTag)
                .AsSelf()
                .As<IAsyncSpawnerService>()
                .As<IUIElementSpawnerService>();

            // Ensure the spawner service is instantiated so it can register with the SceneSpawnerRegistry.
            builder.RegisterBuildCallback(resolver =>
            {
                try
                {
                    resolver.Resolve<UIElementSpawnerService>();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            });
        }
    }
}

