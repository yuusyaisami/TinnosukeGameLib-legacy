#nullable enable
using UnityEngine;
using Game.Input;
using VContainer.Unity;
using System;

namespace Game.UI
{
    // ================================================================
    // UIInputService.cs - UI層の入力処理サービス
    // ================================================================
    //
    // ## 概要
    //
    // UIInputServiceは、低レベルのInputRouter/InputFrameからUI専用の
    // 入力イベント（UIInputEvent）への変換と配信を担当する。
    //
    // ## アーキテクチャ上の位置づけ
    //
    // ```
    // InputSystem → InputRouter → [UIInputService] → UINavigationService
    //                                   ↓
    //                            UIInputEvent生成
    // ```
    //
    // ## 主な責務
    //
    // 1. **InputFrameの購読**:
    //    UIInputConsumerBridgeを介してInputRouterに登録し、
    //    毎フレームの入力データを受け取る
    //
    // 2. **入力イベントの変換**:
    //    InputFrame（低レベルデータ）→ UIInputEvent（UI層の意味的イベント）
    //
    // 3. **入力モード管理**:
    //    マウス/キーボード/ゲームパッドの入力モードを追跡し、
    //    モード変更時にイベントを発火
    //
    // 4. **NavigationServiceへの配信**:
    //    変換されたUIInputEventをNavigationServiceへ流す
    //
    // ## 入力モード
    //
    // - Pointer: マウス操作モード（ホバー/クリック中心）
    // - Keyboard: キーボードモード（ナビゲーション中心）
    // - Gamepad: ゲームパッドモード（ナビゲーション中心）
    //
    // ## UIInputEvent
    //
    // 以下の種類のイベントに変換される:
    // - PointerMove: ポインター移動
    // - Navigate: 方向入力（十字キー/スティック）
    // - Submit/Cancel: 決定/キャンセルボタン
    // - Scroll: スクロール入力
    //
    // ================================================================

    // ================================================================
    // UIInputEventType: UI入力イベントの種類
    // ================================================================

    /// <summary>
    /// UI入力イベントの種類。
    /// 
    /// ## カテゴリ
    /// 
    /// ### ポインター系
    /// - PointerMove: マウス移動
    /// - PointerEnter/Exit: ホバー開始/終了（将来実装）
    /// 
    /// ### ボタン系
    /// - Submit: 決定ボタン（Down/Held/Up）
    /// - Cancel: キャンセルボタン（Down/Held/Up）
    /// 
    /// ### ナビゲーション
    /// - Navigate: 方向入力（ベクトル）
    /// - Scroll: スクロール入力
    /// 
    /// ### 将来拡張
    /// - Drag系: ドラッグ操作
    /// - LongPress: 長押し
    /// </summary>
    public enum UIInputEventType
    {
        None,

        // ポインター系
        PointerMove,
        PointerEnter,
        PointerExit,

        // ボタン系（各ボタンのDown/Held/Upを区別）
        SubmitDown,
        SubmitHeld,
        SubmitUp,
        CancelDown,
        CancelHeld,
        CancelUp,

        // Gameplay Buttons (UI で消費可能なもの)
        AttackDown,
        AttackHeld,
        AttackUp,
        InteractDown,
        InteractHeld,
        InteractUp,
        PauseDown,
        PauseHeld,
        PauseUp,

        // Retry (GameUI Retry action)
        RetryDown,
        RetryHeld,
        RetryUp,

        // スクロール
        Scroll,

        // ナビゲーション（方向キー）
        Navigate,

        // 将来の拡張用
        DragStart,
        DragMove,
        DragEnd,
        DragCancel,
        LongPressStart,
        LongPressEnd,
    }

    // ================================================================
    // UIInputEvent: UI層の入力イベント構造体
    // ================================================================

