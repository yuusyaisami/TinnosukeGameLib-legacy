#nullable enable
using System;
using System.Collections.Generic;
using Game.ActionBlock.Keys;
using Game.Commands;
using Game.Common;
using Game.Scalar;
using Game.DI;
using UnityEngine;
using Game.Input;

namespace Game.Rotation
{
    /// <summary>
    /// Rotation service that gathers rotation input (user input or external) and writes angular velocity into a rotation channel.
    /// </summary>
    public sealed class InputRotateService : IScopeAcquireHandler, IScopeReleaseHandler, IDisposable, IActionBlockable, IResettableService, IEnabledService
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
        bool _acquired;
        bool _sortDirty;

        // 蝗櫁ｻ｢迥ｶ諷・
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

        /// <summary>迴ｾ蝨ｨ縺ｮ隗帝溷ｺｦ・・egrees/sec・・/summary>
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
            IRuntimeResolver resolver)
        {
            _channelHub = channelHub;
            _actionBlockService = actionBlockService;
            _options = options ?? new InputRotateOptions();

            resolver.TryResolve(out IBaseScalarService? scalarSvc);
            _scalar = scalarSvc;

            // Owner Transform 繧貞叙蠕暦ｼ・aseLifetimeScope 縺九ｉ・・
            if (resolver.TryResolve(out IScopeNode? scope) && scope is Component component)
            {
                _ownerTransform = component.transform;
            }
        }

        // ================================================================
        // Scope lifecycle
        // ================================================================

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_acquired)
                return;

            _disposed = false;
            _acquired = true;
            RegisterChannel();
            _actionBlockService?.RegisterBlockable(this);
            ApplyRotationFromAdapters();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
            => Dispose();

        // ================================================================
        // IDisposable
        // ================================================================

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _acquired = false;
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
        /// 蝗櫁ｻ｢繧｢繝繝励ち繧堤匳骭ｲ縲・
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
        /// 蝗櫁ｻ｢繧｢繝繝励ち繧定ｧ｣髯､縲・
        /// </summary>
        public void UnregisterAdapter(IInputRotationAdapter adapter)
        {
            if (adapter == null)
                return;

            _rotationAdapters.Remove(adapter);
        }

        /// <summary>
        /// 蝗櫁ｻ｢縺梧峩譁ｰ縺輔ｌ縺溘％縺ｨ繧帝夂衍縲・
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

            // Block 蛻､螳・
            if (!IsEnabled || IsBlocked() || !hasRotation)
            {
                _channel.AngularVelocity = 0f;
                _currentAngularVelocity = 0f;
                return;
            }

            // 繧ｹ繧ｫ繝ｩ繝ｼ蛟咲紫繧帝←逕ｨ
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
            _acquired = false;
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
    /// InputRotateService 縺ｮ繧ｪ繝励す繝ｧ繝ｳ縲・
    /// </summary>
    public sealed class InputRotateOptions
    {
        public const string DefaultChannelKey = "userRotation";

        public string ChannelKey { get; set; } = DefaultChannelKey;

        public string BlockableId { get; set; } = nameof(InputRotateService);

        /// <summary>隗帝溷ｺｦ縺ｫ縺九￠繧句咲紫繧貞叙蠕励☆繧九せ繧ｫ繝ｩ繝ｼ繧ｭ繝ｼ</summary>
        public Game.Scalar.ScalarKey? SpeedScalarKey { get; set; } = null;
    }

    /// <summary>
    /// 蝗櫁ｻ｢蜈･蜉帙い繝繝励ち縺ｮ繧､繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ縲・
    /// </summary>
    public interface IInputRotationAdapter
    {
        /// <summary>蜆ｪ蜈亥ｺｦ・磯ｫ倥＞縺ｻ縺ｩ蜆ｪ蜈茨ｼ・/summary>
        int RotationPriority { get; }

        /// <summary>
        /// 隗帝溷ｺｦ繧貞叙蠕励・
        /// </summary>
        /// <param name="angularVelocity">degrees/sec</param>
        /// <returns>譛牙柑縺ｪ蝗櫁ｻ｢縺後≠繧後・ true</returns>
        bool TryGetAngularVelocity(out float angularVelocity);
    }
}
