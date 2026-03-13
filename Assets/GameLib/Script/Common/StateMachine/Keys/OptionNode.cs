using System;
using UnityEngine;
using Game.Registry;

namespace Game.StateMachine
{
    /// <summary>
    /// Option のノード（統合版）。
    /// フォルダ = OptionKey（Direction, WeaponType 等の種別）
    /// リーフ = OptionValue（Left, Right, Sword 等の具体値）
    /// </summary>
    [Serializable]
    public sealed class OptionNode : HierarchyNodeBase
    {
        [SerializeField] string explicitKey;
        
        // グローバルであるかどうかは気休め程度の意味合いです、
        // またGlobalの場合はExplorerで行の色が変わります。
        /// <summary>Global Option かどうか（フォルダのみ有効）</summary>
        [SerializeField] bool isGlobal;
        
        /// <summary>デフォルト値として選択されるか（リーフのみ有効）</summary>
        [SerializeField] bool isDefault;
        
        /// <summary>明示的なキー文字列。空なら DisplayPath から自動生成。</summary>
        public string ExplicitKey { get => explicitKey; set => explicitKey = value; }
        
        /// <summary>
        /// Global Option か Local Option か。
        /// フォルダノード（OptionKey）のみで有効。
        /// Global = StateMachine 共通、Local = StateMachine インスタンスごと。
        /// </summary>
        public bool IsGlobal { get => isGlobal; set => isGlobal = value; }
        
        /// <summary>
        /// この値がデフォルトか。
        /// リーフノード（OptionValue）のみで有効。
        /// 親 OptionKey の初期値として使用される。
        /// </summary>
        public bool IsDefault { get => isDefault; set => isDefault = value; }
    }
}
