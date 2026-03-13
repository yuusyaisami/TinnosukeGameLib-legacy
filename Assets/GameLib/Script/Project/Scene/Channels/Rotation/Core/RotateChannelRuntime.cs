// Game.Rotation.RotateChannelRuntime.cs
//
// Rotate チャネルのランタイム状態。

using System;
using UnityEngine;
using Game.Common;

namespace Game.Rotation
{
    /// <summary>
    /// Rotate チャネルのランタイム状態。
    /// </summary>
    public sealed class RotateChannelRuntime : IRotateChannelHandle, IEnabledLayerState
    {
        readonly string _key;
        float _angularVelocity;
        float _pendingTorque;
        int _priority;
        RotateBlendOp _blendOp;
        float _influence;
        readonly BoolLayer _enabledLayer;

        /// <summary>優先度変更時のイベント</summary>
        public event System.Action OnPriorityChanged;

        // ================================================================
        // コンストラクタ
        // ================================================================

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public RotateChannelRuntime(string key, RotateChannelDef def)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (def == null)
                throw new ArgumentNullException(nameof(def));

            _key = key;
            _priority = def.Priority;
            _blendOp = def.BlendOp;
            _influence = def.Influence;

            // 全部 true で初めて true
            _enabledLayer = new BoolLayer(BoolCompositionMode.AllTrue);
            _enabledLayer.Set("default", def.EnabledByDefault);

            _angularVelocity = 0f;
            _pendingTorque = 0f;
        }

        // ================================================================
        // IRotateChannelHandle 実装
        // ================================================================

        /// <summary>チャネルキー</summary>
        public string Key => _key;

        /// <summary>有効状態（BoolLayer 合成結果）</summary>
        public bool Enabled => _enabledLayer.Value;

        /// <summary>現在の角速度（degrees/sec）</summary>
        public float AngularVelocity
        {
            get => _angularVelocity;
            set => _angularVelocity = value;
        }

        /// <summary>瞬間的なトルクを追加</summary>
        public void AddTorque(float torque)
        {
            _pendingTorque += torque;
        }

        /// <summary>優先度</summary>
        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    OnPriorityChanged?.Invoke();
                }
            }
        }

        /// <summary>合成演算</summary>
        public RotateBlendOp BlendOp
        {
            get => _blendOp;
            set => _blendOp = value;
        }

        /// <summary>影響度（0〜1）</summary>
        public float Influence
        {
            get => _influence;
            set => _influence = Mathf.Clamp01(value);
        }

        /// <summary>有効状態レイヤーに値を設定</summary>
        public void SetEnabled(string layerKey, bool enabled)
        {
            _enabledLayer.Set(layerKey, enabled);
        }

        /// <summary>有効状態レイヤーから削除</summary>
        public bool RemoveEnabled(string layerKey)
        {
            return _enabledLayer.Remove(layerKey);
        }

        // ================================================================
        // 内部 API（Hub から呼ばれる）
        // ================================================================

        /// <summary>
        /// Pending Torque を AngularVelocity に適用。
        /// </summary>
        internal void ApplyPendingTorque()
        {
            _angularVelocity += _pendingTorque;
            _pendingTorque = 0f;
        }

        /// <summary>
        /// 角速度をリセット。
        /// </summary>
        internal void ResetAngularVelocity()
        {
            _angularVelocity = 0f;
            _pendingTorque = 0f;
        }
    }
}
