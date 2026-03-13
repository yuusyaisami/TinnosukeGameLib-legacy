#nullable enable
using System;
using System.Collections.Generic;
using Game.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    [CustomPropertyDrawer(typeof(RoomMapAutoTilePattern))]
    public sealed class RoomMapAutoTilePatternDrawer : PropertyDrawer
    {
        sealed class BrushState
        {
            public GridPaintIMGUI.Tool Tool = GridPaintIMGUI.Tool.Pen;
            public RoomMapAutoTileCondKind Kind = RoomMapAutoTileCondKind.Any;
            public int TileId;
            public RoomMapTileTagFlags Tag;
            public Vector2Int LastPainted = new(int.MinValue, int.MinValue);
        }

        static readonly Dictionary<int, BrushState> BrushByKey = new();

        static BrushState GetBrush(SerializedProperty property)
        {
            var key = property.serializedObject.targetObject.GetInstanceID() ^ property.propertyPath.GetHashCode();
            if (!BrushByKey.TryGetValue(key, out var brush) || brush == null)
            {
                brush = new BrushState();
                BrushByKey[key] = brush;
            }

            return brush;
        }

        const float CellPx = 34f;
        const float Pad = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;
            // label + tool row + param row + grid
            return line + Pad + line + Pad + line + Pad + (CellPx * 3f) + Pad;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = EditorGUIUtility.singleLineHeight;

            var headerRect = new Rect(position.x, position.y, position.width, line);
            EditorGUI.LabelField(headerRect, label);

            var condsProp = property.FindPropertyRelative("conds");
            if (condsProp == null)
            {
                EditorGUI.HelpBox(position, "conds が見つかりません。", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            if (condsProp.arraySize != 9)
                condsProp.arraySize = 9;

            var brush = GetBrush(property);

            var toolRect = new Rect(position.x, headerRect.yMax + Pad, position.width, line);
            DrawToolRow(toolRect, brush);

            var paramRect = new Rect(position.x, toolRect.yMax + Pad, position.width, line);
            DrawBrushParams(paramRect, brush);

            var gridRect = new Rect(position.x, paramRect.yMax + Pad, CellPx * 3f, CellPx * 3f);

            var registry = RoomMapTileRegistryLocator.GetOrCreate();

            // Paint handling (mouse down/drag)
            if (GridPaintIMGUI.HandlePaintEvents(
                gridRect,
                width: 3,
                height: 3,
                ref brush.LastPainted,
                paint: (x, y) =>
                {
                    Undo.RecordObject(property.serializedObject.targetObject, "RoomMap AutoTile Pattern Paint");

                    var paintValue = ToCond(brush, paintForEraser: brush.Tool == GridPaintIMGUI.Tool.Eraser);

                    GridPaintIMGUI.ApplyTool(
                        brush.Tool,
                        x,
                        y,
                        width: 3,
                        height: 3,
                        get: (gx, gy) => ReadCond(condsProp, gx, gy),
                        set: (gx, gy, v) => WriteCond(condsProp, gx, gy, v),
                        equals: EqualsCond,
                        paintValue: paintValue);

                    property.serializedObject.ApplyModifiedProperties();
                }))
                GUI.changed = true;

            DrawGrid(gridRect, condsProp, registry);

            EditorGUI.EndProperty();
        }

        static void DrawToolRow(Rect rect, BrushState brush)
        {
            var toolRect = rect;
            toolRect.width = rect.width * 0.46f;

            var kindRect = rect;
            kindRect.xMin = toolRect.xMax + 6f;

            brush.Tool = (GridPaintIMGUI.Tool)GUI.Toolbar(toolRect, (int)brush.Tool, new[] { "Pen", "Eraser", "Bucket" });
            brush.Kind = (RoomMapAutoTileCondKind)EditorGUI.EnumPopup(kindRect, brush.Kind);
        }

        static void DrawBrushParams(Rect rect, BrushState brush)
        {
            switch (brush.Kind)
            {
                case RoomMapAutoTileCondKind.ExactTileId:
                    {
                        DrawTileIdPopup(rect, brush);
                        break;
                    }
                case RoomMapAutoTileCondKind.HasTag:
                    {
                        brush.Tag = (RoomMapTileTagFlags)EditorGUI.EnumFlagsField(rect, "Tag", brush.Tag);
                        break;
                    }
                default:
                    {
                        EditorGUI.LabelField(rect, " ");
                        break;
                    }
            }
        }

        static void DrawTileIdPopup(Rect rect, BrushState brush)
        {
            var registry = RoomMapTileRegistryLocator.GetOrCreate();
            BuildTileOptions(registry, out var options, out var tileIds);

            var currentIdx = 0;
            for (int i = 0; i < tileIds.Length; i++)
            {
                if (tileIds[i] == brush.TileId)
                {
                    currentIdx = i;
                    break;
                }
            }

            var next = EditorGUI.Popup(rect, "Tile", currentIdx, options);
            brush.TileId = (uint)next < (uint)tileIds.Length ? tileIds[next] : 0;
        }

        static void BuildTileOptions(RoomMapTileRegistry registry, out string[] options, out int[] tileIds)
        {
            // Small allocation is fine (3x3 editor); keep it simple.
            var nodes = registry.Nodes;
            var list = new List<(int tileId, string path)>(nodes != null ? nodes.Count : 0);

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
                    list.Add((n.TileId, path ?? string.Empty));
                }
            }

            list.Sort((a, b) =>
            {
                var c = string.Compare(a.path, b.path, StringComparison.Ordinal);
                return c != 0 ? c : a.tileId.CompareTo(b.tileId);
            });

            options = new string[list.Count + 1];
            tileIds = new int[list.Count + 1];
            options[0] = "<None>";
            tileIds[0] = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var id = list[i].tileId;
                tileIds[i + 1] = id;
                var p = list[i].path;
                options[i + 1] = !string.IsNullOrEmpty(p) ? p.Replace("/", " › ") : $"TileId {id}";
            }
        }

        static void ApplyBrush(SerializedProperty cellProp, BrushState brush)
        {
            var kindProp = cellProp.FindPropertyRelative("kind");
            var tileIdProp = cellProp.FindPropertyRelative("tileId");
            var tagProp = cellProp.FindPropertyRelative("tag");

            if (kindProp == null || tileIdProp == null || tagProp == null)
                return;

            // legacy helper (unused)

            kindProp.enumValueIndex = (int)brush.Kind;
            switch (brush.Kind)
            {
                case RoomMapAutoTileCondKind.ExactTileId:
                    tileIdProp.intValue = brush.TileId;
                    tagProp.intValue = 0;
                    break;
                case RoomMapAutoTileCondKind.HasTag:
                    tileIdProp.intValue = 0;
                    tagProp.intValue = (int)brush.Tag;
                    break;
                default:
                    tileIdProp.intValue = 0;
                    tagProp.intValue = 0;
                    break;
            }
        }

        static void DrawGrid(Rect gridRect, SerializedProperty condsProp, RoomMapTileRegistry registry)
        {
            // Background
            EditorGUI.DrawRect(gridRect, new Color(0f, 0f, 0f, 0.08f));

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    var idx = y * 3 + x;
                    var cellProp = condsProp.GetArrayElementAtIndex(idx);
                    var rect = new Rect(gridRect.x + x * CellPx, gridRect.y + y * CellPx, CellPx - 1f, CellPx - 1f);

                    var (bg, text) = GetCellStyle(cellProp, registry);
                    EditorGUI.DrawRect(rect, bg);

                    // Center cell outline for readability
                    if (x == 1 && y == 1)
                    {
                        var outline = rect;
                        outline.x += 0.5f;
                        outline.y += 0.5f;
                        outline.width -= 1f;
                        outline.height -= 1f;
                        Handles.DrawSolidRectangleWithOutline(outline, Color.clear, new Color(0f, 0f, 0f, 0.35f));
                    }

                    GUI.Label(rect, text, EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        static (Color bg, string text) GetCellStyle(SerializedProperty cellProp, RoomMapTileRegistry registry)
        {
            var kindProp = cellProp.FindPropertyRelative("kind");
            var tileIdProp = cellProp.FindPropertyRelative("tileId");
            var tagProp = cellProp.FindPropertyRelative("tag");

            var kind = kindProp != null ? (RoomMapAutoTileCondKind)kindProp.enumValueIndex : RoomMapAutoTileCondKind.Any;
            var tileId = tileIdProp != null ? tileIdProp.intValue : 0;
            var tag = tagProp != null ? (RoomMapTileTagFlags)tagProp.intValue : 0;

            switch (kind)
            {
                case RoomMapAutoTileCondKind.Any:
                    return (new Color(0.90f, 0.90f, 0.90f, 1f), "Any");
                case RoomMapAutoTileCondKind.OutOfBounds:
                    return (new Color(0.80f, 0.80f, 0.80f, 1f), "OOB");
                case RoomMapAutoTileCondKind.ExactTileId:
                    {
                        var name = registry.TryGetDisplayPath(tileId, out var path) && !string.IsNullOrEmpty(path)
                            ? Shorten(path)
                            : $"{tileId}";
                        var color = registry.TryGetPaintColor(tileId, out var c) ? c : new Color(0.62f, 0.80f, 0.95f, 1f);
                        // keep readable (force alpha)
                        color.a = 1f;
                        return (color, name);
                    }
                case RoomMapAutoTileCondKind.HasTag:
                    return (new Color(0.70f, 0.92f, 0.70f, 1f), tag == 0 ? "Tag" : tag.ToString());
                case RoomMapAutoTileCondKind.SameAsCenter:
                    return (new Color(0.95f, 0.88f, 0.60f, 1f), "=");
                case RoomMapAutoTileCondKind.DifferentFromCenter:
                    return (new Color(0.95f, 0.70f, 0.70f, 1f), "!=");
                default:
                    return (new Color(0.90f, 0.90f, 0.90f, 1f), kind.ToString());
            }
        }

        static string Shorten(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var i = path.LastIndexOf('/');
            var leaf = i >= 0 ? path[(i + 1)..] : path;
            if (leaf.Length <= 6)
                return leaf;
            return leaf.Substring(0, 6);
        }

        readonly struct CondValue
        {
            public readonly RoomMapAutoTileCondKind Kind;
            public readonly int TileId;
            public readonly RoomMapTileTagFlags Tag;

            public CondValue(RoomMapAutoTileCondKind kind, int tileId, RoomMapTileTagFlags tag)
            {
                Kind = kind;
                TileId = tileId;
                Tag = tag;
            }
        }

        static CondValue ToCond(BrushState brush, bool paintForEraser)
        {
            if (paintForEraser)
                return new CondValue(RoomMapAutoTileCondKind.Any, 0, 0);

            switch (brush.Kind)
            {
                case RoomMapAutoTileCondKind.ExactTileId:
                    return new CondValue(RoomMapAutoTileCondKind.ExactTileId, brush.TileId, 0);
                case RoomMapAutoTileCondKind.HasTag:
                    return new CondValue(RoomMapAutoTileCondKind.HasTag, 0, brush.Tag);
                case RoomMapAutoTileCondKind.OutOfBounds:
                    return new CondValue(RoomMapAutoTileCondKind.OutOfBounds, 0, 0);
                case RoomMapAutoTileCondKind.SameAsCenter:
                    return new CondValue(RoomMapAutoTileCondKind.SameAsCenter, 0, 0);
                case RoomMapAutoTileCondKind.DifferentFromCenter:
                    return new CondValue(RoomMapAutoTileCondKind.DifferentFromCenter, 0, 0);
                default:
                    return new CondValue(RoomMapAutoTileCondKind.Any, 0, 0);
            }
        }

        static CondValue ReadCond(SerializedProperty condsProp, int x, int y)
        {
            var idx = y * 3 + x;
            if ((uint)idx >= 9u)
                return default;

            var cellProp = condsProp.GetArrayElementAtIndex(idx);
            var kindProp = cellProp.FindPropertyRelative("kind");
            var tileIdProp = cellProp.FindPropertyRelative("tileId");
            var tagProp = cellProp.FindPropertyRelative("tag");
            var kind = kindProp != null ? (RoomMapAutoTileCondKind)kindProp.enumValueIndex : RoomMapAutoTileCondKind.Any;
            var tileId = tileIdProp != null ? tileIdProp.intValue : 0;
            var tag = tagProp != null ? (RoomMapTileTagFlags)tagProp.intValue : 0;
            return new CondValue(kind, tileId, tag);
        }

        static void WriteCond(SerializedProperty condsProp, int x, int y, CondValue v)
        {
            var idx = y * 3 + x;
            if ((uint)idx >= 9u)
                return;

            var cellProp = condsProp.GetArrayElementAtIndex(idx);
            var kindProp = cellProp.FindPropertyRelative("kind");
            var tileIdProp = cellProp.FindPropertyRelative("tileId");
            var tagProp = cellProp.FindPropertyRelative("tag");
            if (kindProp == null || tileIdProp == null || tagProp == null)
                return;

            kindProp.enumValueIndex = (int)v.Kind;
            tileIdProp.intValue = v.TileId;
            tagProp.intValue = (int)v.Tag;
        }

        static bool EqualsCond(CondValue a, CondValue b)
        {
            return a.Kind == b.Kind && a.TileId == b.TileId && a.Tag == b.Tag;
        }
    }
}
