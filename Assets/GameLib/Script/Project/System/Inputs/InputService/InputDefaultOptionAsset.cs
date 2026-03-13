using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Input
{
    /// <summary>
    /// Odin で編集できるデフォルト入力設定。
    /// </summary>
    [CreateAssetMenu(fileName = "InputDefaultOption", menuName = "Game/Input/DefaultOption")]
    public sealed class InputDefaultOptionAsset : SerializedScriptableObject, IInputDefaultOption
    {
        [FoldoutGroup("Pointer"), LabelText("バーチャルカーソル速度")]
        [MinValue(100f), MaxValue(8000f)]
        public float virtualCursorSpeed = 1800f;

        [FoldoutGroup("Pointer"), LabelText("マウス感度倍率")]
        [MinValue(0.1f), MaxValue(10f)]
        public float pointerSensitivityMouse = 1.0f;

        [FoldoutGroup("Pointer"), LabelText("ゲームパッド感度倍率")]
        [MinValue(0.1f), MaxValue(10f)]
        public float pointerSensitivityGamepad = 1.0f;

        [FoldoutGroup("UI Navigation"), LabelText("初回リピート遅延(sec)")]
        [MinValue(0.05f), MaxValue(1f)]
        public float uiRepeatDelay = 0.2f;

        [FoldoutGroup("UI Navigation"), LabelText("リピート間隔(sec)")]
        [MinValue(0.03f), MaxValue(0.5f)]
        public float uiRepeatRate = 0.1f;

        [FoldoutGroup("Locomotion"), LabelText("Slow 時の速度係数")]
        [MinValue(0.1f), MaxValue(1f)]
        public float slowMoveFactor = 0.5f;

        // IInputOption
        public float VirtualCursorSpeed => virtualCursorSpeed;
        public float PointerSensitivityMouse => pointerSensitivityMouse;
        public float PointerSensitivityGamepad => pointerSensitivityGamepad;
        public float UIRepeatDelay => uiRepeatDelay;
        public float UIRepeatRate => uiRepeatRate;
        public float SlowMoveFactor => slowMoveFactor;
    }
}
