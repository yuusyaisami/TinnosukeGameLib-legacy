#nullable enable
using System;
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Collision
{
    /// <summary>
    /// Legacy installer.
    /// This used to register the old HitColliderChannelRuntime (multi-subscriber + command execution).
    /// That system has been replaced by HitColliderChannelHub/HitColliderChannelRuntime (1 query = 1 runtime).
    ///
    /// Prefer: HitColliderChannelHubMB
    /// </summary>
    [Obsolete("Use HitColliderChannelHubMB instead.")]
    [DisallowMultipleComponent]
    public sealed class HitColliderChannelRuntimeMB : MonoBehaviour, IScopeInstaller
    {
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            // Register the new Hub so old scenes don't break immediately.
            builder.Register<IHitColliderChannelHub, HitColliderChannelHub>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}

