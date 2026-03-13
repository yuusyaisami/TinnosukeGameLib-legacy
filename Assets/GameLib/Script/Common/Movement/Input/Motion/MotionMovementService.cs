#nullable enable
// Game.Movement
// ================================================================================
// MotionMovementService - モーション（進行方向変調）サービス実装
// ================================================================================
//
// 【概要】
// IMotionMovement の実装。MotionPreset/MotionRuntime を管理し、
// GuidanceDirection から MotionOutput を生成する。
//
// 【状態管理】
// - _currentMotion: 現在の MotionPreset
// - _currentRuntime: 現在の MotionRuntime
// ================================================================================

using System;
using UnityEngine;
using Game.DI;
using VContainer;

namespace Game.Movement
{
    /// <summary>
    /// モーション（進行方向変調）サービス実装。
    /// </summary>
    public sealed class MotionMovementService : IMotionMovement, IDisposable, IResettableService, IEnabledService
    {
        // ================================================================
        // Dependencies
        // ================================================================

        readonly MotionMovementOptions _options;

        bool _enabled = true;

        // ================================================================
        // State
        // ================================================================

        MotionPreset? _currentMotion;
        MotionRuntime? _currentRuntime;
        bool _disposed;

        // ================================================================
        // Properties
        // ================================================================

        /// <inheritdoc/>
        public bool IsActive => _currentMotion != null && _currentRuntime != null;

        /// <inheritdoc/>
        public MotionPreset? CurrentMotion => _currentMotion;

        /// <inheritdoc/>
        public float ElapsedTime => _currentRuntime?.ElapsedTime ?? 0f;

        /// <inheritdoc/>
        public bool IsEnabled => !_disposed && _enabled;

        // ================================================================
        // Constructor
        // ================================================================

        [Inject]
        public MotionMovementService(MotionMovementOptions? options = null)
        {
            _options = options ?? new MotionMovementOptions();

            // 初期 Motion を設定
            if (_options.InitialMotion != null)
            {
                SetMotion(_options.InitialMotion);
            }
        }

        // ================================================================
        // IMotionMovement
        // ================================================================

        /// <inheritdoc/>
        public MotionOutput Tick(in MovementGuidanceFrame frame)
        {
            if (_disposed || !IsEnabled) return MotionOutput.Default(frame.GuidanceDirection);

            // Motion が無効なら GuidanceDirection をそのまま返す
            if (_currentRuntime == null)
            {
                return MotionOutput.Default(frame.GuidanceDirection);
            }

            return _currentRuntime.Tick(frame);
        }

        /// <inheritdoc/>
        public void SetMotion(MotionPreset? motion)
        {
            if (_disposed) return;

            // 同じ Motion なら何もしない
            if (ReferenceEquals(_currentMotion, motion))
                return;

            // 前の Runtime をクリア
            _currentRuntime?.Clear();
            _currentRuntime = null;

            _currentMotion = motion;

            // 新しい Runtime を作成
            if (motion != null)
            {
                _currentRuntime = motion.CreateRuntime();
                _currentRuntime.Initialize(motion);
            }
        }

        /// <inheritdoc/>
        public void ClearMotion()
        {
            SetMotion(null);
        }

        /// <inheritdoc/>
        public void ResetTime()
        {
            _currentRuntime?.ResetTime();
        }

        // ================================================================
        // IDisposable
        // ================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _currentRuntime?.Clear();
            _currentRuntime = null;
            _currentMotion = null;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _disposed = false;
            _enabled = true;

            _currentRuntime?.Clear();
            _currentRuntime = null;
            _currentMotion = null;

            if (_options.InitialMotion != null)
            {
                SetMotion(_options.InitialMotion);
            }
        }

        /// <inheritdoc/>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }
    }
}
