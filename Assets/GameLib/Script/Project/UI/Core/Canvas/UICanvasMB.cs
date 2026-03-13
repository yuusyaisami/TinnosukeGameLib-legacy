#nullable enable
using UnityEngine;
using VContainer;

namespace Game.UI
{
    // ================================================================
    // UICanvasMB - UIキャンバス設定用MonoBehaviour
    // ================================================================
    //
    // ## 概要
    //
    // UICanvasMBは、UIのキャンバス設定を管理するMonoBehaviour。
    // 通常、UIシステムのルートとなるUIElementにアタッチされる。
    //
    // ## 主な役割
    //
    // 1. **Canvas参照の提供**: UIシステムがCanvas情報にアクセスするための窓口
    // 2. **キャンバスタイプの判定**: Screen/World Canvasの自動判定
    // 3. **座標変換のサポート**: Screen座標⇔Local座標の変換
    // 4. **将来の拡張ポイント**: UI全体の設定を追加する場所
    //
    // ## 設計方針
    //
    // このコンポーネントは最小限の機能から始め、
    // 必要に応じてフィールドを追加していく。
    //
    // 現在は以下のみを管理:
    // - Canvas参照
    // - キャンバスタイプ（自動判定）
    //
    // 将来追加予定の設定例:
    // - デフォルトのアニメーション設定
    // - サウンド設定
    // - テーマ/スキン設定
    // - レイアウト設定
    //
    // ================================================================

    /// <summary>
    /// UIキャンバス設定用MonoBehaviour。
    /// 
    /// ## 使用方法
    /// 
    /// 1. UIシステムのルートUIElementにアタッチ
    /// 2. Canvas参照を設定（または自動取得）
    /// 3. IFeatureInstallerとしてUICanvasServiceを登録
    /// 
    /// ## 自動取得
    /// 
    /// Canvasが設定されていない場合、親階層からCanvasを自動取得する。
    /// </summary>
    public sealed class UICanvasMB : MonoBehaviour, IFeatureInstaller
    {
        // ================================================================
        // Inspector設定
        // ================================================================

        [Header("Canvas設定")]
        [Tooltip("このUIが属するCanvas。空の場合、親階層から自動取得する。")]
        [SerializeField]
        Canvas? _canvas;

        [Header("デバッグ")]
        [Tooltip("起動時にCanvas情報をログ出力する")]
        [SerializeField]
        bool _logOnStart = false;

        // ================================================================
        // 将来の拡張用フィールド（コメントで予約）
        // ================================================================

        // [Header("アニメーション設定")]
        // [Tooltip("UI要素のデフォルトアニメーション設定")]
        // [SerializeField]
        // UIAnimationSettings? _defaultAnimationSettings;

        // [Header("サウンド設定")]
        // [Tooltip("UI操作時のデフォルトサウンド設定")]
        // [SerializeField]
        // UISoundSettings? _defaultSoundSettings;

        // [Header("テーマ設定")]
        // [Tooltip("UIのテーマ/スキン設定")]
        // [SerializeField]
        // UIThemeSettings? _themeSettings;

        // ================================================================
        // キャッシュ
        // ================================================================

        /// <summary>登録されたService</summary>
        UICanvasServiceCore? _service;

        /// <summary>解決済みのCanvas</summary>
        Canvas? _resolvedCanvas;

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>
        /// このMBが管理するCanvas。
        /// Inspector設定または自動取得されたもの。
        /// </summary>
        public Canvas? Canvas
        {
            get
            {
                if (_resolvedCanvas == null)
                {
                    _resolvedCanvas = ResolveCanvas();
                }
                return _resolvedCanvas;
            }
        }

