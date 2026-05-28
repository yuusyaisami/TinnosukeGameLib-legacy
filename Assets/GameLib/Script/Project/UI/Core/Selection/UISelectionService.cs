#nullable enable
using UnityEngine;
using System;
using VContainer;
using System.Collections.Generic;

namespace Game.UI
{
    // ================================================================
    // UISelectionService.cs - UI選択管理サービス実装
    // ================================================================
    //
    // ## 概要
    //
    // UISelectionServiceは、UIの選択状態を一元管理するサービス。
    // 分割されたインターフェース群（UISelectionInterfaces.cs）を実装する。
    //
    // ## 責務
    //
    // 1. **選択状態の保持**: Current, Previous, Hovered
    // 2. **選択操作**: TrySelect, ClearSelection
    // 3. **ナビゲーション選択**: 方向入力による候補選択
    // 4. **ポインター選択**: マウスによるヒット選択
    // 5. **入力転送**: 選択中UIElementへの入力配信
    // 6. **ModalStack連携**: 境界内クランプ
    //
    // ## 依存サービス
    //
    // - IModalStackChannelHubService: Modal Stack Channel境界の判定
    // - ISelectCandidateProvider: 候補の取得（後から設定）
    //   （Mask 判定は CandidateProvider 内で行われる）
    //
    // ## 選択の成功条件
    //
    // TrySelectが成功するには:
    // 1. targetがnullでない
    // 2. targetがModal Stack CurrentInputRoot内にいる
    // 3. targetのUIElementStateがEffectivelyActive
    //
    // ================================================================

    /// <summary>
    /// UI選択管理サービスの実装。
    /// 
    /// ## インターフェース実装
    /// 
    /// - IUISelectionService: 公開API統合
    /// - IUISelectionServiceInternal: ModalStack用内部API
    /// </summary>
    public sealed class UISelectionService : IUISelectionService, IUISelectionServiceInternal, IUISelectionTelemetry, IUISelectionNavigation, IUISelectionBlockService, IDisposable
    {
        // ================================================================
        // 依存サービス
        // ================================================================

        /// <summary>
        /// Modal Stackサービス。
        /// 選択境界の判定に使用。
        /// </summary>
        readonly IModalStackChannelHubService _modalStackHub;

        /// <summary>
        /// 候補プロバイダー。
        /// ナビゲーション/ポインター選択時の候補取得に使用。
        /// SetCandidateProviderで後から設定可能。
        /// </summary>
        ISelectCandidateProvider? _candidateProvider;
        IUINodeGraphService? _nodeGraph;


        // ================================================================
        // 状態
        // ================================================================

        /// <summary>現在選択中ノードの handle。</summary>
        UINodeHandle _currentHandle = UINodeHandle.Invalid;

        /// <summary>前回選択されていたノードの handle。</summary>
        UINodeHandle _previousHandle = UINodeHandle.Invalid;

        /// <summary>現在ホバー中ノードの handle。</summary>
        UINodeHandle _hoveredHandle = UINodeHandle.Invalid;

        /// <summary>現在選択中のUIElementのConsumerリスト</summary>
        readonly List<IUIInputConsumer> _currentConsumers = new();

        // Selection source tracking to avoid pointer-driven clears overriding navigation-driven selection
        public enum SelectionSource { None, Pointer, Navigation }
        SelectionSource _selectionSource = SelectionSource.None;

        // Pointer miss hysteresis to avoid flicker when pointer hit tests are unstable
        int _consecutivePointerMisses = 0;
        const int PointerMissThreshold = 2; // require N consecutive misses before clearing selection

        // ================================================================
        // Selection block (editing etc.)
        // ================================================================

        readonly HashSet<object> _navBlockers = new();
        readonly HashSet<object> _pointerBlockers = new();

        public bool IsNavigationBlocked => _navBlockers.Count > 0;
        public bool IsPointerBlocked => _pointerBlockers.Count > 0;

        public IDisposable AcquireBlock(object owner, UISelectionBlockMask mask = UISelectionBlockMask.All)
        {
            if (owner == null)
                return NoopDisposable.Instance;

            if ((mask & UISelectionBlockMask.Navigation) != 0)
                _navBlockers.Add(owner);
            if ((mask & UISelectionBlockMask.Pointer) != 0)
                _pointerBlockers.Add(owner);

            return new Releaser(this, owner, mask);
        }

        sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();
            public void Dispose() { }
        }

        sealed class Releaser : IDisposable
        {
            UISelectionService? _svc;
            readonly object _owner;
            readonly UISelectionBlockMask _mask;

            public Releaser(UISelectionService svc, object owner, UISelectionBlockMask mask)
            {
                _svc = svc;
                _owner = owner;
                _mask = mask;
            }

            public void Dispose()
            {
                var svc = _svc;
                if (svc == null)
                    return;

                _svc = null;

                if ((_mask & UISelectionBlockMask.Navigation) != 0)
                    svc._navBlockers.Remove(_owner);
                if ((_mask & UISelectionBlockMask.Pointer) != 0)
                    svc._pointerBlockers.Remove(_owner);
            }
        }

        // ================================================================
        // バッファ（GC対策）
        // ================================================================

        /// <summary>候補取得用の一時リスト</summary>
        readonly List<SelectCandidate> _candidateBuffer = new();

        /// <summary>候補取得用の一時リスト（rootごとの集計）</summary>
        readonly List<SelectCandidate> _candidateScratch = new();

        /// <summary>候補重複排除用</summary>
        readonly HashSet<IScopeNode> _candidateDedup = new();

        readonly List<IScopeNode> _rootScopeBuffer = new();

        /// <summary>全候補取得用の一時リスト</summary>
        readonly List<IScopeNode> _selectableBuffer = new();

        /// <summary>全候補取得用の一時リスト（rootごとの集計）</summary>
        readonly List<IScopeNode> _selectableScratch = new();

        // Telemetry: last candidates
        readonly List<SelectCandidate> _lastNavigationCandidates = new();
        readonly List<SelectCandidate> _lastPointerCandidates = new();

        // ================================================================
        // プロパティ - IUISelectionState
        // ================================================================

        /// <inheritdoc/>
        public IScopeNode? CurrentElement => ResolveScopeOrNull(_currentHandle);

        public UINodeHandle CurrentHandle => _currentHandle;

        /// <inheritdoc/>
        public IScopeNode? PreviousElement => ResolveScopeOrNull(_previousHandle);

        public UINodeHandle PreviousHandle => _previousHandle;

        /// <inheritdoc/>
        public IScopeNode? HoveredElement => ResolveScopeOrNull(_hoveredHandle);

        public UINodeHandle HoveredHandle => _hoveredHandle;

        /// <inheritdoc/>
        public IReadOnlyList<IUIInputConsumer> CurrentConsumers => _currentConsumers;


        // Telemetry properties
        ISelectCandidateProvider? IUISelectionTelemetry.CandidateProvider => _candidateProvider;
        IUIModalRoot? IUISelectionTelemetry.CurrentInputRoot => _modalStackHub.CurrentInputRoot;
        IReadOnlyList<SelectCandidate> IUISelectionTelemetry.LastNavigationCandidates => _lastNavigationCandidates;
        IReadOnlyList<SelectCandidate> IUISelectionTelemetry.LastPointerCandidates => _lastPointerCandidates;
        UISelectionService.SelectionSource IUISelectionTelemetry.LastSelectionSource => _selectionSource;
        IScopeNode? IUISelectionTelemetry.Current => CurrentElement;
        IScopeNode? IUISelectionTelemetry.Previous => PreviousElement;
        IScopeNode? IUISelectionTelemetry.Hovered => HoveredElement;
        IReadOnlyList<IUIInputConsumer> IUISelectionTelemetry.CurrentConsumers => _currentConsumers;

        event Action<IScopeNode?>? IUISelectionTelemetry.OnSelectionChanged
        {
            add => OnSelectionChanged += value;
            remove => OnSelectionChanged -= value;
        }

        event Action<IScopeNode?>? IUISelectionTelemetry.OnHoverChanged
        {
            add => OnHoverChanged += value;
            remove => OnHoverChanged -= value;
        }

        event System.Action? IUISelectionTelemetry.OnCandidatesUpdated { add { _onCandidatesUpdated += value; } remove { _onCandidatesUpdated -= value; } }
        System.Action? _onCandidatesUpdated;

        // ================================================================
        // プロパティ - IUISelectionServiceInternal
        // ================================================================

