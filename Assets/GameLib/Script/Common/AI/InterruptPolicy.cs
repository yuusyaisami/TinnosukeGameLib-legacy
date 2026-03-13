#nullable enable
namespace Game.AI
{
    /// <summary>
    /// 割り込みの適用ポリシー
    /// </summary>
    public enum InterruptPolicy
    {
        /// <summary>TargetClip をスタックに積む</summary>
        Push,
        /// <summary>現在の Clip を Pop して TargetClip を Push</summary>
        Replace,
        /// <summary>TargetClip までスタックを巻き戻す</summary>
        PopUntil,
    }
}
