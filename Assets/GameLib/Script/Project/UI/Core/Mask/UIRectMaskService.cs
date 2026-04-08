#nullable enable
using UnityEngine;
using System.Collections.Generic;

namespace Game.UI
{
    // ================================================================
    // UIRectMaskService - Unity Mask ベースの選択範囲制限システム
    // ================================================================
    //
    // ╔════════════════════════════════════════════════════════════════╗
    // ║ 【重要】Mask の配置ルール                                      ║
    // ║                                                                ║
    // ║ UIRectMaskMB は必ず UIElementLifetimeScope と                 ║
    // ║ 【同じ GameObject】に配置すること。                            ║
    // ║                                                                ║
    // ║ 正しい配置:                                                    ║
    // ║   └─ UIElementLifetimeScope (GameObject)                       ║
    // ║       ├─ UIRectMaskMB          ← 同じGameObject                ║
    // ║       ├─ Mask または RectMask2D ← 同じGameObject（Unity標準）  ║
    // ║       └─ 子UIElement...        ← Maskの影響を受ける           ║
    // ║                                                                ║
    // ║ Mask の範囲 = 同じ GameObject の RectTransform の描画領域      ║
    // ╚════════════════════════════════════════════════════════════════╝
    //
    // ## 概要
    //
    // このサービスは **自身の Mask 領域** に対して判定を行う。
    // 親階層の Mask 探索は SelectCandidateProviderScreen が行う。
    //
    // ## 判定の流れ
    //
    // 1. SelectCandidateProviderScreen が候補を収集
    // 2. 各候補の親階層を辿り、IUIRectMaskService を Resolve
    // 3. 各サービスの TestPoint / TestElementVisibility を呼び出し
    // 4. いずれかの Mask で範囲外なら候補から除外
    //
    // ## このサービスの責務
    //
    // - **自分の Mask 領域**のみを判定する
    // - 親階層の Mask は関知しない（それは呼び出し側の責務）
    //
    // ================================================================

    // ================================================================
    // MaskTestResult: Mask判定の結果
    // ================================================================

    /// <summary>
    /// Mask判定の結果を表す構造体。
    /// </summary>
    public readonly struct MaskTestResult
    {
        /// <summary>
        /// 判定が通過したかどうか。
        /// trueの場合、対象は選択可能。
        /// </summary>
        public bool Passed { get; }

        /// <summary>
        /// Maskによって覆われている割合（0.0〜1.0）。
        /// 0.0 = 全く覆われていない（完全に表示）
        /// 1.0 = 完全に覆われている（完全に非表示）
        /// </summary>
        public float OcclusionRatio { get; }

        /// <summary>
        /// この判定をブロックした Mask の GameObject。
        /// </summary>
        public GameObject? BlockingMask { get; }

        public MaskTestResult(bool passed, float occlusionRatio = 0f, GameObject? blockingMask = null)
        {
            Passed = passed;
            OcclusionRatio = occlusionRatio;
            BlockingMask = blockingMask;
        }

        /// <summary>通過した結果</summary>
        public static MaskTestResult Pass => new(true, 0f, null);

        /// <summary>ブロックされた結果</summary>
        public static MaskTestResult Block(GameObject mask, float occlusionRatio = 1f)
            => new(false, occlusionRatio, mask);
    }

    // ================================================================
    // IUIRectMaskService: Maskサービスの公開API
    // ================================================================

    /// <summary>
    /// Unity Mask ベースの選択範囲制限サービス。
    /// 
    /// ## 責務
    /// 
    /// **自分の Mask 領域**に対してのみ判定を行う。
    /// 親階層の Mask 探索・収集は呼び出し側（SelectCandidateProviderScreen）の責務。
    /// 
    /// ## 使用方法
    /// 
    /// SelectCandidateProviderScreen が各候補の親階層を辿り、
    /// IUIRectMaskService を DI コンテナから Resolve して判定を依頼する。
    /// 
    /// ## Mask の検出
    /// 
    /// サービスは UIRectMaskMB と同じ GameObject にある
    /// Mask / RectMask2D コンポーネントを使用する。
    /// </summary>
    public interface IUIRectMaskService
    {
        /// <summary>
        /// このサービスが所属する GameObject（Mask 判定に使用）。
        /// </summary>
        GameObject MaskOwner { get; }

