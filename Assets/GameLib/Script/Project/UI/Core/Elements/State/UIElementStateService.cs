#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using VContainer;
using Cysharp.Threading.Tasks;
using VNext = Game.Commands.VNext;
using Game.Common;
using Game;

namespace Game.UI
{
    // ================================================================
    // UIElementStateService - UIElementの状態と設定を管理するサービス
    // ================================================================
    //
    // ## 概要
    //
    // UIElementStateServiceは、UIElementの以下を管理するサービス:
    //
    // 1. **Active状態**: UIシステムとしての有効/無効状態
    // 2. **Visible状態**: UIシステムとしての表示/非表示状態
    // 3. **当たり判定RectTransform**: ナビゲーション・ポインター選択に使用
    // 4. **ナビゲーション設定**: 方向オーバーライド、選択可能かどうか
    //
    // ## 重要な設計思想
    //
    // UIシステムのActiveはUI側のフラグとスコープActiveの合算。
    // BaseLifetimeScopeではGameObjectのactive状態がスコープActiveになる。
    // UI側はActive/Visibleのロジックを保持し、Scopeの状態と合成して判断する。
    //
    // ## 当たり判定RectTransformについて
    //
    // UIElementが選択可能かどうかを物理的に判定するために使用。
    //
    // ### ナビゲーション時
    // - RectTransformの中心位置を基準に方向計算を行う
    // - RectTransformの領域がMaskで大部分覆われていたら候補から除外
    //
    // ### ポインター（マウス）時
    // - RectTransformの領域内にポインターがあるかで判定
    // - 複数のRectTransformが設定されている場合、いずれかに含まれていればOK
    //
    // ### なぜリストか
    //
    // 複雑な形状のUIElementを表現するため。
    // 例えば、L字型のUIは2つのRectTransformで当たり判定を構成できる。
    //
    // ## ナビゲーション方向オーバーライドについて
    //
    // 通常、ナビゲーションは自動計算されるが、
    // 明示的に「上を押したらこのUIElementへ」を指定できる。
    //
    // オーバーライドが設定されている方向は自動計算より優先される。
    //
    // ## ナビゲーション選択不可について
    //
    // 一部のUIElement（Page、Window、Panelなど）は
    // ナビゲーションで選択されるべきではない。
    //
    // これらは他のUIElementを包含するコンテナであり、
    // 選択単位としては機能しない。
    //
    // ================================================================

    // ================================================================
    // UIElementStateChangedArgs: 状態変更イベント引数
    // ================================================================

    /// <summary>
    /// UIElement状態変更時のイベント引数。
    /// 
    /// ## 用途
    /// 
    /// - 外部システムへの状態変更通知
    /// - アニメーションシステムとの連携
    /// - デバッグ/ログ出力
    /// </summary>
    public readonly struct UIElementStateChangedArgs
    {
        /// <summary>状態を持つUIElement（UIElementLifetimeScope/RuntimeLifetimeScope）</summary>
        public IScopeNode Owner { get; }

        /// <summary>変更前のActive状態</summary>
        public bool PreviousActive { get; }

        /// <summary>変更後のActive状態</summary>
        public bool CurrentActive { get; }

        /// <summary>変更前のVisible状態</summary>
        public bool PreviousVisible { get; }

        /// <summary>変更後のVisible状態</summary>
        public bool CurrentVisible { get; }

        /// <summary>Active状態が変更されたかどうか</summary>
        public bool ActiveChanged => PreviousActive != CurrentActive;

        /// <summary>Visible状態が変更されたかどうか</summary>
        public bool VisibleChanged => PreviousVisible != CurrentVisible;

        public UIElementStateChangedArgs(
            IScopeNode owner,
            bool previousActive,
            bool currentActive,
            bool previousVisible,
            bool currentVisible)
        {
            Owner = owner;
            PreviousActive = previousActive;
            CurrentActive = currentActive;
            PreviousVisible = previousVisible;
            CurrentVisible = currentVisible;
        }
    }

    // ================================================================
    // NavigationOverride: ナビゲーション方向のオーバーライド設定
    // ================================================================

