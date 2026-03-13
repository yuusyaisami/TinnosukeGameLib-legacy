using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Movement
{
    /// <summary>
    /// Movement チャネルハブの MonoBehaviour 実装。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [SerializeField] private MovementProfileSO _movementProfile;
        public void InstallFeature(IContainerBuilder builder, IScopeNode scopeNode)
        {
            builder.Register<IMovementChannelHub, MovementChannelHubService>(Lifetime.Singleton)
                .WithParameter(_movementProfile)
                .WithParameter(scopeNode)
                .As<IScopeReleaseHandler>();
        }
    }
}