using UnityEngine;
using Game.Registry;

namespace Game.StateMachine
{
    /// <summary>
    /// StateKey の階層 Registry。
    /// StateMachine の State を識別する安定文字列キーを管理。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Registry/StateMachine/State Key Registry")]
    public sealed class StateKeyRegistry : HierarchyRegistryBase<StateKeyNode>
    {
        /// <summary>
        /// StableKey を取得。ExplicitKey 優先、なければ DisplayPath から生成。
        /// </summary>
        public override string GetKeyString(StateKeyNode node)
        {
            if (node == null || node.IsFolder)
                return string.Empty;
            
            if (!string.IsNullOrEmpty(node.ExplicitKey))
                return node.ExplicitKey;
            
            // DisplayPath の '/' を '.' に置換
            return GetDisplayPath(node.Id).Replace('/', '.');
        }
        
        /// <summary>
        /// StateKey ノードを作成。
        /// </summary>
        public StateKeyNode CreateStateKey(string parentId, string name, string explicitKey = null, string description = null)
        {
            var node = CreateLeaf(parentId, name);
            node.ExplicitKey = explicitKey;
            node.Description = description;
            return node;
        }
    }
}
