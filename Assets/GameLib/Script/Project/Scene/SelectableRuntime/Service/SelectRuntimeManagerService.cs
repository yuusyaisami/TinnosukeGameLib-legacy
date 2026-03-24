#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.SelectRuntime
{
    public sealed class SelectRuntimeManagerService :
        ISelectRuntimeManagerService,
        ISelectRuntimeManagerStateProvider,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly ISelectRuntimeManagerOptions _options;
        readonly IWorldPointerRuntimeService _pointerService;

        readonly HashSet<SelectableRuntimeMB> _selectables = new();
        readonly Dictionary<WorldPointerTargetMB, SelectableRuntimeMB> _selectableByTarget = new();

        SelectableRuntimeMB? _current;
        SelectableRuntimeMB? _hovered;
        bool _isEnabled = true;

        public event Action<SelectRuntimeSelectionChangedEvent>? OnSelectionChanged;
        public event Action<SelectRuntimeHoveredChangedEvent>? OnHoveredChanged;
        public event Action<SelectableRuntimeMB>? OnLeftClickSelectable;
        public event Action<SelectableRuntimeMB>? OnRightClickSelectable;
        public event Action<SelectableRuntimeMB>? OnLeftLongPressSelectable;
        public event Action<bool>? OnEnabledChanged;

        public SelectableRuntimeMB? Current => _current;
        public SelectableRuntimeMB? Hovered => _hovered;
        public bool IsEnabled => _isEnabled;

        public SelectRuntimeManagerService(
            IScopeNode owner,
            ISelectRuntimeManagerOptions options,
            IWorldPointerRuntimeService pointerService)
        {
            _owner = owner;
            _options = options;
            _pointerService = pointerService;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _pointerService.OnHoveredChanged += HandleHoveredChanged;
            _pointerService.OnLeftClicked += HandleLeftClicked;
            _pointerService.OnRightClicked += HandleRightClicked;
            _pointerService.OnLeftLongPressStarted += HandleLeftLongPressStarted;
            EvaluateIsEnabled();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _pointerService.OnHoveredChanged -= HandleHoveredChanged;
            _pointerService.OnLeftClicked -= HandleLeftClicked;
            _pointerService.OnRightClicked -= HandleRightClicked;
            _pointerService.OnLeftLongPressStarted -= HandleLeftLongPressStarted;

            _selectables.Clear();
            _selectableByTarget.Clear();
            SetHovered(null);
            SetSelection(null);
        }

        public bool EvaluateIsEnabled()
        {
            var next = true;
            if (_options.IsEnabled.HasSource)
            {
                var vars = ResolveLocalVars(_owner);
                var context = new SimpleDynamicContext(vars, _owner);
                if (_options.IsEnabled.TryGet(context, out var resolved))
                    next = resolved;
            }

            if (next == _isEnabled)
                return _isEnabled;

            _isEnabled = next;
            if (!_isEnabled)
            {
                SetHovered(null);
                SetSelection(null);
            }

            OnEnabledChanged?.Invoke(_isEnabled);
            return _isEnabled;
        }

        public void RegisterSelectable(SelectableRuntimeMB selectable)
        {
            if (selectable == null || !_selectables.Add(selectable))
                return;

            var target = selectable.ResolveTarget();
            if (target != null)
                _selectableByTarget[target] = selectable;
        }

        public void UnregisterSelectable(SelectableRuntimeMB selectable)
        {
            if (selectable == null || !_selectables.Remove(selectable))
                return;

            var target = selectable.ResolveTarget();
            if (target != null && _selectableByTarget.TryGetValue(target, out var mapped) && ReferenceEquals(mapped, selectable))
                _selectableByTarget.Remove(target);

            if (ReferenceEquals(_hovered, selectable))
                SetHovered(null);

            if (ReferenceEquals(_current, selectable))
                SetSelection(null);
        }

        public void GetRegisteredSelectables(List<SelectableRuntimeMB> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (var selectable in _selectables)
            {
                if (selectable != null)
                    results.Add(selectable);
            }
        }

        void HandleHoveredChanged(WorldPointerHoverChangedEventData eventData)
        {
            if (!EvaluateIsEnabled())
                return;

            SetHovered(ResolveSelectable(eventData.CurrentTarget));
        }

        void HandleLeftClicked(WorldPointerEventData eventData)
        {
            if (!EvaluateIsEnabled())
                return;

            var selectable = ResolveSelectable(eventData.Target);
            if (selectable == null)
            {
                SetSelection(null);
                return;
            }

            SetSelection(selectable);
            OnLeftClickSelectable?.Invoke(selectable);
        }

        void HandleRightClicked(WorldPointerEventData eventData)
        {
            if (!EvaluateIsEnabled())
                return;

            var selectable = ResolveSelectable(eventData.Target);
            if (selectable != null)
                OnRightClickSelectable?.Invoke(selectable);
        }

        void HandleLeftLongPressStarted(WorldPointerEventData eventData)
        {
            if (!EvaluateIsEnabled())
                return;

            var selectable = ResolveSelectable(eventData.Target);
            if (selectable == null)
                return;

            SetSelection(selectable);
            OnLeftLongPressSelectable?.Invoke(selectable);
        }

        void SetHovered(SelectableRuntimeMB? next)
        {
            if (ReferenceEquals(_hovered, next))
                return;

            var previous = _hovered;
            _hovered = next;
            OnHoveredChanged?.Invoke(new SelectRuntimeHoveredChangedEvent(previous, next));
        }

        void SetSelection(SelectableRuntimeMB? next)
        {
            if (ReferenceEquals(_current, next))
                return;

            var previous = _current;
            _current = next;

            if (previous != null)
            {
                var hovered = ReferenceEquals(previous, _hovered);
                SelectRuntimeCommandUtility.Execute(previous, _owner, selected: false, hovered: hovered, editing: false, previous.OnDeselectedCommands);
            }

            if (next != null)
            {
                var hovered = ReferenceEquals(next, _hovered);
                SelectRuntimeCommandUtility.Execute(next, _owner, selected: true, hovered: hovered, editing: false, next.OnSelectedCommands);
            }

            OnSelectionChanged?.Invoke(new SelectRuntimeSelectionChangedEvent(previous, next));
        }

        SelectableRuntimeMB? ResolveSelectable(WorldPointerTargetMB? target)
        {
            if (target == null)
                return null;

            return _selectableByTarget.TryGetValue(target, out var selectable) ? selectable : null;
        }

        static IVarStore ResolveLocalVars(IScopeNode scope)
        {
            if (scope.Resolver != null && scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                return blackboard.LocalVars;

            return NullVarStore.Instance;
        }
    }
}
