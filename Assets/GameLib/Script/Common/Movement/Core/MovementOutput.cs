// Game.Movement.MovementOutput.cs
//
// Movement 計算結果の出力。

using UnityEngine;

namespace Game.Movement
{
    /// <summary>
    /// Movement 計算結果の出力インターフェース。
    /// </summary>
    public interface IMovementOutput
    {
        /// <summary>現在の速度</summary>
        Vector2 Value { get; }

        /// <summary>変更バージョン</summary>
        uint Version { get; }

        /// <summary>前回から変更があったか</summary>
        bool HasChanged(uint lastVersion);
    }

    /// <summary>
    /// Movement 計算結果の出力実装。
    /// </summary>
    public sealed class MovementOutput : IMovementOutput
    {
        Vector2 _value;
        uint _version;

        /// <summary>現在の速度</summary>
        public Vector2 Value => _value;

        /// <summary>変更バージョン</summary>
        public uint Version => _version;

        /// <summary>前回から変更があったか</summary>
        public bool HasChanged(uint lastVersion) => _version != lastVersion;

        /// <summary>
        /// 値を設定（変更があった場合のみバージョンアップ）。
        /// </summary>
        public void SetValue(Vector2 value)
        {
            if (_value != value)
            {
                _value = value;
                _version++;
            }
        }

        /// <summary>
        /// 強制的に値を設定（常にバージョンアップ）。
        /// </summary>
        public void ForceSetValue(Vector2 value)
        {
            _value = value;
            _version++;
        }

        /// <summary>
        /// リセット。
        /// </summary>
        public void Reset()
        {
            _value = Vector2.zero;
            _version = 0;
        }
    }
}