    /// <summary>
    /// 各方向のナビゲーションオーバーライド設定。
    /// 
    /// ## 用途
    /// 
    /// 自動計算によるナビゲーションを上書きし、
    /// 明示的に「この方向を押したらこのUIElementへ」を指定する。
    /// 
    /// ## 設計
    /// 
    /// nullの場合は自動計算にフォールバック。
    /// 設定されている場合はその要素への移動を試みる
    /// （移動先がActive=falseなら移動しない）。
    /// </summary>
    [Serializable]
    public sealed class NavigationOverride
    {
        /// <summary>
        /// 上方向を押したときの移動先。
        /// nullの場合は自動計算。
        /// UIElementStateMB を指定する。
        /// </summary>
        [Tooltip("上入力時の移動先。空なら自動計算。UIElementStateMB を指定する。")]
        public UIElementStateMB? Up;

        /// <summary>
        /// 下方向を押したときの移動先。
        /// nullの場合は自動計算。
        /// UIElementStateMB を指定する。
        /// </summary>
        [Tooltip("下入力時の移動先。空なら自動計算。UIElementStateMB を指定する。")]
        public UIElementStateMB? Down;

        /// <summary>
        /// 左方向を押したときの移動先。
        /// nullの場合は自動計算。
        /// UIElementStateMB を指定する。
        /// </summary>
        [Tooltip("左入力時の移動先。空なら自動計算。UIElementStateMB を指定する。")]
        public UIElementStateMB? Left;

        /// <summary>
        /// 右方向を押したときの移動先。
        /// nullの場合は自動計算。
        /// UIElementStateMB を指定する。
        /// </summary>
        [Tooltip("右入力時の移動先。空なら自動計算。UIElementStateMB を指定する。")]
        public UIElementStateMB? Right;

        /// <summary>
        /// 指定方向のオーバーライドを取得する。
        /// </summary>
        /// <param name="direction">取得する方向</param>
        /// <returns>オーバーライド先。nullの場合は自動計算を使用。</returns>
        public IScopeNode? GetOverride(NavigateDirection direction)
        {
            var target = direction switch
            {
                NavigateDirection.Up => Up,
                NavigateDirection.Down => Down,
                NavigateDirection.Left => Left,
                NavigateDirection.Right => Right,
                _ => null
            };

            return ResolveScopeNode(target);
        }

        /// <summary>
        /// 指定方向にオーバーライドが設定されているかどうか。
        /// </summary>
        public bool HasOverride(NavigateDirection direction)
        {
            return GetOverride(direction) != null;
        }

        static IScopeNode? ResolveScopeNode(UIElementStateMB? target)
        {
            if (target == null)
                return null;

            if (ScopeFeatureInstallerUtility.TryGetNearestScopeNode(target, includeInactive: true, out var owner))
                return owner;

            return null;
        }
    }

    // ================================================================
    // IUIElementState: UIElement状態の読み取りInterface
    // ================================================================

    /// <summary>
    /// UIElementの状態を読み取るインターフェース。
    /// 
    /// ## 役割
    /// 
    /// 外部システムがUIElementの状態を取得するための読み取り専用API。
    /// 状態の変更はIUIElementStateControllerを通じて行う。
    /// 
    /// ## 使用シーン
    /// 
    /// - 選択処理時のフィルタリング
    /// - ナビゲーション候補の絞り込み
    /// - 描画システムとの連携
    /// - 外部システムからの状態確認
    /// </summary>
    public interface IUIElementState
    {
        // ----------------------------------------------------------------
        // Active/Visible状態
        // ----------------------------------------------------------------

        /// <summary>
        /// このUIElementがActiveかどうか。
        /// 
        /// ## Active=falseの場合
        /// 
        /// - 選択（Select）対象から除外される
        /// - ナビゲーション候補から除外される
        /// - 入力イベントを受け取らない
        /// - ただし、GameObject自体はactive=trueのまま
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// このUIElementが表示されるかどうか。
        /// 
        /// ## Visible=falseの場合
        /// 
        /// - 絶対に描画されない
        /// - Mask等の他の要因に関係なく非表示
        /// - Active状態には影響しない
        /// 
        /// ## 注意
        /// 
        /// Visible=trueでも他の要因で見えなくなることがある:
        /// - Maskによる遮蔽
        /// - Canvas外にいる
        /// - 他のUIに覆われている
        /// - アルファが0
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// このUIElementが実質的にActiveかどうか。
        /// 
        /// ## 計算ロジック
        /// 
        /// 自身のActive状態に加え、親のActive状態も考慮した結果。
        /// 親がActive=falseなら、自身がActive=trueでもfalseを返す。
        /// 
        /// ## 用途
        /// 
        /// 選択判定やナビゲーション判定では、このプロパティを使用する。
        /// 親がActiveでなければ子も実質的にActiveではない。
        /// </summary>
        bool IsEffectivelyActive { get; }

