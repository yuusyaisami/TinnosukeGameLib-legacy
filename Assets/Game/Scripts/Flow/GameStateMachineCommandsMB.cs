#nullable enable
using Game;
using UnityEngine;
using VContainer;

namespace Game.Actions
{
    /// <summary>
    /// Project-scope installer for GameStateMachine vNext command executors.
    /// Attach this under ProjectLifetimeScope.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameStateMachineCommandsMB : MonoBehaviour, IFeatureInstaller
    {
        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            if (owner.Kind != LifetimeScopeKind.Project &&
                owner.Kind != LifetimeScopeKind.Scene &&
                owner.Kind != LifetimeScopeKind.Field)
                return;

            builder.Register<global::Game.Commands.VNext.ChangeGameStateExecutor>(Lifetime.Singleton)
                .As<global::Game.Commands.VNext.ICommandExecutor>();
        }
    }
}
