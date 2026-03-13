#nullable enable
using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    [CustomEditor(typeof(RoomMapLayoutSO))]
    public sealed class RoomMapLayoutSOEditor : OdinEditor
    {
        enum PaintTool
        {
            Pen = 0,
            Eraser = 1,
            Bucket = 2,
        }

        static readonly float CellPx = 18f;
        static readonly float GridPadding = 6f;

        PaintTool _tool = PaintTool.Pen;
        int _selectedTileId;
        int _selectedIndex;
        bool _showGrid;
        int _selectedLayerIndex;

        Vector2 _gridScroll;
        Vector2Int _lastPainted = new Vector2Int(int.MinValue, int.MinValue);

        string[] _options = Array.Empty<string>();
        int[] _optionTileIds = Array.Empty<int>();
        int _lastTileCount = -1;

        SerializedProperty? _layersProp;

        RoomMapLayoutSO Layout => (RoomMapLayoutSO)target;

        protected override void OnEnable()
        {
            base.OnEnable();
            _layersProp = serializedObject.FindProperty("layers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawLayerSection();

            var tree = Tree;
            tree.UpdateTree();
            foreach (var child in tree.RootProperty.Children)
            {
                if (child.Name == "layers") continue;
                if (child.Name == "width") continue;
                if (child.Name == "height") continue;
                if (child.Name == "cellsWidth") continue;
                if (child.Name == "cellsHeight") continue;
                if (child.Name == "cells") continue;
                child.Draw();
            }
            tree.ApplyChanges();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawPaintTools();
        }

        void DrawLayerSection()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);

                if (_layersProp == null)
                {
                    EditorGUILayout.HelpBox("layers プロパティが見つかりません。", MessageType.Error);
                    return;
                }

                var layerCount = _layersProp.arraySize;
                if (layerCount <= 0)
                {
                    if (GUILayout.Button("Create Base Layer"))
                    {
                        _layersProp.arraySize = 1;
                        serializedObject.ApplyModifiedProperties();
                        Layout.RebuildGrid();
                        _selectedLayerIndex = 0;
                    }
                    return;
                }

                _selectedLayerIndex = Mathf.Clamp(_selectedLayerIndex, 0, layerCount - 1);

                var layerNames = BuildLayerNames(_layersProp);
                _selectedLayerIndex = EditorGUILayout.Popup("Active Layer", _selectedLayerIndex, layerNames);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add", GUILayout.Width(60)))
                    {
                        AddLayer(_layersProp);
                    }

                    using (new EditorGUI.DisabledScope(layerCount <= 1))
                    {
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            RemoveLayer(_layersProp, _selectedLayerIndex);
                        }
                    }

                    using (new EditorGUI.DisabledScope(_selectedLayerIndex <= 0))
                    {
                        if (GUILayout.Button("Up", GUILayout.Width(50)))
                        {
                            MoveLayer(_layersProp, _selectedLayerIndex, _selectedLayerIndex - 1);
                        }
                    }

                    using (new EditorGUI.DisabledScope(_selectedLayerIndex >= layerCount - 1))
                    {
                        if (GUILayout.Button("Down", GUILayout.Width(60)))
                        {
                            MoveLayer(_layersProp, _selectedLayerIndex, _selectedLayerIndex + 1);
                        }
                    }
                }

                var layerProp = _layersProp.GetArrayElementAtIndex(_selectedLayerIndex);
                if (layerProp == null)
                    return;

                var nameProp = layerProp.FindPropertyRelative("displayName");
                var widthProp = layerProp.FindPropertyRelative("width");
                var heightProp = layerProp.FindPropertyRelative("height");

                EditorGUI.BeginChangeCheck();
                if (nameProp != null)
                    EditorGUILayout.PropertyField(nameProp, new GUIContent("Name"));
                if (widthProp != null)
                    EditorGUILayout.PropertyField(widthProp, new GUIContent("Width"));
                if (heightProp != null)
                    EditorGUILayout.PropertyField(heightProp, new GUIContent("Height"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    Layout.RebuildLayer(_selectedLayerIndex);
                }
            }
        }

        void AddLayer(SerializedProperty layersProp)
        {
            var insertIndex = layersProp.arraySize;
            layersProp.arraySize++;

            var newLayer = layersProp.GetArrayElementAtIndex(insertIndex);
            if (newLayer != null)
            {
                var nameProp = newLayer.FindPropertyRelative("displayName");
                var widthProp = newLayer.FindPropertyRelative("width");
                var heightProp = newLayer.FindPropertyRelative("height");

                var baseLayer = layersProp.GetArrayElementAtIndex(0);
                var baseWidth = baseLayer?.FindPropertyRelative("width")?.intValue ?? 1;
                var baseHeight = baseLayer?.FindPropertyRelative("height")?.intValue ?? 1;

                if (nameProp != null)
                    nameProp.stringValue = $"Layer {insertIndex}";
                if (widthProp != null)
                    widthProp.intValue = Mathf.Max(1, baseWidth);
                if (heightProp != null)
                    heightProp.intValue = Mathf.Max(1, baseHeight);
            }

            serializedObject.ApplyModifiedProperties();
            Layout.RebuildGrid();
            _selectedLayerIndex = insertIndex;
        }

        void RemoveLayer(SerializedProperty layersProp, int index)
        {
            if (layersProp.arraySize <= 1)
                return;
            if (index < 0 || index >= layersProp.arraySize)
                return;

            layersProp.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            Layout.RebuildGrid();
            _selectedLayerIndex = Mathf.Clamp(_selectedLayerIndex, 0, layersProp.arraySize - 1);
        }

        void MoveLayer(SerializedProperty layersProp, int from, int to)
        {
            if (from < 0 || to < 0 || from >= layersProp.arraySize || to >= layersProp.arraySize)
                return;

            layersProp.MoveArrayElement(from, to);
            serializedObject.ApplyModifiedProperties();
            Layout.RebuildGrid();
            _selectedLayerIndex = to;
        }

        static string[] BuildLayerNames(SerializedProperty layersProp)
        {
            if (layersProp == null || layersProp.arraySize <= 0)
                return new[] { "Layer 0" };

            var names = new string[layersProp.arraySize];
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                var layer = layersProp.GetArrayElementAtIndex(i);
                var nameProp = layer?.FindPropertyRelative("displayName");
                var name = nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue)
                    ? nameProp.stringValue
                    : $"Layer {i}";
                names[i] = name;
            }
            return names;
        }

        void DrawPaintTools()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                EditorGUILayout.LabelField("RoomMap Paint", EditorStyles.boldLabel);

                if (!Layout.TryGetLayer(_selectedLayerIndex, out var layer) || layer == null)
                {
                    EditorGUILayout.HelpBox("Active layer is missing.", MessageType.Warning);
                    return;
                }

                var registry = RoomMapTileRegistryLocator.GetOrCreate();
                EnsureOptions(registry);

                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _tool = (PaintTool)GUILayout.Toolbar((int)_tool, new[] { "Pen", "Eraser", "Bucket" });

                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Build Grid", GUILayout.Width(90)))
                        {
                            Layout.RebuildLayer(_selectedLayerIndex);
                            _showGrid = true;
                            EditorUtility.SetDirty(Layout);
                        }

                        _showGrid = GUILayout.Toggle(_showGrid, "Show", GUILayout.Width(60));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Selected", GUILayout.Width(60));
                        _selectedIndex = EditorGUILayout.Popup(Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _options.Length - 1)), _options);
                        _selectedTileId = (_selectedIndex >= 0 && _selectedIndex < _optionTileIds.Length) ? _optionTileIds[_selectedIndex] : 0;
                    }
                }

                if (EditorApplication.isPlaying)
                    EditorGUILayout.HelpBox("Play Mode 中は編集できません。", MessageType.Info);

                if (!_showGrid)
                    return;

                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    DrawGrid(layer, registry);
            }
        }

        void DrawGrid(RoomMapLayoutLayer layer, RoomMapTileRegistry registry)
        {
            var width = Mathf.Max(1, layer.Width);
            var height = Mathf.Max(1, layer.Height);

            var contentW = width * CellPx + GridPadding * 2f;
            var contentH = height * CellPx + GridPadding * 2f;

            var viewH = Mathf.Min(420f, contentH + 12f);

            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(viewH));
            var rect = GUILayoutUtility.GetRect(contentW, contentH);
            rect = new Rect(rect.x + GridPadding, rect.y + GridPadding, width * CellPx, height * CellPx);

            HandleGridEvents(layer, registry, rect, width, height);
            DrawGridCells(layer, registry, rect, width, height);

            EditorGUILayout.EndScrollView();
        }

        void HandleGridEvents(RoomMapLayoutLayer layer, RoomMapTileRegistry registry, Rect gridRect, int width, int height)
        {
            var e = Event.current;
            if (e == null)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var tool = (Game.EditorTools.GridPaintIMGUI.Tool)(int)_tool;
            var paintValue = _tool == PaintTool.Eraser ? 0 : _selectedTileId;

            var interactiveRect = AdjustForScroll(gridRect);
            if (Game.EditorTools.GridPaintIMGUI.HandlePaintEvents(interactiveRect, width, height, ref _lastPainted, (x, y) =>
                {
                    Undo.RecordObject(Layout, "RoomMap Paint");

                    Game.EditorTools.GridPaintIMGUI.ApplyTool(
                        tool,
                        x,
                        y,
                        width,
                        height,
                        get: layer.GetTileId,
                        set: (sx, sy, v) => layer.SetTileId(sx, sy, v),
                        equals: (a, b) => a == b,
                        paintValue: paintValue);

                    EditorUtility.SetDirty(Layout);
                }))
            {
                GUI.changed = true;
            }
        }

        static Vector2Int MouseToCell(Vector2 mouse, Rect gridRect, int width, int height)
        {
            var local = mouse - new Vector2(gridRect.x, gridRect.y);
            var x = Mathf.FloorToInt(local.x / CellPx);
            var y = Mathf.FloorToInt(local.y / CellPx);
            return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
        }

        void ApplyPaint(RoomMapLayoutLayer layer, int x, int y, int tileId)
        {
            if (layer.GetTileId(x, y) == tileId)
                return;

            Undo.RecordObject(Layout, "RoomMap Paint");
            layer.SetTileId(x, y, tileId);
            EditorUtility.SetDirty(Layout);
        }

        void ApplyBucket(RoomMapLayoutLayer layer, int startX, int startY)
        {
            var replacement = _selectedTileId;

            var target = layer.GetTileId(startX, startY);
            if (target == replacement)
                return;

            var width = layer.Width;
            var height = layer.Height;

            const int maxCells = 10_000;
            if (width * height > maxCells)
            {
                EditorUtility.DisplayDialog("RoomMap Bucket", $"FloodFill は {maxCells} cells 超では実行しません（現在 {width * height}）。", "OK");
                return;
            }

            Undo.RecordObject(Layout, "RoomMap Bucket");

            var visited = new bool[width * height];
            var q = new System.Collections.Generic.Queue<Vector2Int>(capacity: Math.Min(width * height, 256));
            q.Enqueue(new Vector2Int(startX, startY));

            while (q.Count > 0)
            {
                var p = q.Dequeue();

                if (p.x < 0 || p.x >= width || p.y < 0 || p.y >= height)
                    continue;

                var idx = p.y * width + p.x;
                if (idx < 0 || idx >= visited.Length)
                    continue;

                if (visited[idx])
                    continue;
                visited[idx] = true;

                if (layer.GetTileId(p.x, p.y) != target)
                    continue;

                layer.SetTileId(p.x, p.y, replacement);

                q.Enqueue(new Vector2Int(p.x + 1, p.y));
                q.Enqueue(new Vector2Int(p.x - 1, p.y));
                q.Enqueue(new Vector2Int(p.x, p.y + 1));
                q.Enqueue(new Vector2Int(p.x, p.y - 1));
            }

            EditorUtility.SetDirty(Layout);
        }

        void EnsureOptions(RoomMapTileRegistry registry)
        {
            if (registry == null)
            {
                _options = new[] { "<None>" };
                _optionTileIds = new[] { 0 };
                _selectedIndex = 0;
                _selectedTileId = 0;
                return;
            }

            if (_options != null && _optionTileIds != null && _optionTileIds.Length > 0 && _lastTileCount == registry.Count)
            {
                SyncSelectionToTileId();
                return;
            }

            _lastTileCount = registry.Count;

            var list = new System.Collections.Generic.List<(string path, int tileId)>();
            var nodes = registry.Nodes;
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n == null || n.IsFolder)
                        continue;
                    if (n.TileId <= 0)
                        continue;

                    var path = registry.GetDisplayPath(n.Id);
                    if (string.IsNullOrEmpty(path))
                        continue;
                    list.Add((path, n.TileId));
                }
            }

            list.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            var options = new string[Math.Max(1, list.Count + 1)];
            var ids = new int[Math.Max(1, list.Count + 1)];
            options[0] = "<None>";
            ids[0] = 0;
            for (int i = 0; i < list.Count; i++)
            {
                options[i + 1] = list[i].path.Replace("/", " › ");
                ids[i + 1] = list[i].tileId;
            }

            _options = options;
            _optionTileIds = ids;
            SyncSelectionToTileId();
        }

        void SyncSelectionToTileId()
        {
            var idx = 0;
            if (_optionTileIds != null)
            {
                for (int i = 0; i < _optionTileIds.Length; i++)
                {
                    if (_optionTileIds[i] == _selectedTileId)
                    {
                        idx = i;
                        break;
                    }
                }
            }

            _selectedIndex = idx;
            _selectedTileId = (_optionTileIds != null && idx >= 0 && idx < _optionTileIds.Length) ? _optionTileIds[idx] : 0;
        }

        void DrawGridCells(RoomMapLayoutLayer layer, RoomMapTileRegistry registry, Rect gridRect, int width, int height)
        {
            var e = Event.current;
            var interactiveRect = AdjustForScroll(gridRect);
            var mouseCell = new Vector2Int(-1, -1);
            if (e != null && interactiveRect.Contains(e.mousePosition))
                mouseCell = MouseToCell(e.mousePosition, interactiveRect, width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cellRect = new Rect(
                        gridRect.x + x * CellPx,
                        gridRect.y + y * CellPx,
                        CellPx,
                        CellPx);

                    var tileId = layer.GetTileId(x, y);
                    var bg = GUI.backgroundColor;
                    GUI.backgroundColor = GetCellColor(registry, tileId);

                    if (mouseCell.x == x && mouseCell.y == y)
                        GUI.backgroundColor = new Color(0.95f, 0.85f, 0.55f);

                    if (GUI.Button(cellRect, tileId.ToString(), EditorStyles.miniButton))
                    {
                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            if (_tool == PaintTool.Bucket)
                                ApplyBucket(layer, x, y);
                            else
                                ApplyPaint(layer, x, y, _tool == PaintTool.Eraser ? 0 : _selectedTileId);
                        }
                    }

                    GUI.backgroundColor = bg;
                }
            }
        }

        Rect AdjustForScroll(Rect gridRect)
        {
            return new Rect(gridRect.x - _gridScroll.x, gridRect.y - _gridScroll.y, gridRect.width, gridRect.height);
        }

        static Color GetCellColor(RoomMapTileRegistry? registry, int tileId)
        {
            if (tileId == 0)
                return new Color(0.85f, 0.85f, 0.85f);

            if (registry != null && registry.TryGetPaintColor(tileId, out var color))
            {
                color.a = 1f;
                return color;
            }

            return DeriveFallbackColor(tileId);
        }

        static Color DeriveFallbackColor(int tileId)
        {
            const float saturation = 0.65f;
            const float brightness = 0.95f;
            var fractional = Mathf.Repeat(tileId * 0.6180339887f, 1f);
            return Color.HSVToRGB(fractional, saturation, brightness);
        }
    }
}
