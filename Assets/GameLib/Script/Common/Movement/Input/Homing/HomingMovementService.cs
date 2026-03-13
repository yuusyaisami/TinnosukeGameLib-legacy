#nullable enable
// Game.Movement
// ================================================================================
// HomingMovementService - ホーミング（ターゲット追尾）サービス実装
// ================================================================================
//
// 【概要】
// IHomingMovement の実装。TargetChannelHub からターゲットを取得し、
// 入力方向との角度補間で GuidanceDirection を生成する。
//
// 【状態管理】
// - _homingT: 補間進行度（0..1）
// - _guidanceDirection: 現在の出力方向（保持用）
// - _currentTarget: 現在のターゲット情報
// - _initialized: 初回 Tick が行われたか
//
// 【GC 回避】
// - 毎フレームの new を避け、フィールドに状態を保持
// - TargetSnapshot は構造体で stack 上に確保
// ================================================================================

using System;
using UnityEngine;
using Game.Common;
using Game.Targeting;
using Game.Search;
using Game.DI;
using VContainer;

namespace Game.Movement
{
    /// <summary>
    /// ホーミング（ターゲット追尾）サービス実装。
    /// </summary>
    public sealed class HomingMovementService : IHomingMovement, IHomingMovementConfigurable, IDisposable, IResettableService, IEnabledService
    {
        // ================================================================
        // Dependencies
        // ================================================================

        readonly ITargetChannelHub? _targetHub;
        readonly HomingMovementOptions _options;

        bool _serviceEnabled = true;

        // ================================================================
        // State
        // ================================================================

        readonly BoolLayer _homingLayer;

        Vector2 _guidanceDirection;
        TargetSnapshot _currentTarget;
        float _homingT;
        bool _initialized;
        bool _disposed;

        // ================================================================
        // Properties
        // ================================================================

        /// <inheritdoc/>
        public BoolLayer HomingLayer => _homingLayer;

        /// <inheritdoc/>
        public bool HomingEnabled => _homingLayer.Value;

        /// <inheritdoc/>
        public Vector2 GuidanceDirection => _guidanceDirection;

        /// <inheritdoc/>
        public TargetSnapshot CurrentTarget => _currentTarget;

        /// <inheritdoc/>
        public bool IsEnabled => !_disposed && _serviceEnabled;

        // ================================================================
        // Constructor
        // ================================================================

        public HomingMovementService(
            HomingMovementOptions options,
            ITargetChannelHub? targetHub = null)
        {
            _options = options ?? new HomingMovementOptions();
            _targetHub = targetHub;

            // BoolLayer 初期化（AnyTrue モード: いずれかの層が true なら有効）
            _homingLayer = new BoolLayer(BoolCompositionMode.AnyTrue);

            // デフォルト層を設定
            if (_options.EnabledByDefault)
            {
                _homingLayer.Set(_options.DefaultLayerKey, true);
            }

            _guidanceDirection = Vector2.zero;
            _currentTarget = TargetSnapshot.Invalid;
            _homingT = 0f;
            _initialized = false;
        }

        // ================================================================
        // IHomingMovement
        // ================================================================

        /// <inheritdoc/>
        public Vector2 Tick(Vector2 baseDirection, Vector2 ownerPosition, float deltaTime)
        {
            if (_disposed || !IsEnabled)
            {
                // サービス無効時は計算せず入力方向をそのまま返す
                return baseDirection;
            }

            // 初回は BaseDirection で初期化
            if (!_initialized)
            {
                _guidanceDirection = baseDirection;
                _initialized = true;
            }

            // ケースA: HomingEnabled == false → 停止、直前保持
            if (!HomingEnabled)
            {
                // Target 更新なし、補間進行なし
                // _guidanceDirection はそのまま保持
                return _guidanceDirection;
            }

            // ターゲット取得
            _currentTarget = ResolveTarget(ownerPosition);

            // ケースB: HasTarget == false → BaseDirection 採用
            if (!_currentTarget.HasTarget)
            {
                _guidanceDirection = baseDirection;
                _homingT = 0f; // ターゲットがいないのでリセット
                return _guidanceDirection;
            }

            // ケースC: BaseDirection == zero && HasTarget → 完全追従
            if (baseDirection.sqrMagnitude < MovementMath.NormalizeEpsilon)
            {
                _guidanceDirection = _currentTarget.TargetDirection;
                return _guidanceDirection;
            }

            // ケースD: BaseDirection != zero && HasTarget → 角度補間
            float alpha = ComputeAlpha(deltaTime);
            _guidanceDirection = MovementMath.Slerp2D(baseDirection, _currentTarget.TargetDirection, alpha);

            return _guidanceDirection;
        }