        /// <summary>
        /// この Mask の RectTransform（判定領域）。
        /// </summary>
        RectTransform? MaskRect { get; }

        /// <summary>
        /// ポインター位置がこの Mask の範囲内かを判定する。
        /// 
        /// ## 判定内容
        /// 
        /// ポインターがこの Mask の RectTransform 内にあれば Pass。
        /// </summary>
        /// <param name="screenPosition">スクリーン座標</param>
        /// <param name="camera">座標変換に使用するカメラ（ScreenOverlay の場合は null）</param>
        /// <returns>判定結果</returns>
        MaskTestResult TestPoint(Vector2 screenPosition, Camera? camera);

        /// <summary>
        /// 指定した RectTransform がこの Mask でどの程度隠れているかを判定する。
        /// 
        /// ## 用途
        /// 
        /// ナビゲーション選択時に、大部分が隠れている候補をスキップするために使用。
        /// </summary>
        /// <param name="targetRect">判定対象の RectTransform</param>
        /// <param name="camera">座標変換に使用するカメラ</param>
        /// <returns>判定結果</returns>
        MaskTestResult TestElementVisibility(RectTransform targetRect, Camera? camera);

        /// <summary>
        /// 指定した複数の RectTransform がこの Mask でどの程度隠れているかを判定する。
        /// 
        /// ## 用途
        /// 
        /// 複数の HitTestRects を持つ UIElement のナビゲーション可視率判定。
        /// </summary>
        /// <param name="targetRects">判定対象の RectTransform 群</param>
        /// <param name="camera">座標変換に使用するカメラ</param>
        /// <returns>判定結果</returns>
        MaskTestResult TestElementVisibility(IReadOnlyList<RectTransform> targetRects, Camera? camera);

        /// <summary>
        /// ナビゲーション時の遮蔽閾値。
        /// この割合以上 Mask で隠れている候補は選択不可となる。
        /// デフォルト: 0.5 (50%)
        /// </summary>
        float NavigationOcclusionThreshold { get; set; }
    }

    // ================================================================
    // UIRectMaskService: メイン実装
    // ================================================================

    /// <summary>
    /// Unity Mask ベースの選択範囲制限サービス実装。
    /// 
    /// ## 実装方針
    /// 
    /// - **自分の Mask 領域**のみを判定する
    /// - 同じ GameObject にある Mask / RectMask2D を使用
    /// - Mask の形状は RectTransform で決まる
    /// </summary>
    public sealed class UIRectMaskService : IUIRectMaskService
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>Mask が配置されている GameObject</summary>
        readonly GameObject _maskOwner;

        /// <summary>Mask の RectTransform（判定領域）</summary>
        readonly RectTransform? _maskRect;

        /// <summary>一時的な Rect 計算用配列（GC対策）</summary>
        readonly Vector3[] _corners = new Vector3[4];

        /// <summary>ナビゲーション時の遮蔽閾値</summary>
        float _navigationOcclusionThreshold = 0.5f;

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public GameObject MaskOwner => _maskOwner;

        /// <inheritdoc/>
        public RectTransform? MaskRect => _maskRect;

        /// <inheritdoc/>
        public float NavigationOcclusionThreshold
        {
            get => _navigationOcclusionThreshold;
            set => _navigationOcclusionThreshold = Mathf.Clamp01(value);
        }

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="maskOwner">Mask が配置されている GameObject</param>
        /// <param name="navigationOcclusionThreshold">初期遮蔽閾値</param>
        public UIRectMaskService(GameObject maskOwner, float navigationOcclusionThreshold = 0.5f)
        {
            _maskOwner = maskOwner;
            _maskRect = maskOwner?.GetComponent<RectTransform>();
            _navigationOcclusionThreshold = Mathf.Clamp01(navigationOcclusionThreshold);
        }

        // ----------------------------------------------------------------
        // 判定メソッド
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public MaskTestResult TestPoint(Vector2 screenPosition, Camera? camera)
        {
            if (_maskRect == null)
            {
                return MaskTestResult.Pass;
            }

            // ポイントが Mask 範囲内かチェック
            if (!RectTransformUtility.RectangleContainsScreenPoint(_maskRect, screenPosition, camera))
            {
                return MaskTestResult.Block(_maskOwner, 1f);
            }

            return MaskTestResult.Pass;
        }

