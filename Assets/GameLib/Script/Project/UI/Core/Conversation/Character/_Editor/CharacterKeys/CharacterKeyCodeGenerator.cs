#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Editor.CodeGen;
using UnityEditor;
using UnityEngine;

namespace Game.Conversation.Editor
{
    public sealed class CharacterKeyCodeGenerator : TreeCodeGeneratorBase<CharacterKeySettings>
    {
        static CharacterKeyCodeGenerator _instance;
        static CharacterKeyCodeGenerator Instance => _instance ??= new CharacterKeyCodeGenerator();

        CharacterKeyRegistry _registry;

        const string MenuPath = "Tools/Conversation/Character Keys/Generate";

        [MenuItem(MenuPath, priority = 10100)]
        public static void GenerateFromMenu()
        {
            var registry = CharacterKeyRegistryLocator.GetOrCreate();
            var settings = FindOrCreateSettings();
            Generate(registry, settings);
        }

        public static void Generate(CharacterKeyRegistry registry, CharacterKeySettings settings)
        {
            if (registry == null)
            {
                Debug.LogError("[CharacterKeyCodeGenerator] Registry is null.");
                return;
            }

            Instance._registry = registry;
            Instance.Generate(settings);
        }

        public static CharacterKeySettings FindOrCreateSettings()
        {
            return FindOrCreateSettings<CharacterKeySettings>(
                "Assets/GameLib/SO/Conversation/CharacterKeySettings.asset",
                "CharacterKeyCodeGenerator");
        }

        protected override string GeneratorName => "CharacterKeyCodeGenerator";
        protected override string DefaultNamespace => "Game.Conversation.Generated";
        protected override string DefaultRootClassName => "CharacterIds";
        protected override string DefaultOutputPath => "Assets/GameLib/Script/Generated/CharacterIds.g.cs";

        protected override string GetAllArrayType() => "int[]";
        protected override string GetAllArrayName() => "AllCharacterIds";

        protected override CodeGenTreeNode BuildTree()
        {
            if (_registry == null)
                return new CodeGenTreeNode();

            var root = new CodeGenTreeNode();

            var parentMap = new Dictionary<string, List<CharacterKeyNode>>(StringComparer.Ordinal);
            foreach (var node in _registry.Nodes)
            {
                if (node == null)
                    continue;

                var parentId = node.ParentId ?? string.Empty;
                if (!parentMap.TryGetValue(parentId, out var children))
                {
                    children = new List<CharacterKeyNode>();
                    parentMap[parentId] = children;
                }

                children.Add(node);
            }

            CodeGenTreeNode BuildChildren(CharacterKeyNode node, string parentPath)
            {
                var segment = node.Name ?? string.Empty;
                var path = string.IsNullOrEmpty(parentPath) ? segment : $"{parentPath}/{segment}";

                var treeNode = new CodeGenTreeNode
                {
                    Node = new CharacterKeyNodeAdapter(node, path),
                    Path = path,
                };

                if (parentMap.TryGetValue(node.Id, out var children))
                {
                    children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    for (var i = 0; i < children.Count; i++)
                        treeNode.Children.Add(BuildChildren(children[i], path));
                }

                return treeNode;
            }

            if (parentMap.TryGetValue(string.Empty, out var roots))
            {
                roots.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                for (var i = 0; i < roots.Count; i++)
                    root.Children.Add(BuildChildren(roots[i], string.Empty));
            }

            return root;
        }

        protected override void EmitFieldDeclaration(System.Text.StringBuilder sb, int indent, string fieldName, ICodeGenNode node)
        {
            var tabs = new string(' ', indent);
            EmitNodeComment(sb, indent, node);

            if (node is CharacterKeyNodeAdapter adapter)
            {
                if (adapter.IsFolder)
                    return;

                sb.AppendLine($"{tabs}public const int {fieldName} = {adapter.CharacterId};");
            }
        }

        protected override void EmitAllArrayElement(System.Text.StringBuilder sb, int indent, string accessPath)
        {
            var tabs = new string(' ', indent);
            sb.AppendLine($"{tabs}{accessPath},");
        }

        sealed class CharacterKeyNodeAdapter : ICodeGenNode
        {
            readonly CharacterKeyNode _node;
            readonly string _path;

            public CharacterKeyNodeAdapter(CharacterKeyNode node, string path)
            {
                _node = node;
                _path = path;
            }

            public string Name => _node?.Name ?? string.Empty;
            public bool IsFolder => _node?.IsFolder ?? false;
            public string Description => _node?.Description ?? string.Empty;
            public string KeyValue => _node?.StableKey ?? string.Empty;
            public int CharacterId => _node?.CharacterId ?? 0;

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
            if (node is CharacterKeyNodeAdapter adapter)
                return adapter.GetCommentLines();

            return null;
        }
    }
}
#endif
