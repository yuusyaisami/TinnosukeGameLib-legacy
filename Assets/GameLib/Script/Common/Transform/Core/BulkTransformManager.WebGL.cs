#if UNITY_WEBGL && !UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using VContainer.Unity;

namespace Game.TransformSystem
{
    /// <summary>
    /// WebGL 向け：Jobs/Burst を使わずメインスレッドで一括更新する実装。
    /// - for ループで Positions/Rotations を更新
    /// - Transform に直接反映
    /// </summary>
        public sealed class BulkTransformManagerWebGL : IDisposable, IBulkTransformManager, IBulkTransformTransformBridge, ITickPhase
    {
        const int DefaultCapacity = 8192;

        struct Slot
        {
            public int DenseIndex; // -1 = unused
            public int Generation;
            public ITransformIndexReceiver? Receiver;
        }

        Slot[] _slots;
        readonly Stack<int> _freeSlotIds;
        int _nextSlotId;

        // Dense arrays (fixed capacity)
        readonly Transform[] _transforms;
        readonly float3[] _positions;
        readonly float3[] _velocities;
        readonly float[] _rotations;
        readonly float[] _angularVelocities;
        readonly int[] _denseToSlotId;

        int _count;
        readonly int _maxCapacity;
        bool _disposed;

        public int Count => _count;
        public int MaxCapacity => _maxCapacity;

        public TickPhase Phase => TickPhase.Late;

        public BulkTransformManagerWebGL(int maxCapacity = DefaultCapacity)
        {
            _maxCapacity = maxCapacity;

            _slots = new Slot[maxCapacity];
            for (int i = 0; i < maxCapacity; i++)
                _slots[i].DenseIndex = -1;

            _freeSlotIds = new Stack<int>(256);
            _nextSlotId = 0;

            _transforms = new Transform[maxCapacity];
            _positions = new float3[maxCapacity];
            _velocities = new float3[maxCapacity];
            _rotations = new float[maxCapacity];
            _angularVelocities = new float[maxCapacity];
            _denseToSlotId = new int[maxCapacity];

            _count = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _freeSlotIds.Clear();
            _slots = Array.Empty<Slot>();
            _count = 0;
            _nextSlotId = 0;
        }

        public TransformHandle Register(
            Transform transform,
            float3 initialVelocity = default,
            float initialAngularVelocity = 0f,
            ITransformIndexReceiver? receiver = null)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (_disposed) throw new ObjectDisposedException(nameof(BulkTransformManagerWebGL));
            if (_count >= _maxCapacity) throw new InvalidOperationException($"BulkTransformManagerWebGL capacity exceeded: {_maxCapacity}");

            int slotId = _freeSlotIds.Count > 0 ? _freeSlotIds.Pop() : _nextSlotId++;
            if (slotId < 0 || slotId >= _slots.Length)
                throw new InvalidOperationException("Slot array exhausted");

            int denseIndex = _count;

            _transforms[denseIndex] = transform;
            var pos = (float3)(Vector3)transform.position;
            var rot = transform.eulerAngles.z;

            _positions[denseIndex] = pos;
            _velocities[denseIndex] = initialVelocity;
            _rotations[denseIndex] = rot;
            _angularVelocities[denseIndex] = initialAngularVelocity;
            _denseToSlotId[denseIndex] = slotId;

            ref var slot = ref _slots[slotId];
            slot.DenseIndex = denseIndex;
            slot.Generation++;
            slot.Receiver = receiver;

            if (receiver != null) receiver.DenseIndex = denseIndex;

            _count++;
            return new TransformHandle(slotId, slot.Generation);
        }

        public bool Unregister(TransformHandle handle)
        {
            if (_disposed) return false;
            if (!ValidateHandle(handle, out int slotId, out int denseIndex)) return false;
            if (_count <= 0) return false;

            int lastDense = _count - 1;
            if (denseIndex < 0 || denseIndex > lastDense) return false;

            if (denseIndex != lastDense)
            {
                // move last -> denseIndex
                _transforms[denseIndex] = _transforms[lastDense];
                _positions[denseIndex] = _positions[lastDense];
                _velocities[denseIndex] = _velocities[lastDense];
                _rotations[denseIndex] = _rotations[lastDense];
                _angularVelocities[denseIndex] = _angularVelocities[lastDense];

                int movedSlotId = _denseToSlotId[lastDense];
                _denseToSlotId[denseIndex] = movedSlotId;

                ref var movedSlot = ref _slots[movedSlotId];
                movedSlot.DenseIndex = denseIndex;
                if (movedSlot.Receiver != null)
                    movedSlot.Receiver.DenseIndex = denseIndex;
            }

            // clear last
            _transforms[lastDense] = null!;
            _positions[lastDense] = default;
            _velocities[lastDense] = default;
            _rotations[lastDense] = 0f;
            _angularVelocities[lastDense] = 0f;
            _denseToSlotId[lastDense] = 0;

            ref var removedSlot = ref _slots[slotId];
            removedSlot.DenseIndex = -1;
            removedSlot.Receiver = null;
            _freeSlotIds.Push(slotId);

            _count--;
            return true;
        }

