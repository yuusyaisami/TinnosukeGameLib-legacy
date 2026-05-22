#nullable enable
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Collision
{
    [DisallowMultipleComponent]
    public sealed class HitColliderChannelHubMB : MonoBehaviour, IScopeInstaller
    {
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<HitColliderChannelHub>(resolver =>
                {
                    if (resolver.TryResolve<IHitColliderChannelRouter>(out var router) && router != null)
                        return new HitColliderChannelHub(router);

                    Debug.LogWarning("[HitColliderChannelHubMB] IHitColliderChannelRouter was not resolved. Using no-op router.");
                    return new HitColliderChannelHub(NullHitColliderChannelRouter.Instance);
                }, RuntimeLifetime.Singleton)
                .As<IHitColliderChannelHub>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }

    sealed class NullHitColliderChannelRouter : IHitColliderChannelRouter
    {
        public static readonly NullHitColliderChannelRouter Instance = new();
        NullHitColliderChannelRouter() { }

        public void RegisterWatcher(DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags) { }
        public void UnregisterWatcher(DynamicColliderHandle self, HitColliderChannelRuntime runtime) { }
        public void UpdateWatcherFlags(DynamicColliderHandle self, HitColliderChannelRuntime runtime, HitWatchFlags flags) { }
    }
}