    /// <summary>
    /// UI層の入力イベント構造体。
    /// 
    /// ## 設計意図
    /// 
    /// InputFrameから変換された、UI層で使用する意味的なイベント。
    /// 不変（readonly struct）で軽量なデータ構造。
    /// 
    /// ## フィールド
    /// 
    /// - Type: イベントの種類
    /// - PointerPosition: スクリーン座標でのポインター位置
    /// - Direction: 方向入力（Navigate/Scroll用）
    /// - DeltaTime: フレーム経過時間
    /// 
    /// ## ファクトリメソッド
    /// 
    /// コンストラクタの代わりにファクトリメソッドを使用することで、
    /// 各イベントタイプの生成を明確にする。
    /// </summary>
    public readonly struct UIInputEvent
    {
        // ----------------------------------------------------------------
        // プロパティ
        // ----------------------------------------------------------------

        /// <summary>イベントの種類</summary>
        public UIInputEventType Type { get; }

        /// <summary>スクリーン座標でのポインター位置</summary>
        public Vector2 PointerPosition { get; }

        /// <summary>方向入力ベクトル（Navigate/Scroll用）</summary>
        public Vector2 Direction { get; }

        /// <summary>フレーム経過時間</summary>
        public float DeltaTime { get; }

        /// <summary>現在の入力コントロールスキーム（ControlScheme）</summary>
        public ControlScheme Scheme { get; }

        /// <summary>現在の入力使用モード（Pointer / Keyboard / Gamepad）</summary>
        public InputUsageMode UsageMode { get; }

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        public UIInputEvent(
            UIInputEventType type,
            Vector2 pointerPosition = default,
            Vector2 direction = default,
            float deltaTime = 0f,
            ControlScheme scheme = ControlScheme.Unknown,
            InputUsageMode usageMode = InputUsageMode.Unknown)
        {
            Type = type;
            PointerPosition = pointerPosition;
            Direction = direction;
            DeltaTime = deltaTime;
            Scheme = scheme;
            UsageMode = usageMode;
        }

        // ----------------------------------------------------------------
        // ファクトリメソッド
        // ----------------------------------------------------------------

        /// <summary>ポインター移動イベントを生成</summary>
        public static UIInputEvent PointerMove(Vector2 position, float dt, ControlScheme scheme = ControlScheme.Unknown, InputUsageMode usageMode = InputUsageMode.Unknown)
            => new(UIInputEventType.PointerMove, position, default, dt, scheme, usageMode);

        /// <summary>Submit系イベントを生成（Down/Held/Up）</summary>
        public static UIInputEvent Submit(UIInputEventType phase, Vector2 position, float dt, ControlScheme scheme = ControlScheme.Unknown, InputUsageMode usageMode = InputUsageMode.Unknown)
            => new(phase, position, default, dt, scheme, usageMode);

        /// <summary>Cancel系イベントを生成（Down/Held/Up）</summary>
        public static UIInputEvent Cancel(UIInputEventType phase, Vector2 position, float dt, ControlScheme scheme = ControlScheme.Unknown, InputUsageMode usageMode = InputUsageMode.Unknown)
            => new(phase, position, default, dt, scheme, usageMode);

        /// <summary>ナビゲーション（方向入力）イベントを生成</summary>
        public static UIInputEvent NavigateEvent(Vector2 direction, float dt, ControlScheme scheme = ControlScheme.Unknown, InputUsageMode usageMode = InputUsageMode.Unknown)
            => new(UIInputEventType.Navigate, default, direction, dt, scheme, usageMode);

        /// <summary>スクロールイベントを生成</summary>
        public static UIInputEvent ScrollEvent(Vector2 scroll, Vector2 position, float dt, ControlScheme scheme = ControlScheme.Unknown, InputUsageMode usageMode = InputUsageMode.Unknown)
            => new(UIInputEventType.Scroll, position, scroll, dt, scheme, usageMode);
    }

    // ================================================================
    // IUIInputConsumer: UIElement側が実装する入力消費インターフェース
    // ================================================================