        /// <summary>
        /// キャンバスの種類（自動判定）。
        /// </summary>
        public UICanvasType CanvasType
        {
            get
            {
                var canvas = Canvas;
                if (canvas == null) return UICanvasType.ScreenOverlay;

                return canvas.renderMode switch
                {
                    RenderMode.ScreenSpaceOverlay => UICanvasType.ScreenOverlay,
                    RenderMode.ScreenSpaceCamera => UICanvasType.ScreenCamera,
                    RenderMode.WorldSpace => UICanvasType.World,
                    _ => UICanvasType.ScreenOverlay
                };
            }
        }

        // ================================================================
        // IFeatureInstaller実装
        // ================================================================

        /// <summary>
        /// UICanvasServiceCoreをDIコンテナに登録する。
        /// 
        /// ## 処理内容
        /// 
        /// 1. Canvasを解決（Inspector設定または自動取得）
        /// 2. UICanvasServiceCoreをSingletonで登録
        /// 3. IUICanvasServiceとして公開
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // Canvasを解決
            _resolvedCanvas = ResolveCanvas();

            if (_resolvedCanvas == null)
            {
                Debug.LogWarning($"[UICanvasMB] '{name}' could not find Canvas. " +
                               "Please set Canvas in Inspector or ensure parent has Canvas.");
            }

            // UICanvasServiceCoreを登録
            builder.Register<UICanvasServiceCore>(Lifetime.Singleton)
                .WithParameter(typeof(Canvas), _resolvedCanvas)
                .As<IUICanvasService>();

            // CandidateProvider登録（World/Screen両対応）
            builder.Register<ISelectCandidateProvider>(c =>
            {
                var cs = c.Resolve<IUICanvasService>();
                return CanvasType == UICanvasType.World
                    ? new SelectCandidateProviderWorld(cs)
                    : new SelectCandidateProviderScreen(cs);
            }, Lifetime.Singleton)
            .As<ISelectCandidateProvider>();

