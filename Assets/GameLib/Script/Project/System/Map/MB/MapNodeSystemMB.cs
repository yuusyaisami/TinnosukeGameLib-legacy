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
    public sealed class MapNodeSystemMB : MonoBehaviour, IFeatureInstaller, IMapNodeSystemOptions
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

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance<IMapNodeSystemOptions>(this);

            builder.Register<MapNodeGenerator>(Lifetime.Singleton);
            builder.Register<MapNodeVisualizer>(Lifetime.Singleton)
                .As<IMapNodeVisualizer>();
            builder.Register<MapNodeBuilder>(Lifetime.Singleton)
                .As<IMapNodeBuilder>();
            builder.Register<MapNodeManager>(Lifetime.Singleton);

            builder.Register<MapNodeSystemService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IMapNodeSystemService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
