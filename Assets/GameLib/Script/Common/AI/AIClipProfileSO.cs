#nullable enable
using Game.Commands;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Game.AI
{
    /// <summary>
    /// AI Clip システムの設定プロファイル
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/AI Clip Profile", fileName = "AIClipProfile")]
    public sealed class AIClipProfileSO : ScriptableObject
    {
        [Header("Stack Configuration")]
        [LabelText("Max Stack Depth")]
        [Tooltip("Clip スタックの最大深度")]
        [MinValue(1)]
        public int MaxStackDepth = 8;

        [LabelText("Max Transitions Per Frame")]
        [Tooltip("1フレームあたりの最大遷移数（無限ループ防止）")]
        [MinValue(1)]
        public int MaxTransitionsPerFrame = 4;

        [Header("Default Behavior")]
        [LabelText("Default Clip")]
        [Tooltip("起動時にスタックに積まれる初期 Clip")]
        [AssetOrInternal]
        public AIClipSO? DefaultClip;

        [Header("Variables")]
        [LabelText("Initial Variables")]
        [Tooltip("Agent 生成時にコピーされる初期変数")]
        public VarStorePayload? InitialVariables;

        [Header("Debug")]
        [LabelText("Enable Debug Logging")]
        public bool EnableDebugLogging = false;
    }
}
