#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI
{
    // ================================================================
    // UICanvasService - キャンバス種別と設定を管理するシステム
    // ================================================================
    //
    // ## 概要
    //
    // このファイルには以下が定義されている:
    //
    // 1. **UICanvasType列挙型**: キャンバスの種類
    // 2. **IUICanvasService**: キャンバスサービスの公開インターフェース
    // 3. **SelectCandidateProviderScreen**: Screen用の選択候補プロバイダー
    //
    // ## サービス実装について
    //
    // UICanvasServiceCore（実装クラス）はUICanvasMB.csに定義されている。
    // これはMBとServiceのペアを明確にするための設計。
    //
    // ## キャンバス種別
    //
    // 1. **ScreenSpaceOverlay**: 画面に直接描画（UIカメラ不要）
    // 2. **ScreenSpaceCamera**: カメラを通して描画
    // 3. **WorldSpace**: 3D空間上に配置
    //
    // 選択候補の解決方法はキャンバス種別によって異なる:
    // - World系: Raycastベースのヒットテスト（将来実装）
    //
    // ## Mask 判定について
    //
    // SelectCandidateProviderScreen は各候補の親階層を辿り、
    // IUIRectMaskService を Resolve して Mask 判定を行う。
    // - ポインター候補: クリック位置が各 Mask 範囲内かチェック
    // - ナビゲーション候補: UIElement が Mask で隠れていないかチェック
    //
    // ================================================================

    /// <summary>
    /// キャンバスの種類を表す列挙型。
    /// 
    /// ## 使用シーン
    /// 
    /// - 座標変換方法の決定
    /// - ヒットテスト方法の決定
    /// - CandidateProviderの選択
    /// </summary>
    public enum UICanvasType
    {
        /// <summary>
        /// ScreenSpace - Overlay
        /// 
        /// ## 特徴
        /// 
        /// - UIカメラ不要
        /// - 常に最前面に描画
        /// - ワールド座標 = スクリーン座標
        /// </summary>
        ScreenOverlay,

        /// <summary>
        /// ScreenSpace - Camera
        /// 
        /// ## 特徴
        /// 
        /// - UIカメラを使用
        /// - カメラの設定でレンダリング順を制御可能
        /// - ポストプロセスの影響を受けられる
        /// </summary>
        ScreenCamera,

        /// <summary>
        /// WorldSpace
        /// 
        /// ## 特徴
        /// 
        /// - 3D空間上に配置されるUI
        /// - ゲーム内の3Dオブジェクトとしてレンダリング
        /// - VR/ARやゲーム内UIに使用
        /// </summary>
        World
    }

    // ================================================================
    // IUICanvasService: キャンバスサービス公開API
    // ================================================================

    /// <summary>
    /// キャンバス情報を提供するサービスの公開インターフェース。
    /// 
    /// ## 役割
    /// 
    /// - Canvas参照の提供
    /// - キャンバスタイプ情報の提供
    /// - 座標変換ユーティリティの提供
    /// - 選択候補プロバイダーの提供
    /// 
    /// ## 取得方法
    /// 
    /// DIコンテナから取得:
    /// ```csharp
    /// var canvasService = container.Resolve<IUICanvasService>();
    /// ```
    /// </summary>
    public interface IUICanvasService
    {
        // ----------------------------------------------------------------
        // Canvas情報
        // ----------------------------------------------------------------

        /// <summary>
        /// このサービスが管理するCanvas。
        /// 
        /// ## nullの場合
        /// 
        /// UICanvasMBがCanvasを見つけられなかった場合にnullになる。
        /// 座標変換は使用できないが、サービス自体は動作する。
        /// </summary>
        Canvas? Canvas { get; }

        /// <summary>
        /// キャンバスの種類。
        /// 
        /// ## 判定方法
        /// 
        /// Canvas.renderModeから自動判定される。
        /// Canvasがnullの場合はScreenOverlayがデフォルト。
        /// </summary>
        UICanvasType CanvasType { get; }

        /// <summary>
        /// UIカメラ。
        /// 
        /// ## nullの場合
        /// 
        /// - ScreenOverlayモード（カメラ不要）
        /// - Canvasにカメラが設定されていない
        /// </summary>
        Camera? UICamera { get; }

        // ----------------------------------------------------------------
        // 選択候補プロバイダー
        // ----------------------------------------------------------------

        /// <summary>
        /// このキャンバス用の選択候補プロバイダー。
        /// 
        /// ## 役割
        /// 
        /// ナビゲーション・ポインター選択時の候補を提供する。
        /// キャンバスタイプによって適切なプロバイダーが選択される。
        /// </summary>
        ISelectCandidateProvider CandidateProvider { get; }

        // ----------------------------------------------------------------
        // 座標変換
        // ----------------------------------------------------------------

        /// <summary>
        /// スクリーン座標をこのCanvas上のローカル座標に変換する。
        /// 
        /// ## パラメータ
        /// 
        /// screenPosition: スクリーン座標（ピクセル）
        /// localPosition: 変換後のローカル座標
        /// 
        /// ## 戻り値
        /// 
        /// 変換に成功した場合true。
        /// Canvasがnullの場合はfalseを返す。
        /// </summary>
        bool ScreenToLocalPoint(Vector2 screenPosition, out Vector2 localPosition);

        /// <summary>
        /// ローカル座標をスクリーン座標に変換する。
        /// 
        /// ## パラメータ
        /// 
        /// localPosition: Canvas上のローカル座標
        /// 
        /// ## 戻り値
        /// 
        /// スクリーン座標（ピクセル）。
        /// Canvasがnullの場合は入力値をそのまま返す。
        /// </summary>
        Vector2 LocalToScreenPoint(Vector2 localPosition);

        /// <summary>
        /// RectTransformがスクリーン座標を含むかどうかを判定する。
        /// 
        /// ## 用途
        /// 
        /// ポインター（マウス）ヒットテストに使用。
        /// RectTransformの領域内にポインターがあるかを判定。
        /// 
        /// ## パラメータ
        /// 
        /// rect: 判定対象のRectTransform
        /// screenPosition: スクリーン座標
        /// 
        /// ## 戻り値
        /// 
        /// 座標が領域内にある場合true。
        /// rectがnullの場合はfalse。
        /// </summary>
        bool RectContainsScreenPoint(RectTransform rect, Vector2 screenPosition);
    }

    // ================================================================
    // SelectCandidateProviderScreen: Screen用の候補プロバイダー
    // ================================================================

    /// <summary>
    /// ScreenSpace Canvas用の選択候補プロバイダー。
    /// 
    /// ## 役割
    /// 
    /// ナビゲーションとポインター選択時に、
    /// 選択可能なUIElementの候補を提供する。
    /// 
    /// ## Mask 判定
    /// 
    /// 各候補の親階層を辿り、IUIRectMaskService を DI コンテナから Resolve して判定を行う:
    /// - ポインター候補: クリック位置が各親 Mask 範囲内かチェック
    /// - ナビゲーション候補: UIElement が Mask で大部分隠れていないかチェック
    /// - ModalStack の InputRoot で探索を打ち切り（それより上は見ない）
    /// 
    /// ## ナビゲーション候補取得
    /// 
    /// 1. rootScope配下の全UIElementを収集
    /// 2. 各候補に対して親階層の Mask を全てチェック
    /// 3. 現在位置から各候補への方向と距離を計算
    /// 4. 指定方向に合致する候補をスコアリング
    /// 5. スコア順でソートして返す
    /// 
    /// ## ポインター候補取得
    /// 
    /// 1. rootScope配下の全UIElementを収集
    /// 2. 各候補のRectTransformでヒットテスト
    /// 3. 各候補の親階層の Mask で範囲判定
    /// 4. ヒットした候補を前面優先でソート
    /// 
    /// ## 探索ルール
    /// 
    /// - Active=falseのUIElementに到達したら、その枝の探索を停止
    /// - これにより、非Activeな子孫は自動的に候補から除外される
    /// </summary>
    public class SelectCandidateProviderScreen : ISelectCandidateProvider
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>座標変換に使用するCanvasService</summary>
        readonly IUICanvasService _canvasService;

        /// <summary>方向判定の最小閾値（0.0〜1.0）</summary>
        public float DirectionThreshold { get; set; } = 0.3f;

        /// <summary>距離の影響度（スコア計算時の重み）</summary>
        public float DistanceWeight { get; set; } = 0.5f;

        /// <summary>一時リスト（GC対策）</summary>
        readonly List<Game.IScopeNode> _allCandidatesBuffer = new();

        /// <summary>Mask収集用の一時リスト（GC対策）</summary>
        readonly List<IUIRectMaskService> _maskBuffer = new();

        /// <summary>CanvasRendererバッファ（描画順計算用）</summary>
        readonly List<CanvasRenderer> _rendererBuffer = new();

        /// <summary>Graphicバッファ（点ベース描画順計算用）</summary>
        readonly List<Graphic> _graphicBuffer = new();

        /// <summary>Traverse stack（Transform単位で境界を尊重して走査）</summary>
        readonly List<Transform> _traverseStack = new();

        /// <summary>そのTransform直下のGraphicバッファ（境界打ち切り用）</summary>
        readonly List<Graphic> _nodeGraphics = new();

        /// <summary>RectTransform corners buffer（GC対策）</summary>
        readonly Vector3[] _corners = new Vector3[4];

        /// <summary>候補収集用の HashSet（GC対策）</summary>
        readonly HashSet<Game.IScopeNode> _visitedBuffer = new(Game.ReferenceEqualityComparer<Game.IScopeNode>.Instance);

        /// <summary>候補収集用の Stack（GC対策）</summary>
        readonly Stack<Game.IScopeNode> _traverseScopeStack = new();

        readonly Dictionary<Game.IScopeNode, PointerSortKey> _pointerSortKeyCache = new();
        readonly Dictionary<Game.IScopeNode, int> _navigationSelectionOrderCache =
            new(Game.ReferenceEqualityComparer<Game.IScopeNode>.Instance);

        /// <summary>UIElementStateキャッシュ（GC/解決コスト対策）</summary>
        readonly Dictionary<Game.IScopeNode, IUIElementState?> _elementStateCache =
            new(Game.ReferenceEqualityComparer<Game.IScopeNode>.Instance);

        /// <summary>ポインターソート用の Comparer（GC対策でキャッシュ）</summary>
        readonly PointerCandidateComparer _pointerComparer;

        /// <summary>ナビゲーションソート用の Comparer（GC対策でキャッシュ）</summary>
        readonly NavigationCandidateComparer _navigationComparer;

        readonly struct PointerSortKey
        {
            public readonly int SortingLayerValue;
            public readonly int SortingOrder;
            public readonly int RenderOrder;
            public readonly int AbsoluteDepth;

            // Selection priority (higher means prefer)
            public readonly int SelectionOrder;
            // Depth in the UI element tree (higher means child)
            public readonly int UiDepth;
            // Stable tie breaker to avoid nondeterminism
            public readonly int StableTie;

            public readonly int SiblingIndex;

            public PointerSortKey(
                int sortingLayerValue, int sortingOrder, int renderOrder, int absoluteDepth,
                int selectionOrder, int uiDepth, int stableTie, int siblingIndex)
            {
                SortingLayerValue = sortingLayerValue;
                SortingOrder = sortingOrder;
                RenderOrder = renderOrder;
                AbsoluteDepth = absoluteDepth;
                SelectionOrder = selectionOrder;
                UiDepth = uiDepth;
                StableTie = stableTie;
                SiblingIndex = siblingIndex;
            }
        }

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="canvasService">座標変換に使用するCanvasService</param>
        public SelectCandidateProviderScreen(IUICanvasService canvasService)
        {
            _canvasService = canvasService;
            _pointerComparer = new PointerCandidateComparer(_pointerSortKeyCache);
            _navigationComparer = new NavigationCandidateComparer(_navigationSelectionOrderCache);
        }

        // ----------------------------------------------------------------
        // ナビゲーション候補取得
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void GetNavigationCandidates(
            Game.IScopeNode? current,
            NavigateDirection direction,
            Game.IScopeNode rootScope,
            List<SelectCandidate> results)
        {
            results.Clear();
            _elementStateCache.Clear();
            _navigationSelectionOrderCache.Clear();

            // 全候補を収集
            _allCandidatesBuffer.Clear();
            CollectAllSelectableCandidates(rootScope, _allCandidatesBuffer);

            if (_allCandidatesBuffer.Count == 0)
            {
                return;
            }

            // 現在位置を取得
            Vector2 currentCenter = Vector2.zero;
            if (current != null)
            {
                currentCenter = GetElementCenterScreen(current);
            }

            var directionVector = DirectionToVector(direction);
            var camera = GetCamera();

            // 各候補をスコアリング
            foreach (var candidate in _allCandidatesBuffer)
            {
                // 自分自身は除外
                if (ReferenceEquals(candidate, current)) continue;

                // ナビゲーション選択不可の要素は除外
                TryGetElementState(candidate, out var state);
                if (state != null && !state.EvaluateIsNavigationSelectable()) continue;
                var selectionOrder = state?.SelectionOrder ?? 0;

                // Mask 判定: 親階層の全ての Mask をチェック
                if (!TestElementVisibilityAgainstParentMasks(candidate, rootScope, camera))
                {
                    continue;
                }

                var candidateCenter = GetElementCenterScreen(candidate);
                var toCandidate = candidateCenter - currentCenter;
                var distance = toCandidate.magnitude;

                // 距離がほぼ0の場合は除外
                if (distance < 0.001f) continue;

                var normalizedToCandidate = toCandidate / distance;
                var directionMatch = Vector2.Dot(normalizedToCandidate, directionVector);

                // 閾値以下は除外（逆方向の候補を除外）
                if (directionMatch < DirectionThreshold) continue;

                // スコア計算
                // 方向一致度が高く、距離が近いほど高スコア
                var distanceScore = 1f / (1f + distance * 0.01f);
                var score = directionMatch + distanceScore * DistanceWeight;

                results.Add(new SelectCandidate(
                    candidate,
                    score,
                    isExplicitLink: false,
                    directionMatch,
                    distance
                ));

                _navigationSelectionOrderCache[candidate] = selectionOrder;
            }

            // SelectionOrder(降順)を最優先にし、次にスコア/方向一致度/距離で整列する。
            results.Sort(_navigationComparer);
        }

        // ----------------------------------------------------------------
        // ポインター候補取得
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void GetPointerHitCandidates(
            Vector2 screenPosition,
            Game.IScopeNode rootScope,
            List<SelectCandidate> results)
        {
            results.Clear();

            _pointerSortKeyCache.Clear();
            _elementStateCache.Clear();

            // 全候補を収集
            _allCandidatesBuffer.Clear();
            CollectAllSelectableCandidates(rootScope, _allCandidatesBuffer);

            var camera = GetCamera();

            // 各候補でヒットテスト
            foreach (var candidate in _allCandidatesBuffer)
            {
                TryGetElementState(candidate, out var st);

                // Active/Visible をここで先に落とす
                if (st != null && !st.IsEffectivelyActive) continue;
                if (st != null && !st.IsVisible) continue;
                if (st != null && !st.EvaluateIsNavigationSelectable()) continue;

                // ヒットテスト
                if (!HitTestElement(candidate, screenPosition, st)) continue;

                // Mask 判定: 親階層の全ての Mask でポイントをチェック
                if (!TestPointAgainstParentMasks(screenPosition, candidate, rootScope, camera))
                {
                    continue;
                }

                // SiblingIndexは弱い指標なのでここでは単に候補として追加。
                // 描画順はソート時に決定する。
                results.Add(new SelectCandidate(candidate, 0f));

                // Build a full pointer sort key including SelectionOrder and UIDepth
                _pointerSortKeyCache[candidate] = BuildPointerSortKey(candidate, st, rootScope, screenPosition, camera);
            }

            // ソート: 描画順で前面優先に並べる。
            // GCアロケーション回避のため、キャッシュされた Comparer を使用
            results.Sort(_pointerComparer);
        }

        // ----------------------------------------------------------------
        // 全候補取得
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void GetAllSelectableCandidates(
            Game.IScopeNode rootScope,
            List<Game.IScopeNode> results)
        {
            results.Clear();
            _elementStateCache.Clear();
            CollectAllSelectableCandidates(rootScope, results);
        }

        // ----------------------------------------------------------------
        // 内部メソッド - Mask 判定
        // ----------------------------------------------------------------

        /// <summary>
        /// 候補の親階層から全ての IUIRectMaskService を収集する。
        /// rootScope で探索を打ち切る。
        /// </summary>
        void CollectParentMaskServices(
            Game.IScopeNode candidate,
            Game.IScopeNode rootScope,
            List<IUIRectMaskService> results)
        {
            results.Clear();

            // v0.2 Tooltip rule: Mask 判定は Transform 親階層に基づいて行う。
            // (Tooltip は Transform 親を TooltipRoot に固定し、Lifetime 親は Adapter にするため)
            var rootTransform = rootScope.Identity?.SelfTransform;
            var currentTransform = candidate.Identity?.SelfTransform;

            while (currentTransform != null)
            {
                var scopeOnTransform = TryGetScopeNode(currentTransform);
                if (scopeOnTransform != null &&
                    scopeOnTransform.Resolver != null &&
                    scopeOnTransform.Resolver.TryResolve<IUIRectMaskService>(out var maskService))
                {
                    // 重複チェック（同じ MaskOwner のサービスは追加しない）
                    bool isDuplicate = false;
                    foreach (var existing in results)
                    {
                        if (ReferenceEquals(existing.MaskOwner, maskService.MaskOwner))
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (!isDuplicate)
                    {
                        results.Add(maskService);
                    }
                }

                // rootScope の Transform に到達したら終了（rootScope 自身の mask も含めた後で打ち切る）
                if (rootTransform != null && ReferenceEquals(currentTransform, rootTransform))
                {
                    break;
                }

                currentTransform = currentTransform.parent;
            }
        }

        static Game.IScopeNode? TryGetScopeNode(Transform t)
        {
            if (t == null)
                return null;

            // Unity cannot GetComponent by interface reliably; use known concrete scope types.
            var baseScope = t.GetComponent<BaseLifetimeScope>();
            if (baseScope != null)
                return baseScope;

            var runtimeScope = t.GetComponent<RuntimeLifetimeScope>();
            if (runtimeScope != null)
                return runtimeScope;

            return null;
        }

        /// <summary>
        /// ポインター位置が候補の親階層の全ての Mask 範囲内かをチェックする。
        /// </summary>
        bool TestPointAgainstParentMasks(
            Vector2 screenPosition,
            Game.IScopeNode candidate,
            Game.IScopeNode rootScope,
            Camera? camera)
        {
            CollectParentMaskServices(candidate, rootScope, _maskBuffer);

            if (_maskBuffer.Count == 0)
            {
                return true; // Mask がなければ通過
            }

            // 全ての Mask でチェック
            foreach (var maskService in _maskBuffer)
            {
                var result = maskService.TestPoint(screenPosition, camera);
                if (!result.Passed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 候補の UIElement が親階層の全ての Mask で可視かをチェックする。
        /// </summary>
        bool TestElementVisibilityAgainstParentMasks(
            Game.IScopeNode candidate,
            Game.IScopeNode rootScope,
            Camera? camera)
        {
            CollectParentMaskServices(candidate, rootScope, _maskBuffer);

            if (_maskBuffer.Count == 0)
            {
                return true; // Mask がなければ通過
            }

            // 候補の RectTransform を取得
            var candidateRect = candidate.Identity?.SelfTransform != null
                ? candidate.Identity.SelfTransform.GetComponent<RectTransform>()
                : null;
            if (candidateRect == null)
            {
                return true; // RectTransform がなければ通過
            }

            // 全ての Mask でチェック
            foreach (var maskService in _maskBuffer)
            {
                var result = maskService.TestElementVisibility(candidateRect, camera);
                if (!result.Passed)
                {
                    return false;
                }
            }

            return true;
        }

        // ----------------------------------------------------------------
        // 内部メソッド - カメラ取得
        // ----------------------------------------------------------------

        /// <summary>
        /// 座標変換に使用するカメラを取得する。
        /// ScreenOverlay の場合は null を返す。
        /// </summary>
        Camera? GetCamera()
        {
            if (_canvasService.CanvasType == UICanvasType.ScreenOverlay)
            {
                return null;
            }
            return _canvasService.UICamera;
        }

        // ----------------------------------------------------------------
        // 内部メソッド - 候補収集
        // ----------------------------------------------------------------

        /// <summary>
        /// rootScope配下の LifetimeScope ツリーを ScopeNodeHierarchy から深さ優先で走査し、
        /// IUIElementState を持つ選択可能なノードを収集します。
        /// 
        /// ## 探索ルール
        /// 
        /// - 子スコープは ScopeNodeHierarchy から取得する
        /// - 各スコープの Resolver で IUIElementState を解決する
        /// - Active=false の UIElement を見つけたらその枝の探索を打ち切る
        /// </summary>
        void CollectAllSelectableCandidates(
            Game.IScopeNode rootScope,
            List<Game.IScopeNode> results)
        {
            if (rootScope == null)
            {
                return;
            }

            // GCアロケーション回避のため、キャッシュされた HashSet/Stack を使用
            _visitedBuffer.Clear();
            _traverseScopeStack.Clear();

            _traverseScopeStack.Push(rootScope);
            _visitedBuffer.Add(rootScope);

            while (_traverseScopeStack.Count > 0)
            {
                var current = _traverseScopeStack.Pop();
                if (current == null)
                {
                    continue;
                }

                var resolver = current.Resolver;
                IUIElementState? state = null;
                if (resolver != null)
                {
                    resolver.TryResolve<IUIElementState>(out state);
                }

                _elementStateCache[current] = state;

                if (state != null)
                {
                    if (!state.IsActive)
                    {
                        continue;
                    }

                    results.Add(current);
                }

                var children = Game.ScopeNodeHierarchy.GetChildrenOrEmpty(current);
                for (int i = children.Count - 1; i >= 0; i--)
                {
                    var child = children[i];
                    if (child != null)
                    {
                        if (_visitedBuffer.Add(child))
                            _traverseScopeStack.Push(child);
                    }
                }
            }
        }

        // ----------------------------------------------------------------
        // 内部メソッド - ヒットテスト
        // ----------------------------------------------------------------

        /// <summary>
        /// UIElementに対するヒットテストを行う。
        /// 
        /// ## 判定方法
        /// 
        /// UIElementStateServiceのHitTestRectsを使用。
        /// いずれかのRectTransformに含まれていればヒット。
        /// 
        /// ## 注意
        /// 
        /// Mask 判定はこのメソッドでは行わない。
        /// 呼び出し元で別途親階層の IUIRectMaskService を使用する。
        /// </summary>
        bool HitTestElement(Game.IScopeNode element, Vector2 screenPosition)
        {
            return HitTestElement(element, screenPosition, null);
        }

        bool HitTestElement(Game.IScopeNode element, Vector2 screenPosition, IUIElementState? cachedState)
        {
            var state = cachedState;
            if (state == null)
                TryGetElementState(element, out state);

            var selfRect = element.Identity?.SelfTransform as RectTransform;
            var camera = GetCamera();

            if (state == null)
            {
                // Stateがない場合はGameObjectのRectTransformで判定
                // GetComponent を避け、直接キャストを試みる（UIは通常RectTransform）
                if (selfRect != null && _canvasService.RectContainsScreenPoint(selfRect, screenPosition))
                    return true;

                return HitTestOwnedGraphics(element, screenPosition, camera);
            }

            var hitTestRects = state.HitTestRects;
            if (hitTestRects.Count == 0)
            {
                // HitTestRectsが空の場合もGameObjectのRectTransformで判定
                if (selfRect != null && _canvasService.RectContainsScreenPoint(selfRect, screenPosition))
                    return true;

                return HitTestOwnedGraphics(element, screenPosition, camera);
            }

            // いずれかのRectTransformに含まれていればヒット
            foreach (var rect in hitTestRects)
            {
                if (_canvasService.RectContainsScreenPoint(rect, screenPosition))
                {
                    return true;
                }
            }

            // UIElementState の HitTestRects を厳密に優先する。
            // 拡張ヒット（所有 Graphic フォールバック）は指定領域より大きく当たる原因になるため使用しない。
            return false;
        }

        bool HitTestOwnedGraphics(Game.IScopeNode element, Vector2 screenPosition, Camera? camera)
        {
            if (!TryGetElementTransform(element, out var elementTransform) || elementTransform == null)
                return false;

            _traverseStack.Clear();
            _traverseStack.Add(elementTransform);

            while (_traverseStack.Count > 0)
            {
                var tr = _traverseStack[^1];
                _traverseStack.RemoveAt(_traverseStack.Count - 1);

                if (IsOtherUIElementBoundary(tr, elementTransform))
                    continue;

                _nodeGraphics.Clear();
                tr.GetComponents(_nodeGraphics);
                for (int i = 0; i < _nodeGraphics.Count; i++)
                {
                    var g = _nodeGraphics[i];
                    if (g == null || !g.isActiveAndEnabled || !g.raycastTarget)
                        continue;
                    if (g.canvasRenderer != null && g.canvasRenderer.cull)
                        continue;

                    var rt = g.rectTransform;
                    if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPosition, camera))
                        return true;
                }

                for (int cidx = tr.childCount - 1; cidx >= 0; cidx--)
                    _traverseStack.Add(tr.GetChild(cidx));
            }

            return false;
        }

        /// <summary>
        /// UIElementの中心位置をスクリーン座標で取得する。
        /// 
        /// ## 計算方法
        /// 
        /// HitTestRectsの最初のRectTransformの中心を使用。
        /// なければGameObjectのRectTransformを使用。
        /// </summary>
        Vector2 GetElementCenterScreen(Game.IScopeNode element)
        {
            TryGetElementState(element, out var state);
            return GetElementCenterScreen(element, state);
        }

        Vector2 GetElementCenterScreen(Game.IScopeNode element, IUIElementState? cachedState)
        {
            RectTransform? rect = null;

            // HitTestRectsから取得を試みる
            var state = cachedState;
            if (state == null)
                TryGetElementState(element, out state);

            if (state != null && state.HitTestRects.Count > 0)
            {
                rect = state.HitTestRects[0];
            }

            // なければGameObjectのRectTransformを使用（GetComponent を避け直接キャスト）
            if (rect == null)
            {
                rect = element.Identity?.SelfTransform as RectTransform;
            }

            if (rect == null)
            {
                return Vector2.zero;
            }

            return GetRectCenterScreen(rect);
        }

        void TryGetElementState(Game.IScopeNode element, out IUIElementState? state)
        {
            if (_elementStateCache.TryGetValue(element, out state))
                return;

            state = null;
            var resolver = element.Resolver;
            if (resolver != null && resolver.TryResolve<IUIElementState>(out var resolved) && resolved != null)
            {
                state = resolved;
                _elementStateCache[element] = state;
                return;
            }

            state = element.GetUIElementState();
            _elementStateCache[element] = state;
        }

        /// <summary>
        /// RectTransformの中心をスクリーン座標で取得する。
        /// </summary>
        Vector2 GetRectCenterScreen(RectTransform rect)
        {
            rect.GetWorldCorners(_corners);
            var center = (_corners[0] + _corners[2]) * 0.5f;
            return RectTransformUtility.WorldToScreenPoint(GetCamera(), center);
        }

        // ----------------------------------------------------------------
        // 描画順ユーティリティ
        // ----------------------------------------------------------------

        // --- Render key computation (only the render-related fields) ---
        struct RenderKey
        {
            public int SortingLayerValue;
            public int SortingOrder;
            public int RenderOrder;
            public int AbsoluteDepth;
            public int SiblingIndex;
        }

        RenderKey GetPointerRenderKeyAtPoint(Transform elementTransform, Vector2 screenPos, Camera? cam)
        {
            // Traverse per-Transform and stop at other UIElementLifetimeScope boundaries so
            // that an owner does not absorb child UIElements' Graphics (prevents parent stealing child's depth)
            bool hasBest = false;
            int bestLayerValue = 0;
            int bestSortingOrder = 0;
            int bestRenderOrder = 0;
            int bestDepth = 0;

            _traverseStack.Clear();
            _traverseStack.Add(elementTransform);

            while (_traverseStack.Count > 0)
            {
                var tr = _traverseStack[^1];
                _traverseStack.RemoveAt(_traverseStack.Count - 1);

                if (IsOtherUIElementBoundary(tr, elementTransform))
                    continue;

                _nodeGraphics.Clear();
                tr.GetComponents(_nodeGraphics); // only components on this Transform
                for (int i = 0; i < _nodeGraphics.Count; i++)
                {
                    var g = _nodeGraphics[i];
                    if (!g.isActiveAndEnabled) continue;
                    if (g.canvasRenderer != null && g.canvasRenderer.cull) continue;

                    var rt = g.rectTransform;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam)) continue;

                    var canvas = g.canvas;
                    int layerValue = 0;
                    int sortingOrder = 0;
                    int renderOrder = 0;
                    if (canvas != null)
                    {
                        layerValue = SortingLayer.GetLayerValueFromID(canvas.sortingLayerID);
                        sortingOrder = canvas.sortingOrder;
                        renderOrder = canvas.rootCanvas != null ? canvas.rootCanvas.renderOrder : canvas.renderOrder;
                    }

                    int depth = g.canvasRenderer != null ? g.canvasRenderer.absoluteDepth : 0;

                    if (!hasBest)
                    {
                        hasBest = true;
                        bestLayerValue = layerValue;
                        bestSortingOrder = sortingOrder;
                        bestRenderOrder = renderOrder;
                        bestDepth = depth;
                    }
                    else
                    {
                        // sortingLayer -> sortingOrder -> renderOrder -> depth
                        if (layerValue != bestLayerValue)
                        {
                            if (layerValue > bestLayerValue)
                            { bestLayerValue = layerValue; bestSortingOrder = sortingOrder; bestRenderOrder = renderOrder; bestDepth = depth; }
                        }
                        else if (sortingOrder != bestSortingOrder)
                        {
                            if (sortingOrder > bestSortingOrder)
                            { bestSortingOrder = sortingOrder; bestRenderOrder = renderOrder; bestDepth = depth; }
                        }
                        else if (renderOrder != bestRenderOrder)
                        {
                            if (renderOrder > bestRenderOrder)
                            { bestRenderOrder = renderOrder; bestDepth = depth; }
                        }
                        else if (depth > bestDepth)
                        {
                            bestDepth = depth;
                        }
                    }
                }

                // push children (reverse to preserve order similar to recursive DFS)
                for (int cidx = tr.childCount - 1; cidx >= 0; cidx--)
                    _traverseStack.Add(tr.GetChild(cidx));
            }

            var rect = elementTransform.GetComponent<RectTransform>();
            int siblingIndex = rect != null ? rect.GetSiblingIndex() : 0;

            if (hasBest)
            {
                return new RenderKey
                {
                    SortingLayerValue = bestLayerValue,
                    SortingOrder = bestSortingOrder,
                    RenderOrder = bestRenderOrder,
                    AbsoluteDepth = bestDepth,
                    SiblingIndex = siblingIndex
                };
            }

            // Fallback: use owner canvas info and owned-subtree max depth (respecting boundaries)
            int fallbackLayerValue = 0, fallbackSortingOrder = 0, fallbackRenderOrder = 0;
            var c2 = elementTransform.GetComponentInParent<Canvas>();
            if (c2 != null)
            {
                fallbackLayerValue = SortingLayer.GetLayerValueFromID(c2.sortingLayerID);
                fallbackSortingOrder = c2.sortingOrder;
                fallbackRenderOrder = c2.rootCanvas != null ? c2.rootCanvas.renderOrder : c2.renderOrder;
            }

            int max = int.MinValue;
            _traverseStack.Clear();
            _traverseStack.Add(elementTransform);
            while (_traverseStack.Count > 0)
            {
                var tr = _traverseStack[^1];
                _traverseStack.RemoveAt(_traverseStack.Count - 1);

                if (IsOtherUIElementBoundary(tr, elementTransform))
                    continue;

                _rendererBuffer.Clear();
                tr.GetComponents(_rendererBuffer); // only on this Transform
                for (int i = 0; i < _rendererBuffer.Count; i++)
                    max = Mathf.Max(max, _rendererBuffer[i].absoluteDepth);

                for (int cidx = tr.childCount - 1; cidx >= 0; cidx--)
                    _traverseStack.Add(tr.GetChild(cidx));
            }

            int fallbackDepth = max == int.MinValue ? 0 : max;
            return new RenderKey
            {
                SortingLayerValue = fallbackLayerValue,
                SortingOrder = fallbackSortingOrder,
                RenderOrder = fallbackRenderOrder,
                AbsoluteDepth = fallbackDepth,
                SiblingIndex = siblingIndex
            };
        }

        PointerSortKey BuildPointerSortKey(
            Game.IScopeNode e,
            IUIElementState? st,
            Game.IScopeNode rootScope,
            Vector2 screenPos,
            Camera? cam)
        {
            if (!TryGetElementTransform(e, out var elementTransform))
            {
                return default;
            }

            var renderKey = GetPointerRenderKeyAtPoint(elementTransform, screenPos, cam);

            int selectionOrder = st?.SelectionOrder ?? 0;
            int uiDepth = GetUiElementDepth(e, rootScope);
            int stableTie = GetStableTie(elementTransform);

            return new PointerSortKey(
                renderKey.SortingLayerValue,
                renderKey.SortingOrder,
                renderKey.RenderOrder,
                renderKey.AbsoluteDepth,
                selectionOrder,
                uiDepth,
                stableTie,
                renderKey.SiblingIndex);
        }

        int GetUiElementDepth(Game.IScopeNode e, Game.IScopeNode rootScope)
        {
            int d = 0;
            Game.IScopeNode? cur = e;
            while (cur != null && !ReferenceEquals(cur, rootScope))
            {
                cur = cur.Parent;
                if (cur != null && TryResolveOwnedUIState(cur, out _)) d++;
            }
            return d;
        }

        int GetStableTie(Transform elementTransform)
        {
            unchecked
            {
                int h = 17;
                var t = elementTransform;
                while (t != null)
                {
                    h = h * 31 + t.GetSiblingIndex();
                    t = t.parent;
                }
                return h;
            }
        }

        bool IsOtherUIElementBoundary(Transform t, Transform owner)
        {
            if (t == owner) return false;
            if (t.TryGetComponent<UIElementStateMB>(out _))
                return true; // 別の UIElement の境界
            return false;
        }

        bool TryGetElementTransform(Game.IScopeNode e, out Transform transform)
        {
            if (e is Component comp)
            {
                transform = comp.transform;
                return true;
            }

            if (e.Identity?.SelfTransform != null)
            {
                transform = e.Identity.SelfTransform;
                return true;
            }

            transform = null!;
            return false;
        }

        bool TryResolveOwnedUIState(Game.IScopeNode e, out IUIElementState? state)
        {
            state = null;
            var resolver = e.Resolver;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<IUIElementState>(out var st) || st == null)
                return false;

            if (!ReferenceEquals(st.Owner, e))
                return false;

            state = st;
            return true;
        }

        /// <summary>
        /// NavigateDirectionをVector2に変換する。
        /// </summary>
        static Vector2 DirectionToVector(NavigateDirection direction)
        {
            return direction switch
            {
                NavigateDirection.Up => Vector2.up,
                NavigateDirection.Down => Vector2.down,
                NavigateDirection.Left => Vector2.left,
                NavigateDirection.Right => Vector2.right,
                _ => Vector2.zero
            };
        }

        // ----------------------------------------------------------------
        // PointerCandidateComparer（GCアロケーション回避用）
        // ----------------------------------------------------------------

        /// <summary>
        /// ポインター候補のソート用 Comparer。
        /// ラムダのクロージャによるGCアロケーションを回避するためにクラスとして実装。
        /// </summary>
        sealed class PointerCandidateComparer : IComparer<SelectCandidate>
        {
            readonly Dictionary<Game.IScopeNode, PointerSortKey> _cache;

            public PointerCandidateComparer(Dictionary<Game.IScopeNode, PointerSortKey> cache)
            {
                _cache = cache;
            }

            public int Compare(SelectCandidate a, SelectCandidate b)
            {
                var ae = a.Element;
                var be = b.Element;
                if (ae == null) return be == null ? 0 : 1;
                if (be == null) return -1;

                // キャッシュから取得（BuildPointerSortKeyは事前に呼ばれているはず）
                if (!_cache.TryGetValue(ae, out var ak)) return 1;
                if (!_cache.TryGetValue(be, out var bk)) return -1;

                // 優先度（SelectionOrder）が高いほうを最優先
                if (ak.SelectionOrder != bk.SelectionOrder) return bk.SelectionOrder.CompareTo(ak.SelectionOrder);

                // まず見た目（前面）で決める
                if (ak.SortingLayerValue != bk.SortingLayerValue) return bk.SortingLayerValue.CompareTo(ak.SortingLayerValue);
                if (ak.SortingOrder != bk.SortingOrder) return bk.SortingOrder.CompareTo(ak.SortingOrder);
                if (ak.RenderOrder != bk.RenderOrder) return bk.RenderOrder.CompareTo(ak.RenderOrder);
                if (ak.AbsoluteDepth != bk.AbsoluteDepth) return bk.AbsoluteDepth.CompareTo(ak.AbsoluteDepth);

                // 優先度が同じなら「子（深いUIElement）」を優先
                if (ak.UiDepth != bk.UiDepth) return bk.UiDepth.CompareTo(ak.UiDepth);

                // 最終同点潰し（安定化）
                if (ak.StableTie != bk.StableTie) return bk.StableTie.CompareTo(ak.StableTie);

                // 最後の保険: sibling index
                return bk.SiblingIndex.CompareTo(ak.SiblingIndex);
            }
        }

        sealed class NavigationCandidateComparer : IComparer<SelectCandidate>
        {
            readonly Dictionary<Game.IScopeNode, int> _selectionOrderCache;

            public NavigationCandidateComparer(Dictionary<Game.IScopeNode, int> selectionOrderCache)
            {
                _selectionOrderCache = selectionOrderCache;
            }

            public int Compare(SelectCandidate a, SelectCandidate b)
            {
                var ae = a.Element;
                var be = b.Element;
                if (ae == null) return be == null ? 0 : 1;
                if (be == null) return -1;

                if (a.IsExplicitLink != b.IsExplicitLink)
                    return b.IsExplicitLink.CompareTo(a.IsExplicitLink);

                var aOrder = _selectionOrderCache.TryGetValue(ae, out var av) ? av : 0;
                var bOrder = _selectionOrderCache.TryGetValue(be, out var bv) ? bv : 0;
                if (aOrder != bOrder)
                    return bOrder.CompareTo(aOrder);

                var scoreCmp = b.Score.CompareTo(a.Score);
                if (scoreCmp != 0)
                    return scoreCmp;

                var dirCmp = b.DirectionMatch.CompareTo(a.DirectionMatch);
                if (dirCmp != 0)
                    return dirCmp;

                return a.Distance.CompareTo(b.Distance);
            }
        }
    }

    // ================================================================
    // SelectCandidateProviderWorld: World用の候補プロバイダー（暫定）
    // ================================================================
    //
    // 現状は Screen 実装を流用する。World Canvas でも
    // RectTransform のワールド座標をスクリーンに変換できれば
    // 選択判定としては十分なため。
    //
    // 将来的にワールド特有の優先順位や距離評価が必要になったら
    // ここを拡張する。
    public sealed class SelectCandidateProviderWorld : ISelectCandidateProvider
    {
        readonly SelectCandidateProviderScreen _inner;

        public float DirectionThreshold
        {
            get => _inner.DirectionThreshold;
            set => _inner.DirectionThreshold = value;
        }

        public float DistanceWeight
        {
            get => _inner.DistanceWeight;
            set => _inner.DistanceWeight = value;
        }

        public SelectCandidateProviderWorld(IUICanvasService canvasService)
        {
            _inner = new SelectCandidateProviderScreen(canvasService);
        }

        public void GetNavigationCandidates(
            Game.IScopeNode? current,
            NavigateDirection direction,
            Game.IScopeNode rootScope,
            List<SelectCandidate> results)
        {
            _inner.GetNavigationCandidates(current, direction, rootScope, results);
        }

        public void GetPointerHitCandidates(
            Vector2 screenPosition,
            Game.IScopeNode rootScope,
            List<SelectCandidate> results)
        {
            _inner.GetPointerHitCandidates(screenPosition, rootScope, results);
        }

        public void GetAllSelectableCandidates(
            Game.IScopeNode rootScope,
            List<Game.IScopeNode> results)
        {
            _inner.GetAllSelectableCandidates(rootScope, results);
        }
    }
}
