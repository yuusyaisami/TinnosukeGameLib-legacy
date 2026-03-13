// Game.Movement.IMovementChannelHandle.cs
//
// 個別チャネルへの操作ハンドル。

using System;
using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// 個別チャネルへの操作ハンドル。
    /// </summary>
    public interface IMovementChannelHandle
    {
        /// <summary>チャネルキー</summary>
        string Key { get; }

        /// <summary>有効状態（BoolLayer 合成結果）</summary>
        bool Enabled { get; }

        /// <summary>現在の目標速度、既存 API と互換性を保ったまま目標設定として使う</summary>
        Vector2 Velocity { get; set; }

        /// <summary>現在の速度（滑らかに変化する実際の値）</summary>
        Vector2 CurrentVelocity { get; }

        /// <summary>目標速度（滑らかに移行するターゲット値）</summary>
        Vector2 TargetVelocity { get; }

        /// <summary>滑らかさを制御するラムダ（大きいほど速く到達）</summary>
        float Lambda { get; set; }

        /// <summary>ラムダを明示的に指定してターゲット速度を更新</summary>
        void SetTargetVelocity(Vector2 target, float? lambda = null);

        /// <summary>即座に速度を設定（ラムダによる滑らかさを無視）</summary>
        void SetImmediateVelocity(Vector2 value);
        void ResetImmediateVelocity() => SetImmediateVelocity(Vector2.zero);

        /// <summary>瞬間的な力を追加（次の Tick で Velocity に加算される）</summary>
        void AddForce(Vector2 force);

        /// <summary>優先度</summary>
        int Priority { get; set; }

        /// <summary>合成演算</summary>
        MovementBlendOp BlendOp { get; set; }

        /// <summary>影響度（0〜1）</summary>
        float Influence { get; set; }

        /// <summary>有効状態レイヤーに値を設定</summary>
        void SetEnabled(string layerKey, bool enabled);

        /// <summary>有効状態レイヤーから削除</summary>
        bool RemoveEnabled(string layerKey);
    }
}
