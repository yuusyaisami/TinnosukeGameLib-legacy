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
    public sealed class RoomMapCommandsMB : MonoBehaviour, IScopeInstaller
    {
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            // Register RoomMap command executors in Project, Scene and Field scopes (RoomMap may be invoked from Field scopes at runtime)
            if (owner.Kind != LifetimeScopeKind.Project && owner.Kind != LifetimeScopeKind.Scene && owner.Kind != LifetimeScopeKind.Field)
                return;

            builder.Register<global::Game.Commands.VNext.BuildRoomMapExecutor>(RuntimeLifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.ClearRoomMapExecutor>(RuntimeLifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.RemoveRoomMapRectExecutor>(RuntimeLifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.ApplyRoomMapVisualExecutor>(RuntimeLifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();

            builder.Register<global::Game.Commands.VNext.GetRoomMapCenterExecutor>(RuntimeLifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
        }
    }
}

