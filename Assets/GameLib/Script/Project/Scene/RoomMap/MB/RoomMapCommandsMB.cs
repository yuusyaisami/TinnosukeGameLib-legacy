#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.RoomMap
{
    /// <summary>
    /// Project-scope installer for RoomMap vNext command executors.
    /// Attach this under ProjectLifetimeScope.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomMapCommandsMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            // Register RoomMap command executors in Project, Scene and Field scopes (RoomMap may be invoked from Field scopes at runtime)
            if (owner.Kind != LifetimeScopeKind.Project && owner.Kind != LifetimeScopeKind.Scene && owner.Kind != LifetimeScopeKind.Field)
                return;

            builder.Register<global::Game.Commands.VNext.BuildRoomMapExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.ClearRoomMapExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.RemoveRoomMapRectExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.ApplyRoomMapVisualExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.GetRoomMapCenterExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
        }
    }
}
