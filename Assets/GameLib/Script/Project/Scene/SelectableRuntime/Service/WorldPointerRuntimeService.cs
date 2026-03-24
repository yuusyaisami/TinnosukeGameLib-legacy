#nullable enable
using System;
using System.Collections.Generic;
using Game.Input;
using UnityEngine;
using VContainer;

namespace Game.SelectRuntime
{
    public sealed class WorldPointerRuntimeService :
        IWorldPointerRuntimeService,
        IInputConsumer,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        sealed class ButtonSession
        {
            public bool HasPress;
            public WorldPointerTargetMB? PressedTarget;
            public float PressedTime;
            public bool LongPressStarted;
            public WorldPointerEventData PressedData;

            public void Reset()
            {
                HasPress = false;
                PressedTarget = null;
                PressedTime = 0f;
                LongPressStarted = false;
                PressedData = default;
            }
        }

        readonly IInputRouter _inputRouter;
        readonly IWorldPointerRuntimeOptions _options;
        readonly ISelectRuntimeManagerStateProvider _stateProvider;

        readonly HashSet<WorldPointerTargetMB> _targets = new();
        readonly Dictionary<Collider2D, WorldPointerTargetMB> _targetByCollider = new();

        readonly ButtonSession _leftSession = new();
        readonly ButtonSession _rightSession = new();

        WorldPointerTargetMB? _hoveredTarget;

        public InputConsumerPriority Priority => InputConsumerPriority.Gameplay;

        public event Action<WorldPointerHoverChangedEventData>? OnHoveredChanged;
        public event Action<WorldPointerEventData>? OnLeftClicked;
        public event Action<WorldPointerEventData>? OnRightClicked;
        public event Action<WorldPointerEventData>? OnLeftLongPressStarted;
        public event Action<WorldPointerEventData>? OnLeftLongPressEnded;
        public event Action<WorldPointerEventData>? OnRightLongPressStarted;
        public event Action<WorldPointerEventData>? OnRightLongPressEnded;
        public event Action<InputFrame>? OnFrameUpdated;

        public WorldPointerRuntimeService(
            IInputRouter inputRouter,
            IWorldPointerRuntimeOptions options,
            ISelectRuntimeManagerStateProvider stateProvider)
        {
            _inputRouter = inputRouter;
            _options = options;
            _stateProvider = stateProvider;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _inputRouter.RegisterConsumer(this);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _inputRouter.UnregisterConsumer(this);
            ResetState();
            _targets.Clear();
            _targetByCollider.Clear();
        }