        /// <summary>
        /// IUISelectionServiceInternal.Current - ModalStack用のIUIInputConsumer取得。
        /// CurrentConsumersの最優先のものを返す。
        /// </summary>
        IUIInputConsumer? IUISelectionServiceInternal.Current =>
            _currentConsumers.Count > 0 ? _currentConsumers[0] : null;

        // ================================================================
        // イベント
        // ================================================================

        /// <inheritdoc/>
        public event Action<IScopeNode?>? OnSelectionChanged;

        /// <inheritdoc/>
        public event Action<IScopeNode?>? OnHoverChanged;

        // ================================================================
        // コンストラクタ
        // ================================================================

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="modalStackHub">Modal Stack Channelサービス（必須）</param>
        public UISelectionService(IModalStackChannelHubService modalStackHub, ISelectCandidateProvider? candidateProvider = null)
        {
            _modalStackHub = modalStackHub;
            _candidateProvider = candidateProvider;

            // Modal Stack Channel変更時に選択をクランプする
            _modalStackHub.OnLayerStatesChanged += HandleLayerStatesChanged;

            HandleModalBoundaryChanged();
        }

        public void Dispose()
        {
            _modalStackHub.OnLayerStatesChanged -= HandleLayerStatesChanged;
        }

        // ================================================================
        // 設定メソッド（後からの依存注入）
        // ================================================================

        /// <summary>
        /// CandidateProviderを設定する。
        /// 
        /// ## 呼び出しタイミング
        /// 
        /// UILifetimeScopeのBuild完了後、UICanvasServiceから設定される。
        /// </summary>
        public void SetCandidateProvider(ISelectCandidateProvider? provider)
        {
            _candidateProvider = provider;
            HandleModalBoundaryChanged();
        }

        public void SetNodeGraph(IUINodeGraphService? nodeGraph)
        {
            _nodeGraph = nodeGraph;
        }

        bool TryGetActiveRootScopes(List<IScopeNode> results)
        {
            results.Clear();

            var layerStates = _modalStackHub.LayerStates;
            if (layerStates != null && layerStates.Count > 0)
            {
                for (int i = 0; i < layerStates.Count; i++)
                {
                    var layerState = layerStates[i];
                    if (!layerState.InputActive)
                        continue;

                    var scope = layerState.ActiveRoot?.OwnerScope;
                    if (scope != null && !results.Contains(scope))
                        results.Add(scope);
                }
                return results.Count > 0;
            }

            var fallback = _modalStackHub.CurrentInputRoot?.OwnerScope;
            if (fallback != null)
            {
                results.Add(fallback);
                return true;
            }

            return false;
        }

        // ================================================================
        // 選択申請API - IUISelectionNavigation
        // ================================================================

        /// <inheritdoc/>
        public bool Select(IScopeNode? target)
        {
            if (target == null)
            {
                return SelectInternal(UINodeHandle.Invalid);
            }

            if (!TryResolveTarget(target, out var handle) || !CanSelect(handle))
            {
                return false;
            }

            return SelectInternal(handle);
        }

        public bool Select(UINodeHandle target)
        {
            if (!target.IsValid)
                return SelectInternal(UINodeHandle.Invalid);

            if (!CanSelect(target))
                return false;

            return SelectInternal(target);
        }

        /// <inheritdoc/>
        public bool TrySelect(IScopeNode target)
        {
            if (target == null)
            {
                Debug.LogWarning("[UISelectionService] TrySelect: target is null. Use ClearSelection() instead.");
                return false;
            }

            if (!TryResolveTarget(target, out var handle) || !CanSelect(handle))
            {
                return false;
            }

            return SelectInternal(handle);
        }

        public bool TrySelect(UINodeHandle target)
        {
            return CanSelect(target) && SelectInternal(target);
        }

        /// <inheritdoc/>
        public void ClearSelection()
        {
            SelectInternal(UINodeHandle.Invalid);
            _selectionSource = SelectionSource.None;
            _consecutivePointerMisses = 0;
        }

        /// <inheritdoc/>
        public void ForceSelect(IScopeNode? target)
        {
            Debug.LogWarning("[UISelectionService] ForceSelect: This is for debug only!");
            if (target == null)
            {
                SelectInternal(UINodeHandle.Invalid);
                return;
            }

            if (!TryResolveTarget(target, out var handle))
                return;

            SelectInternal(handle);
        }

