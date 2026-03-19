#if UNITY_EDITOR
using System;
using Game.Editor.Foundation;
using Game.Editor.Registry;
using Game.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// MaterialFxPropertyRegistry を閲覧・編集し、選択したキーを SerializedProperty(string) に適用するウィンドウ。
    /// </summary>
    public sealed class MaterialFxPropertyExplorerWindow
        : RegistryExplorerWindowBase<MaterialFxPropertyRegistrySO, MaterialFxPropertyNode, MaterialFxSettings>
    {
        MaterialFxPropertyRegistrySO _registry;
        MaterialFxSettings _settings;

        const string WindowTitle = "MaterialFx Property Explorer";

        // ------------------------------------------------------------
        // Static Open Methods
        // ------------------------------------------------------------

        [MenuItem("Tools/MaterialFx/Explorer")]
        public static void OpenFromMenu()
        {
            Open((SerializedProperty)null);
        }

        public static void Open(SerializedProperty targetStringProperty)
        {
            PreparePickerMode(targetStringProperty, null);
            var window = GetWindow<MaterialFxPropertyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(600, 400);
            window.Show();
            window.Focus();
        }

        public static void Open(Action<string> callback)
        {
            PreparePickerMode(null, callback);
            var window = GetWindow<MaterialFxPropertyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(600, 400);
            window.Show();
            window.Focus();
        }

        // ------------------------------------------------------------
        // Override: Registry / Settings
        // ------------------------------------------------------------

        protected override MaterialFxPropertyRegistrySO GetRegistry()
        {
            if (_registry == null)
                _registry = MaterialFxPropertyRegistryLocator.GetOrCreate();
            return _registry;
        }

        protected override MaterialFxSettings GetSettings()
        {
            if (_settings == null)
                _settings = MaterialFxPropertyRegistryLocator.GetOrCreateSettings();
            return _settings;
        }

        // ------------------------------------------------------------
        // Override: Tree Building
        // ------------------------------------------------------------

        protected override PathTreeNode<RegistryNodeData<MaterialFxPropertyNode>> BuildTree(MaterialFxPropertyRegistrySO registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<MaterialFxPropertyNode>
            {
                Node = node,
                DisplayPath = registry.GetDisplayPath(node.Id),
                IsFolder = node.IsFolder,
                HasKey = !node.IsFolder && !string.IsNullOrEmpty(node.StableKey)
            });
        }

        // ------------------------------------------------------------
        // Override: Tree Config (Sender ごとの行色)
        // ------------------------------------------------------------

        protected override void ConfigureTreeConfig(TreeExplorerConfig<RegistryNodeData<MaterialFxPropertyNode>> config)
        {
            base.ConfigureTreeConfig(config);

            // Sender ごとの行背景色を設定
            config.GetRowBackground = pathNode =>
            {
                var node = pathNode.Entry.Node;
                if (node == null || node.IsFolder)
                    return null;

                var settings = GetSettings();
                return settings?.GetSenderColor(node.Sender);
            };
        }

        // ------------------------------------------------------------
        // Override: Key String
        // ------------------------------------------------------------

        protected override string GetKeyString(MaterialFxPropertyNode node)
        {
            return node?.StableKey ?? string.Empty;
        }

        // ------------------------------------------------------------
        // Override: Detail Pane
        // ------------------------------------------------------------

        protected override void DrawLeafDetail(MaterialFxPropertyNode node)
        {
            var registry = GetRegistry();
            var settings = GetSettings();
            var displayPath = registry.GetDisplayPath(node.Id);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("DisplayPath", displayPath);
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();

                node.Sender = (MaterialFxSenderKind)EditorGUILayout.EnumPopup("Sender", node.Sender);
                node.ValueType = (ValueKind)EditorGUILayout.EnumPopup("ValueType", node.ValueType);
                node.ShaderPropertyName = EditorGUILayout.TextField("ShaderPropertyName", node.ShaderPropertyName ?? string.Empty);
                node.StableKey = EditorGUILayout.TextField("StableKey", node.StableKey ?? string.Empty);

                // EnumDefinition dropdown (Int/Float のみ)
                if (node.ValueType == ValueKind.Int || node.ValueType == ValueKind.Float)
                {
                    DrawEnumDefinitionDropdown(node, settings);
                }

                // Range settings (Float のみ)
                if (node.ValueType == ValueKind.Float)
                {
                    DrawRangeSettings(node);
                }

                EditorGUILayout.LabelField("Description");
                node.Description = EditorGUILayout.TextArea(node.Description ?? string.Empty, GUILayout.Height(60));

                if (EditorGUI.EndChangeCheck())
                {
                    // empty stableKey は自動補完
                    if (string.IsNullOrEmpty(node.StableKey))
                        node.StableKey = registry.ComputeDefaultStableKey(node);

                    EditorUtility.SetDirty(registry);
                }

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Regenerate StableKey"))
                    {
                        node.StableKey = registry.ComputeDefaultStableKey(node);
                        EditorUtility.SetDirty(registry);
                    }

                    GUILayout.FlexibleSpace();

                    if (IsPickerMode)
                    {
                        if (GUILayout.Button("Use This Key", GUILayout.Height(22)))
                        {
                            ApplyKeyToTarget(node.StableKey);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// EnumDefinition のドロップダウンを描画する。
        /// </summary>
        void DrawEnumDefinitionDropdown(MaterialFxPropertyNode node, MaterialFxSettings settings)
        {
            var catalog = settings?.EnumCatalog;
            if (catalog == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("EnumDefinition");
                    EditorGUILayout.HelpBox("EnumCatalog not set in Settings", MessageType.Info);
                }
                return;
            }

            var options = catalog.GetDropdownOptions();
            var currentIndex = catalog.GetDropdownIndex(node.EnumDefinition);

            var newIndex = EditorGUILayout.Popup("EnumDefinition", currentIndex, options);
            if (newIndex != currentIndex)
            {
                node.EnumDefinition = catalog.GetDefinitionFromDropdownIndex(newIndex);
            }

            // 選択中の EnumDefinition の情報を表示
            if (node.EnumDefinition != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Entries:", EditorStyles.miniLabel);
                var entries = node.EnumDefinition.Entries;
                for (int i = 0; i < Mathf.Min(entries.Count, 5); i++)
                {
                    EditorGUILayout.LabelField($"  [{i}] {entries[i].name}", EditorStyles.miniLabel);
                }
                if (entries.Count > 5)
                {
                    EditorGUILayout.LabelField($"  ... and {entries.Count - 5} more", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Range 設定を描画する（Float 時のみ）。
        /// </summary>
        void DrawRangeSettings(MaterialFxPropertyNode node)
        {
            node.RangeEnabled = EditorGUILayout.Toggle("Range Enabled", node.RangeEnabled);

            if (node.RangeEnabled)
            {
                EditorGUI.indentLevel++;
                var minMax = node.RangeMinMax;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Range (Min, Max)");
                    minMax.x = EditorGUILayout.FloatField(minMax.x, GUILayout.Width(60));
                    EditorGUILayout.LabelField("~", GUILayout.Width(15));
                    minMax.y = EditorGUILayout.FloatField(minMax.y, GUILayout.Width(60));
                }

                // Validate: min should be less than max
                if (minMax.x > minMax.y)
                {
                    EditorGUILayout.HelpBox("Min should be less than or equal to Max", MessageType.Warning);
                }

                node.RangeMinMax = minMax;
                EditorGUI.indentLevel--;
            }
        }

        protected override void DrawFolderDetail(MaterialFxPropertyNode node)
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
                var newReg = (MaterialFxPropertyRegistrySO)EditorGUILayout.ObjectField(
                    _registry, typeof(MaterialFxPropertyRegistrySO), false, GUILayout.Width(200));
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
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    _registry = MaterialFxPropertyRegistryLocator.ReloadRegistry();
                    RequestRebuild();
                }

                if (GUILayout.Button("Generate Code", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    var settings = MaterialFxPropertyCodeGenerator.FindOrCreateSettings();
                    MaterialFxPropertyCodeGenerator.Generate(GetRegistry(), settings);
                }
            }
        }

        // ------------------------------------------------------------
        // Override: Custom Context Menu
        // ------------------------------------------------------------

        protected override void AddCustomContextMenuItems(GenericMenu menu, RegistryNodeData<MaterialFxPropertyNode> nodeData)
        {
            if (nodeData.Node != null && !nodeData.Node.IsFolder)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Regenerate StableKey"), false, () =>
                {
                    var registry = GetRegistry();
                    nodeData.Node.StableKey = registry.ComputeDefaultStableKey(nodeData.Node);
                    EditorUtility.SetDirty(registry);
                });
            }
        }
    }
}
#endif
