#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Game.Editor.CodeGen;
using Game.Editor.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.StateMachine.Editor
{
    /// <summary>
    /// OptionRegistry から静的 string クラス（Options.g.cs）を生成するジェネレーター。
    /// フォルダ = OptionKey（nested class + Key定数）
    /// リーフ = OptionValue（const string）
    /// </summary>
    public sealed class OptionCodeGenerator : StringKeyCodeGeneratorBase<OptionSettings>
    {
        static OptionCodeGenerator _instance;
        static OptionCodeGenerator Instance => _instance ??= new OptionCodeGenerator();
        
        OptionRegistry _registry;
        
        const string MenuPath = "Tools/Options/Generate";
        
        [MenuItem(MenuPath, priority = 10110)]
        public static void GenerateFromMenu()
        {
            var registry = OptionRegistryLocator.GetOrCreate();
            var settings = FindOrCreateSettings();
            Generate(registry, settings);
        }
        
        /// <summary>
        /// コード生成を実行する。
        /// </summary>
        public static void Generate(OptionRegistry registry, OptionSettings settings)
        {
            if (registry == null)
            {
                Debug.LogError("[OptionCodeGenerator] Registry is null.");
                return;
            }
            
            Instance._registry = registry;
            Instance.Generate(settings);
        }
        
        /// <summary>
        /// 設定を検索するか、なければ作成する。
        /// </summary>
        public static OptionSettings FindOrCreateSettings()
        {
            return TreeCodeGeneratorBase<OptionSettings>.FindOrCreateSettings<OptionSettings>(
                "Assets/GameLib/SO/StateMachine/OptionSettings.asset",
                "OptionCodeGenerator");
        }
        
        protected override string GeneratorName => "OptionCodeGenerator";
        protected override string DefaultNamespace => "Game.StateMachine.Generated";
        protected override string DefaultRootClassName => "Options";
        protected override string DefaultOutputPath => "Assets/GameLib/Script/Generated/Options.g.cs";
        
        protected override CodeGenTreeNode BuildTree()
        {
            if (_registry == null) return new CodeGenTreeNode();
            
            var root = new CodeGenTreeNode();
            
            // parentId -> children マップを構築
            var parentMap = new Dictionary<string, List<OptionNode>>(StringComparer.Ordinal);
            foreach (var n in _registry.Nodes)
            {
                if (n == null) continue;
                var pid = n.ParentId ?? string.Empty;
                if (!parentMap.TryGetValue(pid, out var list))
                {
                    list = new List<OptionNode>();
                    parentMap[pid] = list;
                }
                list.Add(n);
            }
            
            // 再帰的にツリー構築
            CodeGenTreeNode BuildChildren(OptionNode node, string parentPath)
            {
                var seg = node.Name ?? string.Empty;
                var path = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";
                
                var treeNode = new CodeGenTreeNode
                {
                    Node = new OptionNodeAdapter(node, path, _registry, parentMap),
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
            if (node is OptionNodeAdapter adapter)
            {
                return adapter.GetCommentLines();
            }
            return null;
        }
        
        /// <summary>
        /// フォルダ（OptionKey）の場合、Key定数を追加出力する。
        /// </summary>
        protected override void OnBeforeEmitFields(CodeGenContext context, CodeGenTreeNode root)
        {
            // フォルダの場合に Key 定数を出力するロジックはアダプター側で処理
        }
        
        /// <summary>
        /// OptionNode を ICodeGenNode にアダプトする。
        /// フォルダ（OptionKey）の場合も KeyValue を持ち、Key定数として出力される。
        /// </summary>
        sealed class OptionNodeAdapter : ICodeGenNode
        {
            readonly OptionNode _node;
            readonly string _path;
            readonly OptionRegistry _registry;
            readonly Dictionary<string, List<OptionNode>> _parentMap;
            
            public OptionNodeAdapter(OptionNode node, string path, OptionRegistry registry, Dictionary<string, List<OptionNode>> parentMap)
            {
                _node = node;
                _path = path;
                _registry = registry;
                _parentMap = parentMap;
            }
            
            public string Name => _node?.Name ?? string.Empty;
            public bool IsFolder => _node?.IsFolder ?? false;
            
            public string Description
            {
                get
                {
                    var desc = _node?.Description ?? string.Empty;
                    
                    // フォルダ（OptionKey）の場合、Global/Local 情報を付加
                    if (_node != null && _node.IsFolder)
                    {
                        var scope = _node.IsGlobal ? "[Global]" : "[Local]";
                        desc = string.IsNullOrEmpty(desc) ? scope : $"{scope} {desc}";
                    }
                    
                    // リーフ（OptionValue）でデフォルトの場合、情報を付加
                    if (_node != null && !_node.IsFolder && _node.IsDefault)
                    {
                        desc = string.IsNullOrEmpty(desc) ? "[Default]" : $"[Default] {desc}";
                    }
                    
                    return desc;
                }
            }
            
            public string KeyValue
            {
                get
                {
                    if (_node == null) return string.Empty;
                    // フォルダ（OptionKey）もリーフ（OptionValue）もキーを持つ
                    return _registry.GetKeyString(_node);
                }
            }
            
            /// <summary>
            /// このフォルダが直接リーフを持つか（= OptionKey として有効か）
            /// </summary>
            public bool HasDirectLeaves()
            {
                if (_node == null || !_node.IsFolder) return false;
                if (!_parentMap.TryGetValue(_node.Id, out var children)) return false;
                
                foreach (var c in children)
                {
                    if (!c.IsFolder) return true;
                }
                return false;
            }
            
            public IEnumerable<string> GetCommentLines()
            {
                if (_node == null)
                    yield break;
                
                yield return $"Path: {_path}";
                
                if (_node.IsFolder && _node.IsGlobal)
                    yield return "Scope: Global";
                else if (_node.IsFolder)
                    yield return "Scope: Local";
                
                if (!_node.IsFolder && _node.IsDefault)
                    yield return "IsDefault: true";
            }
        }
    }
}
#endif
