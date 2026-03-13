// Game.Rotation.RotationAdapters.cs
//
// Rotation 出力アダプタ（Transform, Rigidbody2D）。

using System;
using UnityEngine;

namespace Game.Rotation
{
    /// <summary>
    /// Rotation アダプタの基底インターフェース。
    /// </summary>
    public interface IRotationAdapter : IDisposable
    {
        /// <summary>更新</summary>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Transform への Rotation 出力アダプタ。
    /// Output.Value (degrees/sec) * deltaTime を Transform.rotation に加算。
    /// </summary>
    public sealed class TransformRotationAdapter : IRotationAdapter
    {
        readonly UnityEngine.Transform _transform;
        readonly IRotateOutput _output;
        bool _disposed;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public TransformRotationAdapter(UnityEngine.Transform transform, IRotateOutput output)
        {
            _transform = transform ?? throw new ArgumentNullException(nameof(transform));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// 更新（変更があった場合のみ Transform に反映）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || _transform == null) return;

            var angularVelocity = _output.Value;
            if (Mathf.Abs(angularVelocity) > 0.0001f)
            {
                // Z軸回転（2D用）
                _transform.Rotate(0f, 0f, angularVelocity * deltaTime);
            }
        }

        /// <summary>
        /// リソース解放。
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Rigidbody2D への Rotation 出力アダプタ。
    /// Output.Value を Rigidbody2D.angularVelocity に反映。
    /// </summary>
    public sealed class Rigidbody2DRotationAdapter : IRotationAdapter
    {
        readonly Rigidbody2D _rb;
        readonly IRotateOutput _output;
        bool _disposed;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public Rigidbody2DRotationAdapter(Rigidbody2D rb, IRotateOutput output)
        {
            _rb = rb ?? throw new ArgumentNullException(nameof(rb));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>
        /// 更新（変更があった場合のみ Rigidbody に反映）。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || _rb == null) return;

            var angularVelocity = _output.Value;

            // Keep output authoritative even when another system touches angular velocity.
            if (Mathf.Abs(angularVelocity) <= 0.0001f)
            {
                if (Mathf.Abs(_rb.angularVelocity) > 0.0001f)
                    _rb.angularVelocity = 0f;
                return;
            }

            if (_rb.bodyType == RigidbodyType2D.Kinematic)
            {
                _rb.MoveRotation(_rb.rotation + angularVelocity * deltaTime);
                return;
            }

            _rb.angularVelocity = angularVelocity;
        }

        /// <summary>
        /// リソース解放。
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }
}
