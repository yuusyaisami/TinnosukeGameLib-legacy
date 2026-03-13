#nullable enable
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer.Unity;
using Game.TransformSystem;
using Game.Commands.VNext;

namespace Game
{
    public interface IRuntimeTickHub
    {
        void RegisterRange(ITickable[] tickables);
        void UnregisterRange(ITickable[] tickables);
        void RegisterLateRange(ILateTickable[] lateTickables);
        void UnregisterLateRange(ILateTickable[] lateTickables);
        void RegisterFixedRange(IFixedTickable[] tickables);
        void UnregisterFixedRange(IFixedTickable[] tickables);
    }

    [DefaultExecutionOrder(10000)]
    public sealed class RuntimeTickHub : MonoBehaviour, IRuntimeTickHub
    {
        [BoxGroup("Tick Culling")]
        [LabelText("Enable Distance Culling")]
        [SerializeField] bool enableDistanceCulling = false;

        [BoxGroup("Tick Culling")]
        [ShowIf(nameof(enableDistanceCulling))]
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(distanceCullTarget)")]
        ActorSource distanceCullTarget;

        [BoxGroup("Tick Culling")]
        [LabelText("Max Distance")]
        [ShowIf(nameof(enableDistanceCulling))]
        [SerializeField, MinValue(0f)] float distanceCullRange = 0f;

        [BoxGroup("Tick Culling")]
        [LabelText("Use 2D (XY)")]
        [ShowIf(nameof(enableDistanceCulling))]
        [SerializeField] bool distanceCullUse2D = true;

        [BoxGroup("Tick Culling")]
        [LabelText("Check Interval Frames")]
        [ShowIf(nameof(enableDistanceCulling))]
        [SerializeField, MinValue(0)] int distanceCheckIntervalFrames = 5;

        // Split lists so we can control execution phase (Pre / Default / Late)
        readonly List<ITickable> _preTickables = new(64);
        readonly Dictionary<ITickable, int> _preIndices = new(64);

        readonly List<ITickable> _defaultTickables = new(256);
        readonly Dictionary<ITickable, int> _defaultIndices = new(256);

        readonly List<ITickable> _lateTickables = new(64);
        readonly Dictionary<ITickable, int> _lateIndices = new(64);

        readonly List<ITickable> _pendingAdd = new(64);
        readonly List<ITickable> _pendingRemove = new(64);
        readonly List<ILateTickable> _lateOnlyTickables = new(64);
        readonly Dictionary<ILateTickable, int> _lateOnlyIndices = new(64);
        readonly List<ILateTickable> _pendingLateAdd = new(32);
        readonly List<ILateTickable> _pendingLateRemove = new(32);
        bool _iterating;

        readonly List<IFixedTickable> _fixedTickables = new(64);
        readonly Dictionary<IFixedTickable, int> _fixedIndices = new(64);
        readonly List<IFixedTickable> _pendingFixedAdd = new(64);
        readonly List<IFixedTickable> _pendingFixedRemove = new(64);

        IScopeNode? _originScope;
        Transform? _originTransform;
        float _distanceCullRangeSq;
        int _distanceCullLastFrame = -1;
        bool _distanceCullLastAllow = true;
        ActorSourceResolveCache _distanceCullCache;

        void Awake()
        {
            RefreshDistanceCullCache();
        }

        void OnEnable()
        {
            RefreshDistanceCullCache();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            RefreshDistanceCullCache();
        }
#endif

        public void RegisterRange(ITickable[] tickables)
            => RegisterRangeInternal(tickables);

        public void UnregisterRange(ITickable[] tickables)
            => UnregisterRangeInternal(tickables);

        public void RegisterFixedRange(IFixedTickable[] tickables)
            => RegisterFixedRangeInternal(tickables);

        public void UnregisterFixedRange(IFixedTickable[] tickables)
            => UnregisterFixedRangeInternal(tickables);

        public void RegisterLateRange(ILateTickable[] lateTickables)
            => RegisterLateRangeInternal(lateTickables);

        public void UnregisterLateRange(ILateTickable[] lateTickables)
            => UnregisterLateRangeInternal(lateTickables);

