#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Foundation
{
    public enum TreeDropPosition
    {
        Inside,
        Before,
        After
    }

    /// <summary>繝・Μ繝ｼ陦ｨ遉ｺ縺ｮ繝薙ず繝･繧｢繝ｫ險ｭ螳壹・/summary>
    [Serializable]
    public class TreeVisualSettings
    {
        [Tooltip("陦後・鬮倥＆")]
        public float RowHeight = 18f;

        [Tooltip("Inspector setting.")]
        public float IndentWidth = 16f;

        [Tooltip("繧｢繧､繧ｳ繝ｳ繧ｵ繧､繧ｺ縺ｮ豈皮紫 (RowHeight 縺ｫ蟇ｾ縺吶ｋ蛟咲紫)")]
        [Range(0.5f, 1f)]
        public float IconSizeRatio = 0.78f;

        [Tooltip("Inspector setting.")]
        public float DragHoldSeconds = 0.25f;

        [Tooltip("驕ｸ謚櫁｡後・閭梧勹濶ｲ")]
        public Color SelectionColor = new(0.24f, 0.48f, 0.90f, 0.45f);

        [Tooltip("繝峨Ο繝・・繝上う繝ｩ繧､繝医・閭梧勹濶ｲ")]
        public Color DropHighlightColor = new(0.35f, 0.55f, 0.95f, 0.32f);

        [Tooltip("繝峨Ο繝・・繝ｩ繧､繝ｳ縺ｮ濶ｲ")]
        public Color DropLineColor = new(0.35f, 0.75f, 1f, 0.8f);

        /// <summary>險育ｮ玲ｸ医∩縺ｮ繧｢繧､繧ｳ繝ｳ繧ｵ繧､繧ｺ</summary>
        public float IconSize => RowHeight * IconSizeRatio;

        /// <summary>Foldout 縺ｮ蟷・/summary>
        public float FoldoutWidth => 12f;

        /// <summary>Foldout 縺ｨ繧｢繧､繧ｳ繝ｳ縺ｮ髢馴囈</summary>
        public float IconPadding => 4f;

        /// <summary>繝・ヵ繧ｩ繝ｫ繝郁ｨｭ螳壹ｒ霑斐☆</summary>
        public static TreeVisualSettings Default => new();
    }

    public sealed class TreeExplorerConfig<TEntry>
    {
        // 繝薙ず繝･繧｢繝ｫ險ｭ螳・
        public TreeVisualSettings Visual = new();

        // 繝ｩ繝吶Ν繝ｻ繧｢繧､繧ｳ繝ｳ蜿門ｾ・
        public Func<PathTreeNode<TEntry>, string> GetLabel;
        public Func<PathTreeNode<TEntry>, string> GetTooltip;
        public Func<PathTreeNode<TEntry>, bool> IsContainer;
        public Func<PathTreeNode<TEntry>, Texture> GetIcon;
        public Func<PathTreeNode<TEntry>, string, bool> MatchesSearch;

        // 陦後・閭梧勹濶ｲ・・ull 縺ｪ繧峨↑縺暦ｼ・
        public Func<PathTreeNode<TEntry>, Color?> GetRowBackground;

        // 繧､繝吶Φ繝医さ繝ｼ繝ｫ繝舌ャ繧ｯ
        public Action<PathTreeNode<TEntry>> OnSelected;
        public Action<PathTreeNode<TEntry>> OnDoubleClick;
        public Action<PathTreeNode<TEntry>, Vector2> OnContextClick;

        // 繝峨Λ繝・げ・・ラ繝ｭ繝・・
        public Func<PathTreeNode<TEntry>, bool> CanDrag;
        public Func<PathTreeNode<TEntry>, PathTreeNode<TEntry>, TreeDropPosition, bool> CanDrop;
        public Action<PathTreeNode<TEntry>, PathTreeNode<TEntry>, TreeDropPosition> OnDrop;
    }

    /// <summary>豎守畑繝・Μ繝ｼ繝薙Η繝ｼ縺ｮ IMGUI 螳溯｣・・/summary>
    public static class TreeExplorerGUI
    {
        struct Row<TEntry>
        {
            public PathTreeNode<TEntry> Node;
            public int Indent;
        }

        public static void DrawTree<TEntry>(
            Rect rect,
            PathTreeNode<TEntry> root,
            TreeExplorerState state,
            TreeExplorerConfig<TEntry> config)
        {
            if (root == null || config == null)
                return;

            var v = config.Visual ?? TreeVisualSettings.Default;

            var rows = new List<Row<TEntry>>(256);
            if (string.IsNullOrEmpty(state.SearchText))
                CollectVisible(root, 0, rows, state, config);
            else
                CollectSearch(root, 0, rows, state, config);

            float contentHeight = Mathf.Max(rows.Count * v.RowHeight, 10f);
            var viewRect = new Rect(0, 0, rect.width - 16f, contentHeight);

            state.Scroll = GUI.BeginScrollView(rect, state.Scroll, viewRect);

            var e = Event.current;
            bool hitAnyRow = false;
            float y = 0f;
            var nodeLookup = new Dictionary<string, PathTreeNode<TEntry>>(rows.Count, StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (!string.IsNullOrEmpty(r.Node?.FullPath))
                    nodeLookup[r.Node.FullPath] = r.Node;
            }

            var now = EditorApplication.timeSinceStartup;
            PathTreeNode<TEntry> draggingNode = null;
            if (!string.IsNullOrEmpty(state.DraggingPath))
                nodeLookup.TryGetValue(state.DraggingPath, out draggingNode);
            state.DropHoverPath = null;
            state.DropHoverPosition = TreeDropPosition.Inside;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowRect = new Rect(0, y, viewRect.width, v.RowHeight);
                y += v.RowHeight;

                var node = row.Node;
                bool isContainer = config.IsContainer?.Invoke(node) ?? node.Children.Count > 0;
                bool isCollapsed = state.IsCollapsed(node.FullPath);
                bool isSelected = string.Equals(state.SelectedPath, node.FullPath, StringComparison.Ordinal);

                // 陦瑚レ譎ｯ・磯∈謚槭ｈ繧雁・縺ｫ謠上￥・・
                var bg = config.GetRowBackground?.Invoke(node);
                if (bg.HasValue)
                    EditorGUI.DrawRect(rowRect, bg.Value);

                float x = row.Indent * v.IndentWidth;
                Rect foldRect = default;

                // Foldout繧ｯ繝ｪ繝・け繧貞・縺ｫ蜃ｦ逅・ｼ郁｡碁∈謚槭ｈ繧雁━蜈茨ｼ・
                if (isContainer)
                {
                    foldRect = new Rect(x, rowRect.y, v.FoldoutWidth, rowRect.height);
                    if (e.type == EventType.MouseDown &&
                        e.button == 0 &&
                        foldRect.Contains(e.mousePosition))
                    {
                        state.Toggle(node.FullPath);
                        e.Use();
                    }
                }

                if (rowRect.Contains(e.mousePosition))
                {
                    hitAnyRow = true;

                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        state.SelectedPath = node.FullPath;
                        config.OnSelected?.Invoke(node);
                        if (e.clickCount == 2)
                            config.OnDoubleClick?.Invoke(node);
                        state.PendingDragPath = node.FullPath;
                        state.PendingDragStartTime = now;
                        state.MouseDownPosition = e.mousePosition;
                        state.DropHoverPath = null;
                        e.Use();
                    }
                    else if (e.type == EventType.MouseUp && e.button == 1)
                    {
                        state.SelectedPath = node.FullPath;
                        config.OnSelected?.Invoke(node);
                        config.OnContextClick?.Invoke(node, e.mousePosition + rect.position);
                        e.Use();
                    }
                }

                // 髟ｷ謚ｼ縺励ラ繝ｩ繝・げ縺ｮ髢句ｧ句愛螳・
                if (e.type == EventType.MouseDrag &&
                    string.IsNullOrEmpty(state.DraggingPath) &&
                    !string.IsNullOrEmpty(state.PendingDragPath) &&
                    state.PendingDragPath == node.FullPath &&
                    (now - state.PendingDragStartTime) >= v.DragHoldSeconds &&
                    Vector2.Distance(e.mousePosition, state.MouseDownPosition) > 4f &&
                    (config.CanDrag?.Invoke(node) ?? true))
                {
                    state.DraggingPath = state.PendingDragPath;
                    state.PendingDragPath = null;
                    draggingNode = node;
                    e.Use();
                }

                if (isSelected)
                {
                    EditorGUI.DrawRect(rowRect, v.SelectionColor);
                }

                if (isContainer)
                {
                    EditorGUI.Foldout(foldRect, !isCollapsed, GUIContent.none);
                }

                // Foldout(縺ゅｋ縺・・謚倥ｊ逡ｳ縺ｿ縺ｮ遨ｺ縺榊ｹ・ + 蟆代＠縺ｮ菴咏區繧帝ｲ繧√∽ｸ芽ｧ偵→繧｢繧､繧ｳ繝ｳ縺ｮ霍晞屬繧呈純縺医ｋ
                x += v.FoldoutWidth + v.IconPadding;

                Texture icon = config.GetIcon?.Invoke(node);
                if (icon != null)
                {
                    var iconSize = v.IconSize;
                    var iconRect = new Rect(
                        x,
                        rowRect.y + (v.RowHeight - iconSize) * 0.5f,
                        iconSize,
                        iconSize);
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                    x += iconSize + 2f;
                }

                var labelRect = new Rect(x + 2f, rowRect.y, rowRect.width - x - 4f, rowRect.height);
                var label = config.GetLabel?.Invoke(node) ?? node.Segment ?? "(null)";
                var tooltip = config.GetTooltip?.Invoke(node);
                var content = string.IsNullOrEmpty(tooltip) ? new GUIContent(label) : new GUIContent(label, tooltip);
                EditorGUI.LabelField(labelRect, content);

                // 繝峨Ο繝・・蟇ｾ雎｡繝上う繝ｩ繧､繝医ｒ險倬鹸 (陦後・荳・荳ｭ/荳九〒謖ｿ蜈･菴咲ｽｮ繧貞愛譁ｭ)
                if (!string.IsNullOrEmpty(state.DraggingPath) &&
                    draggingNode != null &&
                    rowRect.Contains(e.mousePosition))
                {
                    var dropPos = ComputeDropPosition(rowRect, e.mousePosition, v);
                    if (config.CanDrop?.Invoke(draggingNode, node, dropPos) ?? true)
                    {
                        state.DropHoverPath = node.FullPath;
                        state.DropHoverPosition = dropPos;
                    }
                }

                if (!string.IsNullOrEmpty(state.DropHoverPath) &&
                    state.DropHoverPath == node.FullPath &&
                    !string.IsNullOrEmpty(state.DraggingPath))
                {
                    DrawDropMarker(rowRect, state.DropHoverPosition, row.Indent, v);
                }
            }

            GUI.EndScrollView();

            // 繝峨Ο繝・・蜃ｦ逅・
            if (e.type == EventType.MouseUp && e.button == 0 && !string.IsNullOrEmpty(state.DraggingPath))
            {
                nodeLookup.TryGetValue(state.DraggingPath, out draggingNode);
                PathTreeNode<TEntry> dropNode = null;
                if (!string.IsNullOrEmpty(state.DropHoverPath))
                    nodeLookup.TryGetValue(state.DropHoverPath, out dropNode);

                bool canDrop = draggingNode != null &&
                               (config.CanDrop?.Invoke(draggingNode, dropNode, state.DropHoverPosition) ?? true);
                if (canDrop)
                {
                    config.OnDrop?.Invoke(draggingNode, dropNode, state.DropHoverPosition);
                    e.Use();
                }

                state.ClearDrag();
            }

            if (!hitAnyRow)
            {
                var e2 = Event.current;
                if (e2.type == EventType.MouseUp && e2.button == 1 && rect.Contains(e2.mousePosition))
                {
                    config.OnContextClick?.Invoke(null, e2.mousePosition + rect.position);
                    e2.Use();
                }
            }
        }

        static void CollectVisible<TEntry>(
            PathTreeNode<TEntry> node,
            int indent,
            List<Row<TEntry>> rows,
            TreeExplorerState state,
            TreeExplorerConfig<TEntry> config)
        {
            if (!string.IsNullOrEmpty(node.FullPath))
            {
                rows.Add(new Row<TEntry> { Node = node, Indent = indent });
            }

            bool isContainer = config.IsContainer?.Invoke(node) ?? node.Children.Count > 0;
            if (!isContainer || state.IsCollapsed(node.FullPath))
                return;

            for (int i = 0; i < node.Children.Count; i++)
            {
                CollectVisible(node.Children[i], indent + 1, rows, state, config);
            }
        }

        static void CollectSearch<TEntry>(
            PathTreeNode<TEntry> node,
            int indent,
            List<Row<TEntry>> rows,
            TreeExplorerState state,
            TreeExplorerConfig<TEntry> config)
        {
            bool match = config.MatchesSearch?.Invoke(node, state.SearchText) ?? false;
            if (!string.IsNullOrEmpty(node.FullPath) && match)
            {
                rows.Add(new Row<TEntry> { Node = node, Indent = indent });
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                CollectSearch(node.Children[i], indent + 1, rows, state, config);
            }
        }

        static TreeDropPosition ComputeDropPosition(Rect rowRect, Vector2 mousePos, TreeVisualSettings v)
        {
            float relY = mousePos.y - rowRect.y;
            float top = v.RowHeight * 0.25f;
            float bottom = v.RowHeight * 0.75f;
            if (relY < top) return TreeDropPosition.Before;
            if (relY > bottom) return TreeDropPosition.After;
            return TreeDropPosition.Inside;
        }

        static void DrawDropMarker(Rect rowRect, TreeDropPosition pos, int indent, TreeVisualSettings v)
        {
            float x = rowRect.x + (indent * v.IndentWidth) + v.FoldoutWidth + v.IconPadding + v.IconSize;
            float width = rowRect.width - x;

            switch (pos)
            {
                case TreeDropPosition.Before:
                    DrawLine(x, rowRect.y - 1f, width, 2f, v.DropLineColor);
                    break;
                case TreeDropPosition.After:
                    DrawLine(x, rowRect.yMax - 1f, width, 2f, v.DropLineColor);
                    break;
                default:
                    EditorGUI.DrawRect(rowRect, v.DropHighlightColor);
                    break;
            }
        }

        static void DrawLine(float x, float y, float width, float height, Color color)
        {
            var rect = new Rect(x, y, width, height);
            EditorGUI.DrawRect(rect, color);
        }
    }
}
#endif
