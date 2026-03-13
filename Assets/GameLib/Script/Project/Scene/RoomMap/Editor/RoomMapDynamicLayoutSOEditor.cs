#nullable enable
using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    [CustomEditor(typeof(RoomMapDynamicLayoutSO))]
    public sealed class RoomMapDynamicLayoutSOEditor : OdinEditor
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

        Vector2 _gridScroll;
        Vector2Int _lastPainted = new Vector2Int(int.MinValue, int.MinValue);

        string[] _options = Array.Empty<string>();
        int[] _optionTileIds = Array.Empty<int>();
        int _lastTileCount = -1;

        SerializedProperty? _entriesProp;

        RoomMapDynamicLayoutSO Layout => (RoomMapDynamicLayoutSO)target;

        protected override void OnEnable()
        {
            base.OnEnable();
            _entriesProp = serializedObject.FindProperty("entries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var tree = Tree;
            tree.UpdateTree();
            foreach (var child in tree.RootProperty.Children)
            {
                if (child.Name == "entries") continue;
                child.Draw();
            }
            tree.ApplyChanges();

            EditorGUILayout.Space(6);
            DrawPaintTools();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                EditorGUILayout.LabelField("Entries (Dynamic)", EditorStyles.boldLabel);
                if (_entriesProp == null)
                {
                    EditorGUILayout.HelpBox("entries プロパティが見つかりません。", MessageType.Error);
                }
                else
                {
                    serializedObject.ApplyModifiedProperties();
                    base.OnInspectorGUI();
                }
            }
        }

        void DrawPaintTools()
        {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                EditorGUILayout.LabelField("Dynamic Paint", EditorStyles.boldLabel);

                if (_entriesProp == null)
                {
                    EditorGUILayout.HelpBox("entries プロパティが見つかりません。", MessageType.Error);
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
                    DrawGrid(Layout, registry);
            }
        }

        void DrawGrid(RoomMapDynamicLayoutSO layout, RoomMapTileRegistry registry)
        {
            var width = Mathf.Max(1, layout.Width);
            var height = Mathf.Max(1, layout.Height);

            var contentW = width * CellPx + GridPadding * 2f;
            var contentH = height * CellPx + GridPadding * 2f;

            var viewH = Mathf.Min(420f, contentH + 12f);

            _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUILayout.Height(viewH));
            var rect = GUILayoutUtility.GetRect(contentW, contentH);
            rect = new Rect(rect.x + GridPadding, rect.y + GridPadding, width * CellPx, height * CellPx);

            HandleGridEvents(layout, registry, rect, width, height);
            DrawGridCells(layout, registry, rect, width, height);

            EditorGUILayout.EndScrollView();
        }

        void HandleGridEvents(RoomMapDynamicLayoutSO layout, RoomMapTileRegistry registry, Rect gridRect, int width, int height)
        {
            var e = Event.current;
            if (e == null)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_tool == PaintTool.Bucket && width * height > 10_000)
            {
                EditorUtility.DisplayDialog("RoomMap Bucket", $"FloodFill は 10000 cells 超では実行しません（現在 {width * height}）。", "OK");
                return;
            }

            var tool = (Game.EditorTools.GridPaintIMGUI.Tool)(int)_tool;
            var paintValue = _tool == PaintTool.Eraser ? 0 : _selectedTileId;

            var interactiveRect = AdjustForScroll(gridRect);
            if (Game.EditorTools.GridPaintIMGUI.HandlePaintEvents(interactiveRect, width, height, ref _lastPainted, (x, y) =>
                {
                    Undo.RecordObject(Layout, "RoomMap Dynamic Paint");

                    Game.EditorTools.GridPaintIMGUI.ApplyTool(
                        tool,
                        x,
                        y,
                        width,
                        height,
                        get: (gx, gy) => GetTileIdAtCell(_entriesProp, gx, gy),
                        set: (sx, sy, v) => SetTileIdAtCell(_entriesProp, sx, sy, v),
                        equals: (a, b) => a == b,
                        paintValue: paintValue);

                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(Layout);
                }))
            {
                GUI.changed = true;
            }
        }

        void DrawGridCells(RoomMapDynamicLayoutSO layout, RoomMapTileRegistry registry, Rect gridRect, int width, int height)
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

                    var tileId = GetTileIdAtCell(_entriesProp, x, y);
                    var bg = GUI.backgroundColor;
                    GUI.backgroundColor = GetCellColor(registry, tileId);

                    if (mouseCell.x == x && mouseCell.y == y)
                        GUI.backgroundColor = new Color(0.95f, 0.85f, 0.55f);

                    if (GUI.Button(cellRect, tileId.ToString(), EditorStyles.miniButton))
                    {
                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            if (_tool == PaintTool.Bucket && width * height > 10_000)
                            {
                                EditorUtility.DisplayDialog("RoomMap Bucket", $"FloodFill は 10000 cells 超では実行しません（現在 {width * height}）。", "OK");
                                return;
                            }

                            Undo.RecordObject(Layout, "RoomMap Dynamic Paint");
                            var paintValue = _tool == PaintTool.Eraser ? 0 : _selectedTileId;
                            Game.EditorTools.GridPaintIMGUI.ApplyTool(
                                (Game.EditorTools.GridPaintIMGUI.Tool)(int)_tool,
                                x,
                                y,
                                width,
                                height,
                                get: (gx, gy) => GetTileIdAtCell(_entriesProp, gx, gy),
                                set: (sx, sy, v) => SetTileIdAtCell(_entriesProp, sx, sy, v),
                                equals: (a, b) => a == b,
                                paintValue: paintValue);

                            serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(Layout);
                        }
                    }

                    GUI.backgroundColor = bg;
                }
            }
        }

        static Vector2Int MouseToCell(Vector2 mouse, Rect gridRect, int width, int height)
        {
            var local = mouse - new Vector2(gridRect.x, gridRect.y);
            var x = Mathf.FloorToInt(local.x / CellPx);
            var y = Mathf.FloorToInt(local.y / CellPx);
            return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(y, 0, height - 1));
        }

        static int GetTileIdAtCell(SerializedProperty? entriesProp, int x, int y)
        {
            if (entriesProp == null)
                return 0;

            var target = new Vector2Int(x, y);
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                if (entry == null)
                    continue;

                var cellProp = entry.FindPropertyRelative("Cell");
                if (cellProp == null)
                    continue;
                if (cellProp.vector2IntValue != target)
                    continue;

                var tileProp = entry.FindPropertyRelative("TileId");
                return tileProp != null ? Mathf.Max(0, tileProp.intValue) : 0;
            }
            return 0;
        }

        static void SetTileIdAtCell(SerializedProperty? entriesProp, int x, int y, int tileId)
        {
            if (entriesProp == null)
                return;

            var target = new Vector2Int(x, y);
            var entryIndex = FindEntryIndex(entriesProp, target);
            if (tileId <= 0)
            {
                if (entryIndex >= 0)
                    entriesProp.DeleteArrayElementAtIndex(entryIndex);
                return;
            }

            SerializedProperty? entryProp;
            if (entryIndex >= 0)
            {
                entryProp = entriesProp.GetArrayElementAtIndex(entryIndex);
            }
            else
            {
                var insert = entriesProp.arraySize;
                entriesProp.arraySize++;
                entryProp = entriesProp.GetArrayElementAtIndex(insert);
            }

            if (entryProp == null)
                return;

            var tileProp = entryProp.FindPropertyRelative("TileId");
            var cellProp = entryProp.FindPropertyRelative("Cell");
            var offsetProp = entryProp.FindPropertyRelative("LocalOffset");
            var rotProp = entryProp.FindPropertyRelative("RotationDegZ");
            var scaleProp = entryProp.FindPropertyRelative("Scale");

            if (tileProp != null)
                tileProp.intValue = tileId;
            if (cellProp != null)
                cellProp.vector2IntValue = target;
            if (offsetProp != null && offsetProp.vector3Value == default)
                offsetProp.vector3Value = Vector3.zero;
            if (rotProp != null && Math.Abs(rotProp.floatValue) < 0.0001f)
                rotProp.floatValue = 0f;
            if (scaleProp != null && scaleProp.vector3Value == default)
                scaleProp.vector3Value = Vector3.one;
        }

        static int FindEntryIndex(SerializedProperty entriesProp, Vector2Int cell)
        {
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                var cellProp = entry?.FindPropertyRelative("Cell");
                if (cellProp == null)
                    continue;
                if (cellProp.vector2IntValue == cell)
                    return i;
            }
            return -1;
        }

        Rect AdjustForScroll(Rect gridRect)
        {
            return new Rect(gridRect.x - _gridScroll.x, gridRect.y - _gridScroll.y, gridRect.width, gridRect.height);
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
