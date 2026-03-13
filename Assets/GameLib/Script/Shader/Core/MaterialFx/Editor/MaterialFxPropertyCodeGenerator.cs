#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Editor.CodeGen;
using Game.Editor.Registry;
using Game.Registry;
using UnityEditor;
using UnityEngine;

namespace Game.MaterialFx.Editor
{
    // ================================================================
    // MaterialFxPropertyCodeGenerator - MaterialFxPropertyRegistrySO からコード生成
    // ================================================================

    /// <summary>
    /// MaterialFxPropertyRegistrySO から静的 string クラスを生成するジェネレーター。
    /// </summary>
    public sealed class MaterialFxPropertyCodeGenerator : StringKeyCodeGeneratorBase<MaterialFxSettings>
    {
        // ----------------------------------------------------------------
        // シングルトンインスタンス
        // ----------------------------------------------------------------

        static MaterialFxPropertyCodeGenerator _instance;
        static MaterialFxPropertyCodeGenerator Instance => _instance ??= new MaterialFxPropertyCodeGenerator();

        // ----------------------------------------------------------------
        // フィールド
        // ----------------------------------------------------------------

        MaterialFxPropertyRegistrySO _registry;

        // ----------------------------------------------------------------
        // メニュー
        // ----------------------------------------------------------------

        const string MenuPath = "Tools/MaterialFx/Generate Keys";

        [MenuItem(MenuPath, priority = 10000)]
        public static void GenerateFromMenu()
        {
            var registry = MaterialFxPropertyRegistryLocator.GetOrCreate();
            var settings = FindOrCreateSettings();
            Generate(registry, settings);
        }

        // ----------------------------------------------------------------
        // 公開API
        // ----------------------------------------------------------------

        /// <summary>
        /// コード生成を実行する。
        /// </summary>
        public static void Generate(MaterialFxPropertyRegistrySO registry, MaterialFxSettings settings)
        {
            if (registry == null)
            {
                Debug.LogError("[MaterialFxPropertyCodeGenerator] Registry is null.");
                return;
            }

            Instance._registry = registry;
            Instance.Generate(settings);
        }

        /// <summary>
        /// 設定を検索するか、なければ作成する。
        /// </summary>
        public static MaterialFxSettings FindOrCreateSettings()
        {
            return TreeCodeGeneratorBase<MaterialFxSettings>.FindOrCreateSettings<MaterialFxSettings>(
                "Assets/GameLib/SO/MaterialFx/MaterialFxSettings.asset",
                "MaterialFxPropertyCodeGenerator");
        }

        // ----------------------------------------------------------------
        // 抽象メソッド実装
        // ----------------------------------------------------------------

        /// <inheritdoc/>
        protected override string GeneratorName => "MaterialFxPropertyCodeGenerator";

        /// <inheritdoc/>
        protected override string DefaultNamespace => "Game.MaterialFx.Generated";

        /// <inheritdoc/>
        protected override string DefaultRootClassName => "MaterialFxKeys";

        /// <inheritdoc/>
        protected override string DefaultOutputPath => "Assets/GameLib/Script/Shader/Core/MaterialFx/Generated/MaterialFxKeys.g.cs";

        /// <inheritdoc/>
        protected override CodeGenTreeNode BuildTree()
        {
            if (_registry == null) return new CodeGenTreeNode();

            var root = new CodeGenTreeNode();

            // parentId -> children マップを構築
            var parentMap = new Dictionary<string, List<MaterialFxPropertyNode>>(StringComparer.Ordinal);
            foreach (var n in _registry.Nodes)
            {
                if (n == null) continue;
                var pid = n.ParentId ?? string.Empty;
                if (!parentMap.TryGetValue(pid, out var list))
                {
                    list = new List<MaterialFxPropertyNode>();
                    parentMap[pid] = list;
                }
                list.Add(n);
            }

            // 再帰的にツリー構築
            CodeGenTreeNode BuildChildren(MaterialFxPropertyNode node, string parentPath)
            {
                var seg = node.Name ?? string.Empty;
                var path = string.IsNullOrEmpty(parentPath) ? seg : $"{parentPath}/{seg}";

                var treeNode = new CodeGenTreeNode
                {
                    Node = new MaterialFxNodeAdapter(node, path),
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

        /// <inheritdoc/>
        protected override IEnumerable<string> GetNodeCommentLines(ICodeGenNode node)
        {
            if (node is MaterialFxNodeAdapter adapter)
            {
                return adapter.GetCommentLines();
            }
            return null;
        }

        // ----------------------------------------------------------------
        // ノードアダプター
        // ----------------------------------------------------------------

        /// <summary>
        /// MaterialFxPropertyNode を ICodeGenNode にアダプトする。
        /// </summary>
        sealed class MaterialFxNodeAdapter : ICodeGenNode
        {
            readonly MaterialFxPropertyNode _node;
            readonly string _path;

            public MaterialFxNodeAdapter(MaterialFxPropertyNode node, string path)
            {
                _node = node;
                _path = path;
            }

            public string Name => _node?.Name ?? string.Empty;
            public bool IsFolder => _node?.IsFolder ?? false;
            public string Description => _node?.Description ?? string.Empty;

            public string KeyValue
            {
                get
                {
                    if (_node == null || _node.IsFolder) return string.Empty;

                    // StableKey を使用
                    return _node.StableKey ?? string.Empty;
                }
            }

            /// <summary>
            /// コメント行を取得する（Sender, ValueType, ShaderPropertyName など）。
            /// </summary>
            public IEnumerable<string> GetCommentLines()
            {
                if (_node == null || _node.IsFolder)
                    yield break;

                // Sender
                yield return $"Sender: {_node.Sender}";

                // ValueType
                yield return $"ValueType: {_node.ValueType}";

                // ShaderPropertyName（空でない場合のみ）
                if (!string.IsNullOrEmpty(_node.ShaderPropertyName))
                {
                    yield return $"ShaderPropertyName: {_node.ShaderPropertyName}";
                }

                // Path
                if (!string.IsNullOrEmpty(_path))
                {
                    yield return $"Path: {_path}";
                }
            }
        }
    }
}
#endif
