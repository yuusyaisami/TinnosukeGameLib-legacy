#nullable enable
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;
using Game.TransformSystem;

namespace Game.UI
{
    public sealed class VisualBoundsConfig
    {
        public Transform Root = null!;
        public RectTransform? RootRect;
        public IReadOnlyList<RectTransform> RectTargets = System.Array.Empty<RectTransform>();
        public IReadOnlyList<Image> ImageTargets = System.Array.Empty<Image>();
        public IReadOnlyList<TMP_Text> TextTargets = System.Array.Empty<TMP_Text>();
        public IReadOnlyList<SpriteRenderer> SpriteTargets = System.Array.Empty<SpriteRenderer>();
        public IReadOnlyList<MeshRenderer> MeshTargets = System.Array.Empty<MeshRenderer>();
        public IReadOnlyList<Collider2D> Collider2DTargets = System.Array.Empty<Collider2D>();
        public IReadOnlyList<Collider> ColliderTargets = System.Array.Empty<Collider>();
        public bool ExcludeInactive = true;
        public bool AutoRebuild = true;
        public bool AutoDetectChanges = true;
        public float AutoRebuildIntervalSeconds = 0f;
        public bool RunInLateUpdate = true;
    }

    public sealed class VisualBoundsService :
        IVisualBoundsService,
        IVisualBoundsOutput,
        ITickable,
        ILateTickable,
        ITickPhase,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        const float MaxAbsCoordinate = 1_000_000f;
        const float MaxReasonableSize = 100_000f;
        const float MinNonDegenerateBoundsSizeSqr = 0.000001f;

        readonly VisualBoundsConfig _config;
        readonly Vector3[] _rectCorners = new Vector3[4];
        readonly Vector3[] _boundsCorners = new Vector3[8];
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly Dictionary<int, int> _warnedFrameByKey = new Dictionary<int, int>();

        bool _acquired;
        bool _dirty = true;
        float _nextAutoRebuildTime;
        int _lastRebuildFrame = -1;

        bool _hasBounds;
        Rect _localRect;
        Bounds _worldBounds;
        ScreenClampResult _lastClamp;

        public VisualBoundsService(VisualBoundsConfig config)
        {
            _config = config;
        }

        public TickPhase Phase => _config != null && _config.RunInLateUpdate ? TickPhase.Late : TickPhase.Default;

