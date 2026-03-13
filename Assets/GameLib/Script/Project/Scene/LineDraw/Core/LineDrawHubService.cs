using System;
using System.Collections.Generic;
using Game;
using Game.MaterialFx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.LineDraw
{
    public sealed class LineDrawHubService : ILineDrawService, ITickable, IScopeAcquireHandler, IScopeReleaseHandler, IDisposable
    {
        sealed class LineEntry
        {
            public int Generation;
            public bool Alive;
            public LineKind Kind;
            public LineSpace Space;
            public LineStyle Style;
            public LineAnchor From;
            public LineAnchor To;
            public LinePath Path;
            public bool Dirty;
            public Vector3 LastFrom;
            public Vector3 LastTo;
            public readonly List<Vector3> Points = new();
            public LineRenderInstance Render;
        }

        sealed class LineRenderInstance
        {
            public readonly ILineRenderBackend Backend;
            public readonly LineMeshData MeshData = new();
            public readonly LineMeshBuilder Builder = new();
            public readonly IMaterialFxService MaterialFx;

            public LineRenderInstance(ILineRenderBackend backend, IMaterialFxService materialFx)
            {
                Backend = backend;
                MaterialFx = materialFx;
            }
        }

        sealed class LineDrawSettingsFallback : ILineDrawSettings
        {
            public LineSpace DefaultSpace => LineSpace.Local;
            public LineStyle DefaultStyle => LineStyle.Default;
            public int MaxLineCount => 128;
            public int MaxVertexCount => 4096;
            public float MinSegmentLength => 0.1f;
            public float GeometryQuality => 1f;
            public float AdaptiveQualityScale => 1f;
            public bool UseUnscaledTime => false;
            public bool AutoDrawOnAcquire => false;
            public LineSpace AutoDrawSpace => LineSpace.Local;
            public LineStyle AutoDrawStyle => LineStyle.Default;
            public bool AutoDrawClosed => false;
            public IReadOnlyList<Vector3> AutoDrawPoints => Array.Empty<Vector3>();
        }

        enum LineKind
        {
            Segment,
            Path
        }

        readonly Transform _ownerTransform;
        readonly RectTransform _ownerRect;
        readonly IObjectResolver _resolver;

        ILineDrawSettings _settings;
        ILineDrawMaterialSettings _materialSettings;
        IMaterialFxServiceFactory _materialFxFactory;

        readonly List<LineEntry> _entries = new();
        readonly Stack<int> _freeIds = new();
        readonly Stack<LineRenderInstance> _worldPool = new();
        readonly Stack<LineRenderInstance> _uiPool = new();

        Transform _worldRoot;
        RectTransform _uiRoot;
        Canvas _cachedCanvas;

        Material _defaultWorldMaterial;
        Material _defaultUiMaterial;

        int _activeCount;
        bool _acquired;
        bool _disposed;

        readonly List<LineHandle> _activeHandlesCache = new();
        bool _activeHandlesCacheDirty = true;

        public int ActiveCount => _activeCount;

        public IReadOnlyList<LineHandle> ActiveHandles
        {
            get
            {
                if (_activeHandlesCacheDirty)
                {
                    _activeHandlesCache.Clear();
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        var entry = _entries[i];
                        if (entry != null && entry.Alive)
                            _activeHandlesCache.Add(new LineHandle(i, entry.Generation));
                    }
                    _activeHandlesCacheDirty = false;
                }
                return _activeHandlesCache;
            }
        }

        public LineDrawHubService(IScopeNode scope, Transform ownerTransform, IObjectResolver resolver)
        {
            _ownerTransform = ownerTransform;
            _ownerRect = ownerTransform as RectTransform;
            _resolver = resolver;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed || _acquired)
                return;

            _acquired = true;
            var resolver = scope != null && scope.Resolver != null ? scope.Resolver : _resolver;
            if (resolver != null)
            {
                resolver.TryResolve(out _settings);
                resolver.TryResolve(out _materialSettings);
                resolver.TryResolve(out _materialFxFactory);
            }

            if (_settings == null)
                _settings = new LineDrawSettingsFallback();

            TryAutoDrawOnAcquire();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed || !_acquired)
                return;

            ClearAll();
            _acquired = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ClearAll();
            DisposePools();
            DisposeDefaultMaterials();
        }

        public void Tick()
        {
            if (_disposed || !_acquired)
                return;

            bool ownerMoved = false;
            if (_ownerTransform != null)
            {
                ownerMoved = _ownerTransform.hasChanged;
                if (ownerMoved)
                    _ownerTransform.hasChanged = false;
            }

            var deltaTime = _settings != null && _settings.UseUnscaledTime ? UnityEngine.Time.unscaledDeltaTime : UnityEngine.Time.deltaTime;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null || !entry.Alive || entry.Render == null)
                    continue;

                bool dirty = entry.Dirty;

                // OffsetVelocityによる自動オフセット更新
                if (Mathf.Abs(entry.Style.Pattern.OffsetVelocity) > 0.0001f)
                {
                    var pattern = entry.Style.Pattern;
                    pattern.Offset += pattern.OffsetVelocity * deltaTime;
                    entry.Style.Pattern = pattern;
                    dirty = true;
                }

                if (entry.Kind == LineKind.Segment)
                {
                    var from = ResolveAnchor(entry.From, entry.Space);
                    var to = ResolveAnchor(entry.To, entry.Space);

                    if (!Approximately(entry.LastFrom, from) || !Approximately(entry.LastTo, to))
                    {
                        entry.LastFrom = from;
                        entry.LastTo = to;
                        dirty = true;
                    }

                    if (dirty)
                    {
                        entry.Points.Clear();
                        entry.Points.Add(from);
                        entry.Points.Add(to);
                        BuildEntry(entry, false);
                    }
                }
                else
                {
                    if (entry.Space == LineSpace.World && ownerMoved)
                        dirty = true;

                    if (dirty)
                    {
                        entry.Points.Clear();
                        ResolvePathPoints(entry.Path, entry.Space, entry.Points);
                        BuildEntry(entry, entry.Path != null && entry.Path.Closed);
                    }
                }

                entry.Dirty = false;
            }
        }

        public LineHandle CreateSegment(LineSegmentRequest request)
        {
            if (!_acquired || _disposed)
                return LineHandle.Invalid;

            if (!CanAllocate())
                return LineHandle.Invalid;

            if (!TryAllocateEntry(out var entry, out var handle))
                return LineHandle.Invalid;

            entry.Kind = LineKind.Segment;
            entry.Space = request.Space;
            entry.Style = ResolveStyle(request.Style);
            entry.From = request.From;
            entry.To = request.To;
            entry.Path = null;
            entry.Dirty = true;
            entry.LastFrom = Vector3.zero;
            entry.LastTo = Vector3.zero;

            if (!EnsureBackend(entry))
            {
                ReleaseInternal(handle);
                return LineHandle.Invalid;
            }

            InitializeSegmentEntry(entry);
            return handle;
        }

        public LineHandle CreatePath(LinePathRequest request)
        {
            if (!_acquired || _disposed)
                return LineHandle.Invalid;

            if (!CanAllocate())
                return LineHandle.Invalid;

            if (!TryAllocateEntry(out var entry, out var handle))
                return LineHandle.Invalid;

            entry.Kind = LineKind.Path;
            entry.Space = request.Space;
            entry.Style = ResolveStyle(request.Style);
            entry.Path = request.Path;
            entry.Dirty = true;

            if (!EnsureBackend(entry))
            {
                ReleaseInternal(handle);
                return LineHandle.Invalid;
            }

            InitializePathEntry(entry);
            return handle;
        }

        public bool UpdateSegment(LineHandle handle, LineSegmentRequest request)
        {
            if (!TryGetEntry(handle, out var entry))
                return false;

            entry.Kind = LineKind.Segment;
            entry.Space = request.Space;
            entry.Style = ResolveStyle(request.Style);
            entry.From = request.From;
            entry.To = request.To;
            entry.Path = null;
            entry.Dirty = true;

            return EnsureBackend(entry);
        }

        public bool UpdatePath(LineHandle handle, LinePathRequest request)
        {
            if (!TryGetEntry(handle, out var entry))
                return false;

            entry.Kind = LineKind.Path;
            entry.Space = request.Space;
            entry.Style = ResolveStyle(request.Style);
            entry.Path = request.Path;
            entry.Dirty = true;

            return EnsureBackend(entry);
        }

        public bool UpdateStyle(LineHandle handle, LineStyle style)
        {
            if (!TryGetEntry(handle, out var entry))
                return false;

            entry.Style = ResolveStyle(style);
            entry.Dirty = true;
            return true;
        }

        public bool UpdatePatternOffset(LineHandle handle, float offset)
        {
            if (!TryGetEntry(handle, out var entry))
                return false;

            var style = entry.Style;
            var pattern = style.Pattern;
            pattern.Offset = offset;
            style.Pattern = pattern;
            entry.Style = style;
            entry.Dirty = true;
            return true;
        }

        public bool UpdatePatternOffsetVelocity(LineHandle handle, float velocity)
        {
            if (!TryGetEntry(handle, out var entry))
                return false;

            var style = entry.Style;
            var pattern = style.Pattern;
            pattern.OffsetVelocity = velocity;
            style.Pattern = pattern;
            entry.Style = style;
            entry.Dirty = true;
            return true;
        }

        public bool UpdateBaseWidth(LineHandle handle, float width)
        {
            if (!TryGetEntry(handle, out var entry))
                return false;

            var style = entry.Style;
            style.BaseWidth = width;
            entry.Style = style;
            entry.Dirty = true;
            return true;
        }

        public IMaterialFxService TryGetMaterialFx(LineHandle handle)
        {
            if (!TryGetEntry(handle, out var entry))
                return null;

            return entry.Render?.MaterialFx;
        }

        public bool Release(LineHandle handle)
        {
            if (!TryGetEntry(handle, out _))
                return false;

            ReleaseInternal(handle);
            return true;
        }

        public void ClearAll()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null || !entry.Alive)
                    continue;

                ReleaseInternal(new LineHandle(i, entry.Generation));
            }
        }

        bool CanAllocate()
        {
            if (_settings == null)
                return true;

            if (_settings.MaxLineCount <= 0)
                return true;

            return _activeCount < _settings.MaxLineCount;
        }

        bool TryAllocateEntry(out LineEntry entry, out LineHandle handle)
        {
            entry = null;
            handle = LineHandle.Invalid;

            int id;
            if (_freeIds.Count > 0)
            {
                id = _freeIds.Pop();
            }
            else
            {
                id = _entries.Count;
                _entries.Add(new LineEntry());
            }

            entry = _entries[id];
            if (entry == null)
            {
                entry = new LineEntry();
                _entries[id] = entry;
            }

            entry.Alive = true;
            entry.Dirty = true;
            entry.Points.Clear();

            handle = new LineHandle(id, entry.Generation);
            _activeCount++;
            _activeHandlesCacheDirty = true;
            return true;
        }

        bool TryGetEntry(LineHandle handle, out LineEntry entry)
        {
            entry = null;
            if (!handle.IsValid)
                return false;

            if (handle.Id < 0 || handle.Id >= _entries.Count)
                return false;

            entry = _entries[handle.Id];
            if (entry == null || !entry.Alive)
                return false;

            if (entry.Generation != handle.Generation)
                return false;

            return true;
        }

        void ReleaseInternal(LineHandle handle)
        {
            if (!TryGetEntry(handle, out var entry))
                return;

            if (entry.Render != null)
            {
                entry.Render.Backend.SetActive(false);
                ReturnBackend(entry.Render);
                entry.Render = null;
            }

            entry.Alive = false;
            entry.Generation++;
            entry.Path = null;
            entry.Points.Clear();
            entry.Dirty = false;
            _freeIds.Push(handle.Id);
            _activeCount = Mathf.Max(0, _activeCount - 1);
            _activeHandlesCacheDirty = true;
        }

        void BuildEntry(LineEntry entry, bool closed)
        {
            if (entry.Render == null)
                return;

            float pixelScale = GetPixelScale(entry.Space);
            float minSegmentLength = _settings != null ? _settings.MinSegmentLength : 0.1f;
            int maxVertexCount = _settings != null ? _settings.MaxVertexCount : 4096;
            float quality = _settings != null ? _settings.GeometryQuality : 1f;
            if (quality <= 0f)
                quality = 1f;

            float adaptiveScale = 1f;
            if (_settings != null && _settings.MaxLineCount > 0)
            {
                float targetScale = Mathf.Max(1f, _settings.AdaptiveQualityScale);
                float ratio = Mathf.Clamp01((float)_activeCount / _settings.MaxLineCount);
                adaptiveScale = Mathf.Lerp(1f, targetScale, ratio);
            }

            quality *= adaptiveScale;

            minSegmentLength *= quality;
            maxVertexCount = Mathf.Max(16, Mathf.RoundToInt(maxVertexCount / quality));

            entry.Render.Builder.Build(
                entry.Render.MeshData,
                entry.Points,
                closed,
                entry.Style,
                minSegmentLength,
                maxVertexCount,
                pixelScale);

            if (entry.Render.MeshData.VertexCount == 0 && entry.Points.Count >= 2)
            {
                Debug.LogWarning($"[LineDrawHubService] BuildEntry: Mesh not generated. points={entry.Points.Count}, space={entry.Space}, style.BaseWidth={entry.Style.BaseWidth}");
            }

            entry.Render.Backend.ApplyMesh(entry.Render.MeshData);
            // IsActiveAndEnabledがfalseの場合、SetVerticesDirtyが効かないため、
            // SetActive(true)時にSetAllDirty()を呼ぶ必要がある（LineDrawGraphic.cs参照）
            entry.Render.Backend.SetActive(entry.Render.MeshData.VertexCount > 0);
        }

        void InitializeSegmentEntry(LineEntry entry)
        {
            if (entry == null)
                return;

            entry.Points.Clear();
            var from = ResolveAnchor(entry.From, entry.Space);
            var to = ResolveAnchor(entry.To, entry.Space);
            entry.LastFrom = from;
            entry.LastTo = to;
            entry.Points.Add(from);
            entry.Points.Add(to);
            BuildEntry(entry, false);
            entry.Dirty = false;
        }

        void InitializePathEntry(LineEntry entry)
        {
            if (entry == null)
                return;

            entry.Points.Clear();
            ResolvePathPoints(entry.Path, entry.Space, entry.Points);
            BuildEntry(entry, entry.Path != null && entry.Path.Closed);
            entry.Dirty = false;
        }

        bool EnsureBackend(LineEntry entry)
        {
            if (entry == null)
                return false;

            var desiredKind = entry.Space == LineSpace.RectTransform
                ? LineRenderBackendKind.UI
                : LineRenderBackendKind.World;

            if (entry.Render != null && entry.Render.Backend != null && entry.Render.Backend.Kind == desiredKind)
                return true;

            if (entry.Render != null)
            {
                entry.Render.Backend.SetActive(false);
                ReturnBackend(entry.Render);
                entry.Render = null;
            }

            entry.Render = RentBackend(desiredKind);
            return entry.Render != null;
        }

        LineRenderInstance RentBackend(LineRenderBackendKind kind)
        {
            LineRenderInstance instance = null;
            if (kind == LineRenderBackendKind.UI)
            {
                if (_uiPool.Count > 0)
                    instance = _uiPool.Pop();

                if (instance == null)
                    instance = CreateUiBackend();
            }
            else
            {
                if (_worldPool.Count > 0)
                    instance = _worldPool.Pop();

                if (instance == null)
                    instance = CreateWorldBackend();
            }

            if (instance != null)
                instance.Backend.SetActive(false);

            return instance;
        }

        void ReturnBackend(LineRenderInstance instance)
        {
            if (instance == null || instance.Backend == null)
                return;

            instance.Backend.SetActive(false);
            instance.MeshData.Clear();
            instance.Backend.ApplyMesh(instance.MeshData);

            if (instance.Backend.Kind == LineRenderBackendKind.UI)
                _uiPool.Push(instance);
            else
                _worldPool.Push(instance);
        }

        LineRenderInstance CreateWorldBackend()
        {
            var root = EnsureWorldRoot();
            var backend = new LineMeshRendererBackend(root, "LineDrawWorldLine");
            backend.ApplyMaterial(ResolveWorldMaterial());

            IMaterialFxService fx = null;
            if (_materialFxFactory != null && backend.Renderer != null)
                fx = _materialFxFactory.CreateForRenderer(backend.Renderer);
            return new LineRenderInstance(backend, fx);
        }

        LineRenderInstance CreateUiBackend()
        {
            var root = EnsureUiRoot();
            if (root == null)
                return null;

            // CanvasRendererを明示的に追加（Graphicの描画に必須）
            var go = new GameObject("LineDrawUILine", typeof(RectTransform), typeof(CanvasRenderer), typeof(LineDrawGraphic));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;

            var graphic = go.GetComponent<LineDrawGraphic>();
            graphic.raycastTarget = false;
            graphic.ApplyMaterial(ResolveUiMaterial());
            var fx = _materialFxFactory != null ? _materialFxFactory.CreateForGraphic(graphic) : null;
            return new LineRenderInstance(graphic, fx);
        }

        Transform EnsureWorldRoot()
        {
            if (_worldRoot != null)
                return _worldRoot;

            var go = new GameObject("LineDrawWorldRoot");
            var t = go.transform;
            if (_ownerTransform != null)
                t.SetParent(_ownerTransform, false);
            _worldRoot = t;
            return _worldRoot;
        }

        RectTransform EnsureUiRoot()
        {
            if (_uiRoot != null)
                return _uiRoot;

            var go = new GameObject("LineDrawUIRoot", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            if (_ownerTransform != null)
                rect.SetParent(_ownerTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            _uiRoot = rect;
            return _uiRoot;
        }

        float GetPixelScale(LineSpace space)
        {
            if (space != LineSpace.RectTransform)
                return 1f;

            // UI空間では、Graphicのローカル座標系はすでにピクセル単位で処理されるため、
            // Canvas Scalerによって適切にスケールされます。
            // lossyScaleによる補正は不要で、むしろ問題を引き起こします。
            // 
            // LineDrawGraphicはMaskableGraphicを継承しており、
            // OnPopulateMeshで追加される頂点はRectTransformのローカル座標系で解釈されます。
            // Canvas ScalerがScreen Space - Cameraや Scale With Screen Size の場合、
            // lossyScaleは画面サイズに応じて変化しますが、頂点座標と幅は
            // Reference Resolution基準のピクセル値として扱われるべきです。

            // Canvas Scalerの参照解像度とスクリーン解像度の関係から
            // 適切なスケールを計算することもできますが、
            // 多くのケースでは 1.0 で正しく動作します。
            return 1f;
        }

        Vector3 ResolveAnchor(LineAnchor anchor, LineSpace space)
        {
            if (anchor.Transform != null)
            {
                var world = anchor.Transform.TransformPoint(anchor.LocalOffset);
                return ConvertWorldToLocal(world, space);
            }

            if (space == LineSpace.World)
                return ConvertWorldToLocal(anchor.LocalOffset, space);

            return anchor.LocalOffset;
        }

        void ResolvePathPoints(LinePath path, LineSpace space, List<Vector3> output)
        {
            if (path == null || path.Points == null)
                return;

            for (int i = 0; i < path.Points.Count; i++)
            {
                var p = path.Points[i].Position;
                if (space == LineSpace.World)
                    p = ConvertWorldToLocal(p, space);
                output.Add(p);
            }
        }

        Vector3 ConvertWorldToLocal(Vector3 world, LineSpace space)
        {
            if (space == LineSpace.RectTransform)
            {
                // UI Root と Canvas のキャッシュを活用
                var rect = _uiRoot;
                if (rect == null)
                    rect = _ownerRect;
                if (rect == null)
                {
                    rect = EnsureUiRoot();
                    if (rect == null)
                        goto fallback;
                }

                var local = rect.InverseTransformPoint(world);
                local.z = 0f;
                return local;
            }

        fallback:
            if (_ownerTransform != null)
                return _ownerTransform.InverseTransformPoint(world);

            return world;
        }

        LineStyle ResolveStyle(LineStyle style)
        {
            var fallback = _settings != null ? _settings.DefaultStyle : LineStyle.Default;

            if (style.BaseWidth <= 0f)
                style.BaseWidth = fallback.BaseWidth;

            if (style.UVScale <= 0f)
                style.UVScale = fallback.UVScale;

            if (style.Color == default)
                style.Color = fallback.Color;

            if (IsPatternUnset(style.Pattern))
                style.Pattern = fallback.Pattern;

            if (IsTaperUnset(style.Taper))
                style.Taper = fallback.Taper;

            return style;
        }

        static bool IsPatternUnset(LinePattern pattern)
        {
            return pattern.DashLength == 0f &&
                   pattern.GapLength == 0f &&
                   pattern.DotLength == 0f &&
                   pattern.WaveAmplitude == 0f &&
                   pattern.WaveLength == 0f &&
                   pattern.WavePhase == 0f &&
                   pattern.Type == LinePatternType.Solid;
        }

        static bool IsTaperUnset(LineWidthTaper taper)
        {
            const float epsilon = 0.0001f;
            bool lengthsZero = Mathf.Abs(taper.StartLength) <= epsilon &&
                               Mathf.Abs(taper.EndLength) <= epsilon;
            bool scalesZero = Mathf.Abs(taper.StartScale) <= epsilon &&
                              Mathf.Abs(taper.EndScale) <= epsilon;
            bool scalesDefault = Mathf.Abs(taper.StartScale - 1f) <= epsilon &&
                                 Mathf.Abs(taper.EndScale - 1f) <= epsilon;

            return lengthsZero && (scalesZero || scalesDefault);
        }

        static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= 0.0001f;
        }

        void TryAutoDrawOnAcquire()
        {
            if (_settings == null || !_settings.AutoDrawOnAcquire)
                return;

            var points = _settings.AutoDrawPoints;
            if (points == null || points.Count < 2)
                return;

            var linePoints = new List<LinePoint>(points.Count);
            for (int i = 0; i < points.Count; i++)
                linePoints.Add(new LinePoint(points[i]));

            var style = _settings.AutoDrawStyle;
            var path = new LinePath(linePoints, _settings.AutoDrawClosed);
            var request = new LinePathRequest(path, _settings.AutoDrawSpace, style);
            CreatePath(request);
        }

        Material ResolveWorldMaterial()
        {
            if (_materialSettings != null && _materialSettings.WorldMaterial != null)
                return _materialSettings.WorldMaterial;

            if (_defaultWorldMaterial != null)
                return _defaultWorldMaterial;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            _defaultWorldMaterial = new Material(shader)
            {
                name = "LineDrawDefaultWorld",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            return _defaultWorldMaterial;
        }

        Material ResolveUiMaterial()
        {
            if (_materialSettings != null && _materialSettings.UiMaterial != null)
                return _materialSettings.UiMaterial;

            if (_defaultUiMaterial == null)
                _defaultUiMaterial = Graphic.defaultGraphicMaterial;

            return _defaultUiMaterial;
        }

        void DisposePools()
        {
            while (_worldPool.Count > 0)
            {
                var instance = _worldPool.Pop();
                instance?.MaterialFx?.Dispose();
                instance?.Backend?.Dispose();
            }

            while (_uiPool.Count > 0)
            {
                var instance = _uiPool.Pop();
                instance?.MaterialFx?.Dispose();
                instance?.Backend?.Dispose();
            }

            _worldPool.Clear();
            _uiPool.Clear();
        }

        void DisposeDefaultMaterials()
        {
            if (_defaultWorldMaterial != null)
                UnityEngine.Object.Destroy(_defaultWorldMaterial);
            _defaultWorldMaterial = null;
        }
    }
}
