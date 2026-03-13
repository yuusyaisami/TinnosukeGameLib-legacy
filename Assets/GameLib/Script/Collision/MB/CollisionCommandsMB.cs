#nullable enable
using UnityEngine;
using VContainer;

namespace Game.Collision
{
    /// <summary>
    /// Registers Collision-related vNext command executors.
    /// Attach this under ProjectLifetimeScope (and/or scopes that need to execute these commands).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CollisionCommandsMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            if (owner.Kind != LifetimeScopeKind.Project
                && owner.Kind != LifetimeScopeKind.Scene
                && owner.Kind != LifetimeScopeKind.Field
                && owner.Kind != LifetimeScopeKind.Entity)
                return;

            builder.Register<global::Game.Commands.VNext.SetCollisionEnabledExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
            builder.Register<global::Game.Commands.VNext.SetUnityColliderExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
            builder.Register<global::Game.Commands.VNext.HitColliderRuleControlExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
            builder.Register<global::Game.Commands.VNext.WithHitColliderTargetsExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
        }
    }
}
