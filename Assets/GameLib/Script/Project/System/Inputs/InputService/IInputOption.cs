namespace Game.Input
{
    /// <summary>
    /// 現在有効な入力設定値を参照するためのインターフェイス。
    /// </summary>
    public interface IInputOption
    {
        // Pointer
        float VirtualCursorSpeed { get; }
        float PointerSensitivityMouse { get; }
        float PointerSensitivityGamepad { get; }

        // UI Navigation
        float UIRepeatDelay { get; }
        float UIRepeatRate { get; }

        // Locomotion
        float SlowMoveFactor { get; }
    }

    /// <summary>
    /// プロジェクトのデフォルト入力設定を表す。
    /// </summary>
    public interface IInputDefaultOption : IInputOption
    {
    }
}
