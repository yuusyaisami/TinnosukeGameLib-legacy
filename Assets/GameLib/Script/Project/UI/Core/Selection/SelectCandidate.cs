#nullable enable
using UnityEngine;
using System.Collections.Generic;
using Game;

namespace Game.UI
{
    // ================================================================
    // SelectCandidate.cs - 選択候補に関する型定義
    // ================================================================
    //
    // ## 概要
    //
    // このファイルには、UI選択システムにおける「候補」に関する型が定義されている:
    //
    // 1. **SelectCandidate**: 選択候補を表す構造体
    // 2. **ISelectCandidateProvider**: 候補を提供するインターフェース
    //
    // ## 用途
    //
    // - ナビゲーション選択: 方向キーによる候補選択
    // - ポインター選択: マウスによるヒット候補
    // - フォールバック: 選択が無効になった時の代替候補
    //
    // ## 設計思想
    //
    // UISelectionServiceとは分離し、候補の概念を独立させている。
    // これにより、候補取得ロジックの変更が選択ロジックに影響しない。
    //
    // ## 候補プロバイダーの実装
    //
    // - SelectCandidateProviderScreen: Screen Canvas用（UICanvasServiceInterfaces.csにある）
    // - SelectCandidateProviderWorld: World Canvas用（将来実装）
    //
    // ================================================================

    // ================================================================
    // SelectCandidate: 選択候補を表す構造体
    // ================================================================

    /// <summary>
    /// 選択候補を表す構造体。
    /// 
    /// ## 役割
    /// 
    /// ナビゲーションやポインター選択時に、候補となるUIElementを
    /// スコアや付加情報と共に保持する。
    /// 
    /// ## フィールド説明
    /// 
    /// - **Element**: 候補のUIElementLifetimeScope
    /// - **Score**: 優先度スコア（大きいほど優先）
    /// - **IsExplicitLink**: ナビゲーションオーバーライドで指定されたか
    /// - **DirectionMatch**: 方向一致度（0.0〜1.0）
    /// - **Distance**: 現在位置からの距離
    /// 
    /// ## スコアリング
    /// 
    /// スコアは以下の要素から計算される:
    /// 1. 明示的リンク（最優先）
    /// 2. 方向一致度
    /// 3. 距離（近いほど高スコア）
    /// </summary>
    public readonly struct SelectCandidate
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>
        /// 候補のUIElementLifetimeScope。
        /// nullの場合は無効な候補。
        /// </summary>
        public IScopeNode? Element { get; }

        /// <summary>
        /// 候補のスコア（大きいほど優先）。
        /// 
        /// ## スコアの目安
        /// 
        /// - 100+: 明示的リンク（最優先）
        /// - 0〜1: 通常の候補（方向一致度ベース）
        /// - float.MinValue: 無効な候補
        /// </summary>
        public float Score { get; }

        /// <summary>
        /// 明示的リンクで指定された候補かどうか。
        /// 
        /// ## 明示的リンクとは
        /// 
        /// UIElementStateServiceのNavigationOverrideで
        /// 特定方向の移動先として明示的に指定された場合true。
        /// 
        /// 明示的リンクは自動計算より常に優先される。
        /// </summary>
        public bool IsExplicitLink { get; }

        /// <summary>
        /// 方向一致度（0.0〜1.0）。
        /// 
        /// ## 計算方法
        /// 
        /// 現在位置から候補への方向ベクトルと、
        /// 入力方向ベクトルの内積。
        /// 
        /// 1.0: 完全に同じ方向
        /// 0.0: 直角方向
        /// -1.0: 逆方向（除外される）
        /// </summary>
        public float DirectionMatch { get; }

        /// <summary>
        /// 現在位置からの距離（ピクセル）。
        /// 
        /// ## 用途
        /// 
        /// 同程度の方向一致度の場合、近い候補を優先する。
        /// </summary>
        public float Distance { get; }

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public SelectCandidate(
            IScopeNode? element,
            float score,
            bool isExplicitLink = false,
            float directionMatch = 0,
            float distance = 0)
        {
            Element = element;
            Score = score;
            IsExplicitLink = isExplicitLink;
            DirectionMatch = directionMatch;
            Distance = distance;
        }

        // ----------------------------------------------------------------
        // 静的ファクトリ
        // ----------------------------------------------------------------

        /// <summary>
        /// 無効な候補を表す定数。
        /// </summary>
        public static SelectCandidate Empty => new(null, float.MinValue);

