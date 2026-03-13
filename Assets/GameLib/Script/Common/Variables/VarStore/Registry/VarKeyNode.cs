#nullable enable
using System;
using System.Collections.Generic;
using Game.Registry;
using UnityEngine;

namespace Game.VarStoreKeys
{
    /// <summary>
    /// VarStore のキー定義ノード。
    /// - displayPath（ツリー構造）と stableKey（解決キー）を分離する
    /// - stableKey は rename/move で自動変更しない（資産互換のため）
    /// </summary>
    [Serializable]
    public sealed class VarKeyNode : HierarchyNodeBase
    {
        [SerializeField] int varId;
        [SerializeField] string stableKey = string.Empty;
        [SerializeField] List<string> aliases = new();

        public int VarId { get => varId; set => varId = value; }
        public string StableKey { get => stableKey; set => stableKey = value ?? string.Empty; }
        public List<string> Aliases => aliases;
    }
}

