using System;
using UnityEngine;
using Game.Registry;

namespace Game.Actions
{
    /// <summary>
    /// GameState のノード。
    /// Enum 生成に使う安定IDを保持する。
    /// </summary>
    [Serializable]
    public sealed class GameStateNode : HierarchyNodeBase
    {
        [SerializeField] int stateId;
        [SerializeField, TextArea] string note;

        /// <summary>Enum 値として使う安定ID。</summary>
        public int StateId
        {
            get => stateId;
            set => stateId = value;
        }

        /// <summary>実装者向けメモ。</summary>
        public string Note
        {
            get => note;
            set => note = value;
        }
    }
}
