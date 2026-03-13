using System;
using UnityEngine;
using Game.Registry;

namespace Game.EventKey
{
    /// <summary>
    /// イベントキーのノード。
    /// </summary>
    [Serializable]
    public sealed class EventKeyNode : HierarchyNodeBase
    {
        [SerializeField] string explicitKey; // 空なら自動生成 (path を '.' 置換)

        /// <summary>明示的なキー文字列。空なら自動生成される。</summary>
        public string ExplicitKey { get => explicitKey; set => explicitKey = value; }
    }
}
