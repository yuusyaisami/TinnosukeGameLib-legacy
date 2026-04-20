#nullable enable
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// AI Clip 縺ｮ蝓ｺ蠎輔け繝ｩ繧ｹ縲よｴｾ逕溘〒謖ｯ繧玖・縺・ｒ螳夂ｾｩ縲・
    /// </summary>
    public abstract class AIClipSO : ScriptableObject
    {
        [Header("Identification")]
        [LabelText("Stable Key")]
        [Tooltip("豌ｸ邯唔D縲ゅΟ繧ｰ繝ｻ繝・ヰ繝・げ逕ｨ縲ょ錐蜑堺ｾ晏ｭ倡ｦ∵ｭ｢")]
        public string StableKey = "";

        [Header("Priority & Timing")]
        [LabelText("Priority")]
        [Tooltip("Inspector setting.")]
        public int Priority = 0;

        [LabelText("Update Mode")]
        public AIClipUpdateMode UpdateMode = AIClipUpdateMode.EveryFrame;

        [LabelText("Update Interval (frames)")]
        [ShowIf("@UpdateMode == AIClipUpdateMode.Interval")]
        [MinValue(1)]
        public int UpdateIntervalFrames = 1;

        [Header("Behavior")]
        [LabelText("Allow Reenter")]
        [Tooltip("Inspector setting.")]
        public bool AllowReenter = false;

        [Header("Interrupts")]
        [LabelText("Interrupt Rules")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<InterruptRule> InterruptRules = new();

        /// <summary>Runtime 繧堤函謌撰ｼ域ｴｾ逕溘〒繧ｪ繝ｼ繝舌・繝ｩ繧､繝会ｼ・/summary>
        public abstract AIClipRuntime CreateRuntime(in AIAgentContext ctx);
    }
}