    /// <summary>
    /// UIElement側が実装する入力消費インターフェース。
    /// 
    /// ## 役割
    /// 
    /// UIElement内のコンポーネントが入力を消費するために実装する。
    /// NavigationServiceがUIInputEventを現在の選択に対して流す際に使用。
    /// 
    /// ## InputRouter側との違い
    /// 
    /// - IInputConsumer（InputRouter側）: 低レベルのInputFrameを処理
    /// - IUIInputConsumer（UI側）: UIInputEventを処理
    /// 
    /// 両者は完全に分離されており、UI層は抽象化された入力のみを扱う。
    /// 
    /// ## 優先度
    /// 
    /// 同一UIElement内に複数のIUIInputConsumerがある場合、
    /// Priorityが大きい順に呼び出される。
    /// </summary>
    public interface IUIInputConsumer
    {
        /// <summary>
        /// UIElement内での入力優先度。
        /// 大きいほど先に呼ばれる。
        /// 
        /// ## 例
        /// - 100: ダイアログの閉じるボタン
        /// - 50: リストアイテム
        /// - 0: デフォルト
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 入力イベントを消費する。
        /// 
        /// ## 戻り値
        /// - true: 入力を消費した。後続のConsumerは呼ばれない。
        /// - false: 入力を消費しなかった。次のConsumerに渡される。
        /// </summary>
        bool Consume(in UIInputEvent e);
    }

    // ================================================================
    // IUIInputService: UI入力サービスの公開インターフェース
    // ================================================================

    /// <summary>
    /// UI入力サービスの公開インターフェース。
    /// 
    /// ## 役割
    /// 
    /// 外部から現在の入力モードを確認するためのインターフェース。
    /// 実際の入力処理はNavigationService経由で行われる。
    /// 
    /// ## 使用例
    /// 
    /// ```csharp
    /// // 入力モードに応じたUI表示切り替え
    /// if (_inputService.IsPointerModeActive)
    /// {
    ///     ShowMouseCursor();
    /// }
    /// else if (_inputService.IsNavigationModeActive)
    /// {
    ///     ShowSelectionHighlight();
    /// }
    /// ```
    /// </summary>
    public interface IUIInputService
    {
        /// <summary>
        /// 現在の入力モード（Pointer/Keyboard/Gamepad）。
        /// ControlSchemeServiceから取得。
        /// </summary>
        InputUsageMode CurrentUsageMode { get; }

        /// <summary>
        /// ポインター操作が有効かどうか。
        /// マウスモードの場合true。
        /// </summary>
        bool IsPointerModeActive { get; }

        /// <summary>
        /// ナビゲーション操作が有効かどうか。
        /// キーボード/ゲームパッドモードの場合true。
        /// </summary>
        bool IsNavigationModeActive { get; }

        /// <summary>
        /// 入力モードが変更されたときに発火するイベント。
        /// UIの表示モード切り替えなどに使用。
        /// </summary>
        event Action<InputUsageMode>? OnUsageModeChanged;
    }

    // ================================================================
    // UIInputConsumerBridge: InputRouter ↔ UI入力層のブリッジ
    // ================================================================

    /// <summary>
    /// InputRouter側のIInputConsumerを実装し、UI入力層へ橋渡しするブリッジ。
    /// 
    /// ## 設計意図
    /// 
    /// InputRouterのIInputConsumer（低レベル）とUI層（高レベル）を
    /// 接続するためのアダプター。
    /// 
    /// ## 処理フロー
    /// 
    /// 1. InputRouterがUpdateInputを呼び出す
    /// 2. UIInputService.ProcessInputFrameを呼び出す
    /// 3. InputFrame → UIInputEvent変換が行われる
    /// 
    /// ## 優先度
    /// 
    /// InputConsumerPriority.UIで登録され、
    /// ゲームプレイ入力より高い優先度で処理される。
    /// </summary>
    internal sealed class UIInputConsumerBridge : IInputConsumer
    {
        readonly UIInputService _service;
        readonly InputConsumerPriority _priority;

        /// <summary>InputRouter内での優先度</summary>
        public InputConsumerPriority Priority => _priority;

        public UIInputConsumerBridge(
            UIInputService service,
            InputConsumerPriority priority = InputConsumerPriority.UI)
        {
            _service = service;
            _priority = priority;
        }

        /// <summary>
        /// InputRouterから呼ばれる入力処理。
        /// UIInputServiceへ処理を委譲する。
        /// </summary>
        public void UpdateInput(ref InputFrame frame)
        {
            _service.ProcessInputFrame(ref frame);
        }
    }