            // Ensure the service gets the provider injected when built
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<UICanvasServiceCore>(out var s) && container.TryResolve<ISelectCandidateProvider>(out var p))
                {
                    s.SetCandidateProvider(p);
                }
            });

            // BuildCallback内でServiceをキャッシュ　ー　自身がビルドされたときに自身の範囲内で呼び出す
            builder.RegisterBuildCallback(container =>
            {
                if (container.TryResolve<UICanvasServiceCore>(out var service))
                {
                    _service = service;

                    if (_logOnStart)
                    {
                        LogCanvasInfo();
                    }
                }
            });
        }

        // ================================================================
        // MonoBehaviourライフサイクル
        // ================================================================

        void Reset()
        {
            // エディタでアタッチ時、親のCanvasを自動設定
            _canvas = GetComponentInParent<Canvas>();
        }

        void OnValidate()
        {
            // Inspector変更時にキャッシュをクリア
            _resolvedCanvas = null;
        }

        // ================================================================
        // 内部メソッド
        // ================================================================

        /// <summary>
        /// Canvasを解決する。
        /// 
        /// ## 解決順序
        /// 
        /// 1. Inspector設定があればそれを使用
        /// 2. なければ親階層から自動取得
        /// </summary>
        Canvas? ResolveCanvas()
        {
            if (_canvas != null)
            {
                return _canvas;
            }

            return GetComponentInParent<Canvas>();
        }

        /// <summary>
        /// Canvas情報をログ出力する（デバッグ用）。
        /// </summary>
        void LogCanvasInfo()
        {
            var canvas = Canvas;
            if (canvas == null)
            {
                Debug.Log($"[UICanvasMB] '{name}' - No Canvas");
                return;
            }

            Debug.Log($"[UICanvasMB] '{name}' - Canvas: {canvas.name}, " +
                     $"Type: {CanvasType}, " +
                     $"Camera: {canvas.worldCamera?.name ?? "null"}");
        }
    }

    // ================================================================
    // UICanvasServiceCore - キャンバス情報サービス（シンプル版）
    // ================================================================

    /// <summary>
    /// キャンバス情報を管理するサービスのコア実装。
    /// 
    /// ## 役割
    /// 
    /// - Canvas参照の保持
    /// - キャンバスタイプの判定
    /// - 座標変換ユーティリティ
    /// 
    /// ## 設計
    /// 
    /// CandidateProvider関連は別クラス（SelectCandidateProviderScreen等）に分離。
    /// このサービスは純粋なCanvas情報の提供に専念する。
    /// </summary>
    public sealed class UICanvasServiceCore : IUICanvasService
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        readonly Canvas? _canvas;
        readonly Camera? _uiCamera;
        readonly UICanvasType _canvasType;

        // 注意: CandidateProviderは別途設定される
        ISelectCandidateProvider? _candidateProvider;

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public Canvas? Canvas => _canvas;

        /// <inheritdoc/>
        public UICanvasType CanvasType => _canvasType;

        /// <inheritdoc/>
        public Camera? UICamera => _uiCamera;

        /// <inheritdoc/>
        public ISelectCandidateProvider CandidateProvider
        {
            get
            {
                if (_candidateProvider == null)
                {
                    Debug.LogWarning("[UICanvasServiceCore] CandidateProvider not set. " +
                                   "Creating default ScreenCandidateProvider.");
                    _candidateProvider = new SelectCandidateProviderScreen(this);
                }
                return _candidateProvider;
            }
        }

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="canvas">管理対象のCanvas</param>
        public UICanvasServiceCore(Canvas? canvas)
        {
            _canvas = canvas;

            if (_canvas != null)
            {
                _canvasType = _canvas.renderMode switch
                {
                    RenderMode.ScreenSpaceOverlay => UICanvasType.ScreenOverlay,
                    RenderMode.ScreenSpaceCamera => UICanvasType.ScreenCamera,
                    RenderMode.WorldSpace => UICanvasType.World,
                    _ => UICanvasType.ScreenOverlay
                };

                _uiCamera = _canvas.worldCamera;
            }
            else
            {
                _canvasType = UICanvasType.ScreenOverlay;
                _uiCamera = null;
            }
        }

        // ----------------------------------------------------------------
        // 設定メソッド
        // ----------------------------------------------------------------

        /// <summary>
        /// CandidateProviderを設定する。
        /// 
        /// ## 用途
        /// 
        /// 外部からCandidateProviderを注入する場合に使用。
        /// 例: WorldCanvasでは専用のProviderを設定する。
        /// </summary>
        public void SetCandidateProvider(ISelectCandidateProvider? provider)
        {
            _candidateProvider = provider;
        }

        // ----------------------------------------------------------------
        // 座標変換
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public bool ScreenToLocalPoint(Vector2 screenPosition, out Vector2 localPosition)
        {
            localPosition = Vector2.zero;

            if (_canvas == null)
            {
                return false;
            }

            var rectTransform = _canvas.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return false;
            }

            var camera = _canvasType == UICanvasType.ScreenOverlay ? null : _uiCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPosition,
                camera,
                out localPosition);
        }

        /// <inheritdoc/>
        public Vector2 LocalToScreenPoint(Vector2 localPosition)
        {
            if (_canvas == null)
            {
                return localPosition;
            }

            var rectTransform = _canvas.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return localPosition;
            }

            // ローカル座標をワールド座標に変換
            var worldPos = rectTransform.TransformPoint(localPosition);

            // ワールド座標をスクリーン座標に変換
            var camera = _canvasType == UICanvasType.ScreenOverlay ? null : _uiCamera;
            if (camera != null)
            {
                return camera.WorldToScreenPoint(worldPos);
            }

            return worldPos;
        }

        /// <inheritdoc/>
        public bool RectContainsScreenPoint(RectTransform rect, Vector2 screenPosition)
        {
            if (rect == null)
            {
                return false;
            }

            var camera = _canvasType == UICanvasType.ScreenOverlay ? null : _uiCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, camera);
        }
    }
}
