#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Editor.CodeGen;
using Game.Editor.Registry;
using Game.VarStoreKeys;
using UnityEditor;
using UnityEngine;

namespace Game.VarStoreKeys.Editor
{
    /// <summary>
    /// VarKeyRegistry から varId 定数クラスを生成する。
    /// </summary>
    public sealed class VarKeyCodeGenerator : TreeCodeGeneratorBase<VarKeySettings>
    {
        static VarKeyCodeGenerator _instance;
        static VarKeyCodeGenerator Instance => _instance ??= new VarKeyCodeGenerator();

        VarKeyRegistry _registry;

        const string MenuPath = "Tools/Var Keys/Generate";

        [MenuItem(MenuPath, priority = 10000)]
        public static void GenerateFromMenu()
        {
            var registry = VarKeyRegistryLocator.GetOrCreate();
            var settings = FindOrCreateSettings();
            Generate(registry, settings);
        }

        public static void Generate(VarKeyRegistry registry, VarKeySettings settings)
        {
            if (registry == null)
            {
                Debug.LogError("[VarKeyCodeGenerator] Registry is null.");
                return;
            }

            Instance._registry = registry;
            Instance.Generate(settings);
        }

        public static VarKeySettings FindOrCreateSettings()
        {
            return FindOrCreateSettings<VarKeySettings>(
                "Assets/GameLib/SO/Variable/VarKeySettings.asset",
                "VarKeyCodeGenerator");
        }

        protected override string GeneratorName => "VarKeyCodeGenerator";
        protected override string DefaultNamespace => "Game.Vars.Generated";
        protected override string DefaultRootClassName => "VarIds";
        protected override string DefaultOutputPath => "Assets/GameLib/Script/Generated/VarIds.g.cs";

        protected override string GetAllArrayType() => "int[]";
        protected override string GetAllArrayName() => "AllVarIds";

        protected override CodeGenTreeNode BuildTree()
        {
            if (_registry == null)
                return new CodeGenTreeNode();

            var root = new CodeGenTreeNode();

            var parentMap = new Dictionary<string, List<VarKeyNode>>(StringComparer.Ordinal);
            foreach (var n in _registry.Nodes)
            {
                if (n == null) continue;
                var pid = n.ParentId ?? string.Empty;
                if (!parentMap.TryGetValue(pid, out var list))
                {
                    list = new List<VarKeyNode>();
                    parentMap[pid] = list;
                }
                list.Add(n);
            }

            CodeGenTreeNode BuildChildren(VarKeyNode node, string parentPath)
            {
                var seg = node.Name ?? string.Empty;
                var path = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";

                var treeNode = new CodeGenTreeNode
                {
                    Node = new VarKeyNodeAdapter(node, path),
                    Path = path
                };

                if (parentMap.TryGetValue(node.Id, out var children))
                {
                    children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    foreach (var c in children)
                    {
                        treeNode.Children.Add(BuildChildren(c, path));
                    }
                }

                return treeNode;
            }

            if (parentMap.TryGetValue(string.Empty, out var roots))
            {
                roots.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                foreach (var r in roots)
                {
                    root.Children.Add(BuildChildren(r, string.Empty));
                }
            }

            return root;
        }

        protected override void EmitFieldDeclaration(System.Text.StringBuilder sb, int indent, string fieldName, ICodeGenNode node)
        {
            var tabs = new string(' ', indent);
            EmitNodeComment(sb, indent, node);

            if (node is VarKeyNodeAdapter adapter)
            {
                if (adapter.IsFolder)
                    return;

                sb.AppendLine($"{tabs}public const int {fieldName} = {adapter.VarId};");
            }
        }

        protected override void EmitAllArrayElement(System.Text.StringBuilder sb, int indent, string accessPath)
        {
            var tabs = new string(' ', indent);
            sb.AppendLine($"{tabs}{accessPath},");
        }

        sealed class VarKeyNodeAdapter : ICodeGenNode
        {
            readonly VarKeyNode _node;
            readonly string _path;

            public VarKeyNodeAdapter(VarKeyNode node, string path)
            {
                _node = node;
                _path = path;
            }

            public string Name => _node?.Name ?? string.Empty;
            public bool IsFolder => _node?.IsFolder ?? false;
            public string Description => _node?.Description ?? string.Empty;
            public string KeyValue => _node?.StableKey ?? string.Empty;
            public int VarId => _node?.VarId ?? 0;

            public IEnumerable<string> GetCommentLines()
            {
                if (_node == null || _node.IsFolder)
                    yield break;

                if (!string.IsNullOrEmpty(_path))
                    yield return $"Path: {_path}";

                if (!string.IsNullOrEmpty(_node.StableKey))
                    yield return $"StableKey: {_node.StableKey}";
            }
        }

        protected override IEnumerable<string> GetNodeCommentLines(ICodeGenNode node)
        {
            if (node is VarKeyNodeAdapter adapter)
                return adapter.GetCommentLines();
            return null;
        }
    }
}
#endif

