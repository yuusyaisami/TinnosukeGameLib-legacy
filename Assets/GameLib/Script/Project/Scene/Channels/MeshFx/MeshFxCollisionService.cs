#nullable enable
using System.Collections.Generic;
using Game.Collision;
using Unity.Mathematics;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    interface IMeshFxCollisionService
    {
        void OnAcquire();
        void OnRelease();
        void Clear();
        void Sync(IReadOnlyList<Vector3> centerline, IReadOnlyList<float> widths);
    }

    sealed class MeshFxCollisionService : IMeshFxCollisionService
    {
        const float Epsilon = 0.00001f;

        readonly MeshFxChannelDef _def;
        readonly IScopeNode _ownerScope;

        readonly ICollisionService? _collision;
        readonly IHitColliderScopeRegistry? _scopeRegistry;
        readonly IHitColliderChannelHub? _hitChannelHub;

        readonly List<DynamicColliderHandle> _handles = new(64);
        readonly List<Vector2> _samplePositions = new(128);
        readonly List<float> _sampleRadii = new(128);
        readonly List<Vector2> _lastPositions = new(128);
        readonly List<float> _lastRadii = new(128);

        bool _warnedMissingBackend;
        int _lastLayer = int.MinValue;
        uint _lastHitMask;
        DynamicColliderSetId _lastSetId = (DynamicColliderSetId)255;

        public MeshFxCollisionService(
            MeshFxChannelDef def,
            IScopeNode ownerScope,
            ICollisionService? collisionService,
            IHitColliderScopeRegistry? hitScopeRegistry,
            IHitColliderChannelHub? hitChannelHub)
        {
            _def = def;
            _ownerScope = ownerScope;

            var resolver = ownerScope?.Resolver;
            if (collisionService == null && resolver != null)
                resolver.TryResolve(out collisionService);
            if (hitScopeRegistry == null && resolver != null)
                resolver.TryResolve(out hitScopeRegistry);
            if (hitChannelHub == null && resolver != null)
                resolver.TryResolve(out hitChannelHub);

            _collision = collisionService;
            _scopeRegistry = hitScopeRegistry;
            _hitChannelHub = hitChannelHub;
        }

        public void OnAcquire()
        {
            Clear();
            _warnedMissingBackend = false;
            _lastLayer = int.MinValue;
            _lastHitMask = 0;
            _lastSetId = (DynamicColliderSetId)255;
        }

        public void OnRelease()
        {
            Clear();
        }

        public void Clear()
        {
            for (int i = _handles.Count - 1; i >= 0; i--)
            {
                UnregisterHandle(_handles[i]);
            }

            _handles.Clear();
            _samplePositions.Clear();
            _sampleRadii.Clear();
            _lastPositions.Clear();
            _lastRadii.Clear();
        }

        public void Sync(IReadOnlyList<Vector3> centerline, IReadOnlyList<float> widths)
        {
            if (!_def.CollisionEnabled)
            {
                Clear();
                return;
            }

            if (_collision == null || _scopeRegistry == null)
            {
                if (!_warnedMissingBackend)
                {
                    Debug.LogWarning("[MeshFxCollision] Collision backend is missing. MeshFx collision is disabled for this channel.");
                    _warnedMissingBackend = true;
                }
                return;
            }

            BuildCollisionSamples(centerline, widths, _def.CollisionApproximation, _samplePositions, _sampleRadii);
            if (_samplePositions.Count == 0)
            {
                Clear();
                return;
            }

            EnsureHandleCount(_samplePositions.Count);

            var settings = _def.CollisionApproximation;
            var poseDirty = NeedPoseUpdate(settings);

            var layerDirty = _lastLayer != _def.LayerId;
            var maskDirty = _lastHitMask != _def.HitMask;
            var setDirty = _lastSetId != _def.SetId;

            if (!poseDirty && !layerDirty && !maskDirty && !setDirty)
                return;

            for (int i = 0; i < _handles.Count; i++)
            {
                var handle = _handles[i];
                if (!handle.IsValid || !_collision.IsValid(handle))
                {
                    UnregisterHandle(handle);
                    handle = RegisterHandle(i);
                    _handles[i] = handle;
                }

                if (!handle.IsValid)
                    continue;

                if (poseDirty)
                {
                    var pos = _samplePositions[i];
                    var radius = _sampleRadii[i];
                    _collision.SetPosition(handle, new float2(pos.x, pos.y));
                    _collision.SetRadius(handle, radius);
                }

                if (layerDirty)
                    _collision.SetLayer(handle, _def.LayerId);

                if (maskDirty)
                    _collision.SetHitMask(handle, _def.HitMask);

                if (setDirty)
                    _collision.SetSetId(handle, _def.SetId);
            }

            _lastLayer = _def.LayerId;
            _lastHitMask = _def.HitMask;
            _lastSetId = _def.SetId;

            if (poseDirty)
            {
                SavePoseSnapshot();
            }
        }

        void EnsureHandleCount(int count)
        {
            while (_handles.Count < count)
            {
                var index = _handles.Count;
                _handles.Add(RegisterHandle(index));
            }

            while (_handles.Count > count)
            {
                var lastIndex = _handles.Count - 1;
                UnregisterHandle(_handles[lastIndex]);
                _handles.RemoveAt(lastIndex);
            }
        }

        DynamicColliderHandle RegisterHandle(int sampleIndex)
        {
            if (_collision == null)
                return DynamicColliderHandle.Invalid;

            if (sampleIndex < 0 || sampleIndex >= _samplePositions.Count)
                return DynamicColliderHandle.Invalid;

            var p = _samplePositions[sampleIndex];
            var r = _sampleRadii[sampleIndex];

            var handle = _collision.RegisterDynamic(
                new float2(p.x, p.y),
                r,
                _def.LayerId,
                _def.HitMask,
                _def.SetId);

            if (handle.IsValid && _scopeRegistry != null)
            {
                _scopeRegistry.Register(handle, _ownerScope);
            }

            return handle;
        }

        void UnregisterHandle(DynamicColliderHandle handle)
        {
            if (!handle.IsValid)
                return;

            if (_scopeRegistry != null)
                _scopeRegistry.Unregister(handle, _ownerScope);

            if (_collision != null && _collision.IsValid(handle))
                _collision.UnregisterDynamic(handle);

            // Keep this reference for future extension points and to keep DI contract explicit.
            _ = _hitChannelHub;
        }

        bool NeedPoseUpdate(MeshFxCollisionApproximationSettings settings)
        {
            if (_lastPositions.Count != _samplePositions.Count || _lastRadii.Count != _sampleRadii.Count)
                return true;

            var posThresholdSq = settings.PositionUpdateThreshold * settings.PositionUpdateThreshold;
            var widthThreshold = settings.WidthUpdateThreshold;

            for (int i = 0; i < _samplePositions.Count; i++)
            {
                var delta = _samplePositions[i] - _lastPositions[i];
                if (delta.sqrMagnitude > posThresholdSq)
                    return true;

                if (Mathf.Abs(_sampleRadii[i] - _lastRadii[i]) > widthThreshold)
                    return true;
            }

            var dirThreshold = settings.DirectionUpdateThresholdDeg;
            if (dirThreshold <= 0f)
                return false;

            for (int i = 0; i < _samplePositions.Count - 1; i++)
            {
                var current = _samplePositions[i + 1] - _samplePositions[i];
                var previous = _lastPositions[i + 1] - _lastPositions[i];
                if (current.sqrMagnitude <= Epsilon || previous.sqrMagnitude <= Epsilon)
                    continue;

                var angle = Vector2.Angle(current, previous);
                if (angle > dirThreshold)
                    return true;
            }

            return false;
        }

        void SavePoseSnapshot()
        {
            _lastPositions.Clear();
            _lastPositions.AddRange(_samplePositions);

            _lastRadii.Clear();
            _lastRadii.AddRange(_sampleRadii);
        }

        static void BuildCollisionSamples(
            IReadOnlyList<Vector3> centerline,
            IReadOnlyList<float> widths,
            MeshFxCollisionApproximationSettings settings,
            List<Vector2> outPositions,
            List<float> outRadii)
        {
            outPositions.Clear();
            outRadii.Clear();

            if (centerline == null || widths == null)
                return;
            if (centerline.Count < 2 || widths.Count != centerline.Count)
                return;

            var totalLength = ComputeLength(centerline);
            if (totalLength <= Epsilon)
                return;

            var maxColliderCount = Mathf.Clamp(settings.MaxColliderCount, 4, 256);
            var minDistance = Mathf.Max(0.01f, settings.MinCollisionSampleDistance);
            var minRadius = Mathf.Max(0.005f, settings.MinCollisionRadius);

            var spacing = Mathf.Max(minDistance, totalLength / maxColliderCount);
            BuildSamplesWithSpacing(centerline, widths, spacing, minRadius, outPositions, outRadii);

            if (outPositions.Count > maxColliderCount)
                BuildSamplesWithSpacing(centerline, widths, spacing * 1.25f, minRadius, outPositions, outRadii);
            if (outPositions.Count > maxColliderCount)
                BuildSamplesWithSpacing(centerline, widths, spacing * 1.5f, minRadius, outPositions, outRadii);

            if (outPositions.Count <= maxColliderCount)
                return;

            while (outPositions.Count > maxColliderCount)
            {
                for (int i = outPositions.Count - 2; i > 0 && outPositions.Count > maxColliderCount; i -= 2)
                {
                    outPositions.RemoveAt(i);
                    outRadii.RemoveAt(i);
                }
            }
        }

        static void BuildSamplesWithSpacing(
            IReadOnlyList<Vector3> centerline,
            IReadOnlyList<float> widths,
            float spacing,
            float minRadius,
            List<Vector2> outPositions,
            List<float> outRadii)
        {
            outPositions.Clear();
            outRadii.Clear();

            var totalLength = ComputeLength(centerline);
            if (totalLength <= Epsilon)
                return;

            spacing = Mathf.Max(0.001f, spacing);

            var nextSample = 0f;
            var segIndex = 0;
            var segStartDistance = 0f;
            var segLength = Vector3.Distance(centerline[0], centerline[1]);

            while (nextSample <= totalLength + Epsilon)
            {
                while (segIndex < centerline.Count - 2 && nextSample > segStartDistance + segLength)
                {
                    segStartDistance += segLength;
                    segIndex++;
                    segLength = Vector3.Distance(centerline[segIndex], centerline[segIndex + 1]);
                }

                var a = centerline[segIndex];
                var b = centerline[segIndex + 1];
                var wa = widths[segIndex];
                var wb = widths[segIndex + 1];

                var t = segLength <= Epsilon ? 0f : (nextSample - segStartDistance) / segLength;
                t = Mathf.Clamp01(t);

                var p = Vector3.LerpUnclamped(a, b, t);
                var width = Mathf.LerpUnclamped(wa, wb, t);
                var radius = Mathf.Max(minRadius, width * 0.5f);

                outPositions.Add(new Vector2(p.x, p.y));
                outRadii.Add(radius);

                nextSample += spacing;
            }

            var last = centerline[centerline.Count - 1];
            if (outPositions.Count == 0 || Vector2.Distance(outPositions[outPositions.Count - 1], new Vector2(last.x, last.y)) > 0.0001f)
            {
                outPositions.Add(new Vector2(last.x, last.y));
                outRadii.Add(Mathf.Max(minRadius, widths[widths.Count - 1] * 0.5f));
            }
        }

        static float ComputeLength(IReadOnlyList<Vector3> points)
        {
            var len = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                len += Vector3.Distance(points[i - 1], points[i]);
            }
            return len;
        }
    }
}
