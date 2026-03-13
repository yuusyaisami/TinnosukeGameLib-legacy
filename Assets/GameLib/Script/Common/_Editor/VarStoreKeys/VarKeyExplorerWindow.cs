#if UNITY_EDITOR
using System;
using Game.Editor.Registry;
using Game.Registry;
using Game.VarStoreKeys;
using UnityEditor;
using UnityEngine;

namespace Game.VarStoreKeys.Editor
{
    /// <summary>
    /// VarKeyRegistry を閲覧・編集するウィンドウ。
    /// </summary>
    public sealed class VarKeyExplorerWindow
        : RegistryExplorerWindowBase<VarKeyRegistry, VarKeyNode, VarKeySettings>
    {
        VarKeyRegistry _registry;
        VarKeySettings _settings;

        const string WindowTitle = "Var Key Explorer";

        [MenuItem("Tools/Var Keys/Explorer")]
        public static void OpenFromMenu()
        {
            var window = GetWindow<VarKeyExplorerWindow>(WindowTitle, true);
            window.minSize = new Vector2(560, 380);
            window.Show();
            window.Focus();
        }

        protected override VarKeyRegistry GetRegistry()
        {
            if (_registry == null)
                _registry = VarKeyRegistryLocator.GetOrCreate();
            return _registry;
        }

        protected override VarKeySettings GetSettings()
        {
            if (_settings == null)
                _settings = VarKeyCodeGenerator.FindOrCreateSettings();
            return _settings;
        }

        protected override Game.Editor.Foundation.PathTreeNode<RegistryNodeData<VarKeyNode>> BuildTree(VarKeyRegistry registry)
        {
            return BuildTreeFromRegistry(registry, node => new RegistryNodeData<VarKeyNode>
            {
                Node = node,
                DisplayPath = registry.GetDisplayPath(node.Id),
                IsFolder = node.IsFolder,
                HasKey = !node.IsFolder
            });
        }

        protected override string GetKeyString(VarKeyNode node)
        {
            return GetRegistry()?.GetKeyString(node) ?? string.Empty;
        }

        protected override void DrawLeafDetail(VarKeyNode node)
        {
            var registry = GetRegistry();
            var path = registry.GetDisplayPath(node.Id);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Path", path);
                EditorGUILayout.LabelField("VarId", node.VarId.ToString());
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

                // Aliases (simple list)
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

        protected override void DrawFolderDetail(VarKeyNode node)
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
                var newReg = (VarKeyRegistry)EditorGUILayout.ObjectField(
                    _registry, typeof(VarKeyRegistry), false, GUILayout.Width(220));
                if (EditorGUI.EndChangeCheck() && newReg != _registry)
                {
                    _registry = newReg;
                    RequestRebuild();
                }

                _treeState.SearchText = EditorGUILayout.TextField(
                    _treeState.SearchText, EditorStyles.toolbarSearchField, GUILayout.Width(180));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RequestRebuild();

                if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    var csv = GetRegistry()?.ExportToCsv(includeHeader: true);
                    if (!string.IsNullOrEmpty(csv))
                    {
                        var path = EditorUtility.SaveFilePanel("Export Var Keys CSV", Application.dataPath, "VarKeys", "csv");
                        if (!string.IsNullOrEmpty(path))
                        {
                            System.IO.File.WriteAllText(path, csv, System.Text.Encoding.UTF8);
                            AssetDatabase.Refresh();
                        }
                    }
                }

                if (GUILayout.Button("Import CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    var path = EditorUtility.OpenFilePanel("Import Var Keys CSV", Application.dataPath, "csv");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var text = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
                        GetRegistry()?.ImportFromCsv(text, hasHeader: true);
                        EditorUtility.SetDirty(GetRegistry());
                        RequestRebuild();
                    }
                }

                if (GUILayout.Button("Generate Code", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    VarKeyCodeGenerator.Generate(GetRegistry(), GetSettings());
                }
            }
        }
    }
}
#endif

