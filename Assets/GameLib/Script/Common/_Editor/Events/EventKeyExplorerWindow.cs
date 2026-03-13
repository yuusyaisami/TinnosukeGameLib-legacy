#if UNITY_EDITOR
using System;
using Game.Editor.Registry;
using Game.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.EventKey.Editor
{
    /// <summary>
    /// EventKeyRegistry を閲覧・編集し、選択したキーを SerializedProperty(string) に適用するウィンドウ。
    /// </summary>
    public sealed class EventKeyExplorerWindow
        : RegistryExplorerWindowBase<EventKeyRegistry, EventKeyNode, EventKeySettings>
    {
        EventKeyRegistry _registry;
        EventKeySettings _settings;

        const string WindowTitle = "Event Key Explorer";

        // ------------------------------------------------------------
        // Static Open Methods
        // ------------------------------------------------------------

        [MenuItem("Tools/Event Keys/Explorer")]
        public static void OpenFromMenu()
        {
            Open((SerializedProperty)null);
        }

        public static void Open(SerializedProperty targetStringProperty)
        {
            PreparePickerMode(targetStringProperty, null);
            var window = GetWindow<EventKeyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }

        public static void Open(Action<string> callback)
        {
            PreparePickerMode(null, callback);
            var window = GetWindow<EventKeyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }

        // ------------------------------------------------------------
        // Override: Registry / Settings
        // ------------------------------------------------------------

        protected override EventKeyRegistry GetRegistry()
        {
            if (_registry == null)
                _registry = EventKeyRegistryLocator.GetOrCreate();
            return _registry;
        }

        protected override EventKeySettings GetSettings()
        {
            if (_settings == null)
                _settings = EventKeyRegistryLocator.GetOrCreateSettings();
            return _settings;
        }

        // ------------------------------------------------------------
        // Override: Tree Building
        // ------------------------------------------------------------

        protected override Game.Editor.Foundation.PathTreeNode<RegistryNodeData<EventKeyNode>> BuildTree(EventKeyRegistry registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<EventKeyNode>
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

        protected override string GetKeyString(EventKeyNode node)
        {
            var registry = GetRegistry();
            return registry?.GetKeyString(node) ?? string.Empty;
        }

        // ------------------------------------------------------------
        // Override: Detail Pane
        // ------------------------------------------------------------

        protected override void DrawLeafDetail(EventKeyNode node)
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

        protected override void DrawFolderDetail(EventKeyNode node)
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
                var newReg = (EventKeyRegistry)EditorGUILayout.ObjectField(
                    _registry, typeof(EventKeyRegistry), false, GUILayout.Width(200));
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
                    var settings = EventKeyCodeGenerator.FindOrCreateSettings();
                    EventKeyCodeGenerator.Generate(GetRegistry(), settings);
                }
            }
        }
    }
}
#endif