        public Transform? LocalSpaceRoot => _config?.Root;
        public bool HasBounds => _hasBounds;
        public Rect LocalRect => _localRect;
        public Vector2 LocalCenter => _localRect.center;
        public Vector2 LocalSize => _localRect.size;
        public Bounds WorldBounds => _worldBounds;
        public Vector3 WorldCenter => _worldBounds.center;
        public Vector3 WorldSize => _worldBounds.size;
        public ScreenClampResult LastClamp => _lastClamp;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquired = true;
            MarkDirty();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _acquired = false;
        }

        public void Tick()
        {
            if (!_acquired)
                return;

            TickInternal();
        }

        public void LateTick()
        {
            if (!_acquired)
                return;

            TickInternal();
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        public void RebuildNow()
        {
            Rebuild(force: true);
        }

        public bool TryGetLastOutput(out VisualBoundsOutput output)
        {
            output = new VisualBoundsOutput(_hasBounds, _localRect, _worldBounds, _lastClamp);
            return _hasBounds;
        }

        public void SetClampResult(in ScreenClampResult clamp)
        {
            _lastClamp = clamp;
        }

        void TickInternal()
        {
            if (_config == null || _config.Root == null)
                return;

            if (_config.AutoDetectChanges)
                DetectChanges();

            if (!_dirty && !_config.AutoRebuild)
                return;

            if (_config.AutoRebuildIntervalSeconds > 0f)
            {
                var now = Time.unscaledTime;
                if (now < _nextAutoRebuildTime)
                    return;
                _nextAutoRebuildTime = now + _config.AutoRebuildIntervalSeconds;
            }

            Rebuild(force: false);
        }

        void DetectChanges()
        {
            bool changed = false;

            var rects = _config.RectTargets;
            for (int i = 0; i < rects.Count; i++)
            {
                var rt = rects[i];
                if (rt == null)
                    continue;
                if (rt.hasChanged)
                {
                    rt.hasChanged = false;
                    changed = true;
                }
            }

            var images = _config.ImageTargets;
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                if (img == null)
                    continue;
                var rt = img.rectTransform;
                if (rt != null && rt.hasChanged)
                {
                    rt.hasChanged = false;
                    changed = true;
                }
            }

            var texts = _config.TextTargets;
            for (int i = 0; i < texts.Count; i++)
            {
                var t = texts[i];
                if (t == null)
                    continue;
                if (t.havePropertiesChanged)
                {
                    t.havePropertiesChanged = false;
                    changed = true;
                }
                var rt = t.rectTransform;
                if (rt != null && rt.hasChanged)
                {
                    rt.hasChanged = false;
                    changed = true;
                }
            }

            var sprites = _config.SpriteTargets;
            for (int i = 0; i < sprites.Count; i++)
            {
                var sr = sprites[i];
                if (sr == null)
                    continue;
                var tr = sr.transform;
                if (tr != null && tr.hasChanged)
                {
                    tr.hasChanged = false;
                    changed = true;
                }
            }

            var meshes = _config.MeshTargets;
            for (int i = 0; i < meshes.Count; i++)
            {
                var mr = meshes[i];
                if (mr == null)
                    continue;
                var tr = mr.transform;
                if (tr != null && tr.hasChanged)
                {
                    tr.hasChanged = false;
                    changed = true;
                }
            }

            var colliders2D = _config.Collider2DTargets;
            for (int i = 0; i < colliders2D.Count; i++)
            {
                var c2d = colliders2D[i];
                if (c2d == null)
                    continue;
                var tr = c2d.transform;
                if (tr != null && tr.hasChanged)
                {
                    tr.hasChanged = false;
                    changed = true;
                }
            }

            var colliders3D = _config.ColliderTargets;
            for (int i = 0; i < colliders3D.Count; i++)
            {
                var c3d = colliders3D[i];
                if (c3d == null)
                    continue;
                var tr = c3d.transform;
                if (tr != null && tr.hasChanged)
                {
                    tr.hasChanged = false;
                    changed = true;
                }
            }

            if (changed)
                MarkDirty();
        }

        void Rebuild(bool force)
        {
            if (_config == null || _config.Root == null)
                return;

            var frame = Time.frameCount;
            if (force && !_dirty && _lastRebuildFrame == frame)
                return;

            if (!force && !_dirty)
                return;

            _dirty = false;
            _lastRebuildFrame = frame;
            _hasBounds = false;
            _worldBounds = default;
            _localRect = Rect.zero;

            if (!TryCollectWorldBounds(out var worldBounds))
                return;

            if (!IsFinite(worldBounds) || !IsReasonable(worldBounds))
            {
                Debug.LogWarning($"[VisualBounds] Rebuild rejected invalid world bounds. Center={worldBounds.center} Size={worldBounds.size}");
                return;
            }

            if (!TryResolveLocalRect(worldBounds, _config.Root, out var localRect))
                return;

            _worldBounds = worldBounds;
            _hasBounds = true;
            _localRect = localRect;
        }

        bool TryCollectWorldBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasAny = false;
            _seen.Clear();

            var rects = _config.RectTargets;
            for (int i = 0; i < rects.Count; i++)
            {
                var rt = rects[i];
                if (!ShouldInclude(rt))
                    continue;
                if (!_seen.Add(rt.GetInstanceID()))
                    continue;

                if (TryGetRectTransformBounds(rt, out var b))
                {
                    if (!hasAny)
                    {
                        bounds = b;
                        hasAny = true;
                    }
                    else
                    {
                        bounds.Encapsulate(b);
                    }
                }
                else
                {
                    Debug.LogWarning($"[VisualBounds] RectTransform bounds rejected. Name={rt.name}");
                }
            }

            var images = _config.ImageTargets;
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                if (img == null)
                    continue;
                var rt = img.rectTransform;
                if (!ShouldInclude(rt))
                    continue;
                if (!_seen.Add(rt.GetInstanceID()))
                    continue;

                if (TryGetRectTransformBounds(rt, out var b))
                {
                    if (!hasAny)
                    {
                        bounds = b;
                        hasAny = true;
                    }
                    else
                    {
                        bounds.Encapsulate(b);
                    }
                }
                else
                {
                    Debug.LogWarning($"[VisualBounds] Image RectTransform bounds rejected. Name={img.name}");
                }
            }

            var texts = _config.TextTargets;
            for (int i = 0; i < texts.Count; i++)
            {
                var t = texts[i];
                if (t == null)
                    continue;
                if (_config.ExcludeInactive && !t.gameObject.activeInHierarchy)
                    continue;
                if (!_seen.Add(t.GetInstanceID()))
                    continue;

                if (TryGetTextBounds(t, out var b))
                {
                    if (!hasAny)
                    {
                        bounds = b;
                        hasAny = true;
                    }
                    else
                    {
                        bounds.Encapsulate(b);
                    }
                }
                else
                {
                    Debug.LogWarning($"[VisualBounds] TMP_Text bounds rejected. Name={t.name}");
                }
            }

            var sprites = _config.SpriteTargets;
            for (int i = 0; i < sprites.Count; i++)
            {
                var sr = sprites[i];
                if (sr == null)
                    continue;
                if (_config.ExcludeInactive && (!sr.gameObject.activeInHierarchy || !sr.enabled))
                    continue;
                if (!_seen.Add(sr.GetInstanceID()))
                    continue;

                var b = sr.bounds;
                if (b.size == Vector3.zero || !IsFinite(b) || !IsReasonable(b))
                {
                    Debug.LogWarning($"[VisualBounds] SpriteRenderer bounds rejected. Name={sr.name} Center={b.center} Size={b.size}");
                    continue;
                }

                if (!hasAny)
                {
                    bounds = b;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            var meshes = _config.MeshTargets;
            for (int i = 0; i < meshes.Count; i++)
            {
                var mr = meshes[i];
                if (mr == null)
                    continue;
                if (_config.ExcludeInactive && (!mr.gameObject.activeInHierarchy || !mr.enabled))
                    continue;
                if (!_seen.Add(mr.GetInstanceID()))
                    continue;

                var b = mr.bounds;
                if (IsDegenerateBounds(b))
                {
                    // Mesh の初期化前は zero-size bounds が返ることがあり、再構築時に頻発するため無音スキップ。
                    continue;
                }

                if (!IsFinite(b) || !IsReasonable(b))
                {
                    WarnThrottled(mr.GetInstanceID(), 20, $"[VisualBounds] MeshRenderer bounds rejected. Name={mr.name} Center={b.center} Size={b.size}");
                    continue;
                }

                if (!hasAny)
                {
                    bounds = b;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            var colliders2D = _config.Collider2DTargets;
            for (int i = 0; i < colliders2D.Count; i++)
            {
                var c2d = colliders2D[i];
                if (c2d == null)
                    continue;
                if (_config.ExcludeInactive && (!c2d.gameObject.activeInHierarchy || !c2d.enabled))
                    continue;
                if (!_seen.Add(c2d.GetInstanceID()))
                    continue;

                var b = c2d.bounds;
                if (b.size == Vector3.zero || !IsFinite(b) || !IsReasonable(b))
                {
                    Debug.LogWarning($"[VisualBounds] Collider2D bounds rejected. Name={c2d.name} Center={b.center} Size={b.size}");
                    continue;
                }

                if (!hasAny)
                {
                    bounds = b;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            var colliders3D = _config.ColliderTargets;
            for (int i = 0; i < colliders3D.Count; i++)
            {
                var c3d = colliders3D[i];
                if (c3d == null)
                    continue;
                if (_config.ExcludeInactive && (!c3d.gameObject.activeInHierarchy || !c3d.enabled))
                    continue;
                if (!_seen.Add(c3d.GetInstanceID()))
                    continue;

                var b = c3d.bounds;
                if (b.size == Vector3.zero || !IsFinite(b) || !IsReasonable(b))
                {
                    Debug.LogWarning($"[VisualBounds] Collider bounds rejected. Name={c3d.name} Center={b.center} Size={b.size}");
                    continue;
                }

                if (!hasAny)
                {
                    bounds = b;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return hasAny;
        }

        static bool IsDegenerateBounds(in Bounds bounds)
        {
            return bounds.size.sqrMagnitude <= MinNonDegenerateBoundsSizeSqr;
        }

        bool ShouldInclude(RectTransform? rt)
        {
            if (rt == null)
                return false;
            if (_config.ExcludeInactive && !rt.gameObject.activeInHierarchy)
                return false;
            return true;
        }

        bool TryGetRectTransformBounds(RectTransform rt, out Bounds bounds)
        {
            bounds = default;
            if (rt == null)
                return false;

            rt.GetWorldCorners(_rectCorners);
            for (int i = 0; i < _rectCorners.Length; i++)
            {
                if (!IsFinite(_rectCorners[i]) || !IsReasonable(_rectCorners[i]))
                {
                    Debug.LogWarning($"[VisualBounds] RectTransform corner rejected. Name={rt.name} Corner={i} Value={_rectCorners[i]}");
                    return false;
                }
            }

            bounds = new Bounds(_rectCorners[0], Vector3.zero);
            bounds.Encapsulate(_rectCorners[1]);
            bounds.Encapsulate(_rectCorners[2]);
            bounds.Encapsulate(_rectCorners[3]);
            if (!IsFinite(bounds) || !IsReasonable(bounds))
            {
                Debug.LogWarning($"[VisualBounds] RectTransform bounds rejected after encapsulate. Name={rt.name} Center={bounds.center} Size={bounds.size}");
                return false;
            }

            return true;
        }

        bool TryGetTextBounds(TMP_Text text, out Bounds bounds)
        {
            bounds = default;
            if (text == null)
                return false;

            var shouldRefreshMesh =
                text.havePropertiesChanged ||
                text.textInfo == null ||
                (text.textInfo.characterCount <= 0 && !string.IsNullOrEmpty(text.text));

            if (shouldRefreshMesh)
            {
                text.ForceMeshUpdate();
                text.havePropertiesChanged = false;
            }

            var localBounds = text.textBounds;
            var hasVisibleCharacters = text.textInfo != null && text.textInfo.characterCount > 0;
            var allowSilentFallback = !hasVisibleCharacters;
            if (localBounds.size == Vector3.zero || !IsFinite(localBounds) || !IsReasonable(localBounds))
            {
                if (!allowSilentFallback)
                {
                    WarnThrottled(text.GetInstanceID(), 1, $"[VisualBounds] TMP_Text local bounds rejected. Name={text.name} Center={localBounds.center} Size={localBounds.size}");
                }
                var rtFallback = text.rectTransform;
                if (rtFallback != null && TryGetRectTransformBounds(rtFallback, out bounds))
                {
                    if (!allowSilentFallback)
                    {
                        WarnThrottled(text.GetInstanceID(), 2, $"[VisualBounds] TMP_Text using RectTransform fallback. Name={text.name}");
                    }
                    return true;
                }

                return false;
            }

            var min = localBounds.min;
            var max = localBounds.max;

            _boundsCorners[0] = new Vector3(min.x, min.y, min.z);
            _boundsCorners[1] = new Vector3(min.x, min.y, max.z);
            _boundsCorners[2] = new Vector3(min.x, max.y, min.z);
            _boundsCorners[3] = new Vector3(min.x, max.y, max.z);
            _boundsCorners[4] = new Vector3(max.x, min.y, min.z);
            _boundsCorners[5] = new Vector3(max.x, min.y, max.z);
            _boundsCorners[6] = new Vector3(max.x, max.y, min.z);
            _boundsCorners[7] = new Vector3(max.x, max.y, max.z);

            var t = text.transform;
            bounds = new Bounds(t.TransformPoint(_boundsCorners[0]), Vector3.zero);
            if (!IsFinite(bounds.center) || !IsReasonable(bounds.center))
                return false;
            for (int i = 1; i < _boundsCorners.Length; i++)
            {
                var worldPoint = t.TransformPoint(_boundsCorners[i]);
                if (!IsFinite(worldPoint) || !IsReasonable(worldPoint))
                {
                    WarnThrottled(text.GetInstanceID(), 3, $"[VisualBounds] TMP_Text world corner rejected. Name={text.name} Corner={i} Value={worldPoint}");
                    return false;
                }
                bounds.Encapsulate(worldPoint);
            }

            if (!IsFinite(bounds) || !IsReasonable(bounds))
            {
                WarnThrottled(text.GetInstanceID(), 4, $"[VisualBounds] TMP_Text bounds rejected after encapsulate. Name={text.name} Center={bounds.center} Size={bounds.size}");
                var rtFallback = text.rectTransform;
                if (rtFallback != null && TryGetRectTransformBounds(rtFallback, out bounds))
                {
                    WarnThrottled(text.GetInstanceID(), 5, $"[VisualBounds] TMP_Text using RectTransform fallback after world bound reject. Name={text.name}");
                    return true;
                }

                return false;
            }

            return true;
        }

        void WarnThrottled(int instanceId, int category, string message)
        {
            var key = unchecked((instanceId * 397) ^ category);
            var frame = Time.frameCount;
            const int CooldownFrames = 60;

            if (_warnedFrameByKey.TryGetValue(key, out var lastFrame) && frame - lastFrame < CooldownFrames)
                return;

            _warnedFrameByKey[key] = frame;
            Debug.LogWarning(message);
        }

        bool TryResolveLocalRect(in Bounds worldBounds, Transform root, out Rect localRect)
        {
            localRect = Rect.zero;
            if (root == null)
                return false;

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            FillBoundsCorners(worldBounds, _boundsCorners);
            for (int i = 0; i < _boundsCorners.Length; i++)
            {
                var local = (Vector2)root.InverseTransformPoint(_boundsCorners[i]);
                if (!IsFinite(local) || !IsReasonable(local))
                {
                    Debug.LogWarning($"[VisualBounds] Local corner rejected. Root={root.name} Corner={i} Value={local}");
                    return false;
                }
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            if (!IsFinite(min) || !IsFinite(max))
                return false;

            localRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (localRect.width <= 0f || localRect.height <= 0f)
                return false;

            if (!IsFinite(localRect) || !IsReasonable(localRect))
            {
                Debug.LogWarning($"[VisualBounds] LocalRect rejected. Root={root.name} Rect={localRect}");
                return false;
            }

            return true;
        }

        static void FillBoundsCorners(in Bounds bounds, Vector3[] corners)
        {
            var min = bounds.min;
            var max = bounds.max;

            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(min.x, min.y, max.z);
            corners[2] = new Vector3(min.x, max.y, min.z);
            corners[3] = new Vector3(min.x, max.y, max.z);
            corners[4] = new Vector3(max.x, min.y, min.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(max.x, max.y, min.z);
            corners[7] = new Vector3(max.x, max.y, max.z);
        }

        static bool IsFinite(Vector2 value)
        {
            return
                !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
                !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        }

        static bool IsFinite(Vector3 value)
        {
            return
                !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
                !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        static bool IsFinite(Bounds value)
        {
            return IsFinite(value.center) && IsFinite(value.size);
        }

        static bool IsFinite(Rect value)
        {
            return
                !float.IsNaN(value.xMin) && !float.IsNaN(value.yMin) &&
                !float.IsNaN(value.xMax) && !float.IsNaN(value.yMax) &&
                !float.IsInfinity(value.xMin) && !float.IsInfinity(value.yMin) &&
                !float.IsInfinity(value.xMax) && !float.IsInfinity(value.yMax);
        }

        static bool IsReasonable(Vector2 value)
        {
            return Mathf.Abs(value.x) <= MaxAbsCoordinate && Mathf.Abs(value.y) <= MaxAbsCoordinate;
        }

        static bool IsReasonable(Vector3 value)
        {
            return Mathf.Abs(value.x) <= MaxAbsCoordinate && Mathf.Abs(value.y) <= MaxAbsCoordinate && Mathf.Abs(value.z) <= MaxAbsCoordinate;
        }

        static bool IsReasonable(Bounds value)
        {
            return
                IsReasonable(value.center) &&
                value.size.x >= 0f && value.size.y >= 0f && value.size.z >= 0f &&
                value.size.x <= MaxReasonableSize && value.size.y <= MaxReasonableSize && value.size.z <= MaxReasonableSize;
        }

        static bool IsReasonable(Rect value)
        {
            return
                Mathf.Abs(value.xMin) <= MaxAbsCoordinate &&
                Mathf.Abs(value.yMin) <= MaxAbsCoordinate &&
                Mathf.Abs(value.xMax) <= MaxAbsCoordinate &&
                Mathf.Abs(value.yMax) <= MaxAbsCoordinate &&
                value.width >= 0f && value.height >= 0f &&
                value.width <= MaxReasonableSize && value.height <= MaxReasonableSize;
        }
    }
}
