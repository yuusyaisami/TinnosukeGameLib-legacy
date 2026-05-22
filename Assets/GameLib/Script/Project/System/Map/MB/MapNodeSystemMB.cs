#nullable enable
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.MapNode
{
    public interface IMapNodeSystemOptions
    {
        MapNodeProfileSO? DefaultProfile { get; }
        bool EnableVerboseLog { get; }
        bool AutoBuildOnAcquire { get; }
        Transform DefaultParentTransform { get; }
    }

    [DisallowMultipleComponent]
    public sealed class MapNodeSystemMB : MonoBehaviour, IScopeInstaller, IMapNodeSystemOptions
    {
        [BoxGroup("Profile")]
        [SerializeField]
        MapNodeProfileSO? _defaultProfile;

        [BoxGroup("Runtime")]
        [SerializeField]
        Transform? _defaultParentTransform;

        [BoxGroup("Runtime")]
        [SerializeField]
        bool _autoBuildOnAcquire = false;

        [BoxGroup("Debug")]
        [SerializeField]
        bool _enableVerboseLog = false;

        public MapNodeProfileSO? DefaultProfile => _defaultProfile;
        public bool EnableVerboseLog => _enableVerboseLog;
        public bool AutoBuildOnAcquire => _autoBuildOnAcquire;
        public Transform DefaultParentTransform => _defaultParentTransform != null ? _defaultParentTransform : transform;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance<IMapNodeSystemOptions>(this);

            builder.Register<MapNodeGenerator>(RuntimeLifetime.Singleton);
            builder.Register<MapNodeVisualizer>(RuntimeLifetime.Singleton)
                .As<IMapNodeVisualizer>();
            builder.Register<MapNodeBuilder>(RuntimeLifetime.Singleton)
                .As<IMapNodeBuilder>();
            builder.Register<MapNodeManager>(RuntimeLifetime.Singleton);

            builder.Register<MapNodeSystemService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .As<IMapNodeSystemService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}