        /// <summary>
        /// 入力受付可能かどうか。
        ///
        /// Active/Visible に加えて、Lifecycle の despawn 演出中かも考慮する。
        /// </summary>
        bool AcceptsInput { get; }

        // ----------------------------------------------------------------
        // 当たり判定
        // ----------------------------------------------------------------

        /// <summary>
        /// 当たり判定に使用するRectTransformのリスト。
        /// 
        /// ## 用途
        /// 
        /// - ナビゲーション時の距離・方向計算
        /// - ポインター（マウス）ヒットテスト
        /// - Mask遮蔽率の計算
        /// 
        /// ## 複数設定の意味
        /// 
        /// 複雑な形状のUIElementを表現するために複数のRectTransformを設定可能。
        /// いずれかのRectTransformにヒットすれば、そのUIElementにヒットしたとみなす。
        /// </summary>
        IReadOnlyList<RectTransform> HitTestRects { get; }


        /// <summary>
        /// 選択優先度。
        /// 
        /// ## 用途
        /// 
        /// ナビゲーション候補が複数ある場合の優先順位付け。
        /// 
        /// ## 数値の意味
        /// 
        /// 数値が大きいほど優先度が高い。
        /// 
        int SelectionOrder { get; }

        /// <summary>
        /// ナビゲーション専用の優先度。
        /// 数値が大きいほど優先される。
        /// </summary>
        int NavigationSelectionOrder { get; }

        // ----------------------------------------------------------------
        // ナビゲーション設定
        // ----------------------------------------------------------------

        /// <summary>
        /// このUIElement自体が選択対象になれる条件。
        /// false の場合、ポインター/ナビゲーション/直接選択のすべてから除外される。
        /// </summary>
        Game.Common.DynamicValue<bool> IsSelectable { get; }

        /// <summary>
        /// ナビゲーション（キーボード/ゲームパッド）選択可能条件（DynamicValue&lt;bool&gt;）。
        /// 
        /// 動的に選択可能性を評価する条件。
        /// Blackboard、Scalar、VarStore、Expression など複数の値源をサポート。
        /// 
        /// ## falseを返す場合
        /// 
        /// - ナビゲーションによる選択候補から完全に除外される
        /// - ポインター（マウス）による選択は可能
        /// - Active状態とは独立した設定
        /// 
        /// ## 用途
        /// 
        /// Page、Window、Panelなどのコンテナ要素に設定。
        /// これらはナビゲーションで直接選択されるべきではない。
        /// </summary>
        Game.Common.DynamicValue<bool> IsNavigationSelectable { get; }

        /// <summary>
        /// ナビゲーション方向のオーバーライド設定。
        /// 
        /// ## 用途
        /// 
        /// 自動計算によるナビゲーションを上書きし、
        /// 明示的な移動先を指定する。
        /// 
        /// ## nullの場合
        /// 
        /// すべての方向で自動計算を使用。
        /// </summary>
        NavigationOverride? NavigationOverride { get; }

        // ----------------------------------------------------------------
        // ナビゲーション評価メソッド
        // ----------------------------------------------------------------

        /// <summary>
        /// ナビゲーション選択可能条件を評価する。
        /// </summary>
        /// <returns>ナビゲーション選択可能な場合true</returns>
        bool EvaluateIsSelectable();

        /// <summary>
        /// ナビゲーション選択可能条件を評価する。
        /// </summary>
        /// <returns>ナビゲーション選択可能な場合true</returns>
        bool EvaluateIsNavigationSelectable();

        // ----------------------------------------------------------------
        // 所有者
        // ----------------------------------------------------------------

