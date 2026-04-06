#nullable enable
using System;

namespace Game.TransformSystem
{
    /// <summary>
    /// 合成モード。track の寄与を最終 pose にどう適用するかを指定する。
    /// </summary>
    public enum TransformComposeMode
    {
        Replace = 10,
        Add = 20,
        Multiply = 30,
    }

    /// <summary>
    /// 寄与スロットの種別。合成器内で各 contribution がどの property に作用するかを識別する。
    /// </summary>
    public enum TransformContributionProperty
    {
        WorldPosition = 10,
        LocalPosition = 20,
        LocalRotation = 30,
        LocalScale = 40,
        AnchoredPosition = 50,
        SizeDelta = 60,
        Pivot = 70,
    }

    /// <summary>
    /// track が「どの property に寄与するか」を示す flags。
    /// ビット割り当ては既存 TransformAnimationProperty と一致させている。
    /// </summary>
    [Flags]
    public enum TransformContributionMask
    {
        None = 0,
        WorldPosition = 1 << 0,
        LocalPosition = 1 << 1,
        LocalRotation = 1 << 2,
        LocalScale = 1 << 3,
        AnchoredPosition = 1 << 4,
        SizeDelta = 1 << 5,
        Pivot = 1 << 6,
    }

    /// <summary>
    /// Preset / Clip 再生中に新しい preset が来たときのポリシー。
    /// </summary>
    public enum TransformPresetExecutionPolicy
    {
        StopPrevious = 10,
        Parallel = 20,
        Interrupt = 30,
    }
}
