#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Editor.Foundation;
using Game.Editor.Registry;
using Game.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.Scalar.Editor
{
    /// <summary>
    /// ScalarKeyRegistry を閲覧・編集し、選択したキーを SerializedProperty(string) に適用するウィンドウ。
    /// </summary>
    public sealed class ScalarKeyExplorerWindow
        : RegistryExplorerWindowBase<ScalarKeyRegistry, ScalarKeyNode, ScalarKeySettings>
    {
        ScalarKeyRegistry _registry;
        ScalarKeySettings _settings;

        const string WindowTitle = "Scalar Key Explorer";

        // ------------------------------------------------------------
        // Static Open Methods
        // ------------------------------------------------------------

        [MenuItem("Tools/Scalar Keys/Explorer")]
        public static void OpenFromMenu()
        {
            Open((SerializedProperty)null);
        }

        public static void Open(SerializedProperty targetStringProperty)
        {
            PreparePickerMode(targetStringProperty, null);
            var window = GetWindow<ScalarKeyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }

        public static void Open(Action<string> callback)
        {
            PreparePickerMode(null, callback);
            var window = GetWindow<ScalarKeyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }

        // ------------------------------------------------------------
        // Override: Registry / Settings
        // ------------------------------------------------------------

        protected override ScalarKeyRegistry GetRegistry()
        {
            if (_registry == null)
                _registry = ScalarKeyRegistryLocator.GetOrCreate();
            return _registry;
        }

        protected override ScalarKeySettings GetSettings()
        {
            if (_settings == null)
                _settings = ScalarKeyRegistryLocator.GetOrCreateSettings();
            return _settings;
        }

        // ------------------------------------------------------------
        // Override: Tree Building
        // ------------------------------------------------------------

        protected override PathTreeNode<RegistryNodeData<ScalarKeyNode>> BuildTree(ScalarKeyRegistry registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<ScalarKeyNode>
            {
                Node = node,
                DisplayPath = registry.GetDisplayPath(node.Id),
                IsFolder = node.IsFolder,
                HasKey = !node.IsFolder
            });
        }

        // ------------------------------------------------------------
        // Override: Key String
        // ------------------------------------------------------------

        protected override string GetKeyString(ScalarKeyNode node)
        {
            var registry = GetRegistry();
            return registry?.GetKeyString(node) ?? string.Empty;
        }

        // ------------------------------------------------------------
        // Override: Detail Pane
        // ------------------------------------------------------------

        protected override void DrawLeafDetail(ScalarKeyNode node)
        {
            var registry = GetRegistry();
            var key = GetKeyString(node);
            var path = registry.GetDisplayPath(node.Id);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Path", path);
                EditorGUILayout.LabelField("Key", key);
                EditorGUILayout.Space();

                // ExplicitKey
                EditorGUI.BeginChangeCheck();
                var newExplicit = EditorGUILayout.TextField("Explicit Key", node.ExplicitKey ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                {
                    node.ExplicitKey = newExplicit;
                    EditorUtility.SetDirty(registry);
                }

                // Description
                EditorGUILayout.LabelField("Description");
                EditorGUI.BeginChangeCheck();
                var newDesc = EditorGUILayout.TextArea(node.Description ?? string.Empty, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck())
                {
                    node.Description = newDesc;
                    EditorUtility.SetDirty(registry);
                }

                // Obsolete
                EditorGUI.BeginChangeCheck();
                var newObsolete = EditorGUILayout.Toggle("Obsolete", node.Obsolete);
                if (EditorGUI.EndChangeCheck())
                {
                    node.Obsolete = newObsolete;
                    EditorUtility.SetDirty(registry);
                }

                // Tags
                EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
                var tags = node.Tags ?? Array.Empty<string>();
                for (int i = 0; i < tags.Length; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        tags[i] = EditorGUILayout.TextField(tags[i]);
                        if (EditorGUI.EndChangeCheck())
                        {
                            node.Tags = tags;
                            EditorUtility.SetDirty(registry);
                        }
                        if (GUILayout.Button("-", GUILayout.Width(25)))
                        {
                            var list = new List<string>(tags);
                            list.RemoveAt(i);
                            node.Tags = list.ToArray();
                            EditorUtility.SetDirty(registry);
                            break;
                        }
                    }
                }
                if (GUILayout.Button("Add Tag"))
                {
                    var list = new List<string>(tags) { string.Empty };
                    node.Tags = list.ToArray();
                    EditorUtility.SetDirty(registry);
                }

                EditorGUILayout.Space();

                if (IsPickerMode)
                {
                    if (GUILayout.Button("Use This Key", GUILayout.Height(24)))
                    {
                        ApplyKeyToTarget(key);
                    }
                }
            }
        }

        protected override void DrawFolderDetail(ScalarKeyNode node)
        {
            var registry = GetRegistry();
            var path = registry.GetDisplayPath(node.Id);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Folder", path);
                EditorGUILayout.Space();

                // Description
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

        // ------------------------------------------------------------
        // Override: Header (Code Gen Button)
        // ------------------------------------------------------------

        protected override void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Registry フィールド
                EditorGUI.BeginChangeCheck();
                var newReg = (ScalarKeyRegistry)EditorGUILayout.ObjectField(
                    _registry, typeof(ScalarKeyRegistry), false, GUILayout.Width(200));
                if (EditorGUI.EndChangeCheck() && newReg != _registry)
                {
                    _registry = newReg;
                    RequestRebuild();
                }

                // 検索
                _treeState.SearchText = EditorGUILayout.TextField(
                    _treeState.SearchText, EditorStyles.toolbarSearchField, GUILayout.Width(160));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RequestRebuild();

                if (GUILayout.Button("Generate Code", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    var settings = ScalarKeyCodeGenerator.FindOrCreateSettings();
                    ScalarKeyCodeGenerator.Generate(GetRegistry(), settings);
                }
            }
        }
    }
}
#endif