        /// <summary>
        /// このUIElementを所有するIScopeNode。
        /// </summary>
        IScopeNode? Owner { get; }

        // ----------------------------------------------------------------
        // イベント
        // ----------------------------------------------------------------

        /// <summary>
        /// 状態が変更されたときに発火するイベント。
        /// 
        /// ## 発火タイミング
        /// 
        /// - Active状態が変更されたとき
        /// - Visible状態が変更されたとき
        /// 
        /// ## 注意
        /// 
        /// HitTestRectsやNavigationOverrideの変更では発火しない。
        /// </summary>
        event Action<UIElementStateChangedArgs>? OnStateChanged;
    }

    // ================================================================
    // IUIElementStateController: UIElement状態の制御Interface
    // ================================================================

    /// <summary>
    /// UIElementの状態を制御するインターフェース。
    /// 
    /// ## 役割
    /// 
    /// UIElementの状態を変更するためのAPI。
    /// IUIElementStateを継承し、読み取りと制御の両方を提供。
    /// 
    /// ## 設計方針
    /// 
    /// 読み取りと制御を分離することで、
    /// 外部からの不正な状態変更を防ぐ。
    /// 
    /// 通常のシステムはIUIElementStateのみを参照し、
    /// 状態を変更する必要があるシステムのみがこのインターフェースを使用。
    /// </summary>
    public interface IUIElementStateController : IUIElementState
    {
        // ----------------------------------------------------------------
        // Active/Visible制御
        // ----------------------------------------------------------------

        /// <summary>
        /// Active状態を設定する。
        /// 
        /// ## 効果
        /// 
        /// - trueに設定: 選択可能になる、入力を受け付ける
        /// - falseに設定: 選択不可になる、入力を受け付けない
        /// - OnStateChangedイベントが発火する
        /// </summary>
        /// <param name="active">新しいActive状態</param>
        void SetActive(bool active);

        /// <summary>
        /// Visible状態を設定する。
        /// 
        /// ## 効果
        /// 
        /// - trueに設定: 描画される可能性がある
        /// - falseに設定: 絶対に描画されない
        /// - OnStateChangedイベントが発火する
        /// </summary>
        /// <param name="visible">新しいVisible状態</param>
        void SetVisible(bool visible);

        /// <summary>
        /// Active状態をトグルする。
        /// </summary>
        void ToggleActive();

        /// <summary>
        /// Visible状態をトグルする。
        /// </summary>
        void ToggleVisible();
    }

    // ================================================================
    // UIElementStateService: メイン実装
    // ================================================================

    /// <summary>
    /// UIElementの状態を管理するサービス。
    /// 
    /// ## 登録方法
    /// 
    /// UIElementStateMBを通じてLifetimeScopeに登録される。
    /// UIElementStateMBがFeatureInstallerとして機能し、
    /// このサービスをDIコンテナに登録する。
    /// 
    /// ## 責務
    /// 
    /// 1. Active/Visible状態の保持と変更通知
    /// 2. IsEffectivelyActiveの計算（親の状態を考慮）
    /// 3. 当たり判定用RectTransformリストの保持
    /// 4. ナビゲーション設定の保持
    /// 
    /// ## 依存関係
    /// 
    /// このサービスはUIElementStateMBから初期設定を受け取る。
    /// 設定の変更はUIElementStateMBを通じて行われ、
    /// このサービスに反映される。
    /// </summary>
    public sealed class UIElementStateService : IUIElementStateController, IUIModalRoot, IScopeAcquireHandler, IScopeReleaseHandler
    {
        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>所有者スコープ</summary>
        readonly IScopeNode _owner;

        /// <summary>Active状態</summary>
        bool _isActive = true;

        /// <summary>Visible状態</summary>
        bool _isVisible = true;

        /// <summary>当たり判定に使用するRectTransformのリスト</summary>
        readonly List<RectTransform> _hitTestRects = new();

        /// <summary>ナビゲーションで選択可能かどうか</summary>
        /// <summary>このUIElement自体が選択可能かを決める条件（DynamicValue<bool>）</summary>
        Game.Common.DynamicValue<bool> _isSelectableCondition;

        /// <summary>キャッシュされた選択可能フラグ</summary>
        bool _isSelectableCached = true;

