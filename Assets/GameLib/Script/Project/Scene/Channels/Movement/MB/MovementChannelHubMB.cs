using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Movement
{
    /// <summary>
    /// Movement チャネルハブの MonoBehaviour 実裁E��E
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementChannelHubMB : MonoBehaviour, IScopeInstaller
    {
        [SerializeField] private MovementProfileSO _movementProfile;
        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scopeNode)
        {
            builder.Register<IMovementChannelHub, MovementChannelHubService>(RuntimeLifetime.Singleton)
                .WithParameter(_movementProfile)
                .WithParameter(scopeNode)
                .As<IScopeReleaseHandler>();
        }
    }
}
