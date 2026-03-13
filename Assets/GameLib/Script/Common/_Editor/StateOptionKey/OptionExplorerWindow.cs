#if UNITY_EDITOR
using System;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.StateMachine.Editor
{
    /// <summary>
    /// OptionRegistry を閲覧・編集するウィンドウ。
    /// フォルダ = OptionKey、リーフ = OptionValue として表示。
    /// </summary>
    public sealed class OptionExplorerWindow
        : RegistryExplorerWindowBase<OptionRegistry, OptionNode, OptionSettings>
    {
        OptionRegistry _registry;
        OptionSettings _settings;
        
        const string WindowTitle = "Option Explorer";
        
        [MenuItem("Tools/Options/Explorer")]
        public static void OpenFromMenu()
        {
            Open((SerializedProperty)null);
        }
        
        public static void Open(SerializedProperty targetStringProperty)
        {
            PreparePickerMode(targetStringProperty, null);
            var window = GetWindow<OptionExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }
        
        public static void Open(Action<string> callback)
        {
            PreparePickerMode(null, callback);
            var window = GetWindow<OptionExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(520, 360);
            window.Show();
            window.Focus();
        }
        
        protected override OptionRegistry GetRegistry()
        {
            if (_registry == null)
                _registry = OptionRegistryLocator.GetOrCreate();
            return _registry;
        }
        
        protected override OptionSettings GetSettings()
        {
            if (_settings == null)
                _settings = OptionRegistryLocator.GetOrCreateSettings();
            return _settings;
        }
        
        protected override Game.Editor.Foundation.PathTreeNode<RegistryNodeData<OptionNode>> BuildTree(OptionRegistry registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<OptionNode>
            {
                Node = node,
                DisplayPath = registry.GetDisplayPath(node.Id),
                IsFolder = node.IsFolder,
                HasKey = true  // フォルダ（OptionKey）もリーフ（OptionValue）もキーを持つ
            });
        }
        
        protected override string GetKeyString(OptionNode node)
        {
            var registry = GetRegistry();
            return registry?.GetKeyString(node) ?? string.Empty;
        }
        
        protected override void DrawLeafDetail(OptionNode node)
        {
            var registry = GetRegistry();
            var key = GetKeyString(node);
            var path = registry.GetDisplayPath(node.Id);
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Type", "OptionValue (Leaf)");
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
                
                // IsDefault
                EditorGUI.BeginChangeCheck();
                var newIsDefault = EditorGUILayout.Toggle("Is Default", node.IsDefault);
                if (EditorGUI.EndChangeCheck())
                {
                    node.IsDefault = newIsDefault;
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
        
        protected override void DrawFolderDetail(OptionNode node)
        {
            var registry = GetRegistry();
            var key = GetKeyString(node);
            var path = registry.GetDisplayPath(node.Id);
            
            // フォルダ（OptionKey）もキーを持つ
            var bgColor = node.IsGlobal ? new Color(0.2f, 0.4f, 0.6f, 0.3f) : GUI.backgroundColor;
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            
            using (new EditorGUILayout.VerticalScope("box"))
            {
                GUI.backgroundColor = oldBg;
                
                EditorGUILayout.LabelField("Type", "OptionKey (Folder)");
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
                
                // IsGlobal
                EditorGUI.BeginChangeCheck();
                var newIsGlobal = EditorGUILayout.Toggle("Is Global", node.IsGlobal);
                if (EditorGUI.EndChangeCheck())
                {
                    node.IsGlobal = newIsGlobal;
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
                
                // OptionKey も選択可能（ピッカーモード時）
                if (IsPickerMode)
                {
                    if (GUILayout.Button("Use This OptionKey", GUILayout.Height(24)))
                    {
                        ApplyKeyToTarget(key);
                    }
                }
            }
        }
        
        protected override void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Registry フィールド
                EditorGUI.BeginChangeCheck();
                var newReg = (OptionRegistry)EditorGUILayout.ObjectField(
                    _registry, typeof(OptionRegistry), false, GUILayout.Width(200));
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
                    var settings = OptionCodeGenerator.FindOrCreateSettings();
                    OptionCodeGenerator.Generate(GetRegistry(), settings);
                }
            }
        }
    }
}
#endif