        void RegisterRangeInternal(ITickable[] tickables)
        {
            if (_iterating)
            {
                for (int i = 0; i < tickables.Length; i++)
                {
                    var t = tickables[i];
                    if (t != null)
                        _pendingAdd.Add(t);
                }
                return;
            }

            for (int i = 0; i < tickables.Length; i++)
            {
                var t = tickables[i];
                if (t == null)
                    continue;

                // Determine phase
                var phase = GetPhase(t);
                switch (phase)
                {
                    case TickPhase.Pre:
                        if (_preIndices.ContainsKey(t)) continue;
                        _preIndices.Add(t, _preTickables.Count);
                        _preTickables.Add(t);
                        break;
                    case TickPhase.Late:
                        if (_lateIndices.ContainsKey(t)) continue;
                        _lateIndices.Add(t, _lateTickables.Count);
                        _lateTickables.Add(t);
                        break;
                    default:
                        if (_defaultIndices.ContainsKey(t)) continue;
                        _defaultIndices.Add(t, _defaultTickables.Count);
                        _defaultTickables.Add(t);
                        break;
                }
            }
        }

        void UnregisterRangeInternal(ITickable[] tickables)
        {
            if (_iterating)
            {
                for (int i = 0; i < tickables.Length; i++)
                {
                    var t = tickables[i];
                    if (t != null)
                        _pendingRemove.Add(t);
                }
                return;
            }

            for (int i = 0; i < tickables.Length; i++)
            {
                var t = tickables[i];
                if (t == null)
                    continue;
                RemoveInternal(t);
            }
        }

        void RegisterFixedRangeInternal(IFixedTickable[] tickables)
        {
            if (_iterating)
            {
                for (int i = 0; i < tickables.Length; i++)
                {
                    var t = tickables[i];
                    if (t != null)
                        _pendingFixedAdd.Add(t);
                }
                return;
            }

            for (int i = 0; i < tickables.Length; i++)
            {
                var t = tickables[i];
                if (t == null)
                    continue;
                if (_fixedIndices.ContainsKey(t))
                    continue;

                _fixedIndices.Add(t, _fixedTickables.Count);
                _fixedTickables.Add(t);
            }
        }

        void RegisterLateRangeInternal(ILateTickable[] lateTickables)
        {
            if (_iterating)
            {
                for (int i = 0; i < lateTickables.Length; i++)
                {
                    var t = lateTickables[i];
                    if (t != null)
                        _pendingLateAdd.Add(t);
                }
                return;
            }

            for (int i = 0; i < lateTickables.Length; i++)
            {
                var t = lateTickables[i];
                if (t == null)
                    continue;
                if (_lateOnlyIndices.ContainsKey(t))
                    continue;

                _lateOnlyIndices.Add(t, _lateOnlyTickables.Count);
                _lateOnlyTickables.Add(t);
            }
        }

        void UnregisterLateRangeInternal(ILateTickable[] lateTickables)
        {
            if (_iterating)
            {
                for (int i = 0; i < lateTickables.Length; i++)
                {
                    var t = lateTickables[i];
                    if (t != null)
                        _pendingLateRemove.Add(t);
                }
                return;
            }

            for (int i = 0; i < lateTickables.Length; i++)
            {
                var t = lateTickables[i];
                if (t == null)
                    continue;
                RemoveLateInternal(t);
            }
        }

        void UnregisterFixedRangeInternal(IFixedTickable[] tickables)
        {
            if (_iterating)
            {
                for (int i = 0; i < tickables.Length; i++)
                {
                    var t = tickables[i];
                    if (t != null)
                        _pendingFixedRemove.Add(t);
                }
                return;
            }

            for (int i = 0; i < tickables.Length; i++)
            {
                var t = tickables[i];
                if (t == null)
                    continue;
                RemoveFixedInternal(t);
            }
        }

