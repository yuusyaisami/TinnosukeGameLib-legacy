using System;

namespace Game.Input
{
    /// <summary>
    /// 現在の入力オプション値を保持・提供するサービス。
    /// </summary>
    public sealed class InputOptionService : IInputOption
    {
        readonly IInputDefaultOption _defaultOption;

        public float VirtualCursorSpeed { get; private set; }
        public float PointerSensitivityMouse { get; private set; }
        public float PointerSensitivityGamepad { get; private set; }
        public float UIRepeatDelay { get; private set; }
        public float UIRepeatRate { get; private set; }
        public float SlowMoveFactor { get; private set; }

        public InputOptionService(IInputDefaultOption defaultOption = null)
        {
            _defaultOption = defaultOption;
            if (defaultOption != null)
            {
                Apply(defaultOption);
            }
            else
            {
                // Fallback in case no default asset is provided.
                VirtualCursorSpeed = 1800f;
                PointerSensitivityMouse = 1.0f;
                PointerSensitivityGamepad = 1.0f;
                UIRepeatDelay = 0.2f;
                UIRepeatRate = 0.1f;
                SlowMoveFactor = 0.5f;
            }
        }

        /// <summary>
        /// 現在値を別のオプションセットから反映する。
        /// </summary>
        public void Apply(IInputOption option)
        {
            if (option == null) throw new ArgumentNullException(nameof(option));

            VirtualCursorSpeed = option.VirtualCursorSpeed;
            PointerSensitivityMouse = option.PointerSensitivityMouse;
            PointerSensitivityGamepad = option.PointerSensitivityGamepad;
            UIRepeatDelay = option.UIRepeatDelay;
            UIRepeatRate = option.UIRepeatRate;
            SlowMoveFactor = option.SlowMoveFactor;
        }

        /// <summary>
        /// デフォルトに戻す。
        /// </summary>
        public void ResetToDefault()
        {
            if (_defaultOption != null)
            {
                Apply(_defaultOption);
            }
        }
    }
}