        /// <summary>
        /// 明示的リンクによる候補を作成する。
        /// </summary>
        /// <param name="element">候補のUIElement</param>
        /// <returns>明示的リンク候補</returns>
        public static SelectCandidate FromExplicitLink(IScopeNode element)
        {
            return new SelectCandidate(
                element,
                score: 100f, // 明示的リンクは最優先
                isExplicitLink: true,
                directionMatch: 1f,
                distance: 0f
            );
        }

        // ----------------------------------------------------------------
        // ユーティリティ
        // ----------------------------------------------------------------

        /// <summary>
        /// 候補が有効かどうか。
        /// </summary>
        public bool IsValid => Element != null;

        public override string ToString()
        {
            var name = Element?.Identity?.SelfTransform != null
                ? Element.Identity.SelfTransform.name
                : "null";
            return $"SelectCandidate({name}, Score={Score:F2}, " +
                   $"Explicit={IsExplicitLink}, Dir={DirectionMatch:F2}, Dist={Distance:F1})";
        }
    }

    // ================================================================
    // ISelectCandidateProvider: 選択候補プロバイダー
    // ================================================================

    /// <summary>
    /// 選択候補を提供するインターフェース。
    /// 
    /// ## 役割
    /// 
    /// - ナビゲーション時の候補リスト提供
    /// - ポインターヒット時の候補リスト提供
    /// - ScreenCanvas/WorldCanvasで異なる実装を提供
    /// 
    /// ## 探索ルール
    /// 
    /// 候補探索は、指定されたrootScopeから深さ優先で行われる。
    /// ただし、以下のルールに従う:
    /// 
    /// 1. Active=falseのUIElementが見つかったら、その枝は探索しない
    /// 2. 探索はModal Stack境界内に限定される
    /// 3. Maskによる遮蔽は候補プロバイダー側で考慮される
    /// 
    /// ## 実装クラス
    /// 
    /// - SelectCandidateProviderScreen: Screen Canvas用
    ///   （UICanvasServiceInterfaces.csに定義）
    /// - SelectCandidateProviderWorld: World Canvas用（将来実装）
    /// </summary>
    public interface ISelectCandidateProvider
    {
        /// <summary>
        /// ナビゲーション候補を取得する。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. rootScope配下の全UIElementを深さ優先で収集
        /// 2. 現在位置から各候補への方向と距離を計算
        /// 3. 指定方向に合致する候補をスコアリング
        /// 4. スコア順でソートして結果に追加
        /// 
        /// ## パラメータ
        /// 
        /// - current: 現在選択中のUIElement（nullの場合は原点から計算）
        /// - direction: ナビゲーション方向
        /// - rootScope: 探索範囲の起点（通常はModal Stack CurrentInputRoot）
        /// - results: 結果を格納するリスト（スコア降順でソート済み）
        /// 
        /// ## 結果のソート
        /// 
        /// 結果は以下の優先順位でソートされる:
        /// 1. 明示的リンク（ExplicitLink）
        /// 2. 方向一致度（高いほど優先）
        /// 3. 距離（近いほど優先）
        /// </summary>
        void GetNavigationCandidates(
            IScopeNode? current,
            NavigateDirection direction,
            IScopeNode rootScope,
            List<SelectCandidate> results);

        /// <summary>
        /// ポインターヒット候補を取得する。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. rootScope配下の全UIElementを深さ優先で収集
        /// 2. 各候補のRectTransformでヒットテスト
        /// 3. ヒットした候補を前面優先でソート
        /// 
        /// ## パラメータ
        /// 
        /// - screenPosition: スクリーン座標（ピクセル）
        /// - rootScope: 探索範囲の起点（通常はModal Stack CurrentInputRoot）
        /// - results: 結果を格納するリスト（前面優先でソート済み）
        /// 
        /// ## 結果のソート
        /// 
        /// 結果は前面優先でソートされる:
        /// - ScreenCanvas: SiblingIndex/Canvas SortOrder
        /// - WorldCanvas: カメラからの距離（近いほど前面）
        /// </summary>
        void GetPointerHitCandidates(
            Vector2 screenPosition,
            IScopeNode rootScope,
            List<SelectCandidate> results);

        /// <summary>
        /// すべての選択可能なUIElementを取得する。
        /// 
        /// ## 用途
        /// 
        /// - フォールバック選択の探索
        /// - デバッグ用の候補一覧表示
        /// - 選択可能要素の統計取得
        /// 
        /// ## 探索ルール
        /// 
        /// ナビゲーション候補と同様に、Active=falseの枝は除外。
        /// </summary>
        void GetAllSelectableCandidates(
            IScopeNode rootScope,
            List<IScopeNode> results);
    }
}
