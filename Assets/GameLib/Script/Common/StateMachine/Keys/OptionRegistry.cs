using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Registry;

namespace Game.StateMachine
{
    /// <summary>
    /// Option の階層 Registry（統合版）。
    /// フォルダ = OptionKey、リーフ = OptionValue として管理。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Registry/StateMachine/Option Registry")]
    public sealed class OptionRegistry : HierarchyRegistryBase<OptionNode>
    {
        public override string GetKeyString(OptionNode node)
        {
            if (node == null)
                return string.Empty;
            
            if (!string.IsNullOrEmpty(node.ExplicitKey))
                return node.ExplicitKey;
            
            // DisplayPath の '/' を '.' に置換
            // 例: Movement/Direction/Up → Movement.Direction.Up
            return GetDisplayPath(node.Id).Replace('/', '.');
        }
        
        /// <summary>
        /// OptionKey（フォルダ）を作成。
        /// </summary>
        public OptionNode CreateOptionKey(string parentId, string name, bool isGlobal = false, string explicitKey = null)
        {
            var node = CreateFolder(parentId, name);
            node.ExplicitKey = explicitKey;
            node.IsGlobal = isGlobal;
            return node;
        }
        
        /// <summary>
        /// OptionValue（リーフ）を作成。
        /// </summary>
        public OptionNode CreateOptionValue(string parentId, string name, bool isDefault = false, string explicitKey = null)
        {
            var node = CreateLeaf(parentId, name);
            node.ExplicitKey = explicitKey;
            node.IsDefault = isDefault;
            return node;
        }
        
        /// <summary>
        /// 指定した OptionKey（フォルダ）配下の OptionValue（リーフ）を全て取得。
        /// </summary>
        public IEnumerable<OptionNode> GetOptionValues(string optionKeyId)
        {
            return Nodes
                .Where(n => n != null && !n.IsFolder && n.ParentId == optionKeyId);
        }
        
        /// <summary>
        /// 指定した OptionKey（フォルダ）のデフォルト値を取得。
        /// </summary>
        public OptionNode GetDefaultValue(string optionKeyId)
        {
            return GetOptionValues(optionKeyId)
                .FirstOrDefault(n => n.IsDefault);
        }
        
        /// <summary>
        /// 全ての OptionKey（フォルダで IsFolder == true かつ直下にリーフがあるもの）を取得。
        /// </summary>
        public IEnumerable<OptionNode> GetAllOptionKeys()
        {
            var keysWithValues = new HashSet<string>(
                Nodes.Where(n => n != null && !n.IsFolder)
                     .Select(n => n.ParentId)
                     .Where(pid => !string.IsNullOrEmpty(pid))
            );
            
            return Nodes.Where(n => n != null && n.IsFolder && keysWithValues.Contains(n.Id));
        }
        
        /// <summary>
        /// Global の OptionKey を取得。
        /// </summary>
        public IEnumerable<OptionNode> GetGlobalOptionKeys()
        {
            return GetAllOptionKeys().Where(n => n.IsGlobal);
        }
        
        /// <summary>
        /// Local の OptionKey を取得。
        /// </summary>
        public IEnumerable<OptionNode> GetLocalOptionKeys()
        {
            return GetAllOptionKeys().Where(n => !n.IsGlobal);
        }
    }
}
