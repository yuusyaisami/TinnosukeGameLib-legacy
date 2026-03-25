#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Common.Editor
{
    public sealed class ExpressionGraphPreviewWindow : EditorWindow
    {
        enum PreviewTab
        {
            Graph = 0,
            Setting = 1,
        }

        const float GraphHeight = 280f;
        const float GraphMargin = 12f;

        static readonly GUIContent[] EmptyOptions = { new GUIContent("(none)") };

        IDynamicSource _source;
        ExpressionGraphSourceModel _sourceModel = new();
        readonly ExpressionPlotDiagnostics _diagnostics = new();

        ExpressionGraphSamplingResult _result = new();

        string _xAxisKey = string.Empty;
        float _xMin = -10f;
        float _xMax = 10f;
        int _sampleCount = 201;
        bool _autoFitY = true;
        float _manualYMin = -1f;
        float _manualYMax = 1f;
        bool _showGrid = true;
        bool _showAxes = true;
        bool _showPoints;
        PreviewTab _selectedTab = PreviewTab.Graph;
        float _viewZoom = 1f;
        float _viewPanX;
        float _viewPanY;

        Vector2 _scroll;
        bool _dirty = true;

        [MenuItem("Tools/Game/Expression Graph Preview")]
        public static void OpenEmpty()
        {
            var window = GetWindow<ExpressionGraphPreviewWindow>("Expression Graph Preview");
            window.minSize = new Vector2(680f, 560f);
            window.Focus();
        }

        public static void Open(IDynamicSource source)
        {
            var window = GetWindow<ExpressionGraphPreviewWindow>("Expression Graph Preview");
            window.minSize = new Vector2(680f, 560f);
            window.Load(source);
            window.Focus();
        }

        void Load(IDynamicSource source)
        {
            _source = source;
            var error = string.Empty;
            if (_source != null && ExpressionGraphSamplingService.TryCreateSourceModel(_source, out var model, out error))
            {
                _sourceModel = model;
                _diagnostics.Clear();
                TryInitXAxis();
            }
            else
            {
                _sourceModel = new ExpressionGraphSourceModel();
                _diagnostics.Clear();
                if (!string.IsNullOrEmpty(error))
                    _diagnostics.AddError(error);
            }

            _dirty = true;
            Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawTabs();
            DrawVariableBinding();

            if (_selectedTab == PreviewTab.Setting)
                DrawPlotSettings();

            EnsureSample();
            DrawGraph();
            DrawDiagnostics();

            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Reload Source", EditorStyles.toolbarButton))
                Load(_source);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Expression Graph Preview", EditorStyles.boldLabel);

            var sourceType = _sourceModel.SourceKind == ExpressionPreviewSourceKind.Int ? "IntExpression" : "FloatExpression";
            EditorGUILayout.LabelField("Source Type", _source == null ? "(none)" : sourceType);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(_sourceModel.Expression ?? string.Empty, GUILayout.MinHeight(54f));
            }
        }

        void DrawTabs()
        {
            EditorGUILayout.Space(4f);
            var labels = new[] { "Graph", "Setting" };
            var selected = GUILayout.Toolbar((int)_selectedTab, labels);
            _selectedTab = (PreviewTab)Mathf.Clamp(selected, 0, 1);
        }

        void DrawVariableBinding()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Variable Binding", EditorStyles.boldLabel);

            var identifiers = _result != null ? _result.Identifiers : null;
            var hasIdentifiers = identifiers != null && identifiers.Count > 0;

            EditorGUI.BeginChangeCheck();

            if (hasIdentifiers)
            {
                var options = new GUIContent[identifiers.Count];
                var selectedIndex = -1;
                for (int i = 0; i < identifiers.Count; i++)
                {
                    var key = identifiers[i];
                    options[i] = new GUIContent(key);
                    if (key == _xAxisKey)
                        selectedIndex = i;
                }

                if (selectedIndex < 0)
                    selectedIndex = 0;

                var newIndex = EditorGUILayout.Popup(new GUIContent("X Axis Variable"), selectedIndex, options);
                if (newIndex >= 0 && newIndex < identifiers.Count)
                    _xAxisKey = identifiers[newIndex];
            }
            else
            {
                EditorGUILayout.Popup(new GUIContent("X Axis Variable"), 0, EmptyOptions);
            }

            if (hasIdentifiers)
            {
                for (int i = 0; i < identifiers.Count; i++)
                {
                    var key = identifiers[i];
                    if (key == _xAxisKey)
                        continue;

                    var kind = ResolveIdentifierKind(key);
                    if (kind == ValueKind.Bool)
                    {
                        _ = _boolValueByKey.TryGetValue(key, out var boolValue);
                        _boolValueByKey[key] = EditorGUILayout.Toggle($"{key} (Bool)", boolValue);
                        continue;
                    }

                    _ = _numericValueByKey.TryGetValue(key, out var numericValue);
                    _numericValueByKey[key] = EditorGUILayout.FloatField($"{key}", numericValue);
                }
            }

            if (EditorGUI.EndChangeCheck())
                _dirty = true;
        }

        readonly System.Collections.Generic.Dictionary<string, float> _numericValueByKey = new(System.StringComparer.Ordinal);
        readonly System.Collections.Generic.Dictionary<string, bool> _boolValueByKey = new(System.StringComparer.Ordinal);

        void DrawPlotSettings()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Graph Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            _xMin = EditorGUILayout.FloatField("X Min", _xMin);
            _xMax = EditorGUILayout.FloatField("X Max", _xMax);
            _sampleCount = EditorGUILayout.IntSlider("Sample Count", _sampleCount, 16, 2001);
            _autoFitY = EditorGUILayout.Toggle("Auto Fit Y", _autoFitY);

            if (!_autoFitY)
            {
                _manualYMin = EditorGUILayout.FloatField("Y Min", _manualYMin);
                _manualYMax = EditorGUILayout.FloatField("Y Max", _manualYMax);
            }

            _showGrid = EditorGUILayout.Toggle("Show Grid", _showGrid);
            _showAxes = EditorGUILayout.Toggle("Show Axes", _showAxes);
            _showPoints = EditorGUILayout.Toggle("Show Points", _showPoints);
            _viewZoom = EditorGUILayout.Slider("View Zoom", _viewZoom, 0.15f, 6f);

            if (EditorGUI.EndChangeCheck())
                _dirty = true;

            if (GUILayout.Button("Recalculate"))
                _dirty = true;
        }

        void EnsureSample()
        {
            if (!_dirty)
                return;

            var request = new ExpressionGraphSamplingRequest
            {
                Source = _sourceModel,
                XAxisKey = _xAxisKey,
                XMin = _xMin,
                XMax = _xMax,
                SampleCount = _sampleCount,
            };

            foreach (var pair in _numericValueByKey)
                request.NumericFixedValues[pair.Key] = pair.Value;
            foreach (var pair in _boolValueByKey)
                request.BoolFixedValues[pair.Key] = pair.Value;

            _result = ExpressionGraphSamplingService.Sample(request, _diagnostics);
            EnsureValueDictionaries();
            _dirty = false;
        }

        void DrawGraph()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Graph Preview", EditorStyles.boldLabel);

            var rect = GUILayoutUtility.GetRect(32f, GraphHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            GUI.Box(rect, GUIContent.none);

            if (_result == null || !_result.CanPlot || _result.Samples.Count == 0)
            {
                var reason = _result != null ? _result.CannotPlotReason : "No data.";
                if (string.IsNullOrEmpty(reason))
                    reason = "Cannot plot.";

                var labelRect = new Rect(rect.x + 10f, rect.center.y - 10f, rect.width - 20f, 40f);
                EditorGUI.LabelField(labelRect, reason, EditorStyles.wordWrappedLabel);
                return;
            }

            var yMin = _autoFitY ? _result.YMin : _manualYMin;
            var yMax = _autoFitY ? _result.YMax : _manualYMax;
            if (yMax <= yMin)
                yMax = yMin + 0.0001f;

            var plotRect = new Rect(
                rect.x + GraphMargin,
                rect.y + GraphMargin,
                rect.width - GraphMargin * 2f,
                rect.height - GraphMargin * 2f);

            HandleGraphZoomInput(plotRect, _xMin, _xMax, yMin, yMax);

            var baseXCenter = (_xMin + _xMax) * 0.5f;
            var baseYCenter = (yMin + yMax) * 0.5f;
            var displayXHalf = Mathf.Max((_xMax - _xMin) * 0.5f, 0.0001f) * _viewZoom;
            var displayYHalf = Mathf.Max((yMax - yMin) * 0.5f, 0.0001f) * _viewZoom;
            var displayXCenter = baseXCenter + _viewPanX;
            var displayYCenter = baseYCenter + _viewPanY;
            var displayXMin = displayXCenter - displayXHalf;
            var displayXMax = displayXCenter + displayXHalf;
            var displayYMin = displayYCenter - displayYHalf;
            var displayYMax = displayYCenter + displayYHalf;

            if (_showGrid)
                DrawGrid(plotRect, displayXMin, displayXMax, displayYMin, displayYMax);

            if (_showAxes)
                DrawAxes(plotRect, displayXMin, displayXMax, displayYMin, displayYMax);

            DrawSamples(plotRect, displayXMin, displayXMax, displayYMin, displayYMax);
        }

        void HandleGraphZoomInput(Rect plotRect, float baseXMin, float baseXMax, float baseYMin, float baseYMax)
        {
            var e = Event.current;
            if (e == null || e.type != EventType.ScrollWheel)
                return;
            if (!plotRect.Contains(e.mousePosition))
                return;

            var oldZoom = _viewZoom;
            var factor = e.delta.y > 0f ? 1.1f : 0.9f;
            _viewZoom = Mathf.Clamp(oldZoom * factor, 0.15f, 6f);

            var baseXCenter = (baseXMin + baseXMax) * 0.5f;
            var baseYCenter = (baseYMin + baseYMax) * 0.5f;
            var baseXRange = Mathf.Max(baseXMax - baseXMin, 0.0001f);
            var baseYRange = Mathf.Max(baseYMax - baseYMin, 0.0001f);

            var oldXHalf = baseXRange * 0.5f * oldZoom;
            var oldYHalf = baseYRange * 0.5f * oldZoom;
            var oldXCenter = baseXCenter + _viewPanX;
            var oldYCenter = baseYCenter + _viewPanY;
            var oldXMin = oldXCenter - oldXHalf;
            var oldYMin = oldYCenter - oldYHalf;
            var oldYMax = oldYCenter + oldYHalf;

            var tx = Mathf.InverseLerp(plotRect.xMin, plotRect.xMax, e.mousePosition.x);
            var tyTop = Mathf.InverseLerp(plotRect.yMin, plotRect.yMax, e.mousePosition.y);
            var worldXAtMouse = Mathf.Lerp(oldXMin, oldXCenter + oldXHalf, tx);
            var worldYAtMouse = Mathf.Lerp(oldYMax, oldYMin, tyTop);

            var newXHalf = baseXRange * 0.5f * _viewZoom;
            var newYHalf = baseYRange * 0.5f * _viewZoom;
            var newXRange = newXHalf * 2f;
            var newYRange = newYHalf * 2f;

            var newXMin = worldXAtMouse - tx * newXRange;
            var newYMin = worldYAtMouse - (1f - tyTop) * newYRange;

            var newXCenter = newXMin + newXHalf;
            var newYCenter = newYMin + newYHalf;
            _viewPanX = newXCenter - baseXCenter;
            _viewPanY = newYCenter - baseYCenter;

            Repaint();
            e.Use();
        }

        void DrawGrid(Rect rect, float xMin, float xMax, float yMin, float yMax)
        {
            Handles.BeginGUI();
            var oldColor = Handles.color;

            var xStep = ComputeNiceStep(xMax - xMin);
            var yStep = ComputeNiceStep(yMax - yMin);

            DrawGridAxisLines(rect, xMin, xMax, xStep, isVertical: true);
            DrawGridAxisLines(rect, yMin, yMax, yStep, isVertical: false);
            DrawGridLabels(rect, xMin, xMax, xStep, isVertical: true);
            DrawGridLabels(rect, yMin, yMax, yStep, isVertical: false);

            Handles.color = oldColor;
            Handles.EndGUI();
        }

        void DrawGridAxisLines(Rect rect, float min, float max, float step, bool isVertical)
        {
            if (step <= 0f)
                return;

            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            var start = Mathf.Floor(min / step) * step;
            var end = Mathf.Ceil(max / step) * step;

            for (var value = start; value <= end + step * 0.5f; value += step)
            {
                if (value < min - step * 0.1f || value > max + step * 0.1f)
                    continue;

                var t = Mathf.InverseLerp(min, max, value);
                if (isVertical)
                {
                    var x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                    Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
                }
                else
                {
                    var y = Mathf.Lerp(rect.yMax, rect.yMin, t);
                    Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
                }
            }
        }

        void DrawGridLabels(Rect rect, float min, float max, float step, bool isVertical)
        {
            if (step <= 0f)
                return;

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f, 0.85f) },
                fontSize = 9,
            };

            var start = Mathf.Floor(min / step) * step;
            var end = Mathf.Ceil(max / step) * step;

            for (var value = start; value <= end + step * 0.5f; value += step)
            {
                if (value < min - step * 0.1f || value > max + step * 0.1f)
                    continue;

                if (Mathf.Abs(value) < step * 0.0001f)
                    continue;

                var text = FormatTick(value, step);
                var t = Mathf.InverseLerp(min, max, value);

                if (isVertical)
                {
                    var x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                    GUI.Label(new Rect(x + 2f, rect.yMax - 14f, 52f, 14f), text, style);
                }
                else
                {
                    var y = Mathf.Lerp(rect.yMax, rect.yMin, t);
                    GUI.Label(new Rect(rect.xMin + 3f, y - 8f, 52f, 14f), text, style);
                }
            }
        }

        static float ComputeNiceStep(float range)
        {
            var safeRange = Mathf.Max(range, 0.000001f);
            var rough = safeRange / 8f;
            var magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rough)));
            var normalized = rough / magnitude;

            float nice;
            if (normalized <= 1f) nice = 1f;
            else if (normalized <= 2f) nice = 2f;
            else if (normalized <= 5f) nice = 5f;
            else nice = 10f;

            return nice * magnitude;
        }

        static string FormatTick(float value, float step)
        {
            if (Mathf.Abs(step) >= 1f)
                return Mathf.RoundToInt(value).ToString();

            return value.ToString("0.###");
        }

        void DrawAxes(Rect rect, float xMin, float xMax, float yMin, float yMax)
        {
            Handles.BeginGUI();
            var oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.45f);

            if (xMin <= 0f && xMax >= 0f)
            {
                var x = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(xMin, xMax, 0f));
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
            }

            if (yMin <= 0f && yMax >= 0f)
            {
                var y = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.InverseLerp(yMin, yMax, 0f));
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }

            Handles.color = oldColor;
            Handles.EndGUI();
        }

        void DrawSamples(Rect rect, float xMin, float xMax, float yMin, float yMax)
        {
            Handles.BeginGUI();

            var oldColor = Handles.color;
            Handles.color = new Color(0.2f, 0.85f, 0.45f, 1f);

            var segment = new System.Collections.Generic.List<Vector3>(32);
            for (int i = 0; i < _result.Samples.Count; i++)
            {
                var sample = _result.Samples[i];
                if (!sample.IsValid)
                {
                    FlushSegment(segment);
                    continue;
                }

                var px = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(xMin, xMax, sample.X));
                var py = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.InverseLerp(yMin, yMax, sample.Y));
                var p = new Vector3(px, py, 0f);
                segment.Add(p);

                if (_showPoints)
                    Handles.DrawSolidDisc(p, Vector3.forward, 1.2f);
            }

            FlushSegment(segment);

            Handles.color = oldColor;
            Handles.EndGUI();
        }

        static void FlushSegment(System.Collections.Generic.List<Vector3> segment)
        {
            if (segment == null)
                return;

            if (segment.Count >= 2)
                Handles.DrawAAPolyLine(2.2f, segment.ToArray());

            segment.Clear();
        }

        void DrawDiagnostics()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);

            var canPlot = _result != null && _result.CanPlot;
            EditorGUILayout.HelpBox(canPlot ? "Can Plot" : "Cannot Plot", canPlot ? MessageType.Info : MessageType.Warning);

            var items = _diagnostics.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var msgType = item.Severity switch
                {
                    ExpressionPlotDiagnosticSeverity.Warning => MessageType.Warning,
                    ExpressionPlotDiagnosticSeverity.Error => MessageType.Error,
                    _ => MessageType.Info,
                };

                EditorGUILayout.HelpBox(item.Message, msgType);
            }
        }

        void TryInitXAxis()
        {
            var tokenizer = new ExpressionTokenizer(_sourceModel.Expression ?? string.Empty);
            var tokens = tokenizer.Tokenize(out var lexError);
            if (!string.IsNullOrEmpty(lexError))
                return;

            var parser = new ExpressionParser(tokens, new System.Collections.Generic.Dictionary<string, ValueKind>(System.StringComparer.Ordinal), new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal));
            var used = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            parser = new ExpressionParser(tokens, new System.Collections.Generic.Dictionary<string, ValueKind>(System.StringComparer.Ordinal), used);
            _ = parser.ParseExpression(out var parseError);
            if (!string.IsNullOrEmpty(parseError) || used.Count == 0)
                return;

            foreach (var key in used)
            {
                _xAxisKey = key;
                break;
            }
        }

        void EnsureValueDictionaries()
        {
            if (_result == null || _result.Identifiers == null)
                return;

            for (int i = 0; i < _result.Identifiers.Count; i++)
            {
                var key = _result.Identifiers[i];
                if (key == _xAxisKey)
                    continue;

                var kind = ResolveIdentifierKind(key);
                if (kind == ValueKind.Bool)
                {
                    if (!_boolValueByKey.ContainsKey(key))
                        _boolValueByKey[key] = false;
                    continue;
                }

                if (!_numericValueByKey.ContainsKey(key))
                    _numericValueByKey[key] = 0f;
            }
        }

        ValueKind ResolveIdentifierKind(string key)
        {
            if (_result != null && _result.IdentifierKinds.TryGetValue(key, out var kind))
                return kind;

            return ValueKind.Auto;
        }
    }
}
#endif
