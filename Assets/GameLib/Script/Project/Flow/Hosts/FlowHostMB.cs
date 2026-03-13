#nullable enable

using UnityEngine;
using VContainer;

namespace Game.Flow
{
    /// <summary>
    /// Flow ホストサービスを DI コンテナに登録する MonoBehaviour インストーラ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowHostMB : MonoBehaviour, Game.IFeatureInstaller
    {
        /// <summary>
        /// IFeatureInstaller 実装。FlowHostService をシングルトンとして登録します。
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, Game.IScopeNode scope)
        {
            builder.Register<FlowHostService>(Lifetime.Singleton)
                .As<IFlowHost>()
                .As<Game.Commands.VNext.IFlowHostCommandBridge>()
                .As<Game.IScopeAcquireHandler>()
                .As<Game.IScopeReleaseHandler>();
        }
    }
}