        public void ForceSelect(UINodeHandle target)
        {
            if (target.IsValid && !TryResolveTarget(target, out _))
                return;

            SelectInternal(target);
        }

        // ================================================================
        // ナビゲーション選択 - IUISelectionNavigation
        // ================================================================

        /// <inheritdoc/>
        public bool TryNavigateSelect(NavigateDirection direction)
        {
            if (IsNavigationBlocked)
            {
                // Treat as handled to avoid other navigation actions while blocked.
                return true;
            }

            Debug.Log($"[UISelectionService] TryNavigateSelect: direction={direction}");
            if (_currentHandle.IsValid && !CanNavigateSelect(_currentHandle))
            {
                SelectInternal(UINodeHandle.Invalid);
            }
            if (_candidateProvider == null)
            {
                Debug.LogWarning("[UISelectionService] TryNavigateSelect: No CandidateProvider set.");
                return false;
            }

            if (!TryGetActiveRootScopes(_rootScopeBuffer))
            {
                Debug.LogWarning("[UISelectionService] TryNavigateSelect: No ActiveRoots set.");
                return false;
            }

            _candidateBuffer.Clear();
            _candidateDedup.Clear();
            for (int r = 0; r < _rootScopeBuffer.Count; r++)
            {
                var rootScope = _rootScopeBuffer[r];
                _candidateScratch.Clear();
                _candidateProvider.GetNavigationCandidates(CurrentElement, direction, rootScope, _candidateScratch);
                for (int i = 0; i < _candidateScratch.Count; i++)
                {
                    var c = _candidateScratch[i];
                    if (c.Element == null)
                        continue;
                    if (_candidateDedup.Add(c.Element))
                        _candidateBuffer.Add(c);
                }
            }

            _candidateBuffer.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Telemetry: copy navigation candidates
            _lastNavigationCandidates.Clear();
            _lastNavigationCandidates.AddRange(_candidateBuffer);
            _onCandidatesUpdated?.Invoke();

            string candidatesInfo = "";
            foreach (var candidate in _candidateBuffer)
            {
                var n = candidate.Element?.Identity?.SelfTransform != null
                    ? candidate.Element.Identity.SelfTransform.name
                    : "null";
                candidatesInfo += $"{n} ";
            }

            Debug.Log($"[UISelectionService] TryNavigateSelect: direction={direction}, candidates={_candidateBuffer.Count}, info={candidatesInfo}");
            // 候補を順に試行
            // （Mask 判定は CandidateProvider 内で完了済み）
            foreach (var candidate in _candidateBuffer)
            {
                var e = candidate.Element;
                if (e == null) continue;

                if (!CanNavigateSelect(e))
                {
                    continue;
                }

                // Navigation uses navigation-only selectability before selection.
                if (TrySelect(e))
                {
                    // Navigation selection wins and should be marked as navigation source
                    _selectionSource = SelectionSource.Navigation;
                    return true;
                }
            }

            return false;
        }

        // ================================================================
        // ポインター選択 - IUISelectionNavigation
        // ================================================================

        /// <inheritdoc/>
        public bool TryPointerSelect(Vector2 screenPosition)
        {
            if (IsPointerBlocked)
            {
                // While blocked: update hover only, never change/clear selection.
                UpdateHoverInternal(screenPosition, allowSelectionClear: false);
                return false;
            }

            return TryPointerSelectInternal(screenPosition);
        }

