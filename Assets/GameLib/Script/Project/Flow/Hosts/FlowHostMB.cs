#nullable enable

using UnityEngine;
using VContainer;

namespace Game.Flow
{
    /// <summary>
    /// Flow ホストサービスめEDI コンチE��に登録する MonoBehaviour インスト�Eラ、E
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlowHostMB : MonoBehaviour
    {
        /// <summary>
        /// Verified composition path for FlowHostService registration.
        /// </summary>
        public void InstallFlowHostRuntime(IRuntimeContainerBuilder builder, Game.IScopeNode scope)
        {
            _ = builder ?? throw new System.ArgumentNullException(nameof(builder));
            _ = scope ?? throw new System.ArgumentNullException(nameof(scope));

            builder.Register<FlowHostService>(RuntimeLifetime.Singleton)
                .As<IFlowHost>()
                .As<Game.Commands.VNext.IFlowHostCommandBridge>()
                .As<Game.IScopeAcquireHandler>()
                .As<Game.IScopeReleaseHandler>();
        }
    }
}
