#nullable enable
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// AI Clip の基底クラス。派生で振る舞いを定義。
    /// </summary>
    public abstract class AIClipSO : ScriptableObject
    {
        [Header("Identification")]
        [LabelText("Stable Key")]
        [Tooltip("永続ID。ログ・デバッグ用。名前依存禁止")]
        public string StableKey = "";

        [Header("Priority & Timing")]
        [LabelText("Priority")]
        [Tooltip("割り込み競合時の優先度。高いほど優先")]
        public int Priority = 0;

        [LabelText("Update Mode")]
        public AIClipUpdateMode UpdateMode = AIClipUpdateMode.EveryFrame;

        [LabelText("Update Interval (frames)")]
        [ShowIf("@UpdateMode == AIClipUpdateMode.Interval")]
        [MinValue(1)]
        public int UpdateIntervalFrames = 1;

        [Header("Behavior")]
        [LabelText("Allow Reenter")]
        [Tooltip("同じ Clip の連続 Push を許可するか")]
        public bool AllowReenter = false;

        [Header("Interrupts")]
        [LabelText("Interrupt Rules")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<InterruptRule> InterruptRules = new();

        /// <summary>Runtime を生成（派生でオーバーライド）</summary>
        public abstract AIClipRuntime CreateRuntime(in AIAgentContext ctx);
    }
}
