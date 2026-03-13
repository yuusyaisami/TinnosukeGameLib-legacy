#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Editor.CodeGen;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    /// <summary>
    /// RoomMapTileRegistry から tileId 定数クラスを生成する。
    /// </summary>
    public sealed class RoomMapTileCodeGenerator : TreeCodeGeneratorBase<RoomMapTileSettings>
    {
        static RoomMapTileCodeGenerator _instance;
        static RoomMapTileCodeGenerator Instance => _instance ??= new RoomMapTileCodeGenerator();

        RoomMapTileRegistry _registry;

        const string MenuPath = "Tools/RoomMap Tiles/Generate";

        [MenuItem(MenuPath, priority = 10000)]
        public static void GenerateFromMenu()
        {
            var registry = RoomMapTileRegistryLocator.GetOrCreate();
            var settings = FindOrCreateSettings();
            Generate(registry, settings);
        }

        public static void Generate(RoomMapTileRegistry registry, RoomMapTileSettings settings)
        {
            if (registry == null)
            {
                Debug.LogError("[RoomMapTileCodeGenerator] Registry is null.");
                return;
            }

            Instance._registry = registry;
            Instance.Generate(settings);
        }

        public static RoomMapTileSettings FindOrCreateSettings()
        {
            return FindOrCreateSettings<RoomMapTileSettings>(
                "Assets/GameLib/SO/RoomMap/RoomMapTileSettings.asset",
                "RoomMapTileCodeGenerator");
        }

        protected override string GeneratorName => "RoomMapTileCodeGenerator";
        protected override string DefaultNamespace => "Game.RoomMap.Generated";
        protected override string DefaultRootClassName => "RoomMapTileIds";
        protected override string DefaultOutputPath => "Assets/GameLib/Script/Generated/RoomMapTileIds.g.cs";

        protected override string GetAllArrayType() => "int[]";
        protected override string GetAllArrayName() => "AllTileIds";

        protected override CodeGenTreeNode BuildTree()
        {
            if (_registry == null)
                return new CodeGenTreeNode();

            var root = new CodeGenTreeNode();

            var parentMap = new Dictionary<string, List<RoomMapTileNode>>(StringComparer.Ordinal);
            foreach (var n in _registry.Nodes)
            {
                if (n == null) continue;
                var pid = n.ParentId ?? string.Empty;
                if (!parentMap.TryGetValue(pid, out var list))
                {
                    list = new List<RoomMapTileNode>();
                    parentMap[pid] = list;
                }
                list.Add(n);
            }

            CodeGenTreeNode BuildChildren(RoomMapTileNode node, string parentPath)
            {
                var seg = node.Name ?? string.Empty;
                var path = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";

                var treeNode = new CodeGenTreeNode
                {
                    Node = new RoomMapTileNodeAdapter(_registry, node, path),
                    Path = path
                };

                if (parentMap.TryGetValue(node.Id, out var children))
                {
                    children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    foreach (var c in children)
                        treeNode.Children.Add(BuildChildren(c, path));
                }

                return treeNode;
            }

            if (parentMap.TryGetValue(string.Empty, out var roots))
            {
                roots.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                foreach (var r in roots)
                    root.Children.Add(BuildChildren(r, string.Empty));
            }

            return root;
        }

        protected override void EmitFieldDeclaration(System.Text.StringBuilder sb, int indent, string fieldName, ICodeGenNode node)
        {
            var tabs = new string(' ', indent);
            EmitNodeComment(sb, indent, node);

            if (node is RoomMapTileNodeAdapter adapter)
            {
                if (adapter.IsFolder)
                    return;

                sb.AppendLine($"{tabs}public const int {fieldName} = {adapter.TileId};");
            }
        }

        protected override void EmitAllArrayElement(System.Text.StringBuilder sb, int indent, string accessPath)
        {
            var tabs = new string(' ', indent);
            sb.AppendLine($"{tabs}{accessPath},");
        }

        protected override IEnumerable<string> GetNodeCommentLines(ICodeGenNode node)
        {
            if (node is RoomMapTileNodeAdapter adapter)
                return adapter.GetCommentLines();
            return null;
        }

        sealed class RoomMapTileNodeAdapter : ICodeGenNode
        {
            readonly RoomMapTileRegistry _registry;
            readonly RoomMapTileNode _node;
            readonly string _path;

            public RoomMapTileNodeAdapter(RoomMapTileRegistry registry, RoomMapTileNode node, string path)
            {
                _registry = registry;
                _node = node;
                _path = path;
            }

            public string Name => _node?.Name ?? string.Empty;
            public bool IsFolder => _node?.IsFolder ?? false;
            public string Description => _node?.Description ?? string.Empty;
            public string KeyValue => _node?.StableKey ?? string.Empty;
            public int TileId => _node?.TileId ?? 0;

            public IEnumerable<string> GetCommentLines()
            {
                if (_node == null || _node.IsFolder)
                    yield break;

                if (!string.IsNullOrEmpty(_path))
                    yield return $"Path: {_path}";

                if (!string.IsNullOrEmpty(_node.StableKey))
                    yield return $"StableKey: {_node.StableKey}";

                yield return $"Tags: {_node.Tags}";
            }
        }
    }
}
#endif