        /// <inheritdoc/>
        public void ResetBlend(float resetAlpha)
        {
            // _homingT を減衰
            _homingT *= (1f - Mathf.Clamp01(resetAlpha));
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _guidanceDirection = Vector2.zero;
            _currentTarget = TargetSnapshot.Invalid;
            _homingT = 0f;
            _initialized = false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _disposed = false;
            _serviceEnabled = true;

            // BoolLayer を初期状態に戻す
            _homingLayer.Clear();
            _homingLayer.Set(_options.DefaultLayerKey, _options.EnabledByDefault);

            Clear();
        }

        /// <inheritdoc/>
        public void SetEnabled(bool enabled)
        {
            _serviceEnabled = enabled;
            if (!enabled)
            {
                // 無効化時は進行度をリセット
                _homingT = 0f;
            }
        }

        public void SetBlendParams(HomingBlendParams blendParams)
        {
            _options.BlendParams = blendParams ?? HomingBlendParams.Default;
        }

        // ================================================================
        // Private Methods
        // ================================================================

        /// <summary>
        /// ターゲット情報を取得。最短距離のターゲットを選択。
        /// </summary>
        TargetSnapshot ResolveTarget(Vector2 ownerPosition)
        {
            // TargetChannelHub からランタイム取得
            if (_targetHub == null || !_targetHub.TryGetRuntime(_options.TargetChannelTag, out var runtime))
                return TargetSnapshot.Invalid;

            // Hits を取得（内部で自動キャッシュ更新）
            var hits = runtime.Hits;
            if (hits == null || hits.Count == 0)
                return TargetSnapshot.Invalid;

            // 最短距離のターゲットを選択
            DynamicSearchHit? best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];

                Vector2 hitPos = new Vector2(hit.Position.x, hit.Position.y);
                float distSq = (hitPos - ownerPosition).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = hit;
                }
            }

            if (!best.HasValue)
                return TargetSnapshot.Invalid;

            // TargetSnapshot を構築
            Vector2 targetPos = new Vector2(best.Value.Position.x, best.Value.Position.y);
            var diff = targetPos - ownerPosition;
            float dist = diff.magnitude;

            Vector2 targetDir;
            if (dist > 0.0001f)
            {
                targetDir = diff / dist;
            }
            else
            {
                // 同位置の場合は前回の方向を使用
                targetDir = _currentTarget.HasTarget ? _currentTarget.TargetDirection : Vector2.up;
            }

            return new TargetSnapshot(
                hasTarget: true,
                targetPosition: targetPos,
                ownerPosition: ownerPosition,
                distance: dist,
                targetDirection: targetDir,
                targetScope: best.Value.Scope,
                targetIdentity: best.Value.Identity
            );
        }

        /// <summary>
        /// 補間 α を計算。時間経過で増加。
        /// </summary>
        float ComputeAlpha(float deltaTime)
        {
            var p = _options.BlendParams;

            // _homingT を進行
            _homingT = Mathf.Min(p.MaxAlpha, _homingT + deltaTime * p.BlendSpeed);

            // カーブがあれば適用
            if (p.BlendCurve != null && p.BlendCurve.keys.Length > 0)
                return p.BlendCurve.Evaluate(_homingT);

            return _homingT;
        }

        // ================================================================
        // IDisposable
        // ================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
        }
    }
}
