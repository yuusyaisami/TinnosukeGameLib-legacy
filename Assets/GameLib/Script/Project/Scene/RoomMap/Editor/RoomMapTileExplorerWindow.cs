#if UNITY_EDITOR
using System;
using Game.Editor.Registry;
using Game.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    public sealed class RoomMapTileExplorerWindow
        : RegistryExplorerWindowBase<RoomMapTileRegistry, RoomMapTileNode, RoomMapTileSettings>
    {
        RoomMapTileRegistry _registry;
        RoomMapTileSettings _settings;

        const string WindowTitle = "RoomMap Tile Explorer";

        [MenuItem("Tools/RoomMap Tiles/Explorer")]
        public static void OpenFromMenu()
        {
            var window = GetWindow<RoomMapTileExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(600, 420);
            window.Show();
            window.Focus();
        }

        protected override RoomMapTileRegistry GetRegistry()
        {
            if (_registry == null)
                _registry = RoomMapTileRegistryLocator.GetOrCreate();
            return _registry;
        }

        protected override RoomMapTileSettings GetSettings()
        {
            if (_settings == null)
                _settings = RoomMapTileCodeGenerator.FindOrCreateSettings();
            return _settings;
        }

        protected override Game.Editor.Foundation.PathTreeNode<RegistryNodeData<RoomMapTileNode>> BuildTree(RoomMapTileRegistry registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<RoomMapTileNode>
            {
                Node = node,
                DisplayPath = registry.GetDisplayPath(node.Id),
                IsFolder = node.IsFolder,
                HasKey = !node.IsFolder,
            });
        }

        protected override string GetKeyString(RoomMapTileNode node)
        {
            return GetRegistry()?.GetKeyString(node) ?? string.Empty;
        }

        protected override void DrawLeafDetail(RoomMapTileNode node)
        {
            var registry = GetRegistry();
            var path = registry.GetDisplayPath(node.Id);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Path", path);
                EditorGUILayout.LabelField("TileId", node.TileId.ToString());
                EditorGUILayout.Space();

                // StableKey
                EditorGUI.BeginChangeCheck();
                var newStable = EditorGUILayout.TextField("Stable Key", node.StableKey ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                {
                    var newKey = (newStable ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(newKey) && newKey != node.StableKey)
                    {
                        if (!string.IsNullOrEmpty(node.StableKey))
                            node.Aliases.Add(node.StableKey);
                        node.StableKey = newKey;
                        registry.EnsureLookupRebuild();
                        EditorUtility.SetDirty(registry);
                    }
                }

                // Aliases
                EditorGUILayout.LabelField("Aliases (one per line)");
                var aliasText = string.Join("\n", node.Aliases ?? new());
                EditorGUI.BeginChangeCheck();
                var newAliasText = EditorGUILayout.TextArea(aliasText, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck())
                {
                    node.Aliases.Clear();
                    var lines = (newAliasText ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var s = lines[i]?.Trim();
                        if (string.IsNullOrEmpty(s))
                            continue;
                        if (s == node.StableKey)
                            continue;
                        node.Aliases.Add(s);
                    }
                    registry.EnsureLookupRebuild();
                    EditorUtility.SetDirty(registry);
                }

                EditorGUILayout.Space();

                // Tags
                EditorGUI.BeginChangeCheck();
                var newTags = (RoomMapTileTagFlags)EditorGUILayout.EnumFlagsField("Tags", node.Tags);
                if (EditorGUI.EndChangeCheck())
                {
                    node.Tags = newTags;
                    EditorUtility.SetDirty(registry);
                }

                // Paint color (used by paint UIs)
                EditorGUI.BeginChangeCheck();
                var newColor = EditorGUILayout.ColorField("Paint Color", node.PaintColor);
                if (EditorGUI.EndChangeCheck())
                {
                    node.PaintColor = newColor;
                    EditorUtility.SetDirty(registry);
                }

                // Deprecated
                EditorGUI.BeginChangeCheck();
                var newDeprecated = EditorGUILayout.Toggle("Deprecated", node.Deprecated);
                if (EditorGUI.EndChangeCheck())
                {
                    node.Deprecated = newDeprecated;
                    EditorUtility.SetDirty(registry);
                }

                // Description
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Description");
                EditorGUI.BeginChangeCheck();
                var newDesc = EditorGUILayout.TextArea(node.Description ?? string.Empty, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck())
                {
                    node.Description = newDesc;
                    EditorUtility.SetDirty(registry);
                }
            }
        }

        protected override void DrawFolderDetail(RoomMapTileNode node)
        {
            var registry = GetRegistry();
            var path = registry.GetDisplayPath(node.Id);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Folder", path);
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Description");
                EditorGUI.BeginChangeCheck();
                var newDesc = EditorGUILayout.TextArea(node.Description ?? string.Empty, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck())
                {
                    node.Description = newDesc;
                    EditorUtility.SetDirty(registry);
                }
            }
        }

        protected override void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var newReg = (RoomMapTileRegistry)EditorGUILayout.ObjectField(
                    _registry, typeof(RoomMapTileRegistry), false, GUILayout.Width(240));
                if (EditorGUI.EndChangeCheck() && newReg != _registry)
                {
                    _registry = newReg;
                    RoomMapTileRegistryLocator.SetEditorOverride(newReg);
                    RequestRebuild();
                }

                _treeState.SearchText = EditorGUILayout.TextField(
                    _treeState.SearchText, EditorStyles.toolbarSearchField, GUILayout.Width(180));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RequestRebuild();

                if (GUILayout.Button("Generate Code", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    RoomMapTileCodeGenerator.Generate(GetRegistry(), GetSettings());
                }
            }
        }
    }
}
#endif
