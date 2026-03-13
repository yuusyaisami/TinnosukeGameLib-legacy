// Game.Entity.Movement.BoolLayer.cs
//
// 複数ソースからの bool 値を合成するレイヤー。

using System;
using System.Collections.Generic;

namespace Game.Common
{
    /// <summary>
    /// BoolLayer の合成モード。
    /// </summary>
    public enum BoolCompositionMode
    {
        /// <summary>全て true で true</summary>
        AllTrue,
        /// <summary>いずれか true で true</summary>
        AnyTrue,
        /// <summary>全て false で true</summary>
        AllFalse,
    }

    /// <summary>
    /// 複数ソースからの bool 値を合成するレイヤー。
    /// 例: "input" = true, "stun" = false → AllTrue なら結果は false
    /// </summary>
    public sealed class BoolLayer
    {
        readonly Dictionary<string, bool> _values;
        readonly BoolCompositionMode _mode;
        int _trueCount;

        /// <summary>合成結果</summary>
        public bool Value => _mode switch
        {
            BoolCompositionMode.AllTrue => _trueCount == _values.Count && _values.Count > 0,
            BoolCompositionMode.AnyTrue => _trueCount > 0,
            BoolCompositionMode.AllFalse => _trueCount == 0,
            _ => false,
        };

        /// <summary>登録されたキー数</summary>
        public int Count => _values.Count;

        /// <summary>true の数</summary>
        public int TrueCount => _trueCount;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="mode">合成モード</param>
        public BoolLayer(BoolCompositionMode mode = BoolCompositionMode.AnyTrue)
        {
            _values = new Dictionary<string, bool>(StringComparer.Ordinal);
            _mode = mode;
        }

        /// <summary>
        /// 値を設定。
        /// </summary>
        public void Set(string key, bool value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (_values.TryGetValue(key, out var existing))
            {
                if (existing == value) return;

                if (existing && !value)
                    _trueCount--;
                else if (!existing && value)
                    _trueCount++;
            }
            else
            {
                if (value)
                    _trueCount++;
            }

            _values[key] = value;
        }

        /// <summary>
        /// キーを削除。
        /// </summary>
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (_values.TryGetValue(key, out var value))
            {
                if (value)
                    _trueCount--;
                _values.Remove(key);
                return true;
            }
            return false;
        }

        /// <summary>
        /// キーの値を取得。
        /// </summary>
        public bool TryGet(string key, out bool value)
        {
            return _values.TryGetValue(key, out value);
        }

        /// <summary>
        /// キーが存在するか。
        /// </summary>
        public bool ContainsKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _values.ContainsKey(key);
        }

        /// <summary>
        /// 全てクリア。
        /// </summary>
        public void Clear()
        {
            _values.Clear();
            _trueCount = 0;
        }
    }
}
