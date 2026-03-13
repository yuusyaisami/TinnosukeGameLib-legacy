// Game.Rotation.RotateOutput.cs
//
// Rotation 計算結果の出力。

namespace Game.Rotation
{
    /// <summary>
    /// Rotation 計算結果の出力インターフェース。
    /// </summary>
    public interface IRotateOutput
    {
        /// <summary>現在の角速度（degrees/sec）</summary>
        float Value { get; }

        /// <summary>変更バージョン</summary>
        uint Version { get; }

        /// <summary>前回から変更があったか</summary>
        bool HasChanged(uint lastVersion);
    }

    /// <summary>
    /// Rotation 計算結果の出力実装。
    /// </summary>
    public sealed class RotateOutput : IRotateOutput
    {
        float _value;
        uint _version;

        /// <summary>現在の角速度（degrees/sec）</summary>
        public float Value => _value;

        /// <summary>変更バージョン</summary>
        public uint Version => _version;

        /// <summary>前回から変更があったか</summary>
        public bool HasChanged(uint lastVersion) => _version != lastVersion;

        /// <summary>
        /// 値を設定（変更があった場合のみバージョンアップ）。
        /// </summary>
        public void SetValue(float value)
        {
            // floatの比較は小さな誤差を許容
            const float epsilon = 0.0001f;
            if (System.Math.Abs(_value - value) > epsilon)
            {
                _value = value;
                _version++;
            }
        }

        /// <summary>
        /// 強制的に値を設定（常にバージョンアップ）。
        /// </summary>
        public void ForceSetValue(float value)
        {
            _value = value;
            _version++;
        }

        /// <summary>
        /// リセット。
        /// </summary>
        public void Reset()
        {
            _value = 0f;
            _version = 0;
        }
    }
}
