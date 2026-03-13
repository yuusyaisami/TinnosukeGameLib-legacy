using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Commands;

namespace Game
{
    public interface IPlayerLocationSettings
    {
        string PlayerId { get; }
        LifetimeScopeKind PlayerKind { get; }
        string PlayerCategory { get; }
        bool RequireActive { get; }
        CommandTargetSearchScope SearchScope { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerLocationMB : MonoBehaviour, IFeatureInstaller, IPlayerLocationSettings
    {
        [BoxGroup("Player")]
        [LabelText("Player Id")]
        [Tooltip("LTSIdentityService.Id to resolve as the player scope.")]
        [SerializeField]
        string _playerId = "Player";

        [BoxGroup("Filter")]
        [LabelText("Scope Kind")]
        [Tooltip("Use LifetimeScopeKind.None to ignore kind filtering.")]
        [SerializeField]
        LifetimeScopeKind _playerKind = LifetimeScopeKind.None;

        [BoxGroup("Filter")]
        [LabelText("Category")]
        [SerializeField]
        string _playerCategory = "";

        [BoxGroup("Filter")]
        [LabelText("Require Active")]
        [SerializeField]
        bool _requireActive = true;

        [BoxGroup("Filter")]
        [LabelText("Search Scope")]
        [SerializeField]
        CommandTargetSearchScope _searchScope = CommandTargetSearchScope.All;

        public string PlayerId => _playerId;
        public LifetimeScopeKind PlayerKind => _playerKind;
        public string PlayerCategory => _playerCategory;
        public bool RequireActive => _requireActive;
        public CommandTargetSearchScope SearchScope => _searchScope;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance<IPlayerLocationSettings>(this);
            builder.Register<PlayerLocationService>(Lifetime.Singleton)
                .As<IPlayerLocationService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .WithParameter(scope);
        }
    }
}
