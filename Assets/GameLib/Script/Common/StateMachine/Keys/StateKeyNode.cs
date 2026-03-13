using System;
using UnityEngine;
using Game.Registry;

namespace Game.StateMachine
{
    /// <summary>
    /// StateKey のノード。
    /// StateMachine の State を識別する安定文字列キーを定義。
    /// </summary>
    [Serializable]
    public sealed class StateKeyNode : HierarchyNodeBase
    {
        [SerializeField] string explicitKey;
        
        [SerializeField, TextArea] string note;
        
        /// <summary>明示的なキー文字列。空なら DisplayPath から自動生成。</summary>
        public string ExplicitKey { get => explicitKey; set => explicitKey = value; }
        
        /// <summary>メモ（実装者向け）</summary>
        public string Note { get => note; set => note = value; }
    }
}
