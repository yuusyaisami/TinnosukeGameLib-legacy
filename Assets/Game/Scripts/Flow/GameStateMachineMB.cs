using System;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.Actions
{
    [Serializable]
    public sealed class GameStateCommandEntry
    {
        [LabelText("State")]
        public GameState key;

        [LabelText("Start Commands")]
        public VNext.CommandListData startCommands = new VNext.CommandListData();

        [LabelText("End Commands")]
        public VNext.CommandListData endCommands = new VNext.CommandListData();
    }

    public interface IGameStateMachineSettings
    {
        GameStateCommandEntry[] StateCommands { get; }
        GameState InitialState { get; }
        bool ExecuteInitialStateCommandsOnAcquire { get; }
    }

    [DisallowMultipleComponent]
    public sealed class GameStateMachineMB : MonoBehaviour, IGameStateMachineSettings, IFeatureInstaller
    {
        [BoxGroup("Commands")]
        [LabelText("State Commands")]
        [TableList(AlwaysExpanded = true)]
        [SerializeField]
        GameStateCommandEntry[] _stateCommands = Array.Empty<GameStateCommandEntry>();

        [BoxGroup("Initialization")]
        [LabelText("Initial State")]
        [SerializeField]
        GameState _initialState = GameState.Default;

        [BoxGroup("Initialization")]
        [LabelText("Execute Initial Commands on Acquire")]
        [SerializeField]
        bool _executeInitialStateCommandsOnAcquire = true;

        [BoxGroup("Debug")]
        [LabelText("Debug Viewer")]
        [SerializeField, InlineProperty, HideLabel]
        GameStateMachineDebugViewer _debugViewer = new();

        public GameStateCommandEntry[] StateCommands => _stateCommands;
        public GameState InitialState => _initialState;
        public bool ExecuteInitialStateCommandsOnAcquire => _executeInitialStateCommandsOnAcquire;

        public void InstallFeature(IContainerBuilder builder, IScopeNode owner)
        {
            builder.RegisterInstance<IGameStateMachineSettings>(this);

            builder.Register<GameStateMachineService>(Lifetime.Singleton)
                .As<IGameStateMachineService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(owner);

            builder.RegisterBuildCallback(resolver =>
            {
                if (_debugViewer != null &&
                    resolver.TryResolve<IGameStateMachineService>(out var service) &&
                    service != null)
                {
                    _debugViewer.Bind(service, this);
                }
            });
        }
    }
}
