#nullable enable
namespace Game.Movement
{
    /// <summary>
    /// 移動入力の種別を表す列挙型。
    /// ActionBlock 連携で、種別ごとにブロック可能。
    /// </summary>
    public enum MovementInputType
    {
        /// <summary>未指定（ブロック判定なし）</summary>
        None = 0,
        /// <summary>Player 操作による移動</summary>
        Player = 1,
        /// <summary>AI 制御による移動</summary>
        AI = 2,
        /// <summary>System（イベント・スクリプト等）による移動</summary>
        System = 3,
    }
}
