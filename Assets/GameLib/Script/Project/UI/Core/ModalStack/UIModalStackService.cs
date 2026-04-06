#nullable enable
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Game.UI
{
    // ================================================================
    // ModalOptions: モーダルPush時のオプション設定
    // ================================================================

    /// <summary>
    /// モーダルをPushする際のオプション。
    /// Pop時の挙動やデフォルト選択などを制御する。
    /// 
    /// ## デフォルト選択について
    /// 
    /// DefaultSelectedElementが指定されている場合、
    /// Push時にそのUIElementが選択される。
    /// 
    /// 指定されていない場合は、自動的にモーダル配下の
    /// 最初のナビゲーション選択可能な要素が選択される。
    /// </summary>
    [Serializable]
    public struct ModalOptions
    {
        /// <summary>
        /// モーダルがPopされたときに、自動的に前回の選択にフォールバックするか。
        /// 
        /// ## trueの場合
        /// 
        /// Pop後にPush前の選択状態への復帰を試みる。
        /// ダイアログを閉じた後に元のボタンに戻る動作に使用。
        /// </summary>
        [Tooltip("モーダルがPopされたときに、自動的に前回の選択にフォールバックするか")]
        public bool AutoFallbackOnPop;

        /// <summary>
        /// このモーダル内でのデフォルト選択対象のUIElement。
        /// 
        /// ## 設計
        /// 
        /// 選択単位はUIElementLifetimeScope。
        /// UIにはIUIInputConsumerがあったりなかったりするが、
        /// それは選択可能かどうかを決めるものではない。
        /// Consumerがなくても選択可能、ただ入力消費はしないだけ。
        /// 
        /// ## nullの場合
        /// 
        /// モーダル配下の最初のナビゲーション選択可能なUIElementが選択される。
        /// </summary>
        [Tooltip("このモーダル内でのデフォルト選択対象UIElement")]
        public IScopeNode? DefaultSelectedElement;

        /// <summary>
        /// 背景（モーダル外）をクリックしたときにモーダルを閉じるか。
        /// 
        /// ## 用途
        /// 
        /// Popup系UIで使用される。
        /// メニューの外側をクリックしたときにメニューを閉じる動作。
        /// </summary>
        [Tooltip("背景クリックでモーダルを閉じるか")]
        public bool CloseOnBackgroundClick;

        /// <summary>
        /// キャンセルボタン（Bボタン、Escキー等）でモーダルを閉じるか。
        /// </summary>
        [Tooltip("キャンセルボタンでモーダルを閉じるか")]
        public bool CloseOnCancel;

        /// <summary>
        /// デフォルト設定を返す。
        /// </summary>
        public static ModalOptions Default => new()
        {
            AutoFallbackOnPop = true,
            CloseOnBackgroundClick = false,
            CloseOnCancel = true,
            DefaultSelectedElement = null
        };
    }

    // ================================================================
    // UIModalStackChangeType: Modal Stack変更種別
    // ================================================================

    /// <summary>
    /// Modal Stack変更の種類。
    /// </summary>
    public enum UIModalStackChangeType
    {
        /// <summary>通常の変更</summary>
        Normal,

        /// <summary>即時反映を意図した変更</summary>
        Immediate,

        /// <summary>一時的な変更</summary>
        Temporary,
    }

    public enum ModalStackChangeKind
    {
        RootSwap,
        DescendantPush,
        DescendantPop,
    }

    // ================================================================
    // UIModalStackPolicy: Stackの共存/上書きポリシー
    // ================================================================

    public enum UIModalStackPolicy
    {
        Coexist,
        Override,
    }

    // ================================================================
    // UIModalStackConfig: Stack設定
    // ================================================================

    public readonly struct UIModalStackConfig
    {
        public string Key { get; }
        public int Priority { get; }
        public UIModalStackPolicy Policy { get; }

        public UIModalStackConfig(string key, int priority, UIModalStackPolicy policy)
        {
            Key = key ?? "";
            Priority = priority;
            Policy = policy;
        }
    }

    public readonly struct UIModalActiveRoot
    {
        public string StackKey { get; }
        public IUIModalRoot Root { get; }
        public int Priority { get; }
        public UIModalStackPolicy Policy { get; }

        public UIModalActiveRoot(string stackKey, IUIModalRoot root, int priority, UIModalStackPolicy policy)
        {
            StackKey = stackKey ?? "";
            Root = root;
            Priority = priority;
            Policy = policy;
        }
    }

    /// <summary>
    /// Modal Stack変更イベントのコンテキスト。
    /// </summary>
    public readonly struct UIModalStackChangeContext
    {
        /// <summary>変更対象のStackKey</summary>
        public string StackKey { get; }

        /// <summary>変更前の入力ルート</summary>
        public IUIModalRoot? PreviousRoot { get; }

        /// <summary>変更後の入力ルート</summary>
        public IUIModalRoot? CurrentRoot { get; }

        /// <summary>変更の種類</summary>
        public UIModalStackChangeType ChangeType { get; }

        public UIModalStackChangeContext(
            string stackKey,
            IUIModalRoot? previousRoot,
            IUIModalRoot? currentRoot,
            UIModalStackChangeType changeType)
        {
            StackKey = stackKey ?? "";
            PreviousRoot = previousRoot;
            CurrentRoot = currentRoot;
            ChangeType = changeType;
        }
    }

    public enum ActiveRootsChangeKind
    {
        StackChanged,
        PriorityOverrideChanged,
        DefaultRootChanged,
        ConfigChanged,
    }

    public readonly struct UIModalStackRootsChangeContext
    {
        public IReadOnlyList<UIModalActiveRoot> PreviousRoots { get; }
        public IReadOnlyList<UIModalActiveRoot> CurrentRoots { get; }
        public string CauseStackKey { get; }
        public UIModalStackChangeType ChangeType { get; }
        public ActiveRootsChangeKind ChangeKind { get; }
        public ModalStackChangeKind? StackChangeKind { get; }

        public UIModalStackRootsChangeContext(
            IReadOnlyList<UIModalActiveRoot> previousRoots,
            IReadOnlyList<UIModalActiveRoot> currentRoots,
            string causeStackKey,
            UIModalStackChangeType changeType,
            ActiveRootsChangeKind changeKind,
            ModalStackChangeKind? stackChangeKind)
        {
            PreviousRoots = previousRoots;
            CurrentRoots = currentRoots;
            CauseStackKey = causeStackKey ?? "";
            ChangeType = changeType;
            ChangeKind = changeKind;
            StackChangeKind = stackChangeKind;
        }
    }

    // ================================================================
    // IUIModalStackService: Modal Stackサービスの公開API
    // ================================================================

    /// <summary>
    /// Modal Stackサービスの公開インターフェース。
    /// 
    /// ## Modal Stackとは
    /// 
    /// Modal Stackは、UIのヒエラルキーにおける「最低ライン」を管理するシステム。
    /// これより上の階層（親方向）への選択移動は禁止される。
    /// 
    /// ## 主な役割
    /// 
    /// 1. **選択範囲の制限（クランプ）**:
    ///    Modal Stack最上位のUIElement配下のみが選択可能となる。
    ///    これにより「Popup外へSelectが行って戻れない」問題を構造的に防ぐ。
    /// 
    /// 2. **ナビゲーション範囲の制限**:
    ///    方向キーによるナビゲーションも、CurrentInputRoot配下のみを対象とする。
    /// 
    /// 3. **ヒットテスト範囲の制限**:
    ///    マウスポインターによるSelect判定も、CurrentInputRoot配下のみを対象とする。
    /// 
    /// ## 重要
    /// 
    /// UIの単位はUIElementLifetimeScope。
    /// IUIInputConsumerは入力消費機能であり、選択単位とは無関係。
    /// </summary>
    public interface IUIModalStackService
    {
        /// <summary>現在の有効ルート集合</summary>
        IReadOnlyList<UIModalActiveRoot> ActiveRoots { get; }
        /// <summary>
        /// 現在の入力対象ルート。
        /// 
        /// - Modal Stackが空でなければ、Peek()を返す（最上位のモーダル）
        /// - Modal Stackが空の場合、DefaultRootを返す
        /// 
        /// すべての選択/ナビゲーション/ヒットテストはこのルート配下に制限される。
        /// </summary>
        IUIModalRoot? CurrentInputRoot { get; }

        /// <summary>
        /// Modal Stackが空かどうか。
        /// 空の場合、DefaultRootが CurrentInputRoot になる。
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// 現在のModal Stackの深さ（積まれているモーダルの数）。
        /// </summary>
        int Depth { get; }

        /// <summary>
        /// モーダルをStackにPushする。
        /// 
        /// Push後:
        /// - 新しいモーダルがCurrentInputRootになる
        /// - 選択がモーダル配下にクランプされる
        /// - 前回の選択状態がオプションに応じて保存される
        /// </summary>
        /// <param name="root">Pushするモーダルルート</param>
        /// <param name="options">オプション設定</param>
        /// <param name="changeType">変更の種類</param>
        void PushModal(
            IUIModalRoot root,
            ModalOptions options = default,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal);

        void PushModal(
            string stackKey,
            IUIModalRoot root,
            ModalOptions options = default,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal);

        /// <summary>
        /// 指定したモーダルをStackからPopする（一致チェック付き）。
        /// 
        /// rootがStack最上位と一致しない場合、中間のモーダルも一緒にPopされる。
        /// これにより、不正な順序でのPopを防ぐ。
        /// </summary>
        /// <param name="root">Popするモーダルルート</param>
        /// <param name="changeType">変更の種類</param>
        /// <returns>成功した場合true</returns>
        bool PopModal(
            IUIModalRoot root,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal);

        bool PopModal(
            string stackKey,
            IUIModalRoot root,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal);

        /// <summary>
        /// Stack最上位のモーダルをPopする。
        /// </summary>
        /// <param name="changeType">変更の種類</param>
        /// <returns>PopされたモーダルRoot、またはnull（Stackが空の場合）</returns>
        IUIModalRoot? PopTop(UIModalStackChangeType changeType = UIModalStackChangeType.Normal);

        IUIModalRoot? PopTop(string stackKey, UIModalStackChangeType changeType = UIModalStackChangeType.Normal);

        /// <summary>
        /// Modal Stackをすべてクリアする。
        /// </summary>
        void ClearAll();

        /// <summary>
        /// デフォルトルートを設定する。
        /// 
        /// Modal Stackが空の場合、このルートがCurrentInputRootになる。
        /// 通常はUILifetimeScope初期化時に呼ばれる。
        /// </summary>
        /// <param name="root">デフォルトルート</param>
        void SetDefaultRoot(IUIModalRoot? root);

        void SetDefaultRoot(string stackKey, IUIModalRoot? root);

        void RegisterStack(string stackKey, int priority = 0, UIModalStackPolicy policy = UIModalStackPolicy.Coexist);

        bool TryGetStackConfig(string stackKey, out UIModalStackConfig config);

        /// <summary>
        /// Modal Stackが変更されたときに発火するイベント。
        /// Push/Pop/Clear/DefaultRoot変更時に通知される。
        /// </summary>
        event System.Action<UIModalStackChangeContext>? OnModalStackChanged;
        event System.Action<UIModalStackRootsChangeContext>? OnActiveRootsChanged;

        /// <summary>
        /// 指定したUIElementがCurrentInputRoot内にいるかどうかを判定する。
        /// 
        /// この判定はBaseLifetimeScopeのHierarchyに基づいて行われる。
        /// </summary>
        /// <param name="element">判定対象のUIElement</param>
        /// <returns>CurrentInputRoot内にいればtrue</returns>
        bool IsInCurrentInputRoot(IScopeNode? element);

        bool IsInAnyInputRoot(IScopeNode? element);

        /// <summary>
        /// 指定したUIElementから、CurrentInputRootまでのパスを取得する。
        /// デバッグやログ出力に使用。
        /// </summary>
        /// <param name="element">対象UIElement</param>
        /// <returns>パス文字列</returns>
        string GetPathToCurrentRoot(IScopeNode? element);
    }

    // ================================================================
    // UIModalStackService: メイン実装
    // ================================================================

    /// <summary>
    /// Modal Stackサービスの実装。
    /// 
    /// ## 設計方針
    /// 
    /// - 親子関係の判定はBaseLifetimeScopeのHierarchyを使用する
    /// - Transformの階層ではなく、DIコンテナの階層で管理する
    /// - これにより、WorldUI/ScreenUIどちらでも同じ判定ロジックが使える
    /// 
    /// ## UIの単位について
    /// 
    /// UIの単位はUIElementLifetimeScope。
    /// IUIInputConsumerは入力消費機能であり、選択単位とは別物。
    /// ConsumerがなくてもUIElementは選択可能（消費はしないだけ）。
    /// 
    /// ## 内部構造
    /// 
    /// - _stack: ModalEntryのリスト。最後の要素が最上位
    /// - _defaultRoot: Modal Stackが空の場合に使用されるルート
    /// - _selectionHistoryPerModal: モーダルごとの選択履歴
    /// </summary>
    public sealed class UIModalStackService : IUIModalStackService, IUIModalStackTelemetry
    {
        const string DefaultStackKey = "default";

        // ----------------------------------------------------------------
        // 内部構造体: Stackの各エントリを保持
        // ----------------------------------------------------------------

        /// <summary>
        /// Modal Stackの各エントリ。
        /// Modal Rootとオプション、Push時の状態を保持する。
        /// 
        /// ## 設計
        /// 
        /// 選択の単位はUIElementLifetimeScope。
        /// IUIInputConsumerは使用しない。
        /// </summary>
        struct ModalEntry
        {
            /// <summary>モーダルルート本体</summary>
            public IUIModalRoot Root;

            /// <summary>Push時のオプション</summary>
            public ModalOptions Options;

            /// <summary>Push前に選択されていたUIElement（AutoFallbackOnPop用）</summary>
            public IScopeNode? PreviousSelection;
        }

        sealed class StackState
        {
            public readonly List<ModalEntry> Entries = new();
            public IUIModalRoot? DefaultRoot;
            public UIModalStackConfig Config;
        }

        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        /// <summary>
        /// StackKeyごとのスタック状態。
        /// </summary>
        readonly Dictionary<string, StackState> _stacks = new();

        /// <summary>
        /// 現在の有効ルート集合。
        /// </summary>
        readonly List<UIModalActiveRoot> _activeRoots = new();

        IUIModalRoot? _primaryRoot;

        /// <summary>
        /// SelectionServiceへの参照（Push時の選択保存用）。
        /// nullの場合、選択の保存/復元は行われない。
        /// </summary>
        IUISelectionState? _selectionState;

        /// <summary>
        /// SelectionService（選択操作用）。
        /// </summary>
        IUISelectionNavigation? _selectionNavigation;

        /// <summary>
        /// モーダルごとの選択履歴。
        /// Key: IUIModalRoot（参照で識別）
        /// Value: 最後に選択されていたUIElementLifetimeScope
        /// 
        /// ## 更新タイミング
        /// 
        /// - 選択が変更されるたびに、CurrentInputRootをキーとして記録
        /// - Popされたモーダルの履歴は維持（再Pushで復元可能）
        /// </summary>
        readonly Dictionary<IUIModalRoot, IScopeNode> _selectionHistoryPerModal = new();

        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public IReadOnlyList<UIModalActiveRoot> ActiveRoots => _activeRoots;

        /// <inheritdoc/>
        public IUIModalRoot? CurrentInputRoot => _primaryRoot;

        /// <inheritdoc/>
        public bool IsEmpty => Depth == 0;

        /// <inheritdoc/>
        public int Depth
        {
            get
            {
                int total = 0;
                foreach (var kv in _stacks)
                    total += kv.Value.Entries.Count;
                return total;
            }
        }

        // Telemetry helpers
        string[] IUIModalStackTelemetry.GetStackModalIds()
        {
            var ids = new List<string>();
            foreach (var kv in _stacks)
            {
                var key = kv.Key;
                var entries = kv.Value.Entries;
                for (int i = 0; i < entries.Count; i++)
                    ids.Add($"{key}:{entries[i].Root.ModalId}");
            }
            var arr = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++) arr[i] = ids[i];
            return arr;
        }

        IReadOnlyList<(string ModalId, string SelectedName)> IUIModalStackTelemetry.GetSelectionHistorySnapshot()
        {
            var list = new List<(string, string)>();
            foreach (var kv in _selectionHistoryPerModal)
            {
                var modalId = kv.Key.ModalId;
                var selectedName = kv.Value?.Identity?.SelfTransform != null
                    ? kv.Value.Identity.SelfTransform.name
                    : "(none)";
                list.Add((modalId, selectedName));
            }
            return list;
        }

        /// <inheritdoc/>
        public event System.Action<UIModalStackChangeContext>? OnModalStackChanged;
        public event System.Action<UIModalStackRootsChangeContext>? OnActiveRootsChanged;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。
        /// SelectionServiceはオプションで、nullの場合は選択の保存/復元を行わない。
        /// </summary>
        public UIModalStackService()
        {
            EnsureStack(DefaultStackKey);
        }

        /// <summary>
        /// SelectionServiceを後から設定する。
        /// 循環依存を避けるために分離。
        /// 
        /// ## 履歴機能
        /// 
        /// IUISelectionStateを設定し、選択変更イベントを購読して履歴を更新する。
        /// </summary>
        public void SetSelectionService(IUISelectionState? state, IUISelectionNavigation? navigation = null)
        {
            // 以前のイベント購読を解除
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged -= HandleSelectionChanged;
            }

            _selectionState = state;
            _selectionNavigation = navigation;

            // 新しいイベント購読を開始
            if (_selectionState != null)
            {
                _selectionState.OnSelectionChanged += HandleSelectionChanged;
            }
        }

        /// <summary>
        /// 選択変更時のハンドラ。
        /// モーダルごとの選択履歴を更新する。
        /// </summary>
        void HandleSelectionChanged(IScopeNode? newSelection)
        {
            if (newSelection == null) return;

            if (TryFindContainingActiveRoot(newSelection, out var root) && root != null)
            {
                _selectionHistoryPerModal[root] = newSelection;
            }
        }

        /// <summary>
        /// 指定したモーダルの選択履歴を取得する。
        /// </summary>
        /// <param name="modal">モーダルルート</param>
        /// <returns>履歴があれば最後に選択されていたUIElement、なければnull</returns>
        public IScopeNode? GetSelectionHistory(IUIModalRoot modal)
        {
            if (modal == null) return null;
            return _selectionHistoryPerModal.TryGetValue(modal, out var history) ? history : null;
        }

        /// <summary>
        /// 指定したモーダルの選択履歴をクリアする。
        /// </summary>
        public void ClearSelectionHistory(IUIModalRoot modal)
        {
            if (modal != null)
            {
                _selectionHistoryPerModal.Remove(modal);
            }
        }

        // ----------------------------------------------------------------
        // 公開メソッド
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public void SetDefaultRoot(IUIModalRoot? root)
        {
            SetDefaultRoot(DefaultStackKey, root);
        }

        public void SetDefaultRoot(string stackKey, IUIModalRoot? root)
        {
            var state = EnsureStack(stackKey);
            var changed = !ReferenceEquals(state.DefaultRoot, root);
            state.DefaultRoot = root;
            if (changed)
                UpdateActiveRoots(ActiveRootsChangeKind.DefaultRootChanged, stackKey, UIModalStackChangeType.Normal, null);
        }

        public void RegisterStack(string stackKey, int priority = 0, UIModalStackPolicy policy = UIModalStackPolicy.Coexist)
        {
            var state = EnsureStack(stackKey);
            var newConfig = new UIModalStackConfig(stackKey, priority, policy);
            var changed = state.Config.Priority != newConfig.Priority || state.Config.Policy != newConfig.Policy;
            state.Config = newConfig;
            if (changed)
                UpdateActiveRoots(ActiveRootsChangeKind.ConfigChanged, stackKey, UIModalStackChangeType.Normal, null);
        }

        public bool TryGetStackConfig(string stackKey, out UIModalStackConfig config)
        {
            if (string.IsNullOrEmpty(stackKey))
                stackKey = DefaultStackKey;

            if (_stacks.TryGetValue(stackKey, out var state))
            {
                config = state.Config;
                return true;
            }

            config = default;
            return false;
        }

        /// <inheritdoc/>
        public void PushModal(
            IUIModalRoot root,
            ModalOptions options = default,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            PushModal(DefaultStackKey, root, options, changeType);
        }

        public void PushModal(
            string stackKey,
            IUIModalRoot root,
            ModalOptions options = default,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            if (root == null)
            {
                Debug.LogWarning("[UIModalStackService] PushModal: root is null, ignored.");
                return;
            }

            var state = EnsureStack(stackKey);
            var previousRoot = GetStackCurrentRoot(state);

            // 既にStackに含まれている場合は警告して無視
            for (int i = 0; i < state.Entries.Count; i++)
            {
                if (ReferenceEquals(state.Entries[i].Root, root))
                {
                    return;
                }
            }

            // 現在の選択を保存（AutoFallbackOnPop用）
            IScopeNode? previousSelection = null;
            if (options.AutoFallbackOnPop && _selectionState != null)
            {
                previousSelection = _selectionState.CurrentElement;
            }

            var entry = new ModalEntry
            {
                Root = root,
                Options = options,
                PreviousSelection = previousSelection
            };

            state.Entries.Add(entry);

            NotifyModalStackChanged(stackKey, previousRoot, GetStackCurrentRoot(state), changeType);
            UpdateActiveRoots(ActiveRootsChangeKind.StackChanged, stackKey, changeType, EvaluateChangeKind(previousRoot, GetStackCurrentRoot(state)));

            if (_selectionNavigation != null && options.DefaultSelectedElement != null)
            {
                _selectionNavigation.Select(options.DefaultSelectedElement);
            }
        }

        /// <inheritdoc/>
        public bool PopModal(
            IUIModalRoot root,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            return PopModal(DefaultStackKey, root, changeType);
        }

        public bool PopModal(
            string stackKey,
            IUIModalRoot root,
            UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            if (root == null)
            {
                Debug.LogWarning("[UIModalStackService] PopModal: root is null, ignored.");
                return false;
            }

            if (string.IsNullOrEmpty(stackKey))
                stackKey = DefaultStackKey;

            if (!_stacks.TryGetValue(stackKey, out var state))
                return false;

            var previousRoot = GetStackCurrentRoot(state);

            // Stackを後ろから検索
            int foundIndex = -1;
            for (int i = state.Entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(state.Entries[i].Root, root))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex < 0)
            {
                Debug.LogWarning($"[UIModalStackService] PopModal: '{root.ModalId}' not found in stack.");
                return false;
            }

            ModalEntry? lastPopped = null;
            for (int i = state.Entries.Count - 1; i >= foundIndex; i--)
            {
                lastPopped = state.Entries[i];
                state.Entries.RemoveAt(i);
            }

            NotifyModalStackChanged(stackKey, previousRoot, GetStackCurrentRoot(state), changeType);
            UpdateActiveRoots(ActiveRootsChangeKind.StackChanged, stackKey, changeType, EvaluateChangeKind(previousRoot, GetStackCurrentRoot(state)));

            if (lastPopped.HasValue && lastPopped.Value.Options.AutoFallbackOnPop)
            {
                TryRestoreSelection(lastPopped.Value.PreviousSelection);
            }

            return true;
        }

        /// <inheritdoc/>
        public IUIModalRoot? PopTop(UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            return PopTop(DefaultStackKey, changeType);
        }

        public IUIModalRoot? PopTop(string stackKey, UIModalStackChangeType changeType = UIModalStackChangeType.Normal)
        {
            if (string.IsNullOrEmpty(stackKey))
                stackKey = DefaultStackKey;

            if (!_stacks.TryGetValue(stackKey, out var state))
                return null;

            if (state.Entries.Count == 0)
                return null;

            var previousRoot = GetStackCurrentRoot(state);
            var entry = state.Entries[^1];
            var root = entry.Root;
            state.Entries.RemoveAt(state.Entries.Count - 1);

            Debug.Log($"[UIModalStackService] PopTop: '{root.ModalId}' popped. Depth={Depth}");

            NotifyModalStackChanged(stackKey, previousRoot, GetStackCurrentRoot(state), changeType);
            UpdateActiveRoots(ActiveRootsChangeKind.StackChanged, stackKey, changeType, EvaluateChangeKind(previousRoot, GetStackCurrentRoot(state)));

            if (entry.Options.AutoFallbackOnPop)
            {
                TryRestoreSelection(entry.PreviousSelection);
            }

            return root;
        }

        /// <summary>
        /// 選択をフォールバック復元する。
        /// 
        /// ## フォールバック優先順位
        /// 
        /// 1. 現在のモーダルの選択履歴（_selectionHistoryPerModal）
        /// 2. Push前の選択（previousSelection）
        /// 
        /// ## 処理フロー
        /// 
        /// 1. 履歴があり、有効ならそれを選択
        /// 2. なければpreviousSelectionを選択
        /// </summary>
        void TryRestoreSelection(IScopeNode? previousSelection)
        {
            if (_selectionNavigation == null) return;

            var currentRoot = CurrentInputRoot;

            // 1. 履歴からの復元を試みる
            if (currentRoot != null && _selectionHistoryPerModal.TryGetValue(currentRoot, out var historyElement))
            {
                // 履歴が有効か確認（まだ存在し、CurrentInputRoot内にいる）
                if (historyElement != null && IsInAnyInputRoot(historyElement))
                {
                    _selectionNavigation.Select(historyElement);
                    Debug.Log($"[UIModalStackService] Restored selection from history: {historyElement.Identity?.SelfTransform?.name ?? "(unknown)"}");
                    return;
                }
            }

            // 2. Push前の選択に戻す
            if (previousSelection != null && IsInAnyInputRoot(previousSelection))
            {
                _selectionNavigation.Select(previousSelection);
                Debug.Log($"[UIModalStackService] Restored selection from previous: {previousSelection.Identity?.SelfTransform?.name ?? "(unknown)"}");
            }
        }

        /// <inheritdoc/>
        public void ClearAll()
        {
            if (Depth == 0)
                return;

            //Debug.Log($"[UIModalStackService] ClearAll: Clearing {Depth} modals.");
            var changeType = UIModalStackChangeType.Normal;
            var snapshots = new List<(string Key, IUIModalRoot? PreviousRoot)>();
            foreach (var kv in _stacks)
            {
                var state = kv.Value;
                if (state.Entries.Count == 0)
                    continue;
                snapshots.Add((kv.Key, GetStackCurrentRoot(state)));
                state.Entries.Clear();
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                var snap = snapshots[i];
                if (_stacks.TryGetValue(snap.Key, out var state))
                    NotifyModalStackChanged(snap.Key, snap.PreviousRoot, GetStackCurrentRoot(state), changeType);
            }

            UpdateActiveRoots(ActiveRootsChangeKind.StackChanged, DefaultStackKey, changeType, null);
        }

        /// <inheritdoc/>
        public bool IsInCurrentInputRoot(IScopeNode? element)
        {
            return IsInAnyInputRoot(element);
        }

        public bool IsInAnyInputRoot(IScopeNode? element)
        {
            if (element == null)
                return false;

            if (_activeRoots.Count == 0)
                return true;

            for (int i = 0; i < _activeRoots.Count; i++)
            {
                var root = _activeRoots[i].Root;
                if (root != null && root.IsDescendant(element))
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public string GetPathToCurrentRoot(IScopeNode? element)
        {
            // デバッグ用のパス文字列を生成
            var root = CurrentInputRoot;
            if (root == null)
            {
                return "(no root)";
            }

            if (element == null)
            {
                return $"(null) -> {root.ModalId}";
            }

            // 簡易的な実装（詳細は後で拡張）
            return $"{element.Identity?.SelfTransform?.name ?? "(unknown)"} -> {root.ModalId}";
        }

        void NotifyModalStackChanged(string stackKey, IUIModalRoot? previousRoot, IUIModalRoot? currentRoot, UIModalStackChangeType changeType)
        {
            var context = new UIModalStackChangeContext(stackKey, previousRoot, currentRoot, changeType);
            OnModalStackChanged?.Invoke(context);
        }

        StackState EnsureStack(string stackKey)
        {
            if (string.IsNullOrEmpty(stackKey))
                stackKey = DefaultStackKey;

            if (_stacks.TryGetValue(stackKey, out var state))
                return state;

            state = new StackState
            {
                DefaultRoot = null,
                Config = new UIModalStackConfig(stackKey, 0, UIModalStackPolicy.Coexist)
            };
            _stacks[stackKey] = state;
            return state;
        }

        static IUIModalRoot? GetStackCurrentRoot(StackState state)
        {
            if (state.Entries.Count > 0)
                return state.Entries[^1].Root;
            return state.DefaultRoot;
        }

        bool TryFindContainingActiveRoot(IScopeNode element, out IUIModalRoot? root)
        {
            root = null;
            if (element == null)
                return false;

            for (int i = 0; i < _activeRoots.Count; i++)
            {
                var r = _activeRoots[i].Root;
                if (r != null && r.IsDescendant(element))
                {
                    root = r;
                    return true;
                }
            }

            return false;
        }

        void UpdateActiveRoots(
            ActiveRootsChangeKind changeKind,
            string causeStackKey,
            UIModalStackChangeType changeType,
            ModalStackChangeKind? stackChangeKind)
        {
            var previous = new List<UIModalActiveRoot>(_activeRoots);
            BuildActiveRoots(_activeRoots, out _primaryRoot);

            if (!AreActiveRootsEqual(previous, _activeRoots))
            {
                var context = new UIModalStackRootsChangeContext(previous, _activeRoots, causeStackKey, changeType, changeKind, stackChangeKind);
                OnActiveRootsChanged?.Invoke(context);
            }
        }

        void BuildActiveRoots(List<UIModalActiveRoot> results, out IUIModalRoot? primaryRoot)
        {
            results.Clear();
            primaryRoot = null;

            int overrideMaxPriority = int.MinValue;
            foreach (var kv in _stacks)
            {
                var state = kv.Value;
                var root = GetStackCurrentRoot(state);
                if (root == null)
                    continue;

                if (state.Config.Policy == UIModalStackPolicy.Override)
                    overrideMaxPriority = Math.Max(overrideMaxPriority, state.Config.Priority);
            }

            bool hasOverride = overrideMaxPriority != int.MinValue;

            foreach (var kv in _stacks)
            {
                var state = kv.Value;
                var root = GetStackCurrentRoot(state);
                if (root == null)
                    continue;

                if (hasOverride)
                {
                    if (state.Config.Policy != UIModalStackPolicy.Override)
                        continue;
                    if (state.Config.Priority != overrideMaxPriority)
                        continue;
                }

                results.Add(new UIModalActiveRoot(state.Config.Key, root, state.Config.Priority, state.Config.Policy));
            }

            results.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            if (results.Count > 0)
                primaryRoot = results[0].Root;
        }

        static bool AreActiveRootsEqual(List<UIModalActiveRoot> a, List<UIModalActiveRoot> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!ReferenceEquals(a[i].Root, b[i].Root))
                    return false;
                if (!string.Equals(a[i].StackKey, b[i].StackKey, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        static ModalStackChangeKind EvaluateChangeKind(IUIModalRoot? previousRoot, IUIModalRoot? currentRoot)
        {
            if (previousRoot == null || currentRoot == null)
                return ModalStackChangeKind.RootSwap;

            if (previousRoot.IsDescendant(currentRoot.OwnerScope))
                return ModalStackChangeKind.DescendantPush;

            if (currentRoot.IsDescendant(previousRoot.OwnerScope))
                return ModalStackChangeKind.DescendantPop;

            return ModalStackChangeKind.RootSwap;
        }
    }
}