        void RemoveInternal(ITickable t)
        {
            // remove from whichever list it belongs to
            if (_preIndices.TryGetValue(t, out var pIndex))
            {
                var lastIndex = _preTickables.Count - 1;
                var last = _preTickables[lastIndex];
                _preTickables[pIndex] = last;
                _preTickables.RemoveAt(lastIndex);
                _preIndices.Remove(t);
                if (!ReferenceEquals(last, t)) _preIndices[last] = pIndex;
                return;
            }

            if (_defaultIndices.TryGetValue(t, out var dIndex))
            {
                var lastIndex = _defaultTickables.Count - 1;
                var last = _defaultTickables[lastIndex];
                _defaultTickables[dIndex] = last;
                _defaultTickables.RemoveAt(lastIndex);
                _defaultIndices.Remove(t);
                if (!ReferenceEquals(last, t)) _defaultIndices[last] = dIndex;
                return;
            }

            if (_lateIndices.TryGetValue(t, out var lIndex))
            {
                var lastIndex = _lateTickables.Count - 1;
                var last = _lateTickables[lastIndex];
                _lateTickables[lIndex] = last;
                _lateTickables.RemoveAt(lastIndex);
                _lateIndices.Remove(t);
                if (!ReferenceEquals(last, t)) _lateIndices[last] = lIndex;
                return;
            }
        }

        void RemoveFixedInternal(IFixedTickable t)
        {
            if (_fixedIndices.TryGetValue(t, out var index))
            {
                var lastIndex = _fixedTickables.Count - 1;
                var last = _fixedTickables[lastIndex];
                _fixedTickables[index] = last;
                _fixedTickables.RemoveAt(lastIndex);
                _fixedIndices.Remove(t);
                if (!ReferenceEquals(last, t)) _fixedIndices[last] = index;
            }
        }

        void RemoveLateInternal(ILateTickable t)
        {
            if (_lateOnlyIndices.TryGetValue(t, out var index))
            {
                var lastIndex = _lateOnlyTickables.Count - 1;
                var last = _lateOnlyTickables[lastIndex];
                _lateOnlyTickables[index] = last;
                _lateOnlyTickables.RemoveAt(lastIndex);
                _lateOnlyIndices.Remove(t);
                if (!ReferenceEquals(last, t))
                    _lateOnlyIndices[last] = index;
            }
        }

        void Update()
        {
            _iterating = true;

            var allowTick = ShouldProcessTick();

            // Pre
            if (allowTick)
            {
                for (int i = 0; i < _preTickables.Count; i++)
                {
                    _preTickables[i]?.Tick();
                }
            }

            // Default
            if (allowTick)
            {
                for (int i = 0; i < _defaultTickables.Count; i++)
                {
                    _defaultTickables[i]?.Tick();
                }
            }

            _iterating = false;
        }

        void LateUpdate()
        {
            _iterating = true;

            var allowTick = ShouldProcessTick();

            // Late (Unity LateUpdate phase)
            if (allowTick)
            {
                for (int i = 0; i < _lateTickables.Count; i++)
                {
                    _lateTickables[i]?.Tick();
                }

                for (int i = 0; i < _lateOnlyTickables.Count; i++)
                {
                    _lateOnlyTickables[i]?.LateTick();
                }
            }

            _iterating = false;

            // Apply pending add/remove after all phases this frame.
            if (_pendingAdd.Count > 0)
            {
                for (int i = 0; i < _pendingAdd.Count; i++)
                {
                    var t = _pendingAdd[i];
                    if (t == null)
                        continue;
                    RegisterRangeInternal(new[] { t });
                }
                _pendingAdd.Clear();
            }

            if (_pendingRemove.Count > 0)
            {
                for (int i = 0; i < _pendingRemove.Count; i++)
                {
                    var t = _pendingRemove[i];
                    if (t == null)
                        continue;
                    RemoveInternal(t);
                }
                _pendingRemove.Clear();
            }

            if (_pendingLateAdd.Count > 0)
            {
                for (int i = 0; i < _pendingLateAdd.Count; i++)
                {
                    var t = _pendingLateAdd[i];
                    if (t == null)
                        continue;
                    RegisterLateRangeInternal(new[] { t });
                }
                _pendingLateAdd.Clear();
            }

            if (_pendingLateRemove.Count > 0)
            {
                for (int i = 0; i < _pendingLateRemove.Count; i++)
                {
                    var t = _pendingLateRemove[i];
                    if (t == null)
                        continue;
                    RemoveLateInternal(t);
                }
                _pendingLateRemove.Clear();
            }
        }

