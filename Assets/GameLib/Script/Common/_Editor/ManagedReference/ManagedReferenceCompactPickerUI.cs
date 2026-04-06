#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Game.Common.Editor
{
    internal static class ManagedReferenceCompactPickerUI
    {
        sealed class TypePickerNode
        {
            readonly List<TypePickerNode> _children = new();

            public TypePickerNode(string label, string path)
            {
                Label = label;
                Path = path;
            }

            public string Label { get; }
            public string Path { get; }
            public ManagedReferenceTypePickerItem? Item { get; private set; }
            public List<TypePickerNode> ChildrenInternal => _children;
            public IReadOnlyList<TypePickerNode> Children => _children;
            public bool HasItem => Item.HasValue;

            public void SetItem(ManagedReferenceTypePickerItem item)
            {
                Item = item;
            }
        }

        sealed class TypePickerPopup : PopupWindowContent
        {
            readonly Type? _currentType;
            readonly IReadOnlyList<ManagedReferenceTypePickerItem> _items;
            readonly Action<Type?> _onSelected;
            readonly bool _flattenRootFolder;
            readonly string? _rootFolderName;

            readonly List<ManagedReferenceTypePickerItem> _filtered = new();
            readonly List<TypePickerNode> _rootNodes = new();

            SearchField? _searchField;
            string _search = string.Empty;
            Vector2 _scroll;

            GUIStyle? _groupStyle;
            GUIStyle? _leafStyle;
            GUIStyle? _noneLeafStyle;
            Texture2D? _folderIcon;
            Texture2D? _selectedIcon;

            public TypePickerPopup(
                Type? currentType,
                IReadOnlyList<ManagedReferenceTypePickerItem> items,
                Action<Type?> onSelected)
            {
                _currentType = currentType;
                _items = items;
                _onSelected = onSelected;
                _rootFolderName = GetSingleRootFolderName(items);
                _flattenRootFolder = !string.IsNullOrEmpty(_rootFolderName);
            }

            public override Vector2 GetWindowSize() => new(380f, 300f);

            public override void OnOpen()
            {
                _searchField ??= new SearchField();
                RebuildFiltered();
            }

            public override void OnGUI(Rect rect)
            {
                EnsureStyles();

                DrawToolbar();

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawNoneRow();

                if (_filtered.Count == 0)
                {
                    var emptyRect = EditorGUILayout.GetControlRect(false, 24f);
                    EditorGUI.LabelField(emptyRect, string.IsNullOrWhiteSpace(_search) ? "No types available" : "No matching types", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    DrawNodes(_rootNodes, 0);
                }

                EditorGUILayout.EndScrollView();

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    editorWindow.Close();
                    Event.current.Use();
                }
            }

            void DrawToolbar()
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    var next = _searchField != null
                        ? _searchField.OnToolbarGUI(_search ?? string.Empty)
                        : EditorGUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField);

                    if (!string.Equals(next, _search, StringComparison.Ordinal))
                    {
                        _search = next;
                        RebuildFiltered();
                    }
                }
            }

            void DrawNodes(IReadOnlyList<TypePickerNode> nodes, int depth)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];

                    if (node.Children.Count > 0)
                    {
                        DrawGroupRow(node, depth);
                        DrawNodes(node.Children, depth + 1);
                    }

                    if (node.HasItem && node.Item.HasValue)
                        DrawLeafRow(node, depth);
                }
            }

            void DrawGroupRow(TypePickerNode node, int depth)
            {
                var rowRect = EditorGUILayout.GetControlRect(false, 22f);
                rowRect = IndentRect(rowRect, depth);
                var isHovered = rowRect.Contains(Event.current.mousePosition);
                DrawRowBackground(rowRect, false, isHovered);

                var folderRect = new Rect(rowRect.x + 8f, rowRect.y + 3f, 16f, 16f);
                if (_folderIcon != null)
                    GUI.DrawTexture(folderRect, _folderIcon, ScaleMode.ScaleToFit, true);

                var labelRect = new Rect(folderRect.xMax + 4f, rowRect.y + 1f, rowRect.width - 30f, rowRect.height - 2f);
                var content = new GUIContent(node.Label, node.Path);
                GUI.Label(labelRect, content, _groupStyle);
            }

            void DrawLeafRow(TypePickerNode node, int depth)
            {
                if (!node.Item.HasValue)
                    return;

                var item = node.Item.Value;
                var rowRect = EditorGUILayout.GetControlRect(false, 22f);
                rowRect = IndentRect(rowRect, depth);
                var isHovered = rowRect.Contains(Event.current.mousePosition);
                var isSelected = _currentType == item.Type;

                DrawRowBackground(rowRect, isSelected, isHovered);

                var labelRect = new Rect(rowRect.x + 8f, rowRect.y + 1f, rowRect.width - 34f, rowRect.height - 2f);
                var content = new GUIContent(node.Label, item.MenuPath);
                GUI.Label(labelRect, content, isSelected ? _groupStyle : _leafStyle);

                if (isSelected && _selectedIcon != null)
                {
                    var iconRect = new Rect(rowRect.xMax - 18f, rowRect.y + 3f, 16f, 16f);
                    GUI.DrawTexture(iconRect, _selectedIcon, ScaleMode.ScaleToFit, true);
                }

                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    _onSelected(item.Type);
                    editorWindow.Close();
                    GUI.changed = true;
                    Event.current.Use();
                }
            }

            void DrawNoneRow()
            {
                var rowRect = EditorGUILayout.GetControlRect(false, 22f);
                var isHovered = rowRect.Contains(Event.current.mousePosition);
                var isSelected = _currentType == null;

                DrawRowBackground(rowRect, isSelected, isHovered);

                var labelRect = new Rect(rowRect.x + 8f, rowRect.y + 1f, rowRect.width - 34f, rowRect.height - 2f);
                GUI.Label(labelRect, new GUIContent("None", "Clear binding type"), _noneLeafStyle);

                if (isSelected && _selectedIcon != null)
                {
                    var iconRect = new Rect(rowRect.xMax - 18f, rowRect.y + 3f, 16f, 16f);
                    GUI.DrawTexture(iconRect, _selectedIcon, ScaleMode.ScaleToFit, true);
                }

                if (Event.current.type == EventType.MouseDown
                    && Event.current.button == 0
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    _onSelected(null);
                    editorWindow.Close();
                    GUI.changed = true;
                    Event.current.Use();
                }
            }

            void RebuildFiltered()
            {
                _filtered.Clear();
                _rootNodes.Clear();

                var hasSearch = !string.IsNullOrWhiteSpace(_search);
                var query = hasSearch ? _search.Trim() : string.Empty;

                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (hasSearch
                        && !ContainsIgnoreCase(item.Label, query)
                        && !ContainsIgnoreCase(item.MenuPath, query))
                    {
                        continue;
                    }

                    _filtered.Add(item);
                }

                for (var i = 0; i < _filtered.Count; i++)
                    AddItemToTree(_filtered[i]);
            }

            void AddItemToTree(ManagedReferenceTypePickerItem item)
            {
                var segments = GetDisplaySegments(item.MenuPath, _flattenRootFolder, _rootFolderName);
                if (segments.Length == 0)
                    return;

                var parentNodes = _rootNodes;
                var path = string.Empty;

                for (var i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    path = string.IsNullOrEmpty(path) ? segment : $"{path}/{segment}";

                    var node = FindOrCreateNode(parentNodes, segment, path);
                    if (i == segments.Length - 1)
                    {
                        node.SetItem(item);
                        return;
                    }

                    parentNodes = node.ChildrenInternal;
                }
            }

            static TypePickerNode FindOrCreateNode(List<TypePickerNode> nodes, string label, string path)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (string.Equals(node.Label, label, StringComparison.Ordinal)
                        && string.Equals(node.Path, path, StringComparison.Ordinal))
                    {
                        return node;
                    }
                }

                var created = new TypePickerNode(label, path);
                nodes.Add(created);
                return created;
            }

            static bool ContainsIgnoreCase(string? value, string query)
            {
                return !string.IsNullOrEmpty(value)
                       && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            static Rect IndentRect(Rect rect, int depth)
            {
                var indent = depth * 16f;
                rect.x += indent;
                rect.width = Mathf.Max(0f, rect.width - indent);
                return rect;
            }

            static string[] GetDisplaySegments(string menuPath, bool flattenRootFolder, string? rootFolderName)
            {
                if (string.IsNullOrEmpty(menuPath))
                    return Array.Empty<string>();

                var segments = menuPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (!flattenRootFolder || string.IsNullOrEmpty(rootFolderName) || segments.Length == 0)
                    return segments;

                if (!string.Equals(segments[0], rootFolderName, StringComparison.Ordinal) || segments.Length == 1)
                    return segments;

                var flattened = new string[segments.Length - 1];
                Array.Copy(segments, 1, flattened, 0, flattened.Length);
                return flattened;
            }

            static string? GetSingleRootFolderName(IReadOnlyList<ManagedReferenceTypePickerItem> items)
            {
                string? root = null;
                var hasRoot = false;

                for (var i = 0; i < items.Count; i++)
                {
                    var path = items[i].MenuPath;
                    if (string.IsNullOrEmpty(path))
                        continue;

                    var separatorIndex = path.IndexOf('/');
                    var firstSegment = separatorIndex >= 0 ? path.Substring(0, separatorIndex) : path;

                    if (!hasRoot)
                    {
                        root = firstSegment;
                        hasRoot = true;
                        continue;
                    }

                    if (!string.Equals(root, firstSegment, StringComparison.Ordinal))
                        return null;
                }

                return hasRoot ? root : null;
            }

            static void DrawRowBackground(Rect rowRect, bool isSelected, bool isHovered)
            {
                if (!isSelected && !isHovered)
                    return;

                var fill = isSelected
                    ? (EditorGUIUtility.isProSkin
                        ? new Color(0.20f, 0.30f, 0.40f, 0.92f)
                        : new Color(0.76f, 0.86f, 0.96f, 1f))
                    : (EditorGUIUtility.isProSkin
                        ? new Color(1f, 1f, 1f, 0.05f)
                        : new Color(0f, 0f, 0f, 0.04f));

                EditorGUI.DrawRect(rowRect, fill);

                if (!isSelected)
                    return;

                var accent = EditorGUIUtility.isProSkin
                    ? new Color(0.36f, 0.61f, 0.88f, 1f)
                    : new Color(0.20f, 0.46f, 0.78f, 1f);
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 2f, rowRect.height), accent);
            }

            void EnsureStyles()
            {
                if (_leafStyle != null)
                    return;

                _leafStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip
                };

                _groupStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip
                };

                _noneLeafStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip,
                    fontStyle = FontStyle.Italic
                };

                _folderIcon = ResolveIcon(EditorGUIUtility.isProSkin ? "d_Folder Icon" : "Folder Icon");
                _selectedIcon = ResolveIcon(EditorGUIUtility.isProSkin ? "d_FilterSelectedOnly" : "FilterSelectedOnly");
            }

            static Texture2D? ResolveIcon(string iconName)
            {
                var icon = EditorGUIUtility.IconContent(iconName);
                return icon.image as Texture2D;
            }
        }

        public static void DrawTypeDropdownButton(
            Rect buttonRect,
            string buttonLabel,
            Type? currentType,
            IReadOnlyList<ManagedReferenceTypePickerItem> items,
            Action<Type?> onSelected)
        {
            if (!GUI.Button(buttonRect, buttonLabel, EditorStyles.popup))
                return;

            PopupWindow.Show(buttonRect, new TypePickerPopup(currentType, items, onSelected));
        }
    }
}
#endif