    // ================================================================
    // UIInputService: メイン実装クラス
    // ================================================================

    /// <summary>
    /// UI入力サービスのメイン実装。
    /// 
    /// ## 依存関係
    /// 
    /// - IControlSchemeService: 入力モード（マウス/キーボード/パッド）の取得
    /// - IPointerService: ポインター位置の取得（将来的なヒットテスト用）
    /// - IInputRouter: InputConsumerとしての登録先
    /// - IUINavigationService: UIInputEventの配信先
    /// 
    /// ## ライフサイクル
    /// 
    /// 1. Start: InputRouterへブリッジを登録
    /// 2. 毎フレーム: ProcessInputFrameでイベント変換・配信
    /// 3. Dispose: ブリッジの登録解除
    /// 
    /// ## 入力処理の流れ
    /// 
    /// ```
    /// ProcessInputFrame
    ///   ├─ ProcessPointerInput (ポインターモード時)
    ///   ├─ ProcessNavigationInput (ナビゲーションモード時)
    ///   ├─ ProcessButtonInput (Submit/Cancel)
    ///   └─ ProcessScrollInput (スクロール)
    /// ```
    /// </summary>
    public sealed class UIInputService : IUIInputService, IStartable, IDisposable
    {
        // ----------------------------------------------------------------
        // 依存サービス
        // ----------------------------------------------------------------

        /// <summary>入力スキーム（モード）管理サービス</summary>
        readonly IControlSchemeService _controlSchemeService;

        /// <summary>ポインター位置サービス（将来的なヒットテスト用）</summary>
        readonly IPointerService _pointerService;

        /// <summary>入力ルーター（InputConsumer登録先）</summary>
        readonly IInputRouter _inputRouter;

        /// <summary>ナビゲーションサービス（UIInputEvent配信先）</summary>
        readonly IUINavigationService _navigationService;

        /// <summary>ModalStackサービス（ActiveRoots変更検知用）</summary>
        readonly IUIModalStackService? _modalStackService;

        /// <summary>UIInputオプション</summary>
        readonly UIInputOptions _options;

        // ----------------------------------------------------------------
        // 内部状態
        // ----------------------------------------------------------------

        /// <summary>InputRouterに登録するブリッジ</summary>
        UIInputConsumerBridge? _bridge;

        /// <summary>前回の入力モード（変更検出用）</summary>
        InputUsageMode _lastUsageMode;

        /// <summary>前フレームのポインタ位置（マウス移動検出用）</summary>
        Vector2 _lastPointerPosition;

        /// <summary>前フレームのポインタ位置が有効か</summary>
        bool _hasLastPointerPosition;

        /// <summary>ポインター移動イベントのサンプリング用経過時間</summary>
        float _pointerMoveSampleElapsed;

        /// <summary>この時刻までUI入力をブロックする（unscaledTime）</summary>
        float _blockInputUntilUnscaledTime;

        // ----------------------------------------------------------------
        // IUIInputService プロパティ
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        public InputUsageMode CurrentUsageMode => _controlSchemeService.CurrentUsageMode;

        /// <inheritdoc/>
        public bool IsPointerModeActive => CurrentUsageMode == InputUsageMode.Pointer;

        /// <inheritdoc/>
        public bool IsNavigationModeActive =>
            CurrentUsageMode == InputUsageMode.Keyboard ||
            CurrentUsageMode == InputUsageMode.Gamepad;

        /// <inheritdoc/>
        public event Action<InputUsageMode>? OnUsageModeChanged;

        // ----------------------------------------------------------------
        // コンストラクタ
        // ----------------------------------------------------------------

        /// <summary>
        /// コンストラクタ。VContainerからDI注入される。
        /// </summary>
        public UIInputService(
            IControlSchemeService controlSchemeService,
            IPointerService pointerService,
            IInputRouter inputRouter,
            IUINavigationService navigationService,
            UIInputOptions? options = null,
            IUIModalStackService? modalStackService = null)
        {
            _controlSchemeService = controlSchemeService;
            _pointerService = pointerService;
            _inputRouter = inputRouter;
            _navigationService = navigationService;
            _options = options ?? new UIInputOptions();
            _modalStackService = modalStackService;
        }

