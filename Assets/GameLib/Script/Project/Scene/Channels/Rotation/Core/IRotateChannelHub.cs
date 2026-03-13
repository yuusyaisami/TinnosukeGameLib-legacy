// Game.Rotation.IRotateChannelHub.cs
//
// Rotate チャネルハブのインターフェース。

using System;

namespace Game.Rotation
{
    /// <summary>
    /// Rotate チャネルハブのインターフェース。
    /// </summary>
    public interface IRotateChannelHub : IDisposable
    {
        /// <summary>現在の合成出力</summary>
        IRotateOutput Output { get; }

        /// <summary>チャネルを登録</summary>
        IRotateChannelHandle RegisterChannel(string key, RotateChannelDef def);

        /// <summary>チャネルを解除</summary>
        void UnregisterChannel(string key);

        /// <summary>チャネルを取得</summary>
        bool TryGetChannel(string key, out IRotateChannelHandle handle);

        /// <summary>キーでチャネルが存在するか</summary>
        bool ContainsChannel(string key);

        /// <summary>全チャネルを更新し、出力を再計算</summary>
        void Tick(float deltaTime);
    }
}