        /// <summary>ナビゲーションで選択可能かどうかを決める条件（DynamicValue<bool>）</summary>
        /// <summary>ナビゲーションで選択可能かを決める条件（DynamicValue<bool>）</summary>
        Game.Common.DynamicValue<bool> _isNavigationSelectableCondition;

        /// <summary>キャッシュされたナビゲーション選択可能フラグ</summary>
        bool _isNavigationSelectableCached = true;

        /// <summary>ナビゲーション方向のオーバーライド設定</summary>
        NavigationOverride? _navigationOverride;

        /// <summary>選択優先度</summary>
        int _selectionOrder = 0;

        /// <summary>ナビゲーション専用優先度</summary>
        int _navigationSelectionOrder = 0;

        /// <summary>選択時に実行するコマンドリスト</summary>
        readonly VNext.CommandListData _onSelectedCommands;

        /// <summary>選択解除時に実行するコマンドリスト</summary>
        readonly VNext.CommandListData _onDeselectedCommands;

        /// <summary>UISelectionServiceの参照（選択監視用）</summary>
        IUISelectionState? _selectionState;

        /// <summary>コマンド実行用Runner</summary>
        VNext.ICommandRunner? _commandRunner;

        /// <summary>前回の選択状態（自分が選択されていたか）</summary>
        bool _wasSelected;

        /// <summary>コマンド実行用CancellationTokenSource</summary>
        CancellationTokenSource? _commandCts;

        /// <summary>親のUIElementStateキャッシュ（IsEffectivelyActive最適化用）</summary>
        IUIElementState? _cachedParentState;
        bool _parentStateCacheResolved;
        IScopeNode? _cachedParentScope;

        /// <summary>IsEffectivelyActiveのキャッシュ</summary>
        bool _cachedEffectivelyActive;

        /// <summary>IsEffectivelyActiveのDirtyフラグ</summary>
        bool _effectiveActiveDirty = true;

        /// <summary>OwnerのActive状態キャッシュ</summary>
        bool _lastOwnerActive;

        /// <summary>Lifecycle の despawn 状態参照</summary>
        IScopeLifecycleService? _lifecycleService;

        // ----------------------------------------------------------------
        // プロパティ - Active/Visible
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public bool IsActive => _isActive && _owner.IsActive;

        /// <inheritdoc/>
        public bool IsVisible => _isVisible;

        /// <inheritdoc/>
        public IScopeNode? Owner => _owner;

        /// <inheritdoc/>
        public bool AcceptsInput => IsVisible && IsEffectivelyActive && !IsLifecycleDespawning();

        string IUIModalRoot.ModalId => _owner.Identity?.SelfTransform != null
            ? _owner.Identity.SelfTransform.name
            : "(unknown)";

        bool IUIModalRoot.IsActive => IsEffectivelyActive;

        IScopeNode? IUIModalRoot.OwnerScope => _owner;

