// Game.Movement.MovementChannelRuntime.cs
//
// Movement チャネルのランタイム状態。


#nullable enable
using System;
using UnityEngine;
using Game.CameraSystem;
using Game.Common;

namespace Game.Movement
{
    /// <summary>
    /// Movement チャネルのランタイム状態。
    /// 目標速度と実際の速度を分離し、SmoothedVector3 によって滑らかに近づける。
    /// </summary>
    public sealed class MovementChannelRuntime : IMovementChannelHandle, IEnabledLayerState
    {
        readonly string _key;
        readonly BoolLayer _enabledLayer;
        SmoothedVector3 _smoothedVelocity;
        Vector2 _pendingForce;
        int _priority;
        MovementBlendOp _blendOp;
        float _influence;
        float _lambda;
        float _decelLambda;

        /// <summary>優先度変更時のイベント</summary>
        public event System.Action? OnPriorityChanged;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public MovementChannelRuntime(string key, MovementChannelDef def)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            if (def == null)
                throw new ArgumentNullException(nameof(def));

            _key = key;
            _priority = def.Priority;
            _blendOp = def.BlendOp;
            _influence = def.Influence;
            _lambda = Mathf.Max(0f, def.SmoothingLambda);
            _decelLambda = Mathf.Max(0f, def.DecelerationLambda);

            _enabledLayer = new BoolLayer(BoolCompositionMode.AllTrue);
            _enabledLayer.Set("default", def.EnabledByDefault);

            _smoothedVelocity = new SmoothedVector3(Vector3.zero);
            _pendingForce = Vector2.zero;
        }

        /// <summary>チャネルキー</summary>
        public string Key => _key;

        /// <summary>有効状態（BoolLayer 合成結果）</summary>
        public bool Enabled => _enabledLayer.Value;

        /// <summary>既存 API との互換性を保った target 設定</summary>
        public Vector2 Velocity
        {
            get => CurrentVelocity;
            set => SetTargetVelocity(value);
        }

        /// <summary>現在の速度（SmoothedVector3 の実測値）</summary>
        public Vector2 CurrentVelocity => ToVector2(_smoothedVelocity.Current);

        /// <summary>目標速度（SmoothedVector3 に与えたターゲット値）</summary>
        public Vector2 TargetVelocity => ToVector2(_smoothedVelocity.Target);

        /// <summary>ラムダ（滑らかさ）</summary>
        public float Lambda
        {
            get => _lambda;
            set => _lambda = Mathf.Max(0f, value);
        }

        /// <summary>減速専用ラムダ</summary>
        public float DecelerationLambda
        {
            get => _decelLambda;
            set => _decelLambda = Mathf.Max(0f, value);
        }

        /// <summary>ラムダを明示して目標速度を更新</summary>
        public void SetTargetVelocity(Vector2 target, float? lambda = null)
        {
            float lambdaValue = lambda.HasValue ? Mathf.Max(0f, lambda.Value) : _lambda;
            _lambda = lambdaValue;
            _smoothedVelocity.SetTarget(ToVector3(target), _lambda);
        }

        /// <summary>即座に速度を切り替える</summary>
        public void SetImmediateVelocity(Vector2 value)
        {
            _pendingForce = Vector2.zero;
            _smoothedVelocity.SetImmediate(ToVector3(value));
        }

        /// <summary>瞬間的な力を追加</summary>
        public void AddForce(Vector2 force)
        {
            _pendingForce += force;
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
        public MovementBlendOp BlendOp
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

        /// <summary>
        /// Pending Force をターゲットに加算する。
        /// </summary>
        internal void ApplyPendingForce()
        {
            if (_pendingForce == Vector2.zero)
                return;

            var nextTarget = TargetVelocity + _pendingForce;
            _pendingForce = Vector2.zero;
            _smoothedVelocity.SetTarget(ToVector3(nextTarget), _lambda);
        }

        /// <summary>
        /// deltaTime に沿って速度を進める。
        /// </summary>
        internal void Advance(float deltaTime)
        {
            if (_decelLambda > 0f)
            {
                var current = _smoothedVelocity.Current;
                var target = _smoothedVelocity.Target;
                if (target.sqrMagnitude < current.sqrMagnitude)
                {
                    _smoothedVelocity.TickWithLambda(deltaTime, _decelLambda);
                    return;
                }
            }

            _smoothedVelocity.Tick(deltaTime);
        }

        /// <summary>
        /// 速度をリセットして、現在値とターゲットをゼロにする。
        /// </summary>
        internal void ResetVelocity()
        {
            _pendingForce = Vector2.zero;
            _smoothedVelocity.SetImmediate(Vector3.zero);
        }

        static Vector3 ToVector3(Vector2 value) => new(value.x, value.y, 0f);

        static Vector2 ToVector2(Vector3 value) => new(value.x, value.y);
    }
}