        // ----------------------------------------------------------------
        // ライフサイクル
        // ----------------------------------------------------------------

        /// <summary>
        /// 開始時処理。
        /// InputRouterへブリッジを登録し、入力モード変更を購読する。
        /// </summary>
        public void Start()
        {
            // InputRouterへブリッジを登録
            // これにより毎フレームUpdateInputが呼ばれるようになる
            _bridge = new UIInputConsumerBridge(this, InputConsumerPriority.UI);
            _inputRouter.RegisterConsumer(_bridge);

            // 入力モード変更を購読
            _controlSchemeService.OnUsageModeChanged += HandleUsageModeChanged;
            _lastUsageMode = _controlSchemeService.CurrentUsageMode;

            if (_modalStackService != null)
                _modalStackService.OnActiveRootsChanged += HandleModalActiveRootsChanged;
        }

        /// <summary>
        /// 破棄時処理。
        /// ブリッジの登録解除とイベント購読解除を行う。
        /// </summary>
        public void Dispose()
        {
            // ブリッジの登録解除
            if (_bridge != null)
            {
                _inputRouter.UnregisterConsumer(_bridge);
                _bridge = null;
            }

            // イベント購読解除
            _controlSchemeService.OnUsageModeChanged -= HandleUsageModeChanged;

            if (_modalStackService != null)
                _modalStackService.OnActiveRootsChanged -= HandleModalActiveRootsChanged;
        }

        // ----------------------------------------------------------------
        // イベントハンドラ
        // ----------------------------------------------------------------

        /// <summary>
        /// 入力モード変更時のハンドラ。
        /// モードが実際に変わった場合のみイベントを発火する。
        /// </summary>
        void HandleUsageModeChanged(InputUsageMode newMode)
        {
            if (_lastUsageMode != newMode)
            {
                _lastUsageMode = newMode;
                OnUsageModeChanged?.Invoke(newMode);
            }
        }

        void HandleModalActiveRootsChanged(UIModalStackRootsChangeContext context)
        {
            _ = context;
            if (!_options.BlockInputAfterModalActiveRootsChanged)
                return;

            var blockDuration = Mathf.Max(0f, _options.BlockDurationAfterModalActiveRootsChanged);
            if (blockDuration <= 0f)
                return;

            var nextUntil = Time.unscaledTime + blockDuration;
            if (nextUntil > _blockInputUntilUnscaledTime)
                _blockInputUntilUnscaledTime = nextUntil;
        }

        // ----------------------------------------------------------------
        // 入力処理（内部）
        // ----------------------------------------------------------------

        /// <summary>
        /// InputFrameを処理し、UIInputEventに変換してNavigationへ流す。
        /// UIInputConsumerBridgeから毎フレーム呼ばれる。
        /// 
        /// ## 処理順序
        /// 
        /// 1. ポインター入力（マウスモード時のみ）
        /// 2. ナビゲーション入力（キーボード/パッドモード時のみ）
        /// 3. ボタン入力（全モード共通）
        /// 4. スクロール入力（全モード共通）
        /// </summary>
        internal void ProcessInputFrame(ref InputFrame frame)
        {
            var dt = frame.DeltaTime;
            var pointerPos = frame.PointerScreen;
            if (_controlSchemeService.CurrentScheme == ControlScheme.Mouse)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null)
                    pointerPos = mouse.position.ReadValue();
            }

            if (IsInputBlocked())
            {
                ConsumeAllInput(ref frame);
                _lastPointerPosition = pointerPos;
                _hasLastPointerPosition = true;
                return;
            }

            // Note: pointer activity notification is handled by the low-level InputRouter; UI layer must not drive ControlScheme.