        public void RegisterTarget(WorldPointerTargetMB target)
        {
            if (target == null || !_targets.Add(target))
                return;

            var colliders = target.ResolveColliders();
            for (int i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];
                if (collider != null)
                    _targetByCollider[collider] = target;
            }
        }

        public void UnregisterTarget(WorldPointerTargetMB target)
        {
            if (target == null || !_targets.Remove(target))
                return;

            var colliders = target.ResolveColliders();
            for (int i = 0; i < colliders.Count; i++)
            {
                var collider = colliders[i];
                if (collider != null && _targetByCollider.TryGetValue(collider, out var mapped) && ReferenceEquals(mapped, target))
                    _targetByCollider.Remove(collider);
            }

            if (ReferenceEquals(_hoveredTarget, target))
                SetHovered(default);

            if (ReferenceEquals(_leftSession.PressedTarget, target))
                _leftSession.Reset();

            if (ReferenceEquals(_rightSession.PressedTarget, target))
                _rightSession.Reset();
        }

        public void UpdateInput(ref InputFrame frame)
        {
            var enabled = _stateProvider.EvaluateIsEnabled();
            if (!enabled)
            {
                ResetState();
                OnFrameUpdated?.Invoke(frame);
                return;
            }

            var hitData = Raycast(frame.PointerScreen);
            SetHovered(hitData);
            ProcessButton(frame.PointerLeft, hitData, _leftSession, _options.LongPressSeconds, OnLeftClicked, OnLeftLongPressStarted, OnLeftLongPressEnded);
            ProcessButton(frame.PointerRight, hitData, _rightSession, _options.LongPressSeconds, OnRightClicked, OnRightLongPressStarted, OnRightLongPressEnded);
            OnFrameUpdated?.Invoke(frame);
        }

        void ProcessButton(
            ButtonState button,
            WorldPointerEventData hitData,
            ButtonSession session,
            float longPressSeconds,
            Action<WorldPointerEventData>? clickEvent,
            Action<WorldPointerEventData>? longPressStartEvent,
            Action<WorldPointerEventData>? longPressEndEvent)
        {
            if (button.Down)
            {
                session.HasPress = true;
                session.PressedTarget = hitData.Target;
                session.PressedTime = Time.unscaledTime;
                session.LongPressStarted = false;
                session.PressedData = hitData;
            }

            if (button.Held && session.HasPress)
            {
                if (!session.LongPressStarted &&
                    ReferenceEquals(session.PressedTarget, hitData.Target) &&
                    Time.unscaledTime - session.PressedTime >= Mathf.Max(0.05f, longPressSeconds))
                {
                    session.LongPressStarted = true;
                    longPressStartEvent?.Invoke(hitData.Target != null ? hitData : session.PressedData);
                }
                else if (!session.LongPressStarted &&
                         session.PressedTarget != null &&
                         !ReferenceEquals(session.PressedTarget, hitData.Target))
                {
                    session.Reset();
                }
            }

            if (!button.Up || !session.HasPress)
                return;

            if (session.LongPressStarted)
            {
                var endData = session.PressedTarget != null ? session.PressedData : hitData;
                longPressEndEvent?.Invoke(endData);
                session.Reset();
                return;
            }

            if (session.PressedTarget == null)
            {
                if (hitData.Target == null)
                    clickEvent?.Invoke(hitData);
            }
            else if (ReferenceEquals(session.PressedTarget, hitData.Target))
            {
                clickEvent?.Invoke(hitData);
            }

            session.Reset();
        }

        WorldPointerEventData Raycast(Vector2 screenPosition)
        {
            var camera = _options.WorldCamera != null ? _options.WorldCamera : Camera.main;
            if (camera == null)
                return default;

            var ray = camera.ScreenPointToRay(screenPosition);
            var hit = Physics2D.GetRayIntersection(ray, camera.farClipPlane > 0f ? camera.farClipPlane : 1000f, _options.HitMask);
            if (!hit.collider)
                return new WorldPointerEventData(null, screenPosition, Vector3.zero, Vector3.zero, null);

            var target = ResolveTarget(hit.collider);
            if (target == null)
                return new WorldPointerEventData(null, screenPosition, hit.point, hit.normal, hit.collider);

            return new WorldPointerEventData(target, screenPosition, hit.point, hit.normal, hit.collider);
        }

        WorldPointerTargetMB? ResolveTarget(Collider2D? collider)
        {
            if (collider == null)
                return null;

            if (_targetByCollider.TryGetValue(collider, out var mapped) && mapped != null && _targets.Contains(mapped))
                return mapped;

            var fromParent = collider.GetComponentInParent<WorldPointerTargetMB>(true);
            if (fromParent != null && _targets.Contains(fromParent))
                return fromParent;

            return null;
        }

        void SetHovered(WorldPointerEventData hitData)
        {
            if (ReferenceEquals(_hoveredTarget, hitData.Target))
                return;

            var previous = _hoveredTarget;
            _hoveredTarget = hitData.Target;
            OnHoveredChanged?.Invoke(new WorldPointerHoverChangedEventData(previous, _hoveredTarget, hitData));
        }

        void ResetState()
        {
            _leftSession.Reset();
            _rightSession.Reset();
            if (_hoveredTarget == null)
                return;

            var previous = _hoveredTarget;
            _hoveredTarget = null;
            OnHoveredChanged?.Invoke(new WorldPointerHoverChangedEventData(previous, null, default));
        }
    }
}