        bool IUIModalRoot.IsDescendant(IScopeNode? target)
        {
            if (target == null)
                return false;

            var current = target;
            while (current != null)
            {
                if (ReferenceEquals(current, _owner))
                    return true;
                current = current.Parent;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool IsEffectivelyActive
        {
            get
            {
                var ownerActive = _owner.IsActive;
                if (!_effectiveActiveDirty && _lastOwnerActive == ownerActive)
                    return _cachedEffectivelyActive;

                _lastOwnerActive = ownerActive;
                EnsureParentStateCache();

                var selfActive = _isActive && ownerActive;

                // 自身がActiveでなければfalse
                if (!selfActive)
                {
                    _cachedEffectivelyActive = false;
                    _effectiveActiveDirty = false;
                    return false;
                }

                if (_cachedParentState != null)
                    _cachedEffectivelyActive = _cachedParentState.IsEffectivelyActive && selfActive;
                else
                    _cachedEffectivelyActive = selfActive;

                _effectiveActiveDirty = false;
                return _cachedEffectivelyActive;
            }
        }

        // ----------------------------------------------------------------
        // プロパティ - 当たり判定
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public IReadOnlyList<RectTransform> HitTestRects => _hitTestRects;

        /// <inheritdoc/>
        public int SelectionOrder => _selectionOrder;

        /// <inheritdoc/>
        public int NavigationSelectionOrder => _navigationSelectionOrder;

        // ----------------------------------------------------------------
        // プロパティ - ナビゲーション
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public Game.Common.DynamicValue<bool> IsSelectable => _isSelectableCondition;

        /// <inheritdoc/>
        public Game.Common.DynamicValue<bool> IsNavigationSelectable => _isNavigationSelectableCondition;

        /// <inheritdoc/>
        public NavigationOverride? NavigationOverride => _navigationOverride;

        // ----------------------------------------------------------------
        // イベント
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public event Action<UIElementStateChangedArgs>? OnStateChanged;

        // ----------------------------------------------------------------
        // 選択イベントコマンド
        // ----------------------------------------------------------------

        /// <summary>
        /// 選択時に実行するコマンドリスト。
        /// Set/Add/Remove/Swap で操作可能。
        /// </summary>
        public VNext.CommandListData OnSelectedCommands => _onSelectedCommands;

        /// <summary>
        /// 選択解除時に実行するコマンドリスト。
        /// Set/Add/Remove/Swap で操作可能。
        /// </summary>
        public VNext.CommandListData OnDeselectedCommands => _onDeselectedCommands;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// 
        /// ## パラメータ
        /// 
        /// owner: このサービスを持つIScopeNode（UIElementLifetimeScope/RuntimeLifetimeScope）
        /// </summary>
        /// <param name="owner">所有者のスコープノード</param>
        public UIElementStateService(IScopeNode owner, IUIElementStateOptions options, IUISelectionState? selectionState, VNext.ICommandRunner commandRunner)
        {
            _owner = owner;

            // selectionState may not be registered in this scope at construction time.
            // Accept nullable and try to resolve later in OnAcquire if needed.
            _selectionState = selectionState;
            _commandRunner = commandRunner;

            // 初期設定を反映
            _isSelectableCondition = options.IsSelectable;
            _isNavigationSelectableCondition = options.IsNavigationSelectable;
            _navigationOverride = options.NavigationOverride;
            _onSelectedCommands = options.OnSelectedCommands ?? new VNext.CommandListData();
            _onDeselectedCommands = options.OnDeselectedCommands ?? new VNext.CommandListData();
            SetHitTestRects(options.HitTestRects);

            _selectionOrder = options.SelectionOrder;
            _navigationSelectionOrder = options.NavigationSelectionOrder;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _effectiveActiveDirty = true;
            EnsureParentStateCache();
            scope.TryResolveInAncestors<IScopeLifecycleService>(out _lifecycleService);

            // selectionState may not have been available at construction time; try resolve from scope's container
            if (_selectionState == null)
            {
                if (scope.Resolver != null && scope.Resolver.TryResolve<IUISelectionState>(out var ss))
                    _selectionState = ss;
            }

            // 初期化処理: 過去の購読を外してから再登録する（多重登録を防ぐ）
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged -= HandleSelectionChanged;
                _selectionState.OnSelectionChanged += HandleSelectionChanged;

                // 初期選択状態を反映
                _wasSelected = ReferenceEquals(_selectionState?.CurrentElement, _owner);
            }
        }

        public void OnRelease(IScopeNode scope, bool isDestroy)
        {
            // クリーンアップ処理: 購読を解除
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged -= HandleSelectionChanged;
            }

            UnbindParentState();
            _parentStateCacheResolved = false;
            _effectiveActiveDirty = true;
            _lifecycleService = null;
        }




        /// <summary>
        /// 選択変更時のハンドラ。
        /// 自分が選択されたか、選択解除されたかを判定してコマンドを実行する。
        /// </summary>
        void HandleSelectionChanged(IScopeNode? newSelection)
        {
            bool wasSelected = _wasSelected;
            bool isNowSelected = ReferenceEquals(newSelection, _owner);

            // 状態が変化していない場合は何もしない
            if (wasSelected == isNowSelected)
            {
                return;
            }

            _wasSelected = isNowSelected;

            if (isNowSelected)
            {
                // 選択された
                ExecuteOnSelectedCommands().Forget();
            }
            else
            {
                // 選択解除された
                ExecuteOnDeselectedCommands().Forget();
            }
        }

