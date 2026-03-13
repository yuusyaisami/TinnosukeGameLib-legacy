// Game.Entity.Movement.IMovementChannelHub.cs
//
// Movement チャネルハブのインターフェース。

using System;

namespace Game.Movement
{
    /// <summary>
    /// Movement チャネルハブのインターフェース。
    /// </summary>
    public interface IMovementChannelHub : IDisposable
    {
        /// <summary>現在の合成出力</summary>
        IMovementOutput Output { get; }

        /// <summary>チャネルを登録</summary>
        IMovementChannelHandle RegisterChannel(string key, MovementChannelDef def);

        /// <summary>チャネルを解除</summary>
        void UnregisterChannel(string key);

        /// <summary>チャネルを取得</summary>
        bool TryGetChannel(string key, out IMovementChannelHandle handle);

        /// <summary>キーでチャネルが存在するか</summary>
        bool ContainsChannel(string key);

        /// <summary>全チャネルを更新し、出力を再計算</summary>
        void Tick(float deltaTime);
    }
}
