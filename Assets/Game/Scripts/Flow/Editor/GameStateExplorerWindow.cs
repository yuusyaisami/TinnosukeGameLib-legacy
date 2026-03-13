#if UNITY_EDITOR
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.Actions.Editor
{
    /// <summary>
    /// GameStateRegistry を閲覧・編集するウィンドウ。
    /// </summary>
    public sealed class GameStateExplorerWindow
        : RegistryExplorerWindowBase<GameStateRegistry, GameStateNode, GameStateSettings>
    {
        GameStateRegistry _registry;
        GameStateSettings _settings;

        const string WindowTitle = "Game State Explorer";

        [MenuItem("Tools/Game State/Explorer")]
        public static void OpenFromMenu()
        {
            Open();
        }

        public static void Open()
        {
            ClearPickerMode();
            var window = GetWindow<GameStateExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }

        protected override GameStateRegistry GetRegistry()
        {
            if (_registry == null)
                _registry = GameStateRegistryLocator.GetOrCreate();
            return _registry;
        }

        protected override GameStateSettings GetSettings()
        {
            if (_settings == null)
                _settings = GameStateRegistryLocator.GetOrCreateSettings();
            return _settings;
        }

        protected override Game.Editor.Foundation.PathTreeNode<RegistryNodeData<GameStateNode>> BuildTree(GameStateRegistry registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<GameStateNode>
            {
                Node = node,
                DisplayPath = registry.GetDisplayPath(node.Id),
                IsFolder = node.IsFolder,
                HasKey = !node.IsFolder
            });
        }

        protected override string GetKeyString(GameStateNode node)
        {
            var registry = GetRegistry();
            return registry?.GetKeyString(node) ?? string.Empty;
        }

        protected override void DrawLeafDetail(GameStateNode node)
        {
            var registry = GetRegistry();
            var path = registry.GetDisplayPath(node.Id);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Path", path);
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();
                var newId = EditorGUILayout.IntField("State Id", node.StateId);
                if (EditorGUI.EndChangeCheck())
                {
                    node.StateId = newId;
                    EditorUtility.SetDirty(registry);
                }

                EditorGUILayout.LabelField("Description");
                EditorGUI.BeginChangeCheck();
                var newDesc = EditorGUILayout.TextArea(node.Description ?? string.Empty, GUILayout.Height(60));
                if (EditorGUI.EndChangeCheck())
                {
                    node.Description = newDesc;
                    EditorUtility.SetDirty(registry);
                }

                EditorGUILayout.LabelField("Note");
                EditorGUI.BeginChangeCheck();
                var newNote = EditorGUILayout.TextArea(node.Note ?? string.Empty, GUILayout.Height(40));
                if (EditorGUI.EndChangeCheck())
                {
                    node.Note = newNote;
                    EditorUtility.SetDirty(registry);
                }

                if (IsPickerMode)
                {
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Use This State", GUILayout.Height(24)))
                    {
                        ApplyKeyToTarget(node.Name ?? string.Empty);
                    }
                }
            }
        }

        protected override void DrawFolderDetail(GameStateNode node)
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
                var newReg = (GameStateRegistry)EditorGUILayout.ObjectField(
                    _registry, typeof(GameStateRegistry), false, GUILayout.Width(220));
                if (EditorGUI.EndChangeCheck() && newReg != _registry)
                {
                    _registry = newReg;
                    RequestRebuild();
                }

                _treeState.SearchText = EditorGUILayout.TextField(
                    _treeState.SearchText, EditorStyles.toolbarSearchField, GUILayout.Width(160));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RequestRebuild();

                if (GUILayout.Button("Generate Code", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    var settings = GameStateCodeGenerator.FindOrCreateSettings();
                    GameStateCodeGenerator.Generate(GetRegistry(), settings);
                }
            }
        }
    }
}
#endif
