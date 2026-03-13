// Game.Rotation.IRotateChannelHandle.cs
//
// 個別 Rotate チャネルへの操作ハンドル。

using System;

namespace Game.Rotation
{
    /// <summary>
    /// 個別 Rotate チャネルへの操作ハンドル。
    /// </summary>
    public interface IRotateChannelHandle
    {
        /// <summary>チャネルキー</summary>
        string Key { get; }

        /// <summary>有効状態（BoolLayer 合成結果）</summary>
        bool Enabled { get; }

        /// <summary>現在の角速度（degrees/sec）</summary>
        float AngularVelocity { get; set; }

        /// <summary>瞬間的なトルクを追加（次の Tick で AngularVelocity に加算される）</summary>
        void AddTorque(float torque);

        /// <summary>優先度</summary>
        int Priority { get; set; }

        /// <summary>合成演算</summary>
        RotateBlendOp BlendOp { get; set; }

        /// <summary>影響度（0〜1）</summary>
        float Influence { get; set; }

        /// <summary>有効状態レイヤーに値を設定</summary>
        void SetEnabled(string layerKey, bool enabled);

        /// <summary>有効状態レイヤーから削除</summary>
        bool RemoveEnabled(string layerKey);
    }
}
