#nullable enable
using System.Collections.Generic;
using Game;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.MapNode
{
    public interface IMapNodePlayerOptions
    {
        MapNodeProfileSO? DefaultProfile { get; }
        Transform DefaultParentTransform { get; }
        bool AutoBuildOnAcquire { get; }
        bool AutoSaveOnMove { get; }
        MapNodeMoveOptions DefaultMoveOptions { get; }
        MapNodePlayerSaveOptions DefaultSaveOptions { get; }
        CommandListData OnSelectedNodeChangedCommands { get; }
    }

    [DisallowMultipleComponent]
    public sealed class MapNodePlayerMB : MonoBehaviour, IScopeInstaller, IMapNodePlayerOptions
    {
        [BoxGroup("Build")]
        [SerializeField]
        MapNodeProfileSO? _defaultProfile;

        [BoxGroup("Build")]
        [SerializeField]
        Transform? _defaultParentTransform;

        [BoxGroup("Build")]
        [SerializeField]
        bool _autoBuildOnAcquire = false;

        [BoxGroup("Move")]
        [SerializeField]
        MapNodeMoveOptions _defaultMoveOptions = new()
        {
            AllowMoveToLocked = false,
            AllowMoveToVisited = true,
            AllowMoveToCompleted = false,
            AllowMoveToDisabled = false,
            DefaultLayerDirection = MapNodeLayerMoveDirection.ForwardOnly,
            LayerMoveRules = new List<MapNodeLayerMoveRule>(),
            AutoUnlockNext = true,
            AutoLockOthers = false,
            StateForCurrent = MapNodeState.Visited,
            StateForPrevious = MapNodeState.Visited,
        };

        [BoxGroup("Save")]
        [SerializeField]
        bool _autoSaveOnMove = false;

        [BoxGroup("Save")]
        [SerializeField]
        MapNodePlayerSaveOptions _defaultSaveOptions = new()
        {
            Target = MapNodePlayerSaveTarget.Blackboard,
            WriteCurrentNode = true,
            WriteNodeLists = true,
            UseOverrideScope = false,
        };

        [BoxGroup("Commands"), LabelText("On Selected Node Changed")]
        [CommandListFunctionName("MapNodePlayer.OnSelectedNodeChanged")]
        [SerializeField]
        CommandListData _onSelectedNodeChangedCommands = new();

        public MapNodeProfileSO? DefaultProfile => _defaultProfile;
        public Transform DefaultParentTransform => _defaultParentTransform != null ? _defaultParentTransform : transform;
        public bool AutoBuildOnAcquire => _autoBuildOnAcquire;
        public bool AutoSaveOnMove => _autoSaveOnMove;
        public MapNodeMoveOptions DefaultMoveOptions => _defaultMoveOptions;
        public MapNodePlayerSaveOptions DefaultSaveOptions => _defaultSaveOptions;
        public CommandListData OnSelectedNodeChangedCommands => _onSelectedNodeChangedCommands ?? new CommandListData();

        void Awake()
        {
            BindDebugOwners();
        }

        void OnValidate()
        {
            BindDebugOwners();
        }

        void BindDebugOwners()
        {
            _onSelectedNodeChangedCommands?.BindDebugOwner(this, nameof(_onSelectedNodeChangedCommands));
        }

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance<IMapNodePlayerOptions>(this);

            // MapNode player commands

            builder.Register<MapNodePlayerService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .As<IMapNodePlayerService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}

