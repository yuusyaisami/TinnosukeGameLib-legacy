#nullable enable
using UnityEngine;
using VContainer;

namespace Game.UI
{
    // ================================================================
    // UIRectMaskMB - UIRectMaskService 登録用 MonoBehaviour
    // ================================================================
    //
    // ╔════════════════════════════════════════════════════════════════╗
    // ║ 【重要】配置ルール                                             ║
    // ║                                                                ║
    // ║ このコンポーネントは必ず UIElementLifetimeScope と            ║
    // ║ 【同じ GameObject】に配置すること！                            ║
    // ║                                                                ║
    // ║ 正しい配置例:                                                  ║
    // ║   └─ ScrollView (GameObject)                                   ║
    // ║       ├─ UIElementLifetimeScope  ← 必須                        ║
    // ║       ├─ UIRectMaskMB            ← このコンポーネント          ║
    // ║       ├─ Unity Mask              ← Unity 標準の Mask           ║
    // ║       ├─ Image                   ← Mask の形状を決める         ║
    // ║       └─ Content (子)                                          ║
    // ║           └─ 子 UIElement...     ← Mask の影響を受ける         ║
    // ║                                                                ║
    // ║ ※ Unity 標準の Mask または RectMask2D と併用する              ║
    // ║ ※ Mask の形状は同じ GameObject の Image で決まる              ║
    // ╚════════════════════════════════════════════════════════════════╝
    //
    // ## 役割
    //
    // 1. **UIRectMaskService の DI 登録**: IFeatureInstaller として登録
    // 2. **設定フィールドの提供**: ナビゲーション遮蔽閾値などの設定
    //
    // ## 設計方針
    //
    // - Mask 判定ロジックは UIRectMaskService に集約
    // - このコンポーネントは DI 登録と設定のみを担当
    // - 実際の Mask コンポーネント（Unity Mask / RectMask2D）は別途必要
    //
    // ## 使用の流れ
    //
    // 1. UIRectMaskMB を UIElementLifetimeScope と同じ GameObject に追加
    // 2. Unity の Mask または RectMask2D を同じ GameObject に追加
    // 3. Image を追加して Mask の形状を定義
    // 4. 子の UIElement は自動的に Mask の影響を受ける
    //
    // ================================================================

    /// <summary>
    /// UIRectMaskService を DI 登録する FeatureInstaller。
    /// 
    /// ## 概要
    /// 
    /// このコンポーネントは UIRectMaskService をコンテナに登録し、
    /// SelectCandidateProviderScreen から Mask 判定を利用可能にする。
    /// 
    /// ## 配置ルール
    /// 
    /// 必ず UIElementLifetimeScope と同じ GameObject に配置すること。
    /// Unity の Mask/RectMask2D コンポーネントと併用する。
    /// </summary>
    public sealed class UIRectMaskMB : MonoBehaviour, IFeatureInstaller
    {
        // ----------------------------------------------------------------
        // Inspector設定
        // ----------------------------------------------------------------

        [Header("Mask Settings")]
        [Tooltip("ナビゲーション時の遮蔽閾値（0.0〜1.0）。\n" +
                 "この割合以上 Mask で隠れている候補は選択不可となる。")]
        [Range(0f, 1f)]
        [SerializeField]
        float _navigationOcclusionThreshold = 0.5f;

        [Header("Debug")]
        [Tooltip("デバッグログを出力するか")]
        [SerializeField]
        bool _enableDebugLog = false;

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <summary>
        /// ナビゲーション時の遮蔽閾値。
        /// </summary>
        public float NavigationOcclusionThreshold => _navigationOcclusionThreshold;

        /// <summary>
        /// デバッグログを出力するか。
        /// </summary>
        public bool EnableDebugLog => _enableDebugLog;

        // ----------------------------------------------------------------
        // IFeatureInstaller 実装
        // ----------------------------------------------------------------

        /// <summary>
        /// UIRectMaskService を DI コンテナに登録する。
        /// </summary>
        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            // UIRectMaskService を Singleton で登録
            // 自分自身の GameObject を MaskOwner として渡す
            var maskOwner = gameObject;
            var threshold = _navigationOcclusionThreshold;

            builder.Register<UIRectMaskService>(Lifetime.Singleton)
                .WithParameter(typeof(GameObject), maskOwner)
                .WithParameter(typeof(float), threshold)
                .As<IUIRectMaskService>();

            if (_enableDebugLog)
            {
                builder.RegisterBuildCallback(_ =>
                {
                    Debug.Log($"[UIRectMaskMB] Service registered on '{name}'. Threshold: {threshold}");
                });
            }
        }

        // ----------------------------------------------------------------
        // エディタ用
        // ----------------------------------------------------------------

#if UNITY_EDITOR
        void OnValidate()
        {
            // 配置ルールの警告
            var scope = GetComponent<UIElementLifetimeScope>();
            if (scope == null)
            {
                Debug.LogWarning(
                    $"[UIRectMaskMB] '{name}' には UIElementLifetimeScope がありません。\n" +
                    "UIRectMaskMB は必ず UIElementLifetimeScope と同じ GameObject に配置してください。",
                    this);
            }

            // Unity Mask の確認
            var mask = GetComponent<UnityEngine.UI.Mask>();
            var rectMask2D = GetComponent<UnityEngine.UI.RectMask2D>();
            if (mask == null && rectMask2D == null)
            {
                Debug.LogWarning(
                    $"[UIRectMaskMB] '{name}' には Unity の Mask/RectMask2D がありません。\n" +
                    "Mask 機能を使用するには Unity の Mask または RectMask2D を追加してください。",
                    this);
            }
        }
#endif
    }
}
