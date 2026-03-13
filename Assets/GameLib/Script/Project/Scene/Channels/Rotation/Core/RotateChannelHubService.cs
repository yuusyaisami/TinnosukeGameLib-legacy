// Game.Rotation.RotateChannelHubService.cs
//
// Rotate チャネルハブの実装。

using System;
using System.Collections.Generic;
using UnityEngine;
using Game.DI;

namespace Game.Rotation
{
    /// <summary>
    /// Rotate チャネルハブの実装。
    /// チャネルを集約し、優先度順で合成して最終角速度を算出。
    /// </summary>
    public sealed class RotateChannelHubService : IRotateChannelHub, IResettableService, IEnabledService
    {
        readonly Dictionary<string, RotateChannelRuntime> _channels;
        readonly List<RotateChannelRuntime> _sortedChannels;
        readonly RotateOutput _output;
        readonly List<RotateChannelDef> _initialDefs;
        bool _sortDirty;
        bool _disposed;
        bool _enabled = true;

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>現在の合成出力</summary>
        public IRotateOutput Output => _output;

        /// <summary>登録チャネル数</summary>
        public int ChannelCount => _channels.Count;

        /// <inheritdoc/>
        public bool IsEnabled => !_disposed && _enabled;

        // ================================================================
        // コンストラクタ
        // ================================================================

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public RotateChannelHubService(List<RotateChannelDef> channelDefs = null)
        {
            _channels = new Dictionary<string, RotateChannelRuntime>(StringComparer.Ordinal);
            _sortedChannels = new List<RotateChannelRuntime>();
            _output = new RotateOutput();
            _initialDefs = channelDefs != null ? new List<RotateChannelDef>(channelDefs) : new List<RotateChannelDef>();

            // 初期チャネル登録
            if (channelDefs != null)
            {
                foreach (var def in channelDefs)
                {
                    RegisterChannel(def.Tag, def);
                }
            }
        }

        // ================================================================
        // チャネル管理
        // ================================================================

        /// <summary>
        /// チャネルを登録。
        /// </summary>
        public IRotateChannelHandle RegisterChannel(string key, RotateChannelDef def)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (def == null)
                throw new ArgumentNullException(nameof(def));

            // 既存チャネルがあればそれを返す
            if (_channels.TryGetValue(key, out var existing))
                return existing;

            var runtime = new RotateChannelRuntime(key, def);
            runtime.OnPriorityChanged += () => _sortDirty = true;

            _channels[key] = runtime;
            _sortedChannels.Add(runtime);
            _sortDirty = true;

            return runtime;
        }

        /// <summary>
        /// チャネルを解除。
        /// </summary>
        public void UnregisterChannel(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!_channels.TryGetValue(key, out var runtime))
                return;

            _channels.Remove(key);
            _sortedChannels.Remove(runtime);
        }

        /// <summary>
        /// チャネルを取得。
        /// </summary>
        public bool TryGetChannel(string key, out IRotateChannelHandle handle)
        {
            if (!string.IsNullOrEmpty(key) && _channels.TryGetValue(key, out var runtime))
            {
                handle = runtime;
                return true;
            }
            handle = null;
            return false;
        }

        /// <summary>
        /// キーでチャネルが存在するか。
        /// </summary>
        public bool ContainsChannel(string key)
        {
            return !string.IsNullOrEmpty(key) && _channels.ContainsKey(key);
        }

        // ================================================================
        // 更新
        // ================================================================

        /// <summary>
        /// 全チャネルを更新し、出力を再計算。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed) return;

            if (!IsEnabled)
            {
                _output.SetValue(0f);
                return;
            }

            // 優先度順ソート（昇順）
            if (_sortDirty)
            {
                _sortedChannels.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                _sortDirty = false;
            }

            // Torque 適用
            foreach (var channel in _sortedChannels)
            {
                channel.ApplyPendingTorque();
            }

            // チャネル合成
            float composedAngularVelocity = ComputeComposedAngularVelocity();
            _output.SetValue(composedAngularVelocity);
        }

        /// <summary>
        /// チャネルを合成して最終角速度を算出。
        /// </summary>
        float ComputeComposedAngularVelocity()
        {
            float result = 0f;

            foreach (var channel in _sortedChannels)
            {
                if (!channel.Enabled)
                    continue;

                float channelValue = channel.AngularVelocity * channel.Influence;

                switch (channel.BlendOp)
                {
                    case RotateBlendOp.Add:
                        result += channelValue;
                        break;

                    case RotateBlendOp.Multiply:
                        result *= channelValue;
                        break;

                    case RotateBlendOp.Override:
                        result = channelValue;
                        break;

                    case RotateBlendOp.Max:
                        result = Mathf.Max(result, channelValue);
                        break;

                    case RotateBlendOp.Lerp:
                        result = Mathf.Lerp(result, channelValue, channel.Influence);
                        break;
                }
            }

            return result;
        }

        // ================================================================
        // IResettableService
        // ================================================================

        /// <summary>
        /// リセット。
        /// </summary>
        public void Reset()
        {
            _disposed = false;
            _enabled = true;

            foreach (var channel in _sortedChannels)
            {
                channel.ResetAngularVelocity();
            }

            _output.Reset();
        }

        /// <summary>
        /// 有効/無効を設定。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _output.SetValue(0f);
            }
        }

        // ================================================================
        // IDisposable
        // ================================================================

        /// <summary>
        /// リソース解放。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _channels.Clear();
            _sortedChannels.Clear();
        }
    }
}
