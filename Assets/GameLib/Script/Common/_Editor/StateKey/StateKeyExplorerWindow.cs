#if UNITY_EDITOR
using System;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.StateMachine.Editor
{
    /// <summary>
    /// StateKeyRegistry を閲覧・編集し、選択したキーを SerializedProperty(string) に適用するウィンドウ。
    /// </summary>
    public sealed class StateKeyExplorerWindow
        : RegistryExplorerWindowBase<StateKeyRegistry, StateKeyNode, StateKeySettings>
    {
        StateKeyRegistry _registry;
        StateKeySettings _settings;
        
        const string WindowTitle = "State Key Explorer";
        
        [MenuItem("Tools/State Keys/Explorer")]
        public static void OpenFromMenu()
        {
            Open((SerializedProperty)null);
        }
        
        public static void Open(SerializedProperty targetStringProperty)
        {
            PreparePickerMode(targetStringProperty, null);
            var window = GetWindow<StateKeyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }
        
        public static void Open(Action<string> callback)
        {
            PreparePickerMode(null, callback);
            var window = GetWindow<StateKeyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }
        
        protected override StateKeyRegistry GetRegistry()
        {
            if (_registry == null)
                _registry = StateKeyRegistryLocator.GetOrCreate();
            return _registry;
        }
        
        protected override StateKeySettings GetSettings()
        {
            if (_settings == null)
                _settings = StateKeyRegistryLocator.GetOrCreateSettings();
            return _settings;
        }
        
        protected override Game.Editor.Foundation.PathTreeNode<RegistryNodeData<StateKeyNode>> BuildTree(StateKeyRegistry registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<StateKeyNode>
            {
                Node = node,
                DisplayPath = registry.GetDisplayPath(node.Id),
                IsFolder = node.IsFolder,
                HasKey = !node.IsFolder
            });
        }
        
        protected override string GetKeyString(StateKeyNode node)
        {
            var registry = GetRegistry();
            return registry?.GetKeyString(node) ?? string.Empty;
        }
        
        protected override void DrawLeafDetail(StateKeyNode node)
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
                
                // Note
                EditorGUILayout.LabelField("Note");
                EditorGUI.BeginChangeCheck();
                var newNote = EditorGUILayout.TextArea(node.Note ?? string.Empty, GUILayout.Height(40));
                if (EditorGUI.EndChangeCheck())
                {
                    node.Note = newNote;
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
        
        protected override void DrawFolderDetail(StateKeyNode node)
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
        
        protected override void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Registry フィールド
                EditorGUI.BeginChangeCheck();
                var newReg = (StateKeyRegistry)EditorGUILayout.ObjectField(
                    _registry, typeof(StateKeyRegistry), false, GUILayout.Width(200));
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
                    var settings = StateKeyCodeGenerator.FindOrCreateSettings();
                    StateKeyCodeGenerator.Generate(GetRegistry(), settings);
                }
            }
        }
    }
}
#endif