        /// <summary>
        /// 選択時コマンドを実行する。
        /// </summary>
        async UniTaskVoid ExecuteOnSelectedCommands()
        {
            if (_commandRunner == null) return;
            if (_onSelectedCommands.Count == 0) return;

            // 既存の実行をキャンセル
            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = new CancellationTokenSource();

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, NullVarStore.Instance, _commandRunner, _owner, options);

            try
            {
                var result = await _commandRunner.ExecuteListAsync(_onSelectedCommands, ctx, _commandCts.Token, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[UIElementStateService] OnSelected command failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常終了
            }
        }

        /// <summary>
        /// 選択解除時コマンドを実行する。
        /// </summary>
        async UniTaskVoid ExecuteOnDeselectedCommands()
        {
            if (_commandRunner == null) return;
            if (_onDeselectedCommands.Count == 0) return;

            // 既存の実行をキャンセル
            _commandCts?.Cancel();
            _commandCts?.Dispose();
            _commandCts = new CancellationTokenSource();

            var options = VNext.CommandRunOptions.Default;
            var ctx = new VNext.CommandContext(_owner, NullVarStore.Instance, _commandRunner, _owner, options);

            try
            {
                var result = await _commandRunner.ExecuteListAsync(_onDeselectedCommands, ctx, _commandCts.Token, options);
                if (result.Status == VNext.CommandRunStatus.Error)
                    Debug.LogError($"[UIElementStateService] OnDeselected command failed: {result.Message}");
            }
            catch (OperationCanceledException)
            {
                // キャンセルは正常終了
            }
        }

        // ----------------------------------------------------------------
        // Active/Visible制御
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void SetActive(bool active)
        {
            if (_isActive == active) return;

            var prevActive = IsActive;
            _isActive = active;
            var currentActive = IsActive;

            _effectiveActiveDirty = true;

            NotifyStateChanged(prevActive, currentActive, _isVisible, _isVisible);

            Debug.Log($"[UIElementStateService] '{_owner.Identity?.SelfTransform.name}' Active changed: {prevActive} -> {active}");
        }

        /// <inheritdoc/>
        public void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;

            var prevVisible = _isVisible;
            _isVisible = visible;

            _effectiveActiveDirty = true;

            NotifyStateChanged(_isActive, _isActive, prevVisible, _isVisible);

            //Debug.Log($"[UIElementStateService] '{_owner.Identity?.SelfTransform.name}' Visible changed: {prevVisible} -> {visible}");
        }

        /// <inheritdoc/>
        public void ToggleActive()
        {
            SetActive(!_isActive);
        }

        /// <inheritdoc/>
        public void ToggleVisible()
        {
            SetVisible(!_isVisible);
        }

        // ----------------------------------------------------------------
        // 設定メソッド（MBから呼ばれる）
        // ----------------------------------------------------------------

        /// <summary>
        /// 当たり判定用RectTransformを設定する。
        /// 
        /// ## 呼び出し元
        /// 
        /// UIElementStateMBのInstallFeatureで呼び出される。
        /// Inspector設定を反映するために使用。
        /// </summary>
        /// <param name="rects">当たり判定用RectTransformのリスト</param>
        public void SetHitTestRects(IEnumerable<RectTransform>? rects)
        {
            _hitTestRects.Clear();

            if (rects == null) return;

            foreach (var rect in rects)
            {
                if (rect != null)
                {
                    _hitTestRects.Add(rect);
                }
            }
        }

        /// <summary>
        /// ナビゲーション選択可能条件を評価する。
        /// DynamicValue<bool>の値源に応じて、現在の条件を評価する。
        /// </summary>
        /// <returns>ナビゲーション選択可能な場合true</returns>
        public bool EvaluateIsSelectable()
        {
            if (IsLifecycleDespawning())
            {
                _isSelectableCached = false;
                return false;
            }

            var varStore = _owner.Resolver?.TryResolve<IVarStore>(out var resolved) == true ? resolved : new VarStore();
            var context = new Game.Common.SimpleDynamicContext(varStore, _owner);
            if (_isSelectableCondition.TryGet(context, out var selectable))
            {
                _isSelectableCached = selectable;
                return _isSelectableCached;
            }

            return _isSelectableCached;
        }

