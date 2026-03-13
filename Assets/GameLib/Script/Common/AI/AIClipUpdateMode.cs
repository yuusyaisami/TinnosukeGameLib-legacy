#nullable enable
namespace Game.AI
{
    /// <summary>
    /// AI Clip の更新モード
    /// </summary>
    public enum AIClipUpdateMode
    {
        /// <summary>毎フレーム OnUpdate を呼ぶ</summary>
        EveryFrame,
        /// <summary>指定フレーム間隔で OnUpdate を呼ぶ</summary>
        Interval,
        /// <summary>OnUpdate を呼ばない（Monitor だけで動く Clip 用）</summary>
        Manual,
    }
}
