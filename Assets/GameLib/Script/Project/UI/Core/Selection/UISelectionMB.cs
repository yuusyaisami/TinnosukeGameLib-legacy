#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    // ================================================================
    // UISelectionMB: UISelectionServiceのFeatureInstaller
    // ================================================================

    /// <summary>
    /// UISelectionServiceをDIコンテナに登録するFeatureInstaller。
    /// 
    /// ## 概要
    /// 
    /// UISelectionServiceは、UIの選択状態を管理する中核サービス。
    /// 多くのUIシステムがこのサービスにアクセスして現在の選択状態を取得する。
    /// 
    /// ## 主な機能
    /// 
    /// 1. **選択状態の管理**: 現在選択中のUIElement（Current）を保持
    /// 2. **ホバー状態の管理**: マウス操作時のホバー対象を保持
    /// 3. **選択履歴**: 前回の選択（Previous）を保持し、フォールバックに使用
    /// 4. **Modal Stackとの連携**: 選択範囲をCurrentInputRoot内に制限
    /// 
    /// ## 重要な制約
    /// 
    /// - Selectedは常にCurrentInputRoot配下に存在する必要がある
    /// - Modal Stackが変更されたとき、選択は自動的にクランプされる
    /// 
    /// ## 設計上の注意
    /// 
    /// 実際のSelect処理（物理的判定）はここでは行わない。
    /// WorldUI/ScreenUIで判定方法が異なるため、Interfaceで分離する。
    /// </summary>
    public sealed class UISelectionMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector設定
        // ----------------------------------------------------------------

        [Header("Debug")]
        [Tooltip("選択変更のログを出力するか")]
        [SerializeField]
        bool _enableSelectionLogging = false;

        [Header("Debug")]
        [SerializeField]
        UISelectionDebugView _debugView = new UISelectionDebugView();

        // ----------------------------------------------------------------
        // IFeatureInstaller実装
        // ----------------------------------------------------------------

        /// <summary>
        /// UISelectionServiceをDIコンテナに登録する。
        /// 
        /// 登録順序の注意:
        /// - UISelectionServiceはIUIModalStackServiceに依存する
        /// - UIModalStackMBが先に登録されている必要がある
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // 選択設定を登録
            builder.RegisterInstance(new UISelectionOptions
            {
                EnableSelectionLogging = _enableSelectionLogging
            });

            // UISelectionServiceを登録
            // - IUISelectionService: 公開インターフェース
            builder.Register<UISelectionService>(Lifetime.Singleton)
                .As<IUISelectionService>()
                .As<IUISelectionState>()
                .As<IUISelectionNavigation>()
                .As<IUISelectionTelemetry>()
                .As<IUISelectionBlockService>();

            // Register debug view instance
            builder.RegisterInstance(_debugView);

            // Bind debug view to telemetry after build
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IUISelectionTelemetry>(out var telemetry))
                {
                    _debugView.Bind(telemetry);
                }
            });
        }
    }

    // ================================================================
    // UISelectionOptions: UISelectionServiceのオプション設定
    // ================================================================

    /// <summary>
    /// UISelectionServiceのオプション設定。
    /// MBのInspector設定をServiceに渡すために使用。
    /// </summary>
    public sealed class UISelectionOptions
    {
        /// <summary>選択変更のログを出力するか</summary>
        public bool EnableSelectionLogging { get; set; }
    }
}
