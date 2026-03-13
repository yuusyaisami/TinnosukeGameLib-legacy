#nullable enable
using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Authoring/Debug 用の stableKey と、実行用の varId を併せ持つ参照。
    /// - Runtime は varId のみを参照する（stableKey の解決をしない）
    /// - stableKey はデバッグ/Inspector 表示用
    /// </summary>
    [Serializable]
    public struct VarKeyRef
    {
        [SerializeField, LabelText("Stable Key")]
        string stableKey;

        [SerializeField, LabelText("VarId")]
        [MinValue(1)]
        int varId;

        public string StableKey => stableKey;
        public int VarId => varId;

        public VarKeyRef(int varId, string stableKey = "")
        {
            this.varId = varId;
            this.stableKey = stableKey ?? string.Empty;
        }
    }
}

