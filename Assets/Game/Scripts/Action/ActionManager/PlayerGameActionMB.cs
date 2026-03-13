using UnityEngine;
using VContainer;
using Sirenix.OdinInspector;
using VNext = Game.Commands.VNext;

namespace Game.Actions
{
    public interface IPlayerGameActionSettings
    {
        VNext.CommandListData StartGageCommands { get; }
        VNext.CommandListData BuildGageCommands { get; }
        VNext.CommandListData StopGageSectorCommands { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerGameActionMB : MonoBehaviour, IFeatureInstaller, IPlayerGameActionSettings
    {
        [Header("Action Gage Commands")]
        [Tooltip("Commands to run when StartGage is called.")]
        [BoxGroup("Commands")]
        [LabelText("Start Gage")]
        [SerializeField]
        [VNext.CommandListFunctionName("PlayerGameAction.StartGage")]
        VNext.CommandListData _startGageCommands = new();

        [Tooltip("Commands to run when BuildGage is called.")]
        [BoxGroup("Commands")]
        [LabelText("Build Gage")]
        [SerializeField]
        [VNext.CommandListFunctionName("PlayerGameAction.BuildGage")]
        VNext.CommandListData _buildGageCommands = new();

        [Tooltip("Commands to run when StopGageSector is called.")]
        [BoxGroup("Commands")]
        [LabelText("Stop Gage Sector")]
        [SerializeField]
        [VNext.CommandListFunctionName("PlayerGameAction.StopGageSector")]
        VNext.CommandListData _stopGageSectorCommands = new();

        public VNext.CommandListData StartGageCommands => _startGageCommands;
        public VNext.CommandListData BuildGageCommands => _buildGageCommands;
        public VNext.CommandListData StopGageSectorCommands => _stopGageSectorCommands;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.RegisterInstance<IPlayerGameActionSettings>(this);
            builder.Register<IPlayerGameActionService, PlayerGameActionService>(Lifetime.Singleton);
        }
    }
}
