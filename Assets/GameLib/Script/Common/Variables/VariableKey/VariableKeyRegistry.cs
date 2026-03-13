using System.Collections.Generic;
using UnityEngine;
using Game.Registry;

namespace Game.VariableKeys
{
    /// <summary>
    /// 変数キーの階層構造レジストリ。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Registry/Variables/Variable Key Registry")]
    public sealed class VariableKeyRegistry : HierarchyRegistryBase<VariableKeyNode>
    {
        // 後方互換: 古い Editor コードから呼ばれる場合に備える
        public List<VariableKeyNode> NodesEditable => nodes;

        /// <summary>
        /// 実際に CommandVariableBag などに使うキー文字列を取得。
        /// explicitKey があればそれ、なければ displayPath の '/' を '.' にしたもの。
        /// </summary>
        public override string GetKeyString(VariableKeyNode node)
        {
            if (node == null || node.IsFolder)
                return string.Empty;

            if (!string.IsNullOrEmpty(node.ExplicitKey))
                return node.ExplicitKey;

            var path = GetDisplayPath(node.Id);
            return path.Replace('/', '.');
        }

        /// <summary>
        /// キーノードを作成する（explicitKey, description 指定可能）。
        /// </summary>
        public VariableKeyNode CreateKey(string parentId, string name, string explicitKey = null, string description = null)
        {
            var node = CreateLeaf(parentId, name);
            node.ExplicitKey = explicitKey;
            node.Description = description;
            return node;
        }
    }
}
