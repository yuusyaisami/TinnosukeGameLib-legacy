#nullable enable
using Game.Commands;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Game.AI
{
    /// <summary>
    /// AI Clip 繧ｷ繧ｹ繝・Β縺ｮ險ｭ螳壹・繝ｭ繝輔ぃ繧､繝ｫ
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/AI Clip Profile", fileName = "AIClipProfile")]
    public sealed class AIClipProfileSO : ScriptableObject
    {
        [Header("Stack Configuration")]
        [LabelText("Max Stack Depth")]
        [Tooltip("Clip 繧ｹ繧ｿ繝・け縺ｮ譛螟ｧ豺ｱ蠎ｦ")]
        [MinValue(1)]
        public int MaxStackDepth = 8;

        [LabelText("Max Transitions Per Frame")]
        [Tooltip("Inspector setting.")]
        [MinValue(1)]
        public int MaxTransitionsPerFrame = 4;

        [Header("Default Behavior")]
        [LabelText("Default Clip")]
        [Tooltip("襍ｷ蜍墓凾縺ｫ繧ｹ繧ｿ繝・け縺ｫ遨阪∪繧後ｋ蛻晄悄 Clip")]
        [AssetOrInternal]
        public AIClipSO? DefaultClip;

        [Header("Variables")]
        [LabelText("Initial Variables")]
        [Tooltip("Agent 逕滓・譎ゅ↓繧ｳ繝斐・縺輔ｌ繧句・譛溷､画焚")]
        public VarStorePayload? InitialVariables;

        [Header("Debug")]
        [LabelText("Enable Debug Logging")]
        public bool EnableDebugLogging = false;
    }
}
