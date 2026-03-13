#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    // ================================================================
    // UINavigationMB: UINavigationServiceのFeatureInstaller
    // ================================================================

    /// <summary>
    /// UINavigationServiceをDIコンテナに登録するFeatureInstaller。
    /// 
    /// ## 概要
    /// 
    /// UINavigationServiceは、UIInputServiceから受け取ったUIInputEventを処理し、
    /// 現在選択中のUIElementに配信する役割を持つ。
    /// また、方向キー入力によるナビゲーション（上下左右の選択移動）も担当する。
    /// 
    /// ## 主な機能
    /// 
    /// 1. **入力イベントの配信**: 現在のSelectにUIInputEventを配信
    /// 2. **方向ナビゲーション**: 上下左右キーによる選択移動
    /// 3. **リピート処理**: 方向キー長押し時の連続移動
    /// 
    /// ## 設定項目
    /// 
    /// - ナビゲーションの入力閾値
    /// - リピート開始までの遅延時間
    /// - リピート間隔
    /// </summary>
    public sealed class UINavigationMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector設定
        // ----------------------------------------------------------------

        [Header("Navigation Threshold")]
        [Tooltip("方向入力として認識する最小の大きさ（0.0〜1.0）")]
        [Range(0.1f, 0.9f)]
        [SerializeField]
        float _navigateThreshold = 0.5f;

        [Header("Repeat Settings")]
        [Tooltip("方向キー長押し時、リピートが始まるまでの遅延（秒）")]
        [Range(0.1f, 1.0f)]
        [SerializeField]
        float _repeatDelay = 0.4f;

        [Tooltip("リピート発火の間隔（秒）")]
        [Range(0.05f, 0.5f)]
        [SerializeField]
        float _repeatRate = 0.1f;

        [Header("Debug")]
        [Tooltip("ナビゲーションイベントのログを出力するか")]
        [SerializeField]
        bool _enableNavigationLogging = false;



        [Header("Debug")]
        [SerializeField]
        UINavigationDebugView _debugView = new UINavigationDebugView();

        // ----------------------------------------------------------------
        // IFeatureInstaller実装
        // ----------------------------------------------------------------

        /// <summary>
        /// UINavigationServiceをDIコンテナに登録する。
        /// 
        /// 登録順序の注意:
        /// - UINavigationServiceはIUISelectionServiceに依存する
        /// - UISelectionMBが先に登録されている必要がある
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // ナビゲーション設定を登録
            builder.RegisterInstance(new UINavigationOptions
            {
                NavigateThreshold = _navigateThreshold,
                RepeatDelay = _repeatDelay,
                RepeatRate = _repeatRate,
                EnableNavigationLogging = _enableNavigationLogging
            });

            // UINavigationServiceを登録
            // - IUINavigationService: 公開インターフェース
            builder.Register<UINavigationService>(Lifetime.Singleton)
                .As<IUINavigationService>()
                .As<IUINavigationTelemetry>();

            builder.Register<UIInputNavigateManagerService>(Lifetime.Singleton)
            .As<IUIInputNavigateService>()
            .As<IScopeAcquireHandler>()
            .As<IScopeReleaseHandler>();

            // Register debug view instance and bind
            builder.RegisterInstance(_debugView);
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<IUINavigationTelemetry>(out var telemetry))
                {
                    _debugView.Bind(telemetry);
                }
            });
        }
    }

    // ================================================================
    // UINavigationOptions: UINavigationServiceのオプション設定
    // ================================================================

    /// <summary>
    /// UINavigationServiceのオプション設定。
    /// MBのInspector設定をServiceに渡すために使用。
    /// </summary>
    public sealed class UINavigationOptions
    {
        /// <summary>方向入力として認識する最小の大きさ</summary>
        public float NavigateThreshold { get; set; } = 0.5f;

        /// <summary>リピートが始まるまでの遅延（秒）</summary>
        public float RepeatDelay { get; set; } = 0.4f;

        /// <summary>リピート発火の間隔（秒）</summary>
        public float RepeatRate { get; set; } = 0.1f;

        /// <summary>ナビゲーションイベントのログを出力するか</summary>
        public bool EnableNavigationLogging { get; set; }
    }
}
