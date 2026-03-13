using UnityEngine;
using VContainer;
using Game;

namespace Game.Times
{
    // ================================================================
    // TimeInstallerMB - TimeService の DI 登録
    // ================================================================
    //
    // ## 概要
    //
    // IFeatureInstaller として TimeService を DI コンテナに登録する。
    // Inspector で初期値を設定可能。
    //
    // ## 配置
    //
    // 通常は ProjectLifetimeScope に配置。
    //
    // ## TimeScale 制御
    //
    // - Unity の Time.timeScale は常に全 Kind の min で制御
    // - 外部ライブラリは LTS の TimeScaleBehavior で Scaled/Unscaled を制御
    //
    // ================================================================

    [DisallowMultipleComponent]
    public sealed class TimeInstallerMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector 設定
        // ----------------------------------------------------------------

        [Header("Base Scale Defaults")]
        [Tooltip("GamePlay Kind の基準スケール")]
        [SerializeField, Range(0f, 2f)]
        float baseGamePlayScale = 1f;

        [Tooltip("Pause Kind の基準スケール")]
        [SerializeField, Range(0f, 2f)]
        float basePauseScale = 1f;

        // ----------------------------------------------------------------
        // IFeatureInstaller
        // ----------------------------------------------------------------

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<TimeService>(Lifetime.Singleton)
                   .As<ITimeService>()
                   .As<IScopeAcquireHandler>()
                   .As<IScopeReleaseHandler>();

            // 初期値適用（Build 後に実行）
            builder.RegisterBuildCallback(resolver =>
            {
                var time = resolver.Resolve<ITimeService>();
                time.SetBaseScale(TimeScaleKind.GamePlay, Mathf.Max(0f, baseGamePlayScale));
                time.SetBaseScale(TimeScaleKind.Pause, Mathf.Max(0f, basePauseScale));
            });
        }
    }
}
