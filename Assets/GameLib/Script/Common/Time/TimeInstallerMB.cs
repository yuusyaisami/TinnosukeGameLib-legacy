using UnityEngine;
using VContainer;
using Game;

namespace Game.Times
{
    // ================================================================
    // TimeInstallerMB - TimeService の DI 登録
    // ================================================================
    //
    // ## 概要E
    //
    // IScopeInstaller として TimeService めEDI コンチE��に登録する、E
    // Inspector で初期値を設定可能、E
    //
    // ## 配置
    //
    // 通常は ProjectLifetimeScope に配置、E
    //
    // ## TimeScale 制御
    //
    // - Unity の Time.timeScale は常に全 Kind の min で制御
    // - 外部ライブラリは LTS の TimeScaleBehavior で Scaled/Unscaled を制御
    //
    // ================================================================

    [DisallowMultipleComponent]
    public sealed class TimeInstallerMB : MonoBehaviour, IScopeInstaller
    {
        // ----------------------------------------------------------------
        // Inspector 設宁E
        // ----------------------------------------------------------------

        [Header("Base Scale Defaults")]
        [Tooltip("GamePlay Kind の基準スケール")]
        [SerializeField, Range(0f, 2f)]
        float baseGamePlayScale = 1f;

        [Tooltip("Pause Kind の基準スケール")]
        [SerializeField, Range(0f, 2f)]
        float basePauseScale = 1f;

        // ----------------------------------------------------------------
        // IScopeInstaller
        // ----------------------------------------------------------------

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<TimeService>(RuntimeLifetime.Singleton)
                   .As<ITimeService>()
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>();

            // 初期値適用�E�Euild 後に実行！E
            builder.RegisterBuildCallback(resolver =>
            {
                var time = resolver.Resolve<ITimeService>();
                time.SetBaseScale(TimeScaleKind.GamePlay, Mathf.Max(0f, baseGamePlayScale));
                time.SetBaseScale(TimeScaleKind.Pause, Mathf.Max(0f, basePauseScale));
            });
        }
    }
}