        public bool EvaluateIsNavigationSelectable()
        {
            if (IsLifecycleDespawning())
            {
                _isNavigationSelectableCached = false;
                return false;
            }

            var varStore = _owner.Resolver?.TryResolve<IVarStore>(out var resolved) == true ? resolved : new VarStore();
            var context = new Game.Common.SimpleDynamicContext(varStore, _owner);
            if (_isNavigationSelectableCondition.TryGet(context, out var selectable))
            {
                _isNavigationSelectableCached = selectable;
                return _isNavigationSelectableCached;
            }

            return _isNavigationSelectableCached;
        }

        /// <summary>
        /// ナビゲーション選択可能フラグをキャッシュする（主にUIElementStateMBから呼ばれる）。
        /// 
        /// ## 呼び出し元
        /// 
        /// UIElementStateMBのInstallFeatureで呼び出される。
        /// </summary>
        [System.Obsolete("DynamicValue<bool> に移行しました。EvaluateIsNavigationSelectable() を使用してください。")]
        public void SetNavigationSelectable(bool selectable)
        {
            _isNavigationSelectableCached = selectable;
        }

        /// <summary>
        /// 選択可能条件を設定する。
        /// </summary>
        public void SetSelectableCondition(Game.Common.DynamicValue<bool> condition)
        {
            _isSelectableCondition = condition;
        }

        /// <summary>
        /// ナビゲーション選択可能条件を設定する。
        /// </summary>
        public void SetNavigationSelectableCondition(Game.Common.DynamicValue<bool> condition)
        {
            _isNavigationSelectableCondition = condition;
        }

        /// <summary>
        /// 選択優先度を設定する。
        /// </summary>
        public void SetSelectionOrder(int selectionOrder)
        {
            _selectionOrder = selectionOrder;
        }

        /// <summary>
        /// ナビゲーション優先度を設定する。
        /// </summary>
        public void SetNavigationSelectionOrder(int navigationSelectionOrder)
        {
            _navigationSelectionOrder = navigationSelectionOrder;
        }

        /// <summary>
        /// ナビゲーションオーバーライドを設定する。
        /// 
        /// ## 呼び出し元
        /// 
        /// UIElementStateMBのInstallFeatureで呼び出される。
        /// </summary>
        /// <param name="override">オーバーライド設定（nullで自動計算を使用）</param>
        public void SetNavigationOverride(NavigationOverride? @override)
        {
            _navigationOverride = @override;
        }

        // ----------------------------------------------------------------
        // 内部メソッド
        // ----------------------------------------------------------------

        /// <summary>
        /// 状態変更を通知する。
        /// </summary>
        void NotifyStateChanged(bool prevActive, bool currActive, bool prevVisible, bool currVisible)
        {
            var args = new UIElementStateChangedArgs(
                _owner,
                prevActive,
                currActive,
                prevVisible,
                currVisible
            );

            OnStateChanged?.Invoke(args);
        }

        void EnsureParentStateCache()
        {
            var parentScope = _owner.Parent;
            if (_parentStateCacheResolved && ReferenceEquals(parentScope, _cachedParentScope))
                return;

            UnbindParentState();
            _cachedParentScope = parentScope;
            _parentStateCacheResolved = true;

            if (parentScope != null)
            {
                var parentResolver = parentScope.Resolver;
                if (parentResolver != null && parentResolver.TryResolve<IUIElementState>(out var parentState) && parentState != null)
                {
                    _cachedParentState = parentState;
                    _cachedParentState.OnStateChanged -= HandleParentStateChanged;
                    _cachedParentState.OnStateChanged += HandleParentStateChanged;
                }
            }

            _effectiveActiveDirty = true;
        }

        void UnbindParentState()
        {
            if (_cachedParentState != null)
                _cachedParentState.OnStateChanged -= HandleParentStateChanged;

            _cachedParentState = null;
            _cachedParentScope = null;
        }

        void HandleParentStateChanged(UIElementStateChangedArgs args)
        {
            _effectiveActiveDirty = true;
        }

        bool IsLifecycleDespawning()
        {
            return _lifecycleService != null && _lifecycleService.IsDespawning;
        }
    }
}