        /// <inheritdoc/>
        public MaskTestResult TestElementVisibility(RectTransform targetRect, Camera? camera)
        {
            if (_maskRect == null || targetRect == null)
            {
                return MaskTestResult.Pass;
            }

            // Mask のスクリーン矩形を取得
            var maskScreenRect = GetScreenRect(_maskRect, camera);

            // 対象のスクリーン矩形を取得
            var targetScreenRect = GetScreenRect(targetRect, camera);

            // 交差領域を計算
            var intersection = IntersectRects(targetScreenRect, maskScreenRect);

            if (intersection.width <= 0 || intersection.height <= 0)
            {
                // 交差なし = 完全に遮蔽
                return MaskTestResult.Block(_maskOwner, 1f);
            }

            // 遮蔽率を計算
            var targetArea = targetScreenRect.width * targetScreenRect.height;
            if (targetArea <= 0)
            {
                return MaskTestResult.Pass;
            }

            var visibleArea = intersection.width * intersection.height;
            var occlusionRatio = 1f - (visibleArea / targetArea);

            if (occlusionRatio >= _navigationOcclusionThreshold)
            {
                return MaskTestResult.Block(_maskOwner, occlusionRatio);
            }

            return new MaskTestResult(true, occlusionRatio, null);
        }

        /// <inheritdoc/>
        public MaskTestResult TestElementVisibility(IReadOnlyList<RectTransform> targetRects, Camera? camera)
        {
            if (_maskRect == null || targetRects == null || targetRects.Count == 0)
            {
                return MaskTestResult.Pass;
            }

            var maskScreenRect = GetScreenRect(_maskRect, camera);
            var targetArea = 0f;
            var visibleArea = 0f;

            for (var i = 0; i < targetRects.Count; i++)
            {
                var targetRect = targetRects[i];
                if (targetRect == null)
                {
                    continue;
                }

                var targetScreenRect = GetScreenRect(targetRect, camera);
                var rectArea = GetRectArea(targetScreenRect);
                if (rectArea <= 0f)
                {
                    continue;
                }

                targetArea += rectArea;
                var intersection = IntersectRects(targetScreenRect, maskScreenRect);
                visibleArea += GetRectArea(intersection);
            }

            if (targetArea <= 0f)
            {
                return MaskTestResult.Pass;
            }

            var visibilityRatio = Mathf.Clamp01(visibleArea / targetArea);
            var occlusionRatio = 1f - visibilityRatio;

            if (occlusionRatio >= _navigationOcclusionThreshold)
            {
                return MaskTestResult.Block(_maskOwner, occlusionRatio);
            }

            return new MaskTestResult(true, occlusionRatio, null);
        }

        // ----------------------------------------------------------------
        // 内部メソッド - 矩形計算
        // ----------------------------------------------------------------

        /// <summary>
        /// RectTransform のスクリーン座標での矩形を取得する。
        /// </summary>
        Rect GetScreenRect(RectTransform rect, Camera? camera)
        {
            rect.GetWorldCorners(_corners);

            // スクリーン座標に変換
            for (int i = 0; i < 4; i++)
            {
                if (camera != null)
                {
                    _corners[i] = camera.WorldToScreenPoint(_corners[i]);
                }
                // ScreenSpaceOverlay の場合はワールド座標 = スクリーン座標
            }

            return CalculateRectFromCorners(_corners);
        }

        /// <summary>
        /// 四隅の座標から Rect を計算する。
        /// </summary>
        static Rect CalculateRectFromCorners(Vector3[] corners)
        {
            float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 2つの矩形の交差領域を計算する。
        /// </summary>
        static Rect IntersectRects(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);

            float width = xMax - xMin;
            float height = yMax - yMin;

            if (width < 0 || height < 0)
            {
                return new Rect(0, 0, 0, 0);
            }

            return new Rect(xMin, yMin, width, height);
        }

        static float GetRectArea(Rect rect)
        {
            var width = Mathf.Max(0f, rect.width);
            var height = Mathf.Max(0f, rect.height);
            return width * height;
        }
    }
}
