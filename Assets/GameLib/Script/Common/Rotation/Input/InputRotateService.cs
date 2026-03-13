#nullable enable
using System;
using System.Collections.Generic;
using Game.ActionBlock.Keys;
using Game.Commands;
using Game.Common;
using Game.Scalar;
using Game.DI;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Game.Input;

namespace Game.Rotation
{
    /// <summary>
    /// Rotation service that gathers rotation input (user input or external) and writes angular velocity into a rotation channel.
    /// </summary>
    public sealed class InputRotateService : IStartable, IDisposable, IActionBlockable, IResettableService, IEnabledService
    {
        // ================================================================
        // Dependencies
        // ================================================================

        readonly IRotateChannelHub _channelHub;
        readonly IActionBlockService? _actionBlockService;
        readonly InputRotateOptions _options;
        readonly IBaseScalarService? _scalar;
        readonly Transform? _ownerTransform;

        bool _enabled = true;

        // ================================================================
        // State
        // ================================================================

        IRotateChannelHandle? _channel;
        readonly List<IInputRotationAdapter> _rotationAdapters = new();
        bool _disposed;
        bool _sortDirty;

        // 回転状態
        float _currentAngularVelocity;

        // ================================================================
        // Properties
        // ================================================================

        /// <inheritdoc/>
        public string ActionBlockKind => ActionBlockKeys.Entity.SystemRotation;

        /// <inheritdoc/>
        public string BlockableId => _options.BlockableId;

        /// <inheritdoc/>
        public BoolLayer BlockLayer { get; } = new();

        /// <summary>現在の角速度（degrees/sec）</summary>
        public float CurrentAngularVelocity => _currentAngularVelocity;

        /// <inheritdoc/>
        public bool IsEnabled => !_disposed && _enabled;

        // ================================================================
        // Constructor
        // ================================================================

        public InputRotateService(
            IRotateChannelHub channelHub,
            IActionBlockService? actionBlockService,
            InputRotateOptions options,
            IObjectResolver resolver)
        {
            _channelHub = channelHub;
            _actionBlockService = actionBlockService;
            _options = options ?? new InputRotateOptions();

            resolver.TryResolve(out IBaseScalarService? scalarSvc);
            _scalar = scalarSvc;

            // Owner Transform を取得（BaseLifetimeScope から）
            if (resolver.TryResolve(out BaseLifetimeScope? scope) && scope != null)
            {
                _ownerTransform = scope.transform;
            }
        }

        // ================================================================
        // IStartable
        // ================================================================

        public void Start()
        {
            RegisterChannel();
            _actionBlockService?.RegisterBlockable(this);
            ApplyRotationFromAdapters();
        }

        // ================================================================
        // IDisposable
        // ================================================================

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _actionBlockService?.UnregisterBlockable(this);
            _channel = null;
        }

        void RegisterChannel()
        {
            var key = _options.ChannelKey ?? InputRotateOptions.DefaultChannelKey;
            if (_channelHub.TryGetChannel(key, out var existing))
            {
                _channel = existing;
                return;
            }

            _channel = _channelHub.RegisterChannel(key, RotateChannelDef.Input(key));
        }

        bool IsBlocked() => BlockLayer.Value;

        // ================================================================
        // Adapter Management
        // ================================================================

        /// <summary>
        /// 回転アダプタを登録。
        /// </summary>
        public void RegisterAdapter(IInputRotationAdapter adapter)
        {
            if (adapter == null || _disposed)
                return;

            if (_rotationAdapters.Contains(adapter))
                return;

            _rotationAdapters.Add(adapter);
            _sortDirty = true;
        }

        /// <summary>
        /// 回転アダプタを解除。
        /// </summary>
        public void UnregisterAdapter(IInputRotationAdapter adapter)
        {
            if (adapter == null)
                return;

            _rotationAdapters.Remove(adapter);
        }

        /// <summary>
        /// 回転が更新されたことを通知。
        /// </summary>
        public void NotifyRotationUpdated()
        {
            if (_disposed)
                return;

            ApplyRotationFromAdapters();
        }

        // ================================================================
        // Core Logic
        // ================================================================

        void ApplyRotationFromAdapters()
        {
            EnsureChannel();
            if (_channel == null)
                return;

            if (!IsEnabled)
            {
                _channel.AngularVelocity = 0f;
                _currentAngularVelocity = 0f;
                return;
            }

            if (_sortDirty)
            {
                _rotationAdapters.Sort((a, b) => b.RotationPriority.CompareTo(a.RotationPriority));
                _sortDirty = false;
            }

            float angularVelocity = 0f;
            bool hasRotation = false;

            for (int i = 0; i < _rotationAdapters.Count; i++)
            {
                var adapter = _rotationAdapters[i];
                if (adapter != null && adapter.TryGetAngularVelocity(out var candidate))
                {
                    angularVelocity = candidate;
                    hasRotation = true;
                    break;
                }
            }

            ApplyRotation(angularVelocity, hasRotation);
        }

        void ApplyRotation(float angularVelocity, bool hasRotation)
        {
            if (_channel == null)
                return;

            // Block 判定
            if (!IsEnabled || IsBlocked() || !hasRotation)
            {
                _channel.AngularVelocity = 0f;
                _currentAngularVelocity = 0f;
                return;
            }

            // スカラー倍率を適用
            float speedMul = 1f;
            if (_scalar != null && _options.SpeedScalarKey.HasValue && _scalar.GlobalTryGet(_options.SpeedScalarKey.Value, out float mul))
            {
                speedMul = mul;
            }

            float finalAngularVelocity = angularVelocity * speedMul;
            _currentAngularVelocity = finalAngularVelocity;

            _channel.AngularVelocity = finalAngularVelocity;
        }

        void EnsureChannel()
        {
            if (_channel != null || _disposed)
                return;
            RegisterChannel();
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _disposed = false;
            _enabled = true;
            _currentAngularVelocity = 0f;
            _sortDirty = false;
            BlockLayer.Clear();

            if (_channel != null)
            {
                _channel.AngularVelocity = 0f;
            }
        }

        /// <inheritdoc/>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled && _channel != null)
            {
                _channel.AngularVelocity = 0f;
                _currentAngularVelocity = 0f;
            }
        }
    }

    /// <summary>
    /// InputRotateService のオプション。
    /// </summary>
    public sealed class InputRotateOptions
    {
        public const string DefaultChannelKey = "userRotation";

        public string ChannelKey { get; set; } = DefaultChannelKey;

        public string BlockableId { get; set; } = nameof(InputRotateService);

        /// <summary>角速度にかける倍率を取得するスカラーキー</summary>
        public Game.Scalar.ScalarKey? SpeedScalarKey { get; set; } = null;
    }

    /// <summary>
    /// 回転入力アダプタのインターフェース。
    /// </summary>
    public interface IInputRotationAdapter
    {
        /// <summary>優先度（高いほど優先）</summary>
        int RotationPriority { get; }

        /// <summary>
        /// 角速度を取得。
        /// </summary>
        /// <param name="angularVelocity">degrees/sec</param>
        /// <returns>有効な回転があれば true</returns>
        bool TryGetAngularVelocity(out float angularVelocity);
    }
}