            // ポインターモードの判定は基本的にControlSchemeServiceに依るが、
            // キーボード運用時はマウス移動があればそのフレームのみPointerとして扱う。
            var moveThreshold = Mathf.Max(0f, _options.PointerMovePixelThreshold);
            var moveThresholdSqr = moveThreshold * moveThreshold;
            var pointerDelta = pointerPos - _lastPointerPosition;
            var mouseMovedEnough = !_hasLastPointerPosition || pointerDelta.sqrMagnitude >= moveThresholdSqr;
            var pointerModeThisFrame = _controlSchemeService.CurrentUsageMode == InputUsageMode.Pointer
                || (mouseMovedEnough && _controlSchemeService.CurrentUsageMode == InputUsageMode.Keyboard);

            _pointerMoveSampleElapsed += Mathf.Max(0f, dt);
            var sampleInterval = Mathf.Max(0f, _options.PointerMoveSampleInterval);
            var hasReachedPointerSampleInterval = sampleInterval <= 0f || _pointerMoveSampleElapsed >= sampleInterval;

            var pointerPressedThisFrame = frame.Click.Down || frame.PointerLeft.Down || frame.PointerRight.Down;
            var shouldForcePointerSync = _options.ForcePointerSyncOnPress && pointerPressedThisFrame;

            // Read current scheme/usage once per frame
            var currentScheme = _controlSchemeService.CurrentScheme;
            var currentUsage = _controlSchemeService.CurrentUsageMode;

            if (pointerModeThisFrame && (shouldForcePointerSync || (mouseMovedEnough && hasReachedPointerSampleInterval)))
            {
                ProcessPointerInput(ref frame, pointerPos, dt, currentScheme, InputUsageMode.Pointer);
                _pointerMoveSampleElapsed = 0f;
            }

            // ナビゲーションモードの場合（キーボード/ゲームパッド）
            if (IsNavigationModeActive)
            {
                ProcessNavigationInput(ref frame, pointerPos, dt, currentScheme, currentUsage);
            }

            // 共通のボタン入力処理（Submit/Cancel）
            ProcessButtonInput(ref frame, pointerPos, dt, currentScheme, currentUsage);

            // スクロール処理
            ProcessScrollInput(ref frame, pointerPos, dt, currentScheme, currentUsage);

