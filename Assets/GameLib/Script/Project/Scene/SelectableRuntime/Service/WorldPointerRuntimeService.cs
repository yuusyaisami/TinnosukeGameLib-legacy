#nullable enable
using System;
using System.Collections.Generic;
using Game.Input;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;

namespace Game.SelectRuntime
{
    public sealed class WorldPointerRuntimeService :
        IWorldPointerRuntimeService,
        IInputConsumer,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        const bool EnableShortPressDebugLog = true;

        sealed class ButtonSession
        {
            public bool HasPress;
            public WorldPointerTargetMB? PressedTarget;
            public float PressedTime;
            public float ShortPressSeconds;
            public float LongPressSeconds;
            public bool ShortPressStarted;
            public bool LongPressStarted;
            public WorldPointerEventData PressedData;

            public void Reset()
            {
                HasPress = false;
                PressedTarget = null;
                PressedTime = 0f;
                ShortPressSeconds = 0f;
                LongPressSeconds = 0f;
                ShortPressStarted = false;
                LongPressStarted = false;
                PressedData = default;
            }
        }

        readonly IInputRouter _inputRouter;
        readonly IWorldPointerRuntimeOptions _options;

        readonly HashSet<WorldPointerTargetMB> _targets = new();
        readonly Dictionary<Collider2D, WorldPointerTargetMB> _targetByCollider = new();
        readonly Dictionary<WorldPointerTargetMB, RenderKey> _renderKeyCache = new();
        readonly RaycastHit2D[] _raycastHits = new RaycastHit2D[64];

        readonly ButtonSession _leftSession = new();
        readonly ButtonSession _rightSession = new();

        WorldPointerTargetMB? _hoveredTarget;
        WorldPointerEventData _currentHoverData;

        public InputConsumerPriority Priority => InputConsumerPriority.Gameplay;

        public event Action<WorldPointerHoverChangedEventData>? OnHoveredChanged;
        public event Action<WorldPointerEventData>? OnLeftClicked;
        public event Action<WorldPointerEventData>? OnRightClicked;
        public event Action<WorldPointerEventData>? OnLeftShortPressStarted;
        public event Action<WorldPointerEventData>? OnLeftShortPressEnded;
        public event Action<WorldPointerEventData>? OnRightShortPressStarted;
        public event Action<WorldPointerEventData>? OnRightShortPressEnded;
        public event Action<WorldPointerEventData>? OnLeftLongPressStarted;
        public event Action<WorldPointerEventData>? OnLeftLongPressEnded;
        public event Action<WorldPointerEventData>? OnRightLongPressStarted;
        public event Action<WorldPointerEventData>? OnRightLongPressEnded;
        public event Action<InputFrame>? OnFrameUpdated;

