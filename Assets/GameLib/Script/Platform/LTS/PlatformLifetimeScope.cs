using VContainer;
using VContainer.Unity;
using Game;
using Game.Common;
using UnityEngine;
// プラットフォーム固有の依存関係を登録するためのLifetimeScope
namespace Game.Platform
{
    // 通常の呼び出しよりも早く初期化されるようになっています。 (order = -15)
    // またProjectlifetimeScopeのPrefabの子供になっています。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlatformMB))]
    [RequireComponent(typeof(Game.Commands.CommandRunnerMB))]
    [RequireComponent(typeof(Game.Scalar.BaseScalarMB))]
    [RequireComponent(typeof(BlackboardMB))] // Platform Blackboard
    [RequireComponent(typeof(Game.Common.EventMB))] // Project Event Service
    public class PlatformLifetimeScope : BaseLifetimeScope<ProjectLifetimeScope>
    {
        protected override bool UseBuildCoordinator => true; // 普通の LifetimeScope として起動時に Build
        protected override bool IsBuildRoot => false;

        protected override void ConfigureBase(IContainerBuilder builder)
        {
            // Platform スコープ固有の登録をここに書く

            builder.Register<PlatformHardwareVarAutoRegisterService>(Lifetime.Singleton)
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>();
        }


    }
}