            // 更新: 次フレーム比較用にポインタ位置を保存
            _lastPointerPosition = pointerPos;
            _hasLastPointerPosition = true;
        }

        bool IsInputBlocked()
        {
            if (!_options.BlockInputAfterModalActiveRootsChanged)
                return false;

            return Time.unscaledTime < _blockInputUntilUnscaledTime;
        }

        static void ConsumeAllInput(ref InputFrame frame)
        {
            frame.MoveConsumed = true;
            frame.NavigateConsumed = true;
            frame.ScrollConsumed = true;

            frame.Dodge.ForceConsume();
            frame.Slow.ForceConsume();
            frame.Attack.ForceConsume();
            frame.Interact.ForceConsume();
            frame.Pause.ForceConsume();

            frame.Submit.ForceConsume();
            frame.Cancel.ForceConsume();
            frame.Click.ForceConsume();
            frame.PointerLeft.ForceConsume();
            frame.PointerRight.ForceConsume();
            frame.Retry.ForceConsume();
        }

        /// <summary>
        /// ポインター（マウス）入力の処理。
        /// 
        /// ## 現状
        /// 
        /// ポインター移動イベントをNavigationへ通知するのみ。
        /// 
        /// ## TODO
        /// 
        /// ヒットテストシステムと連携し、ホバー対象の
        /// 自動Select判定を実装する必要がある。
        /// </summary>
        void ProcessPointerInput(ref InputFrame frame, Vector2 pointerPos, float dt, ControlScheme scheme, InputUsageMode usage)
        {
            // ポインター移動イベントをNavigationへ通知
            var moveEvent = UIInputEvent.PointerMove(pointerPos, dt, scheme, usage);
            _navigationService.ReceiveInputEvent(in moveEvent);
        }

        /// <summary>
        /// ナビゲーション（方向）入力の処理。
        /// 
        /// ## 処理内容
        /// 
        /// 方向入力（十字キー/スティック）がある場合、
        /// NavigateイベントをNavigationServiceへ送信する。
        /// 
        /// ## 消費
        /// 
        /// NavigationServiceがtrueを返した場合、
        /// frame.NavigateConsumedをtrueにして後続の処理をブロック。
        /// </summary>
        void ProcessNavigationInput(ref InputFrame frame, Vector2 pointerPos, float dt, ControlScheme scheme, InputUsageMode usage)
        {
            // 方向入力がある場合
            if (frame.Navigate != Vector2.zero && !frame.NavigateConsumed)
            {
                var navEvent = UIInputEvent.NavigateEvent(frame.Navigate, dt, scheme, usage);
                if (_navigationService.ReceiveInputEvent(in navEvent))
                {
                    frame.NavigateConsumed = true;
                }
            }
        }

        /// <summary>
        /// ボタン入力（Submit/Cancel）の処理。
        /// 
        /// ## 処理内容
        /// 
        /// Submit/Cancelそれぞれについて、Down/Held/Upの
        /// 状態に応じたイベントを生成し、Navigationへ送信。
        /// 
        /// ## 消費
        /// 
        /// Down時にNavigationServiceがtrueを返した場合のみ
        /// Consumedフラグを立てる。Held/Upは常に送信。
        /// </summary>
        void ProcessButtonInput(ref InputFrame frame, Vector2 pointerPos, float dt, ControlScheme scheme, InputUsageMode usage)
        {
            ProcessButton(ref frame.Submit, UIInputEventType.SubmitDown, UIInputEventType.SubmitHeld, UIInputEventType.SubmitUp, pointerPos, dt, scheme, usage);
            ProcessButton(ref frame.Cancel, UIInputEventType.CancelDown, UIInputEventType.CancelHeld, UIInputEventType.CancelUp, pointerPos, dt, scheme, usage);
            ProcessButton(ref frame.Retry, UIInputEventType.RetryDown, UIInputEventType.RetryHeld, UIInputEventType.RetryUp, pointerPos, dt, scheme, usage);

            // Gameplay buttons that may be used by UI elements (e.g., ButtonChannel trigger)
            ProcessButton(ref frame.Attack, UIInputEventType.AttackDown, UIInputEventType.AttackHeld, UIInputEventType.AttackUp, pointerPos, dt, scheme, usage);
            ProcessButton(ref frame.Interact, UIInputEventType.InteractDown, UIInputEventType.InteractHeld, UIInputEventType.InteractUp, pointerPos, dt, scheme, usage);
            ProcessButton(ref frame.Pause, UIInputEventType.PauseDown, UIInputEventType.PauseHeld, UIInputEventType.PauseUp, pointerPos, dt, scheme, usage);
        }

        void ProcessButton(ref ButtonState button, UIInputEventType down, UIInputEventType held, UIInputEventType up, Vector2 pointerPos, float dt, ControlScheme scheme, InputUsageMode usage)
        {
            if (button.Down && !button.Consumed)
            {
                var e = new UIInputEvent(down, pointerPos, default, dt, scheme, usage);
                if (_navigationService.ReceiveInputEvent(in e))
                {
                    button.Consumed = true;
                }
                return;
            }

            if (button.Held && !button.Consumed)
            {
                var e = new UIInputEvent(held, pointerPos, default, dt, scheme, usage);
                _navigationService.ReceiveInputEvent(in e);
                return;
            }

            if (button.Up && !button.Consumed)
            {
                var e = new UIInputEvent(up, pointerPos, default, dt, scheme, usage);
                _navigationService.ReceiveInputEvent(in e);
            }
        }

        /// <summary>
        /// スクロール入力の処理。
        /// 
        /// ## 処理内容
        /// 
        /// スクロール入力がある場合、ScrollイベントをNavigationへ送信。
        /// リストのスクロールや、ズーム操作などに使用される。
        /// </summary>
        void ProcessScrollInput(ref InputFrame frame, Vector2 pointerPos, float dt, ControlScheme scheme, InputUsageMode usage)
        {
            if (frame.Scroll != Vector2.zero && !frame.ScrollConsumed)
            {
                var e = UIInputEvent.ScrollEvent(frame.Scroll, pointerPos, dt, scheme, usage);
                if (_navigationService.ReceiveInputEvent(in e))
                {
                    frame.ScrollConsumed = true;
                }
            }
        }
    }
}
