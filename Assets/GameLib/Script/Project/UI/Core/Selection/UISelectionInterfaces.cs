#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Game.UI
{
    // ================================================================
    // UISelectionInterfaces.cs - UISelectionServiceの分割インターフェース群
    // ================================================================
    //
    // ## 概要
    //
    // UISelectionServiceの機能を論理的に分割したインターフェース群:
    //
    // 1. **IUISelectionState**: 選択状態の読み取り（Current, Previous, イベント）
    // 2. **IUISelectionNavigation**: ナビゲーション/ポインター選択API
    // 3. **IUISelectionInputRelay**: 入力転送API
    // 4. **IUISelectionService**: 上記3つを統合した完全API
    // 5. **IUISelectionServiceInternal**: ModalStack用内部API
    //
    // ## なぜ分割するか
    //
    // 1. **依存関係の最小化**: 各システムは必要な機能だけを参照
    // 2. **テスト容易性**: モック作成が容易
    // 3. **責務の明確化**: インターフェースを見れば何ができるかわかる
    //
    // ## 使用シーン
    //
    // - UINavigationService: IUISelectionNavigation（ナビゲーション機能のみ）
    // - UIInputService: IUISelectionInputRelay（入力転送のみ）
    // - デバッグUI: IUISelectionState（状態確認のみ）
    // - 統合システム: IUISelectionService（全機能）
    //
    // ================================================================

    // ================================================================
    // IUISelectionState: 選択状態の読み取りインターフェース
    // ================================================================

    /// <summary>
    /// 選択状態の読み取り専用インターフェース。
    /// 
    /// ## 役割
    /// 
    /// 現在の選択状態を外部に公開する。
    /// 選択の変更はこのインターフェースからは行えない。
    /// 
    /// ## 使用シーン
    /// 
    /// - UIの選択状態表示（ハイライト表示等）
    /// - 選択状態に応じた処理分岐
    /// - デバッグUI/ログ出力
    /// - 選択変更イベントの購読
    /// </summary>
    public interface IUISelectionState
    {
        // ----------------------------------------------------------------
        // 選択状態
        // ----------------------------------------------------------------

        /// <summary>
        /// 現在選択中のUIElementLifetimeScope。
        /// 
        /// ## nullの場合
        /// 
        /// - 選択がクリアされている
        /// - まだ何も選択されていない
        /// </summary>
        IScopeNode? CurrentElement { get; }

        /// <summary>
        /// 前回選択されていたUIElementLifetimeScope。
        /// 
        /// ## 用途
        /// 
        /// - 選択履歴の追跡
        /// - 戻る操作の実装
        /// - フォールバック選択の参照
        /// </summary>
        IScopeNode? PreviousElement { get; }

        /// <summary>
        /// 現在ホバー中のUIElementLifetimeScope。
        /// 
        /// ## 用途
        /// 
        /// - マウス操作時のホバー表示
        /// - ツールチップ表示のトリガー
        /// 
        /// ## 注意
        /// 
        /// ナビゲーションモードではnullになる。
        /// ホバーと選択は独立した概念。
        /// </summary>
        IScopeNode? HoveredElement { get; }

        /// <summary>
        /// 現在選択中のUIElementに登録されたIUIInputConsumer一覧。
        /// 
        /// ## 用途
        /// 
        /// - 入力イベントの配信先リスト
        /// - 選択中UIElementの機能確認
        /// 
        /// ## ソート順
        /// 
        /// Priority降順でソートされている。
        /// インデックス0が最優先のConsumer。
        /// 
        /// ## 注意
        /// 
        /// UIElementは選択可能だがConsumerがないこともある。
        /// その場合は空リストになる。
        /// </summary>
        IReadOnlyList<IUIInputConsumer> CurrentConsumers { get; }

        // ----------------------------------------------------------------
        // イベント
        // ----------------------------------------------------------------

        /// <summary>
        /// 選択が変更されたときに発火するイベント。
        /// 
        /// ## 発火タイミング
        /// 
        /// - Select/TrySelectが成功した時
        /// - ClearSelectionが呼ばれた時
        /// - フォールバック選択が実行された時
        /// 
        /// ## 引数
        /// 
        /// 新しい選択先（nullの場合は選択なし）
        /// </summary>
        event Action<IScopeNode?>? OnSelectionChanged;

        /// <summary>
        /// ホバーが変更されたときに発火するイベント。
        /// 
        /// ## 発火タイミング
        /// 
        /// - マウス移動でホバー対象が変わった時
        /// - ナビゲーションモードに切り替わった時（nullになる）
        /// </summary>
        event Action<IScopeNode?>? OnHoverChanged;
    }

    // ================================================================
    // IUISelectionNavigation: ナビゲーション/ポインター選択インターフェース
    // ================================================================

    /// <summary>
    /// ナビゲーション/ポインターによる選択を行うインターフェース。
    /// 
    /// ## 役割
    /// 
    /// 入力デバイス（キーボード/ゲームパッド/マウス）からの
    /// 選択操作を処理する。
    /// 
    /// ## 使用シーン
    /// 
    /// - UINavigationService: ナビゲーション入力を選択に変換
    /// - UIInputService: ポインター移動をホバー/選択に変換
    /// - ModalStackService: Pop時の選択復元
    /// </summary>
    public interface IUISelectionNavigation
    {
        // ----------------------------------------------------------------
        // 直接選択API
        // ----------------------------------------------------------------

        /// <summary>
        /// 指定したUIElementを選択する。
        /// 
        /// ## 処理
        /// 
        /// TrySelectと同じ判定（CanSelect）を行い、許可される場合のみ選択を変更。
        /// 
        /// ## 用途
        /// 
        /// - ModalStackからの選択復元
        /// - プログラム的な選択変更
        /// </summary>
        /// <param name="target">選択したいUIElement</param>
        /// <returns>選択状態が変更された場合true（同一対象/不許可の場合はfalse）</returns>
        bool Select(IScopeNode? target);

        /// <summary>
        /// 指定したUIElementの選択を試みる。
        /// 
        /// ## 成功条件
        /// 
        /// 1. targetがnullでない
        /// 2. targetがModal Stack境界内にいる
        /// 3. targetのUIElementStateがEffectivelyActive
        /// 
        /// ## 失敗時
        /// 
        /// 現在の選択は維持される。
        /// 
        /// ## 用途
        /// 
        /// - 外部から明示的に選択を変更したい場合
        /// - クリック/タップによる選択
        /// </summary>
        /// <param name="target">選択したいUIElement</param>
        /// <returns>選択に成功した場合true</returns>
        bool TrySelect(IScopeNode target);

        /// <summary>
        /// 選択をクリアする（選択なしの状態にする）。
        /// 
        /// ## 用途
        /// 
        /// - 画面遷移時の選択リセット
        /// - バックグラウンドクリック時
        /// </summary>
        void ClearSelection();

        // ----------------------------------------------------------------
        // ナビゲーション選択
        // ----------------------------------------------------------------

        /// <summary>
        /// ナビゲーション入力による選択を試みる。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. CandidateProviderから候補を取得
        /// 2. 候補を方向とスコアでソート
        /// 3. 上位候補からTrySelectを試行
        /// 4. 成功するまで繰り返す
        /// 
        /// ## 戻り値
        /// 
        /// 選択が変更された場合true。
        /// 候補がない、または全ての候補で失敗した場合false。
        /// </summary>
        /// <param name="direction">ナビゲーション方向</param>
        /// <returns>選択が変更された場合true</returns>
        bool TryNavigateSelect(NavigateDirection direction);

        // ----------------------------------------------------------------
        // ポインター選択
        // ----------------------------------------------------------------

        /// <summary>
        /// ポインター位置による選択を試みる。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. CandidateProviderからヒット候補を取得
        /// 2. 前面優先でCanSelectを通る候補を採用
        /// 3. Hover と Select を同期して更新
        /// 3. 成功したら終了
        /// 
        /// ## 戻り値
        /// 
        /// 選択が変更された場合true。
        /// ヒットしない、または全ての候補で失敗した場合false。
        /// </summary>
        /// <param name="screenPosition">スクリーン座標</param>
        /// <returns>選択が変更された場合true</returns>
        bool TryPointerSelect(Vector2 screenPosition);

        /// <summary>
        /// ポインター位置によるホバー更新。
        /// 
        /// ## 処理
        /// 
        /// 選択は変更せず、Hoveredプロパティのみ更新。
        /// IUISelectionState.OnHoverChangedイベントが発火する。
        /// </summary>
        /// <param name="screenPosition">スクリーン座標</param>
        void UpdateHover(Vector2 screenPosition);

        // ----------------------------------------------------------------
        // ユーティリティ
        // ----------------------------------------------------------------

        /// <summary>
        /// 指定したUIElementが選択可能かどうかを判定する。
        /// 
        /// ## 判定内容
        /// 
        /// TrySelectの判定ロジックと同等:
        /// - Modal Stack境界内にいるか
        /// - EffectivelyActiveか
        /// 
        /// ## 用途
        /// 
        /// - UI上で選択可能かどうかの表示
        /// - 候補フィルタリング
        /// </summary>
        bool CanSelect(IScopeNode? target);
    }

    // ================================================================
    // IUISelectionInputRelay: 入力転送インターフェース
    // ================================================================

    /// <summary>
    /// 現在の選択に入力を転送するインターフェース。
    /// 
    /// ## 役割
    /// 
    /// 選択中のUIElementに入力イベントを配信する。
    /// 
    /// ## 使用シーン
    /// 
    /// - UINavigationService: ボタン入力の転送
    /// - カスタム入力処理: 特殊な入力イベントの転送
    /// </summary>
    public interface IUISelectionInputRelay
    {
        /// <summary>
        /// 現在選択中のUIElementに入力イベントを転送する。
        /// 
        /// ## 処理フロー
        /// 
        /// 1. CurrentConsumersを取得（Priority順）
        /// 2. 各Consumerに順番に入力を渡す
        /// 3. いずれかがtrueを返したら消費済みとして終了
        /// 
        /// ## 戻り値
        /// 
        /// いずれかのConsumerが消費した場合true。
        /// 選択がない、または誰も消費しなかった場合false。
        /// </summary>
        /// <param name="inputEvent">転送する入力イベント</param>
        /// <returns>消費された場合true</returns>
        bool SendInputToCurrentSelection(in UIInputEvent inputEvent);
    }

    // ================================================================
    // IUISelectionService: 統合インターフェース
    // ================================================================

    /// <summary>
    /// UI選択サービスの統合インターフェース。
    /// 
    /// ## 役割
    /// 
    /// 状態/ナビゲーション/入力転送の全機能を統合。
    /// DIコンテナにはこのインターフェースで登録される。
    /// 
    /// ## 継承関係
    /// 
    /// - IUISelectionState: 状態読み取り
    /// - IUISelectionNavigation: ナビゲーション/ポインター選択
    /// - IUISelectionInputRelay: 入力転送
    /// 
    /// ## 追加機能
    /// 
    /// - ForceSelect: デバッグ用強制選択
    /// </summary>
    public interface IUISelectionService
        : IUISelectionState,
          IUISelectionNavigation,
          IUISelectionInputRelay
    {
        /// <summary>
        /// 強制的に選択を変更する（判定をバイパス）。
        /// 
        /// ## 警告
        /// 
        /// デバッグ用途のみ。本番では使用しないこと。
        /// Modal Stack境界やActive状態を無視して選択を変更する。
        /// </summary>
        void ForceSelect(IScopeNode? target);
    }

    // ================================================================
    // IUISelectionServiceInternal: ModalStack用内部インターフェース
    // ================================================================

    /// <summary>
    /// UISelectionServiceの内部用インターフェース。
    /// 
    /// ## 役割
    /// 
    /// ModalStackServiceがSelectionServiceを操作するために使用。
    /// 
    /// ## なぜ別インターフェースか
    /// 
    /// ModalStackServiceは選択の保存/復元を行う必要があるが、
    /// 現状ではIUIInputConsumerベースで処理している部分がある。
    /// この過渡期の互換性のために内部インターフェースを使用。
    /// 
    /// ## TODO
    /// 
    /// 将来的にはUIElementLifetimeScopeベースに統一し、
    /// このインターフェースは簡素化または削除される予定。
    /// </summary>
    public interface IUISelectionServiceInternal
    {
        /// <summary>
        /// 現在選択中のIUIInputConsumer（ModalStack用）。
        /// 
        /// ## 戻り値
        /// 
        /// CurrentConsumersの最優先（インデックス0）のConsumer。
        /// 選択がない場合はnull。
        /// </summary>
        IUIInputConsumer? Current { get; }

        /// <summary>
        /// IUIInputConsumerを選択する（ModalStack用）。
        /// 
        /// ## 処理
        /// 
        /// IUIInputConsumerからUIElementLifetimeScopeを逆引きして選択。
        /// nullの場合はClearSelectionと同等。
        /// </summary>
        void Select(IUIInputConsumer? target);
    }
}