        bool TryPointerSelectInternal(Vector2 screenPosition)
        {
            if (_currentHandle.IsValid && !CanSelect(_currentHandle))
            {
                SelectInternal(UINodeHandle.Invalid);
            }
            if (_candidateProvider == null)
            {
                SetHoverInternal(UINodeHandle.Invalid);
                Debug.LogWarning("[UISelectionService] TryPointerSelect: No CandidateProvider set.");
                return false;
            }

            if (!TryGetActiveRootScopes(_rootScopeBuffer))
            {
                SetHoverInternal(UINodeHandle.Invalid);
                return false;
            }

            _candidateBuffer.Clear();
            _candidateDedup.Clear();
            for (int r = 0; r < _rootScopeBuffer.Count; r++)
            {
                var rootScope = _rootScopeBuffer[r];
                _candidateScratch.Clear();
                _candidateProvider.GetPointerHitCandidates(screenPosition, rootScope, _candidateScratch);
                for (int i = 0; i < _candidateScratch.Count; i++)
                {
                    var c = _candidateScratch[i];
                    if (c.Element == null)
                        continue;
                    if (_candidateDedup.Add(c.Element))
                        _candidateBuffer.Add(c);
                }
            }

            // Telemetry: copy pointer candidates
            _lastPointerCandidates.Clear();
            _lastPointerCandidates.AddRange(_candidateBuffer);
            _onCandidatesUpdated?.Invoke();

            // 前面優先で試行（Hover と Select を同期）
            // （Mask 判定は CandidateProvider 内で完了済み）
            foreach (var candidate in _candidateBuffer)
            {
                var e = candidate.Element;
                if (e == null) continue;
                if (!CanSelect(e)) continue;

                // Reset miss counter when we have a hit
                _consecutivePointerMisses = 0;

                if (TryResolveTarget(e, out var handle) && handle == _currentHandle)
                {
                    SetHoverInternal(handle);
                    return false;
                }

                SetHoverInternal(handle);

                // Attempt to select; only update selection source if selection actually changed (avoid converting navigation-driven selection to pointer-driven on harmless hover)
                var selected = SelectInternal(handle);
                if (selected)
                {
                    _selectionSource = SelectionSource.Pointer;
                }
                return selected;
            }

            // ヒットなし：ミスカウンターをインクリメントするが、しきい値を超えた場合のみ選択をクリアする
            _consecutivePointerMisses++;

            // Always clear hover immediately
            SetHoverInternal(UINodeHandle.Invalid);

            if (_consecutivePointerMisses >= PointerMissThreshold)
            {
                // Only clear selection if it was driven by the pointer
                if (_selectionSource == SelectionSource.Pointer)
                {
                    SelectInternal(UINodeHandle.Invalid);
                    _selectionSource = SelectionSource.None;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void UpdateHover(Vector2 screenPosition)
        {
            if (IsPointerBlocked)
            {
                UpdateHoverInternal(screenPosition, allowSelectionClear: false);
                return;
            }

            UpdateHoverInternal(screenPosition, allowSelectionClear: true);
        }

        void UpdateHoverInternal(Vector2 screenPosition, bool allowSelectionClear)
        {
            if (_candidateProvider == null)
            {
                SetHoverInternal(UINodeHandle.Invalid);
                return;
            }

            if (!TryGetActiveRootScopes(_rootScopeBuffer))
            {
                SetHoverInternal(UINodeHandle.Invalid);
                return;
            }

            _candidateBuffer.Clear();
            _candidateDedup.Clear();
            for (int r = 0; r < _rootScopeBuffer.Count; r++)
            {
                var rootScope = _rootScopeBuffer[r];
                _candidateScratch.Clear();
                _candidateProvider.GetPointerHitCandidates(screenPosition, rootScope, _candidateScratch);
                for (int i = 0; i < _candidateScratch.Count; i++)
                {
                    var c = _candidateScratch[i];
                    if (c.Element == null)
                        continue;
                    if (_candidateDedup.Add(c.Element))
                        _candidateBuffer.Add(c);
                }
            }

            _lastPointerCandidates.Clear();
            _lastPointerCandidates.AddRange(_candidateBuffer);
            _onCandidatesUpdated?.Invoke();

            foreach (var candidate in _candidateBuffer)
            {
                var e = candidate.Element;
                if (e == null)
                    continue;

                if (CanSelect(e))
                {
                    if (allowSelectionClear)
                        _consecutivePointerMisses = 0;

                    if (TryResolveTarget(e, out var handle))
                        SetHoverInternal(handle);
                    return;
                }
            }

            SetHoverInternal(UINodeHandle.Invalid);

            if (!allowSelectionClear)
                return;

            _consecutivePointerMisses++;
            if (_consecutivePointerMisses >= PointerMissThreshold && _selectionSource == SelectionSource.Pointer)
            {
                SelectInternal(UINodeHandle.Invalid);
                _selectionSource = SelectionSource.None;
            }
        }

        // ================================================================
        // 入力転送 - IUISelectionInputRelay
        // ================================================================

        /// <inheritdoc/>
        public bool SendInputToCurrentSelection(in UIInputEvent inputEvent)
        {
            //Debug.Log($"[UISelectionService] SendInputToCurrentSelection: eventType={inputEvent.Type}");
            if (_currentHandle.IsValid && !CanSelect(_currentHandle))
            {
                SelectInternal(UINodeHandle.Invalid);
                return false;
            }
            if (_currentConsumers.Count == 0)
            {
                return false;
            }

            // Priority順にソート済みと仮定して、順に処理
            foreach (var consumer in _currentConsumers)
            {
                if (consumer.Consume(in inputEvent))
                {
                    return true;
                }
            }

            return false;
        }

        // ================================================================
        // ユーティリティ - IUISelectionNavigation
        // ================================================================

        /// <inheritdoc/>
        public bool CanSelect(IScopeNode? target)
        {
            return TryResolveTarget(target, out var handle) && CanSelect(handle);
        }

        public bool CanSelect(UINodeHandle target)
        {
            if (!target.IsValid)
                return false;

            if (!_modalStackHub.IsInAnyInputRoot(target))
                return false;

            if (!TryResolveTarget(target, out var resolved))
                return false;

            var state = resolved.GetUIElementState();
            if (state != null && !state.IsEffectivelyActive)
                return false;
            if (state != null && !state.IsVisible)
                return false;
            if (state != null && !state.EvaluateIsSelectable())
                return false;

            return true;
        }

        bool CanNavigateSelect(IScopeNode? target)
        {
            return TryResolveTarget(target, out var handle) && CanNavigateSelect(handle);
        }

        bool CanNavigateSelect(UINodeHandle target)
        {
            if (!CanSelect(target))
            {
                return false;
            }

            if (!TryResolveTarget(target, out var resolved))
                return false;

            var state = resolved.GetUIElementState();
            if (state != null && !state.EvaluateIsNavigationSelectable())
            {
                return false;
            }

            return true;
        }

        // ================================================================
        // 内部メソッド
        // ================================================================

        /// <summary>
        /// 選択を内部的に変更する。
        /// </summary>
        bool SelectInternal(UINodeHandle target)
        {
            if (_currentHandle == target)
            {
                return false;
            }

            _previousHandle = _currentHandle;
            _currentHandle = target;

            // Consumerリストを更新
            UpdateCurrentConsumers();

            // イベント発火
            OnSelectionChanged?.Invoke(CurrentElement);

            var previous = PreviousElement;
            var current = CurrentElement;
            var prevName = previous?.Identity?.SelfTransform != null ? previous.Identity.SelfTransform.name : "null";
            var curName = current?.Identity?.SelfTransform != null ? current.Identity.SelfTransform.name : "null";

            return true;
        }

        /// <summary>
        /// ホバーを内部的に変更する。
        /// </summary>
        void SetHoverInternal(UINodeHandle target)
        {
            if (_hoveredHandle == target)
            {
                return;
            }

            _hoveredHandle = target;
            OnHoverChanged?.Invoke(HoveredElement);
        }

        bool TryResolveTarget(UINodeHandle target, out IScopeNode resolved)
        {
            resolved = null!;
            return target.IsValid && _nodeGraph != null && _nodeGraph.TryGetOwner(target, out resolved);
        }

        bool TryResolveTarget(IScopeNode? target, out UINodeHandle handle)
        {
            handle = UINodeHandle.Invalid;
            return target != null && _nodeGraph != null && _nodeGraph.TryGetHandle(target, out handle);
        }

        IScopeNode? ResolveScopeOrNull(UINodeHandle target)
        {
            return TryResolveTarget(target, out var resolved) ? resolved : null;
        }

        UINodeHandle ResolveHandle(IScopeNode? target)
        {
            if (target != null && _nodeGraph != null && _nodeGraph.TryGetHandle(target, out var handle))
                return handle;

            return UINodeHandle.Invalid;
        }

        /// <summary>
        /// 現在の選択からIUIInputConsumerリストを更新する。
        /// </summary>
        void UpdateCurrentConsumers()
        {
            _currentConsumers.Clear();

            var current = CurrentElement;
            if (current == null)
            {
                return;
            }

            var resolver = current.Resolver;
            if (resolver == null)
            {
                Debug.LogWarning($"[UISelectionService] Current element '{current.Identity?.SelfTransform?.name ?? "(unknown)"}' has no Resolver.");
                return;
            }

            // Resolver から ConsumerHub を取得
            if (resolver.TryResolve<IUIInputConsumerHub>(out var hub))
            {
                hub.GetAllConsumers(_currentConsumers);
            }
            // Priority順にソート（大きい方が先）
            _currentConsumers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        void HandleLayerStatesChanged(ModalLayerStatesChangedContext context)
        {
            _ = context;
            HandleModalBoundaryChanged();
        }

        void HandleModalBoundaryChanged()
        {
            if (_currentHandle.IsValid && !CanSelect(_currentHandle))
            {
                SelectInternal(UINodeHandle.Invalid);
            }

            if (!_currentHandle.IsValid && TryGetModalFallbackSelection(out var modalFallbackHandle))
            {
                SelectInternal(modalFallbackHandle);
                return;
            }

            if (!_currentHandle.IsValid && _candidateProvider != null)
            {
                if (!TryGetActiveRootScopes(_rootScopeBuffer))
                    return;

                _selectableBuffer.Clear();
                _candidateDedup.Clear();
                for (int r = 0; r < _rootScopeBuffer.Count; r++)
                {
                    _selectableScratch.Clear();
                    _candidateProvider.GetAllSelectableCandidates(_rootScopeBuffer[r], _selectableScratch);
                    for (int i = 0; i < _selectableScratch.Count; i++)
                    {
                        var e = _selectableScratch[i];
                        if (e == null) continue;
                        if (_candidateDedup.Add(e))
                            _selectableBuffer.Add(e);
                    }
                }

                _selectableBuffer.Sort((a, b) =>
                {
                    var aState = a.GetUIElementState();
                    var bState = b.GetUIElementState();
                    var aOrder = aState?.NavigationSelectionOrder ?? 0;
                    var bOrder = bState?.NavigationSelectionOrder ?? 0;
                    return bOrder.CompareTo(aOrder);
                });

                for (int i = 0; i < _selectableBuffer.Count; i++)
                {
                    var e = _selectableBuffer[i];
                    if (CanNavigateSelect(e) && TrySelect(e))
                    {
                        break;
                    }
                }
            }
        }

        bool TryGetModalFallbackSelection(out UINodeHandle handle)
        {
            handle = UINodeHandle.Invalid;

            var currentInputRoot = _modalStackHub.CurrentInputRoot;
            if (currentInputRoot == null)
                return false;

            var rootStates = _modalStackHub.RootStates;
            for (int i = 0; i < rootStates.Count; i++)
            {
                var state = rootStates[i];
                if (!ReferenceEquals(state.Root, currentInputRoot))
                    continue;

                if (state.DefaultSelectedHandle.IsValid && CanSelect(state.DefaultSelectedHandle))
                {
                    handle = state.DefaultSelectedHandle;
                    return true;
                }

                if (state.DefaultSelectedElement != null && TryResolveTarget(state.DefaultSelectedElement, out handle) && CanSelect(handle))
                    return true;

                break;
            }

            handle = UINodeHandle.Invalid;
            return false;
        }

        // ================================================================
        // IUISelectionServiceInternal 明示的実装
        // ================================================================

        /// <summary>
        /// IUIInputConsumerを選択する（ModalStack用）。
        /// </summary>
        void IUISelectionServiceInternal.Select(IUIInputConsumer? target)
        {
            if (target == null)
            {
                ClearSelection();
                return;
            }

            if (target is IUIHandleNode handleNode && handleNode.NodeHandle.IsValid)
            {
                SelectInternal(handleNode.NodeHandle);
                return;
            }

            // IUIInputConsumerからIScopeNodeを逆引き
            if (target is MonoBehaviour mb)
            {
                var parents = mb.GetComponentsInParent<MonoBehaviour>(includeInactive: true);
                for (int i = 0; i < parents.Length; i++)
                {
                    if (parents[i] is IScopeNode scope)
                    {
                        if (TryResolveTarget(scope, out var handle))
                        {
                            SelectInternal(handle);
                            return;
                        }
                    }
                }
            }

            Debug.LogWarning($"[UISelectionService] Cannot select IUIInputConsumer '{target}': " +
                           "Cannot find parent IScopeNode.");
        }
    }
}