        public WorldPointerRuntimeService(
            IInputRouter inputRouter,
            IWorldPointerRuntimeOptions options)
        {
            _inputRouter = inputRouter;
            _options = options;
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

        public bool TryGetCurrentHover(out WorldPointerEventData eventData)
        {
            if (_hoveredTarget == null)
            {
                eventData = default;
                return false;
            }

            eventData = _currentHoverData;
            return true;
        }

        public void UpdateInput(ref InputFrame frame)
        {
            if (!_options.Enabled)
            {
                ResetState();
                OnFrameUpdated?.Invoke(frame);
                return;
            }

            var hitData = Raycast(frame.PointerScreen);
            SetHovered(hitData);
            ProcessButton(frame.PointerLeft, hitData, _leftSession, _options.ShortPressSeconds, _options.LongPressSeconds,
                OnLeftClicked, OnLeftShortPressStarted, OnLeftShortPressEnded, OnLeftLongPressStarted, OnLeftLongPressEnded);
            ProcessButton(frame.PointerRight, hitData, _rightSession, _options.ShortPressSeconds, _options.LongPressSeconds,
                OnRightClicked, OnRightShortPressStarted, OnRightShortPressEnded, OnRightLongPressStarted, OnRightLongPressEnded);
            OnFrameUpdated?.Invoke(frame);
        }

        void ProcessButton(
            ButtonState button,
            WorldPointerEventData hitData,
            ButtonSession session,
            float shortPressSeconds,
            float longPressSeconds,
            Action<WorldPointerEventData>? clickEvent,
            Action<WorldPointerEventData>? shortPressStartEvent,
            Action<WorldPointerEventData>? shortPressEndEvent,
            Action<WorldPointerEventData>? longPressStartEvent,
            Action<WorldPointerEventData>? longPressEndEvent)
        {
            void InvokeEvent(Action<WorldPointerEventData>? evt, string label, in WorldPointerEventData data)
            {
                if (EnableShortPressDebugLog && (label == "LeftShortPressStart" || label == "LeftShortPressEnd" || label == "LeftClick"))
                {
                    var listeners = evt?.GetInvocationList()?.Length ?? 0;
                    //Debug.Log($"[WorldPointerRuntimeService] Emit {label} target={(data.Target != null ? data.Target.name : "(none)")} listeners={listeners}");
                }

                if (evt == null)
                    return;

                evt.Invoke(data);
            }

            void EmitClickOnRelease(in WorldPointerEventData releaseHitData)
            {
                if (session.PressedTarget == null)
                {
                    if (releaseHitData.Target == null)
                        InvokeEvent(clickEvent, "LeftClick", releaseHitData);
                }
                else if (ReferenceEquals(session.PressedTarget, releaseHitData.Target))
                {
                    InvokeEvent(clickEvent, "LeftClick", releaseHitData);
                }
            }

            if (button.Down)
            {
                session.HasPress = true;
                session.PressedTarget = hitData.Target;
                session.PressedTime = Time.unscaledTime;
                session.ShortPressSeconds = ResolveShortPressSeconds(hitData.Target, shortPressSeconds);
                session.LongPressSeconds = ResolveLongPressSeconds(hitData.Target, longPressSeconds);
                session.ShortPressStarted = false;
                session.LongPressStarted = false;
                session.PressedData = hitData;
            }

            if (button.Held && session.HasPress)
            {
                var elapsed = Time.unscaledTime - session.PressedTime;
                if (!session.ShortPressStarted &&
                    !session.LongPressStarted &&
                    elapsed >= Mathf.Max(0.05f, session.ShortPressSeconds) &&
                    elapsed < Mathf.Max(session.ShortPressSeconds, session.LongPressSeconds) &&
                    ReferenceEquals(session.PressedTarget, hitData.Target))
                {
                    session.ShortPressStarted = true;
                    var data = hitData.Target != null ? hitData : session.PressedData;
                    InvokeEvent(shortPressStartEvent, "LeftShortPressStart", data);
                }

                if (!session.LongPressStarted &&
                    ReferenceEquals(session.PressedTarget, hitData.Target) &&
                    elapsed >= Mathf.Max(0.05f, session.LongPressSeconds))
                {
                    session.LongPressStarted = true;
                    var data = hitData.Target != null ? hitData : session.PressedData;
                    InvokeEvent(longPressStartEvent, "LeftLongPressStart", data);
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
                InvokeEvent(longPressEndEvent, "LeftLongPressEnd", endData);
                EmitClickOnRelease(hitData);
                session.Reset();
                return;
            }

            if (!session.ShortPressStarted)
            {
                var elapsedOnRelease = Time.unscaledTime - session.PressedTime;
                // 短押しは「長押し閾値に達する前にリリースされた押下」として扱う。
                // これにより、素早い tap でも Click + ShortPress が同時に検出される。
                var inShortWindow = elapsedOnRelease >= 0f &&
                                    elapsedOnRelease < Mathf.Max(0.05f, session.LongPressSeconds);
                var sameTargetOnRelease = session.PressedTarget == null
                    ? hitData.Target == null
                    : ReferenceEquals(session.PressedTarget, hitData.Target);

                if (inShortWindow && sameTargetOnRelease)
                {
                    session.ShortPressStarted = true;
                    var shortStartData = session.PressedTarget != null ? session.PressedData : hitData;
                    InvokeEvent(shortPressStartEvent, "LeftShortPressStart", shortStartData);
                }
            }

            if (session.ShortPressStarted)
            {
                var endData = session.PressedTarget != null ? session.PressedData : hitData;
                InvokeEvent(shortPressEndEvent, "LeftShortPressEnd", endData);
                EmitClickOnRelease(hitData);

                session.Reset();
                return;
            }

            EmitClickOnRelease(hitData);

            session.Reset();
        }

        static float ResolveShortPressSeconds(WorldPointerTargetMB? target, float fallbackSeconds)
        {
            return target != null ? target.ShortPressSeconds : Mathf.Max(0.05f, fallbackSeconds);
        }

        static float ResolveLongPressSeconds(WorldPointerTargetMB? target, float fallbackSeconds)
        {
            return target != null ? target.LongPressSeconds : Mathf.Max(0.05f, fallbackSeconds);
        }

        WorldPointerEventData Raycast(Vector2 screenPosition)
        {
            var camera = _options.WorldCamera != null ? _options.WorldCamera : Camera.main;
            if (camera == null)
                return default;

            var ray = camera.ScreenPointToRay(screenPosition);
            var hitCount = Physics2D.GetRayIntersectionNonAlloc(
                ray,
                _raycastHits,
                camera.farClipPlane > 0f ? camera.farClipPlane : 1000f,
                _options.HitMask);

            if (hitCount <= 0)
                return new WorldPointerEventData(null, screenPosition, Vector3.zero, Vector3.zero, null);

            var target = ResolveFrontmostTarget(hitCount);
            if (target.target == null)
                return new WorldPointerEventData(null, screenPosition, target.hit.point, target.hit.normal, target.hit.collider);

            return new WorldPointerEventData(target.target, screenPosition, target.hit.point, target.hit.normal, target.hit.collider);
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

        (WorldPointerTargetMB? target, RaycastHit2D hit) ResolveFrontmostTarget(int hitCount)
        {
            _renderKeyCache.Clear();

            WorldPointerTargetMB? bestTarget = null;
            RaycastHit2D bestHit = default;
            RenderKey bestKey = default;
            bool hasBest = false;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _raycastHits[i];
                if (!hit.collider)
                    continue;

                var target = ResolveTarget(hit.collider);
                if (target == null)
                    continue;

                var candidateKey = GetRenderKey(target);
                if (!hasBest || IsFrontOf(candidateKey, hit, target, bestKey, bestHit, bestTarget))
                {
                    hasBest = true;
                    bestTarget = target;
                    bestHit = hit;
                    bestKey = candidateKey;
                }
            }

            _renderKeyCache.Clear();
            return (bestTarget, bestHit);
        }

        static bool IsFrontOf(
            RenderKey candidateKey,
            RaycastHit2D candidateHit,
            WorldPointerTargetMB candidateTarget,
            RenderKey currentBestKey,
            RaycastHit2D currentBestHit,
            WorldPointerTargetMB? currentBestTarget)
        {
            var compare = candidateKey.CompareTo(currentBestKey);
            if (compare != 0)
                return compare > 0;

            if (!Mathf.Approximately(candidateHit.distance, currentBestHit.distance))
                return candidateHit.distance < currentBestHit.distance;

            return GetStableTieBreaker(candidateTarget) > GetStableTieBreaker(currentBestTarget);
        }

        static int GetStableTieBreaker(WorldPointerTargetMB? target)
        {
            return target != null ? target.GetInstanceID() : 0;
        }

        RenderKey GetRenderKey(WorldPointerTargetMB target)
        {
            if (target == null)
                return default;

            if (_renderKeyCache.TryGetValue(target, out var cached))
                return cached;

            var best = default(RenderKey);
            var hasBest = false;

            var traversal = ListPool<Transform>.Get();
            var renderers = ListPool<Renderer>.Get();
            try
            {
                traversal.Add(target.transform);
                while (traversal.Count > 0)
                {
                    var current = traversal[traversal.Count - 1];
                    traversal.RemoveAt(traversal.Count - 1);

                    if (current != target.transform &&
                        current.TryGetComponent<WorldPointerTargetMB>(out var nestedTarget) &&
                        nestedTarget != null &&
                        !ReferenceEquals(nestedTarget, target))
                    {
                        continue;
                    }

                    current.GetComponents(renderers);
                    for (var i = 0; i < renderers.Count; i++)
                    {
                        var renderer = renderers[i];
                        if (renderer == null || !renderer.enabled)
                            continue;

                        var candidate = FromRenderer(renderer);
                        if (!hasBest || candidate.CompareTo(best) > 0)
                        {
                            best = candidate;
                            hasBest = true;
                        }
                    }

                    renderers.Clear();

                    for (var i = current.childCount - 1; i >= 0; i--)
                        traversal.Add(current.GetChild(i));
                }
            }
            finally
            {
                ListPool<Renderer>.Release(renderers);
                ListPool<Transform>.Release(traversal);
            }

            if (hasBest)
            {
                _renderKeyCache[target] = best;
                return best;
            }

            var fallback = FromTransform(target.transform);
            _renderKeyCache[target] = fallback;
            return fallback;
        }

        static RenderKey FromRenderer(Renderer renderer)
        {
            var sortingLayerValue = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
            var sortingOrder = renderer.sortingOrder;
            var renderQueue = renderer.sharedMaterial != null ? renderer.sharedMaterial.renderQueue : 0;
            var depth = renderer.bounds.center.z;
            var siblingIndex = renderer.transform.GetSiblingIndex();
            return new RenderKey(sortingLayerValue, sortingOrder, renderQueue, depth, siblingIndex);
        }

        static RenderKey FromTransform(Transform transform)
        {
            var siblingIndex = transform != null ? transform.GetSiblingIndex() : 0;
            var depth = transform != null ? transform.position.z : 0f;
            return new RenderKey(0, 0, 0, depth, siblingIndex);
        }

        readonly struct RenderKey : IComparable<RenderKey>
        {
            readonly int _sortingLayerValue;
            readonly int _sortingOrder;
            readonly int _renderQueue;
            readonly float _depth;
            readonly int _siblingIndex;

            public RenderKey(int sortingLayerValue, int sortingOrder, int renderQueue, float depth, int siblingIndex)
            {
                _sortingLayerValue = sortingLayerValue;
                _sortingOrder = sortingOrder;
                _renderQueue = renderQueue;
                _depth = depth;
                _siblingIndex = siblingIndex;
            }

            public int CompareTo(RenderKey other)
            {
                var compare = _sortingLayerValue.CompareTo(other._sortingLayerValue);
                if (compare != 0)
                    return compare;

                compare = _sortingOrder.CompareTo(other._sortingOrder);
                if (compare != 0)
                    return compare;

                compare = _renderQueue.CompareTo(other._renderQueue);
                if (compare != 0)
                    return compare;

                compare = _depth.CompareTo(other._depth);
                if (compare != 0)
                    return compare;

                return _siblingIndex.CompareTo(other._siblingIndex);
            }
        }

        void SetHovered(WorldPointerEventData hitData)
        {
            if (ReferenceEquals(_hoveredTarget, hitData.Target))
            {
                _currentHoverData = hitData;
                return;
            }

            var previous = _hoveredTarget;
            _hoveredTarget = hitData.Target;
            _currentHoverData = hitData;
            OnHoveredChanged?.Invoke(new WorldPointerHoverChangedEventData(previous, _hoveredTarget, hitData));
        }

        void ResetState()
        {
            _leftSession.Reset();
            _rightSession.Reset();
            _currentHoverData = default;
            if (_hoveredTarget == null)
                return;

            var previous = _hoveredTarget;
            _hoveredTarget = null;
            _currentHoverData = default;
            OnHoveredChanged?.Invoke(new WorldPointerHoverChangedEventData(previous, null, default));
        }
    }
}
