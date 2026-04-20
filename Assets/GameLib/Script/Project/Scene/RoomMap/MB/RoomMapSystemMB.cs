#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.RoomMap
{
    public interface IRoomMapSystemOptions
    {
        Transform RoomMapParentTransform { get; }
        string RuntimeSpawnerTag { get; }
    }
    [DisallowMultipleComponent]
    public sealed class RoomMapSystemMB : MonoBehaviour, IFeatureInstaller, IRoomMapSystemOptions
    {
        [Header("RoomMap System")]
        [SerializeField] Transform _roomMapParentTransform = null!;
        [SerializeField] string _runtimeSpawnerTag = "";
        public Transform RoomMapParentTransform => _roomMapParentTransform;
        public string RuntimeSpawnerTag => _runtimeSpawnerTag;
        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            builder.RegisterInstance<IRoomMapSystemOptions>(this);
            builder.Register<RoomMapBuilder>(RuntimeLifetime.Singleton)
                .As<IRoomMapBuilder>();

            builder.Register<RoomMapVisualizer>(RuntimeLifetime.Singleton)
                .As<IRoomMapVisualizer>();

            builder.Register<RoomMapSystemService>(RuntimeLifetime.Singleton)
                .WithParameter(owner)
                .As<IRoomMapSystemService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }
    }
}
