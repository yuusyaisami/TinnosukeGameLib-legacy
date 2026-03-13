#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Input;

namespace Game.UI
{
    // ================================================================
    // UIInputMB: UIInputServiceのFeatureInstaller
    // ================================================================

    /// <summary>
    /// UIInputServiceをDIコンテナに登録するFeatureInstaller。
    /// 
    /// ## 概要
    /// 
    /// UIInputServiceは、低レベルのInputRouter（プロジェクト全体の入力交通整理）から
    /// 入力を受け取り、UI専用のUIInputEventに変換してNavigationServiceへ流す役割を持つ。
    /// 
    /// ## データフロー
    /// 
    /// InputRouter (IInputConsumer)
    ///     ↓ InputFrame
    /// UIInputService (変換処理)
    ///     ↓ UIInputEvent
    /// UINavigationService
    ///     ↓
    /// 現在選択中のUIElement
    /// 
    /// ## 設定項目
    /// 
    /// このMBでは特に設定項目はありませんが、
    /// 将来的に入力変換のカスタマイズオプションを追加可能。
    /// </summary>
    public sealed class UIInputMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector設定（将来の拡張用）
        // ----------------------------------------------------------------

        [Header("Debug")]
        [Tooltip("入力イベントのログを出力するか")]
        [SerializeField]
        bool _enableInputLogging = false;

        [Header("ModalStack Input Guard")]
        [Tooltip("ModalStack の ActiveRoots 変更直後に一定時間すべての入力をブロックするか")]
        [SerializeField]
        bool _blockInputAfterModalActiveRootsChanged = true;

        [Tooltip("ActiveRoots 変更後に入力をブロックする秒数")]
        [Min(0f)]
        [SerializeField]
        float _blockDurationAfterModalActiveRootsChanged = 0.1f;

        // ----------------------------------------------------------------
        // IFeatureInstaller実装
        // ----------------------------------------------------------------

        /// <summary>
        /// UIInputServiceとその関連サービスをDIコンテナに登録する。
        /// 
        /// 登録順序の注意:
        /// - UIInputServiceはIUINavigationServiceに依存する
        /// - そのため、UINavigationMBより後に登録されるか、
        ///   同じConfigureフェーズで一緒に登録される必要がある
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // UIInputServiceを登録
            // - IUIInputService: 公開インターフェース
            // - IStartable: Start時にInputRouterへの登録を行う
            // - IDisposable: Dispose時にクリーンアップを行う
            builder.Register<UIInputService>(Lifetime.Singleton)
                .As<IUIInputService>()
                .As<IStartable>()
                .As<global::System.IDisposable>();

            // 設定値を登録（将来の拡張用）
            builder.RegisterInstance(new UIInputOptions
            {
                EnableInputLogging = _enableInputLogging,
                BlockInputAfterModalActiveRootsChanged = _blockInputAfterModalActiveRootsChanged,
                BlockDurationAfterModalActiveRootsChanged = Mathf.Max(0f, _blockDurationAfterModalActiveRootsChanged)
            });
        }
    }

    // ================================================================
    // UIInputOptions: UIInputServiceのオプション設定
    // ================================================================

    /// <summary>
    /// UIInputServiceのオプション設定。
    /// MBのInspector設定をServiceに渡すために使用。
    /// </summary>
    public sealed class UIInputOptions
    {
        /// <summary>入力イベントのログを出力するか</summary>
        public bool EnableInputLogging { get; set; }

        /// <summary>ActiveRoots 変更後に入力ブロックを有効化するか</summary>
        public bool BlockInputAfterModalActiveRootsChanged { get; set; } = true;

        /// <summary>ActiveRoots 変更後に入力をブロックする秒数</summary>
        public float BlockDurationAfterModalActiveRootsChanged { get; set; } = 0.1f;
    }
}
