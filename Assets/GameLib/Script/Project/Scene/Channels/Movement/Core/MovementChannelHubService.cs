// Game.Entity.Movement.MovementChannelHubService.cs
//
// Movement チャネルハブの実装。

using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Scalar;
using Game.Scalar.Generated;
using Game.DI;
using Game.Profile;

namespace Game.Movement
{
    /// <summary>
    /// Movement チャネルハブの実装。
    /// チャネルを集約し、優先度順で合成して最終速度を算出。
    /// </summary>
    public sealed class MovementChannelHubService : IMovementChannelHub, IEnabledService, IScopeReleaseHandler
    {
        readonly Dictionary<string, MovementChannelRuntime> _channels;
        readonly List<MovementChannelRuntime> _sortedChannels;
        readonly MovementOutput _output;
        bool _sortDirty;
        bool _disposed;
        readonly IBaseScalarService _scalarService;
        readonly IScopeNode _scope;
        bool _enabled = true;

        // ================================================================
        // プロパティ
        // ================================================================

        /// <summary>現在の合成出力</summary>
        public IMovementOutput Output => _output;

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
        public MovementChannelHubService(IScopeNode scopeNode = null, IBaseScalarService scalarService = null, IScopeBindingRegistry profileRegistry = null)
        {
            _scope = scopeNode;
            _channels = new Dictionary<string, MovementChannelRuntime>(StringComparer.Ordinal);
            _sortedChannels = new List<MovementChannelRuntime>();
            _output = new MovementOutput();
            _scalarService = scalarService;
        }

        // ================================================================
        // チャネル管理
        // ================================================================

        /// <summary>
        /// チャネルを登録。
        /// </summary>
        public IMovementChannelHandle RegisterChannel(string key, MovementChannelDef def)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (def == null)
                throw new ArgumentNullException(nameof(def));

            // 既存チャネルがあればそれを返す
            if (_channels.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var runtime = new MovementChannelRuntime(key, def);
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
        public bool TryGetChannel(string key, out IMovementChannelHandle handle)
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
                _output.SetValue(Vector2.zero);
                return;
            }

            // 優先度順ソート（昇順）
            if (_sortDirty)
            {
                _sortedChannels.Sort((a, b) => a.Priority.CompareTo(b.Priority));
                _sortDirty = false;
            }

            // Force 適用（damping なし）+ 滑らかさ更新
            foreach (var channel in _sortedChannels)
            {
                channel.ApplyPendingForce();
                channel.Advance(deltaTime);
                //Debug.Log($"[MovementChannelHubService] Channel '{channel.Key}': CurrentVelocity={channel.CurrentVelocity}, TargetVelocity={channel.TargetVelocity}, Priority={channel.Priority}, Influence={channel.Influence}, Enabled={channel.Enabled}, HandleID: {channel.GetHashCode()}");
            }

            // チャネル合成
            Vector2 composedVelocity = ComputeComposedVelocity();
            // Log composedVelocity when it changes for debugging (dev builds only)
            Vector2 prev = _output.Value;
            //Debug.Log($"[MovementChannelHubService] Composed Velocity: {composedVelocity} (Prev: {prev}), Channels: {_channels.Count}, Enabled: {IsEnabled}, Scope: {_scope.Identity.SelfTransform.name}");
            _output.SetValue(composedVelocity);


        }

        /// <summary>
        /// チャネルを合成して最終速度を算出。
        /// </summary>
        Vector2 ComputeComposedVelocity()
        {
            Vector2 result = Vector2.zero;

            foreach (var channel in _sortedChannels)
            {
                if (!channel.Enabled)
                    continue;

                Vector2 channelValue = channel.CurrentVelocity * channel.Influence;

                switch (channel.BlendOp)
                {
                    case MovementBlendOp.Add:
                        result += channelValue;
                        break;

                    case MovementBlendOp.Multiply:
                        result = Vector2.Scale(result, channelValue);
                        break;

                    case MovementBlendOp.Override:
                        result = channelValue;
                        break;

                    case MovementBlendOp.Max:
                        result = new Vector2(
                            Mathf.Max(result.x, channelValue.x),
                            Mathf.Max(result.y, channelValue.y));
                        break;

                    case MovementBlendOp.Lerp:
                        result = Vector2.Lerp(result, channelValue, channel.Influence);
                        break;
                }
            }

            return result;
        }

        // ================================================================
        // 便利メソッド
        // ================================================================

        /// <summary>
        /// 全チャネルの速度をリセット。
        /// </summary>
        public void ResetAllVelocities()
        {
            foreach (var channel in _sortedChannels)
            {
                channel.ResetVelocity();
            }
        }

        /// <summary>
        /// 指定レイヤーキーで全チャネルの有効状態を設定。
        /// 例: SetAllEnabled("stun", false) で全チャネルを無効化
        /// </summary>
        public void SetAllEnabled(string layerKey, bool enabled)
        {
            foreach (var channel in _sortedChannels)
            {
                channel.SetEnabled(layerKey, enabled);
            }
        }

        // ========= ScopeReleaseHandler =================================================
        public void OnRelease(IScopeNode scopeNode, bool isReset)
        {
            if (isReset)
            {
                Reset();
            }
        }


        // ================================================================
        // Dispose
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

        /// <inheritdoc/>
        public void Reset()
        {
            _disposed = false;
            _enabled = true;

            _channels.Clear();
            _sortedChannels.Clear();
            _output.SetValue(Vector2.zero);
        }

        /// <inheritdoc/>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _output.SetValue(Vector2.zero);
                ResetAllVelocities();
            }
        }
    }
}
