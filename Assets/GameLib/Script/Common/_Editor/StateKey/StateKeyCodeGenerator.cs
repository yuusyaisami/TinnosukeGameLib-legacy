#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Editor.CodeGen;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.StateMachine.Editor
{
    /// <summary>
    /// StateKeyRegistry から静的 string クラス（StateKeys.g.cs）を生成するジェネレーター。
    /// </summary>
    public sealed class StateKeyCodeGenerator : StringKeyCodeGeneratorBase<StateKeySettings>
    {
        static StateKeyCodeGenerator _instance;
        static StateKeyCodeGenerator Instance => _instance ??= new StateKeyCodeGenerator();
        
        StateKeyRegistry _registry;
        
        const string MenuPath = "Tools/State Keys/Generate";
        
        [MenuItem(MenuPath, priority = 10100)]
        public static void GenerateFromMenu()
        {
            var registry = StateKeyRegistryLocator.GetOrCreate();
            var settings = FindOrCreateSettings();
            Generate(registry, settings);
        }
        
        /// <summary>
        /// コード生成を実行する。
        /// </summary>
        public static void Generate(StateKeyRegistry registry, StateKeySettings settings)
        {
            if (registry == null)
            {
                Debug.LogError("[StateKeyCodeGenerator] Registry is null.");
                return;
            }
            
            Instance._registry = registry;
            Instance.Generate(settings);
        }
        
        /// <summary>
        /// 設定を検索するか、なければ作成する。
        /// </summary>
        public static StateKeySettings FindOrCreateSettings()
        {
            return TreeCodeGeneratorBase<StateKeySettings>.FindOrCreateSettings<StateKeySettings>(
                "Assets/GameLib/SO/StateMachine/StateKeySettings.asset",
                "StateKeyCodeGenerator");
        }
        
        protected override string GeneratorName => "StateKeyCodeGenerator";
        protected override string DefaultNamespace => "Game.StateMachine.Generated";
        protected override string DefaultRootClassName => "StateKeys";
        protected override string DefaultOutputPath => "Assets/GameLib/Script/Generated/StateKeys.g.cs";
        
        protected override CodeGenTreeNode BuildTree()
        {
            if (_registry == null) return new CodeGenTreeNode();
            
            var root = new CodeGenTreeNode();
            
            // parentId -> children マップを構築
            var parentMap = new Dictionary<string, List<StateKeyNode>>(StringComparer.Ordinal);
            foreach (var n in _registry.Nodes)
            {
                if (n == null) continue;
                var pid = n.ParentId ?? string.Empty;
                if (!parentMap.TryGetValue(pid, out var list))
                {
                    list = new List<StateKeyNode>();
                    parentMap[pid] = list;
                }
                list.Add(n);
            }
            
            // 再帰的にツリー構築
            CodeGenTreeNode BuildChildren(StateKeyNode node, string parentPath)
            {
                var seg = node.Name ?? string.Empty;
                var path = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";
                
                var treeNode = new CodeGenTreeNode
                {
                    Node = new StateKeyNodeAdapter(node, path, _registry),
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
            
            // ルート直下
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
        
        protected override IEnumerable<string> GetNodeCommentLines(ICodeGenNode node)
        {
            if (node is StateKeyNodeAdapter adapter)
            {
                return adapter.GetCommentLines();
            }
            return null;
        }
        
        /// <summary>
        /// StateKeyNode を ICodeGenNode にアダプトする。
        /// </summary>
        sealed class StateKeyNodeAdapter : ICodeGenNode
        {
            readonly StateKeyNode _node;
            readonly string _path;
            readonly StateKeyRegistry _registry;
            
            public StateKeyNodeAdapter(StateKeyNode node, string path, StateKeyRegistry registry)
            {
                _node = node;
                _path = path;
                _registry = registry;
            }
            
            public string Name => _node?.Name ?? string.Empty;
            public bool IsFolder => _node?.IsFolder ?? false;
            public string Description => _node?.Description ?? string.Empty;
            
            public string KeyValue
            {
                get
                {
                    if (_node == null || _node.IsFolder) return string.Empty;
                    return _registry.GetKeyString(_node);
                }
            }
            
            public IEnumerable<string> GetCommentLines()
            {
                if (_node == null || _node.IsFolder)
                    yield break;
                
                yield return $"Path: {_path}";
                
                if (!string.IsNullOrEmpty(_node.Note))
                    yield return $"Note: {_node.Note}";
            }
        }
    }
}
#endif