        void FixedUpdate()
        {
            _iterating = true;

            var allowTick = ShouldProcessTick();

            if (allowTick)
            {
                for (int i = 0; i < _fixedTickables.Count; i++)
                {
                    _fixedTickables[i]?.FixedTick();
                }
            }

            _iterating = false;

            if (_pendingFixedAdd.Count > 0)
            {
                for (int i = 0; i < _pendingFixedAdd.Count; i++)
                {
                    var t = _pendingFixedAdd[i];
                    if (t == null)
                        continue;
                    RegisterFixedRangeInternal(new[] { t });
                }
                _pendingFixedAdd.Clear();
            }

            if (_pendingFixedRemove.Count > 0)
            {
                for (int i = 0; i < _pendingFixedRemove.Count; i++)
                {
                    var t = _pendingFixedRemove[i];
                    if (t == null)
                        continue;
                    RemoveFixedInternal(t);
                }
                _pendingFixedRemove.Clear();
            }
        }

        static TickPhase GetPhase(ITickable t)
        {
            if (t is ITickPhase p) return p.Phase;
            return TickPhase.Default;
        }

        bool ShouldProcessTick()
        {
            if (!enableDistanceCulling)
                return true;

            if (distanceCullRange <= 0f)
                return true;

            var interval = distanceCheckIntervalFrames;
            if (interval > 0)
            {
                var frame = Time.frameCount;
                if (_distanceCullLastFrame >= 0 && frame - _distanceCullLastFrame < interval)
                    return _distanceCullLastAllow;
                _distanceCullLastFrame = frame;
            }

            EnsureOriginScope();
            if (_originScope == null)
            {
                _distanceCullLastAllow = true;
                return true;
            }

            var targetScope = ActorSourceFastResolver.ResolveCached(_originScope, distanceCullTarget, ref _distanceCullCache);
            if (!TryGetScopeTransform(targetScope, out var targetTransform) || targetTransform == null)
            {
                _distanceCullLastAllow = true;
                return true;
            }

            var originTransform = _originTransform != null ? _originTransform : transform;
            var a = originTransform.position;
            var b = targetTransform.position;

            float distSq;
            if (distanceCullUse2D)
            {
                var dx = a.x - b.x;
                var dy = a.y - b.y;
                distSq = dx * dx + dy * dy;
            }
            else
            {
                var delta = a - b;
                distSq = delta.sqrMagnitude;
            }

            _distanceCullLastAllow = distSq <= _distanceCullRangeSq;
            return _distanceCullLastAllow;
        }

        void RefreshDistanceCullCache()
        {
            _distanceCullRangeSq = Mathf.Max(0f, distanceCullRange) * Mathf.Max(0f, distanceCullRange);
            _distanceCullLastFrame = -1;
            _distanceCullLastAllow = true;
            EnsureOriginScope();
        }

        void EnsureOriginScope()
        {
            if (_originScope != null && _originTransform != null)
                return;

            var runtimeScope = GetComponent<RuntimeLifetimeScope>();
            if (runtimeScope != null)
            {
                _originScope = runtimeScope;
                _originTransform = runtimeScope.Identity?.SelfTransform;
                return;
            }

            var baseScope = GetComponent<BaseLifetimeScope>();
            if (baseScope != null)
            {
                _originScope = baseScope;
                _originTransform = baseScope.Identity?.SelfTransform;
                return;
            }

            _originScope = null;
            _originTransform = transform;
        }

        static bool TryGetScopeTransform(IScopeNode? scope, out Transform? transform)
        {
            transform = null;
            if (scope == null)
                return false;

            var fromIdentity = scope.Identity?.SelfTransform;
            if (fromIdentity != null)
            {
                transform = fromIdentity;
                return true;
            }

            if (scope is Component component && component != null)
            {
                transform = component.transform;
                return true;
            }

            return false;
        }
    }
}
