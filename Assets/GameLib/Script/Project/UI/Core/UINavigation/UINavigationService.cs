#nullable enable
using UnityEngine;
using Game.Input;
using System;

namespace Game.UI
{
    // ================================================================
    // NavigateDirection: ナビゲーションの方向
    // ================================================================

    /// <summary>
    /// ナビゲーションの方向を表す列挙型
    /// </summary>
    public enum NavigateDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    // ================================================================
    // INavigateCandidateProvider: Select側からナビゲーション候補を取得
    // ================================================================

    /// <summary>
    /// 現在選択中のUIElementから、各方向の移動先候補を取得するインターフェース。
    /// SelectシステムがUIElementごとに実装を提供する。
    /// </summary>
    public interface INavigateCandidateProvider
    {
        /// <summary>
        /// 指定方向へのナビゲーション先を取得する。
        /// nullの場合は自動計算にフォールバック。
        /// </summary>
        IUIInputConsumer? GetNavigateTarget(NavigateDirection direction);

        /// <summary>
        /// このProviderが選択可能かどうか
        /// </summary>
        bool IsSelectable { get; }
    }

    // ================================================================
    // IUINavigationService: ナビゲーションサービスの公開API
    // ================================================================

    /// <summary>
    /// UIナビゲーションサービスの公開インターフェース。
    /// UIInputServiceからUIInputEventを受け取り、
    /// 現在のSelectに基づいて入力を処理する。
    /// </summary>
    public interface IUINavigationService
    {
        /// <summary>
        /// UIInputServiceからの入力イベントを受け取る。
        /// trueを返した場合、入力は消費された。
        /// </summary>
        bool ReceiveInputEvent(in UIInputEvent e);

        /// <summary>
        /// 現在のナビゲーション対象（選択中のUIElement）の入力消費者
        /// </summary>
        IUIInputConsumer? CurrentTarget { get; }

        /// <summary>
        /// ナビゲーション対象が変更されたときに発火
        /// </summary>
        event Action<IUIInputConsumer?>? OnTargetChanged;



        /// <summary>
        /// 指定方向へナビゲーションを実行する
        /// </summary>
        bool NavigateInDirection(NavigateDirection direction);
    }

    // ================================================================
    // UINavigationService: メイン実装
    // ================================================================

    /// <summary>
    /// UIナビゲーションサービス。
    /// 
    /// 役割:
    /// - UIInputServiceからUIInputEventを受け取る
    /// - 現在のSelectにイベントを配信する
    /// - 上下左右のナビゲーション入力を処理する
    /// - ポインター入力によるSelect判定（スタブ）
    /// 
    /// 依存:
    /// - IUISelectionService: 現在の選択状態を管理
    /// - 将来的にModalStack等と連携
    /// </summary>
    public sealed class UINavigationService : IUINavigationService, IUINavigationTelemetry, IDisposable
    {
        readonly IUISelectionService _selectionService;
        readonly IControlSchemeService _controlSchemeService;
        readonly IUIInputNavigateService? _inputNavigate;
        readonly UINavigationOptions? _options;

        IScopeNode? _currentElement;

        // ナビゲーションの入力閾値とリピート設定
        // オプションから取得、なければデフォルト値
        float NavigateThreshold => _options?.NavigateThreshold ?? 0.5f;
        float NavigateRepeatDelay => _options?.RepeatDelay ?? 0.4f;
        float NavigateRepeatRate => _options?.RepeatRate ?? 0.1f;

        float _navigateRepeatTimer;
        NavigateDirection _lastNavigateDirection;
        Vector2 _lastPointerPosition;
        bool _hasLastPointerPosition;

        /// <summary>
        /// 現在選択中のUIElement。
        /// CurrentConsumersから最優先のIUIInputConsumerを取得可能。
        /// </summary>
        public IUIInputConsumer? CurrentTarget =>
            _selectionService.CurrentConsumers.Count > 0
                ? _selectionService.CurrentConsumers[0]
                : null;

        // IUINavigationTelemetry implementation
        IUIInputConsumer? IUINavigationTelemetry.CurrentTarget => CurrentTarget;
        NavigateDirection IUINavigationTelemetry.LastNavigateDirection => _lastNavigateDirection;
        InputUsageMode IUINavigationTelemetry.InputUsageMode => _controlSchemeService.CurrentUsageMode;
        float IUINavigationTelemetry.NavigateRepeatTimer => _navigateRepeatTimer;
        float IUINavigationTelemetry.NavigateThreshold => NavigateThreshold;
        float IUINavigationTelemetry.RepeatDelay => NavigateRepeatDelay;
        float IUINavigationTelemetry.RepeatRate => NavigateRepeatRate;

        public event Action<IUIInputConsumer?>? OnTargetChanged;