        public bool IsValid(TransformHandle handle) => ValidateHandle(handle, out _, out _);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ValidateHandle(TransformHandle handle, out int slotId, out int denseIndex)
        {
            slotId = handle.Id;
            denseIndex = -1;

            if (_slots.Length == 0) return false;
            if ((uint)slotId >= (uint)_slots.Length) return false;

            ref var slot = ref _slots[slotId];
            if (slot.Generation != handle.Generation || slot.DenseIndex < 0) return false;

            denseIndex = slot.DenseIndex;
            return true;
        }

        public void SetVelocity(TransformHandle handle, float3 velocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            _velocities[denseIndex] = velocity;
        }

        public bool TryGetVelocity(TransformHandle handle, out float3 velocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                velocity = default;
                return false;
            }
            velocity = _velocities[denseIndex];
            return true;
        }

        public void Teleport(TransformHandle handle, float3 position)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            _positions[denseIndex] = position;
        }

        public bool TryGetPosition(TransformHandle handle, out float3 position)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                position = default;
                return false;
            }
            position = _positions[denseIndex];
            return true;
        }

        public void SetAngularVelocity(TransformHandle handle, float angularVelocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            _angularVelocities[denseIndex] = angularVelocity;
        }

        public bool TryGetAngularVelocity(TransformHandle handle, out float angularVelocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                angularVelocity = default;
                return false;
            }
            angularVelocity = _angularVelocities[denseIndex];
            return true;
        }

        public void SetRotation(TransformHandle handle, float rotation)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            _rotations[denseIndex] = rotation;
        }

        public bool TryGetRotation(TransformHandle handle, out float rotation)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                rotation = default;
                return false;
            }
            rotation = _rotations[denseIndex];
            return true;
        }

        public void SetAllVelocities(float3 velocity)
        {
            for (int i = 0; i < _count; i++) _velocities[i] = velocity;
        }

        public void SetAllAngularVelocities(float angularVelocity)
        {
            for (int i = 0; i < _count; i++) _angularVelocities[i] = angularVelocity;
        }

        public void Tick() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (_disposed || _count == 0) return;

            // Update
            for (int i = 0; i < _count; i++)
            {
                _positions[i] += _velocities[i] * deltaTime;
                _rotations[i] += _angularVelocities[i] * deltaTime;
            }

            // Apply
            for (int i = 0; i < _count; i++)
            {
                var t = _transforms[i];
                if (!t) continue;

                var p = _positions[i];
                t.position = new Vector3(p.x, p.y, p.z);
                t.rotation = Quaternion.AngleAxis(_rotations[i], Vector3.forward);
            }
        }

        /// <summary>
        /// WebGL では非同期スケジューリングできないので、Tick と同義にする。
        /// </summary>
        public void TickAsync(float deltaTime) => Tick(deltaTime);

        public void CleanupDestroyed()
        {
            if (_disposed || _count == 0) return;

            // dense 側から削除する（swap-remove なので i を進めない）
            for (int i = 0; i < _count;)
            {
                bool dead = !_transforms[i];

                if (!dead)
                {
                    // receiver が UnityEngine.Object の場合も破棄検出
                    int slotId = _denseToSlotId[i];
                    ref var slot = ref _slots[slotId];
                    if (slot.Receiver is UnityEngine.Object obj && obj == null)
                        dead = true;
                }

                if (dead)
                {
                    int slotId = _denseToSlotId[i];
                    var handle = new TransformHandle(slotId, _slots[slotId].Generation);
                    Unregister(handle);
                    continue; // swap された要素を同じ i で再チェック
                }

                i++;
            }
        }
        public bool IsManaged(Transform transform)
        {
            if (_disposed || transform == null) return false;
            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_transforms[i], transform))
                    return true;
            }
            return false;
        }

        public bool TryTeleportTransform(Transform transform, float3 position)
        {
            if (_disposed || transform == null) return false;
            for (int i = 0; i < _count; i++)
            {
                if (!ReferenceEquals(_transforms[i], transform))
                    continue;
                _positions[i] = position;
                return true;
            }
            return false;
        }

        public bool TryGetManagedPosition(Transform transform, out float3 position)
        {
            position = default;
            if (_disposed || transform == null) return false;
            for (int i = 0; i < _count; i++)
            {
                if (!ReferenceEquals(_transforms[i], transform))
                    continue;
                position = _positions[i];
                return true;
            }
            return false;
        }
    }
}
#endif