        public UINavigationService(
            IUISelectionService selectionService,
            IControlSchemeService controlSchemeService,
            UINavigationOptions? options = null,
            IUIInputNavigateService? inputNavigate = null)
        {
            _selectionService = selectionService;
            _controlSchemeService = controlSchemeService;
            _options = options;
            _inputNavigate = inputNavigate;

            // SelectionServiceの変更を購読
            _selectionService.OnSelectionChanged += HandleSelectionChanged;
        }

        public void Dispose()
        {
            _selectionService.OnSelectionChanged -= HandleSelectionChanged;
        }

        void HandleSelectionChanged(IScopeNode? newSelection)
        {
            if (!ReferenceEquals(_currentElement, newSelection))
            {
                _currentElement = newSelection;
                // IUIInputConsumerベースのイベントも発火（互換性のため）
                OnTargetChanged?.Invoke(CurrentTarget);
            }
        }

        public bool ReceiveInputEvent(in UIInputEvent e)
        {
            // まず現在のSelectに入力を流す
            var consumed = DispatchToCurrentTarget(in e);

            // Hover は UI 全体の状態なので、consume と独立して常に更新する
            if (e.Type == UIInputEventType.PointerMove)
            {
                // delegate to ProcessNavigationEvent so pointer handling respects input scheme
                ProcessNavigationEvent(in e);
                return consumed;
            }

            // 消費されなかった場合、ナビゲーション処理を試みる
            if (!consumed)
            {
                // Selection が消費しなかった場合、InputNavigate を挟む（Selection > InputNavigate > UINavigate）
                if (_inputNavigate != null)
                {
                    consumed = _inputNavigate.ReceiveInputEvent(in e);
                }

                if (consumed)
                    return true;

                consumed = ProcessNavigationEvent(in e);
            }

            return consumed;
        }

        bool DispatchToCurrentTarget(in UIInputEvent e)
        {
            // SelectionServiceを通じて現在の選択に入力を配信
            return _selectionService.SendInputToCurrentSelection(in e);
        }

        bool ProcessNavigationEvent(in UIInputEvent e)
        {
            switch (e.Type)
            {
                case UIInputEventType.Navigate:
                    return HandleNavigateInput(e.Direction, e.DeltaTime);

                case UIInputEventType.PointerMove:
                    return HandlePointerMove(e.PointerPosition);

                default:
                    return false;
            }
        }

        bool HandleNavigateInput(Vector2 direction, float deltaTime)
        {
            var navDir = DirectionFromVector(direction);
            if (navDir == NavigateDirection.None)
            {
                // 入力がなくなったらリピートをリセット
                _lastNavigateDirection = NavigateDirection.None;
                _navigateRepeatTimer = 0f;
                return false;
            }

            // リピート処理
            if (navDir == _lastNavigateDirection)
            {
                _navigateRepeatTimer += deltaTime;
                if (_navigateRepeatTimer < NavigateRepeatDelay)
                {
                    return true; // 消費はするが移動はしない
                }

                // リピート発火
                if (_navigateRepeatTimer >= NavigateRepeatDelay + NavigateRepeatRate)
                {
                    _navigateRepeatTimer = NavigateRepeatDelay;
                }
                else
                {
                    return true; // まだリピート間隔に達していない
                }
            }
            else
            {
                // 新しい方向への入力
                _lastNavigateDirection = navDir;
                _navigateRepeatTimer = 0f;
            }

            return NavigateInDirection(navDir);
        }

        bool HandlePointerMove(Vector2 pointerPosition)
        {
            var hasMoved = !_hasLastPointerPosition
                || (pointerPosition - _lastPointerPosition).sqrMagnitude > 0.000001f;
            _lastPointerPosition = pointerPosition;
            _hasLastPointerPosition = true;

            // Only apply pointer->select synchronization when we are in pointer (mouse) mode.
            // If the input scheme is Keyboard/Gamepad we don't want pointer movement to
            // affect selection (user requested behavior).
            if (_controlSchemeService.CurrentUsageMode != InputUsageMode.Pointer)
            {
                //Debug.Log($"[UINavigationService] Skipping pointer move handling due to input usage mode. usageMode={_controlSchemeService.CurrentUsageMode}");
                return false;
            }

            if (!hasMoved)
            {
                return false;
            }

            // Try pointer select (hover+select synchronization). If nothing is hit, clear selection.
            var didSelect = _selectionService.TryPointerSelect(pointerPosition);

            return false; // ポインター移動自体は消費しない
        }


        public bool NavigateInDirection(NavigateDirection direction)
        {
            if (direction == NavigateDirection.None) return false;

            // SelectionServiceのTryNavigateSelectを使用
            return _selectionService.TryNavigateSelect(direction);
        }


        NavigateDirection DirectionFromVector(Vector2 v)
        {
            var threshold = NavigateThreshold;
            if (v.sqrMagnitude < threshold * threshold)
                return NavigateDirection.None;

            // 主要な方向を判定
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            {
                return v.x > 0 ? NavigateDirection.Right : NavigateDirection.Left;
            }
            else
            {
                return v.y > 0 ? NavigateDirection.Up : NavigateDirection.Down;
            }
        }
    }
}
