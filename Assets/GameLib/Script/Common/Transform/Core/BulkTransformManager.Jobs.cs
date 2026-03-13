#if !UNITY_WEBGL || UNITY_EDITOR
#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using VContainer.Unity;

namespace Game.TransformSystem
{
    public sealed class BulkTransformManagerJobs : IDisposable, IBulkTransformManager, IBulkTransformTransformBridge, ITickPhase
    {
        const int DefaultCapacity = 8192;

        struct Slot
        {
            public int DenseIndex;
            public int Generation;
            public ITransformIndexReceiver? Receiver;
        }

        Slot[] _slots;
        readonly Stack<int> _freeSlotIds;
        int _nextSlotId;

        TransformAccessArray _transforms;
        NativeList<float3> _positions;
        NativeList<float3> _velocities;
        NativeList<float> _rotations;
        NativeList<float> _angularVelocities;
        readonly List<int> _denseToSlotId;
        readonly List<Transform> _transformList;

        JobHandle _inFlightHandle;
        bool _hasInFlight;

        int _count;
        readonly int _maxCapacity;
        bool _disposed;

        public int Count => _count;
        public int MaxCapacity => _maxCapacity;

        public TickPhase Phase => TickPhase.Late;

        public BulkTransformManagerJobs(int maxCapacity = DefaultCapacity)
        {
            _maxCapacity = maxCapacity;

            _slots = new Slot[maxCapacity];
            for (int i = 0; i < maxCapacity; i++) _slots[i].DenseIndex = -1;

            _freeSlotIds = new Stack<int>(256);
            _nextSlotId = 0;

            _transforms = new TransformAccessArray(maxCapacity);
            _positions = new NativeList<float3>(maxCapacity, Allocator.Persistent);
            _velocities = new NativeList<float3>(maxCapacity, Allocator.Persistent);
            _rotations = new NativeList<float>(maxCapacity, Allocator.Persistent);
            _angularVelocities = new NativeList<float>(maxCapacity, Allocator.Persistent);
            _denseToSlotId = new List<int>(maxCapacity);
            _transformList = new List<Transform>(maxCapacity);

            _count = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            CompleteInFlight();

            _transforms.Dispose();
            if (_positions.IsCreated) _positions.Dispose();
            if (_velocities.IsCreated) _velocities.Dispose();
            if (_rotations.IsCreated) _rotations.Dispose();
            if (_angularVelocities.IsCreated) _angularVelocities.Dispose();

            _denseToSlotId.Clear();
            _freeSlotIds.Clear();
            _slots = null!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CompleteInFlight()
        {
            if (_hasInFlight)
            {
                _inFlightHandle.Complete();
                _hasInFlight = false;
            }
        }

        public TransformHandle Register(
            Transform transform,
            float3 initialVelocity = default,
            float initialAngularVelocity = 0f,
            ITransformIndexReceiver? receiver = null)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (_count >= _maxCapacity) throw new InvalidOperationException($"BulkTransformManagerJobs capacity exceeded: {_maxCapacity}");

            CompleteInFlight();

            int slotId = _freeSlotIds.Count > 0 ? _freeSlotIds.Pop() : _nextSlotId++;
            if (_nextSlotId > _slots.Length) throw new InvalidOperationException("Slot array exhausted");

            int denseIndex = _count;

            _transforms.Add(transform);

            var pos = (float3)(Vector3)transform.position;
            var rot = transform.eulerAngles.z;
            _positions.Add(pos);
            _velocities.Add(initialVelocity);
            _rotations.Add(rot);
            _angularVelocities.Add(initialAngularVelocity);
            _denseToSlotId.Add(slotId);

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

            CompleteInFlight();

            if (_count <= 0) return false;

            int lastDense = _count - 1;
            if (denseIndex < 0 || denseIndex > lastDense) return false;
            if (_denseToSlotId.Count <= lastDense) return false;

            if (denseIndex != lastDense)
            {
                int movedSlotId = _denseToSlotId[lastDense];
                ref var movedSlot = ref _slots[movedSlotId];

                movedSlot.DenseIndex = denseIndex;
                if (movedSlot.Receiver != null)
                    movedSlot.Receiver.DenseIndex = denseIndex;

                _denseToSlotId[denseIndex] = movedSlotId;
            }

            ref var removedSlot = ref _slots[slotId];
            removedSlot.DenseIndex = -1;
            removedSlot.Receiver = null;
            _freeSlotIds.Push(slotId);

            _denseToSlotId.RemoveAt(lastDense);

            _positions.RemoveAtSwapBack(denseIndex);
            _velocities.RemoveAtSwapBack(denseIndex);
            _rotations.RemoveAtSwapBack(denseIndex);
            _angularVelocities.RemoveAtSwapBack(denseIndex);
            _transforms.RemoveAtSwapBack(denseIndex);

            _count--;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ValidateHandle(TransformHandle handle, out int slotId, out int denseIndex)
        {
            slotId = handle.Id;
            denseIndex = -1;

            if (_slots == null) return false;
            if ((uint)slotId >= (uint)_slots.Length) return false;

            ref var slot = ref _slots[slotId];
            if (slot.Generation != handle.Generation || slot.DenseIndex < 0) return false;

            denseIndex = slot.DenseIndex;
            return true;
        }

        public bool IsValid(TransformHandle handle) => ValidateHandle(handle, out _, out _);

        public void SetVelocity(TransformHandle handle, float3 velocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            CompleteInFlight();
            _velocities[denseIndex] = velocity;
        }

        public bool TryGetVelocity(TransformHandle handle, out float3 velocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                velocity = default;
                return false;
            }
            CompleteInFlight();
            velocity = _velocities[denseIndex];
            return true;
        }

        public void Teleport(TransformHandle handle, float3 position)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            CompleteInFlight();
            _positions[denseIndex] = position;
        }

        public bool TryGetPosition(TransformHandle handle, out float3 position)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                position = default;
                return false;
            }
            CompleteInFlight();
            position = _positions[denseIndex];
            return true;
        }

        public bool IsManaged(Transform transform)
        {
            if (_disposed || transform == null) return false;
            CompleteInFlight();

            for (int i = 0; i < _count; i++)
            {
                var t = _transforms[i];
                if (ReferenceEquals(t, transform))
                    return true;
            }
            return false;
        }

        public bool TryTeleportTransform(Transform transform, float3 position)
        {
            if (_disposed || transform == null) return false;
            CompleteInFlight();

            for (int i = 0; i < _count; i++)
            {
                var t = _transforms[i];
                if (!ReferenceEquals(t, transform))
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
            CompleteInFlight();

            for (int i = 0; i < _count; i++)
            {
                var t = _transforms[i];
                if (!ReferenceEquals(t, transform))
                    continue;

                position = _positions[i];
                return true;
            }

            return false;
        }

        public void SetAngularVelocity(TransformHandle handle, float angularVelocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            CompleteInFlight();
            _angularVelocities[denseIndex] = angularVelocity;
        }

        public bool TryGetAngularVelocity(TransformHandle handle, out float angularVelocity)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                angularVelocity = default;
                return false;
            }

            CompleteInFlight();
            angularVelocity = _angularVelocities[denseIndex];
            return true;
        }

        public void SetRotation(TransformHandle handle, float rotation)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex)) return;
            CompleteInFlight();
            _rotations[denseIndex] = rotation;
        }

        public bool TryGetRotation(TransformHandle handle, out float rotation)
        {
            if (!ValidateHandle(handle, out _, out int denseIndex))
            {
                rotation = default;
                return false;
            }

            CompleteInFlight();
            rotation = _rotations[denseIndex];
            return true;
        }

        public void SetAllVelocities(float3 velocity)
        {
            CompleteInFlight();
            for (int i = 0; i < _count; i++) _velocities[i] = velocity;
        }

        public void SetAllAngularVelocities(float angularVelocity)
        {
            CompleteInFlight();
            for (int i = 0; i < _count; i++) _angularVelocities[i] = angularVelocity;
        }

        public void Tick() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (_disposed || _count == 0) return;

            CompleteInFlight();

            var updateJob = new UpdateTransformsJob
            {
                Positions = _positions.AsArray(),
                Velocities = _velocities.AsArray(),
                Rotations = _rotations.AsArray(),
                AngularVelocities = _angularVelocities.AsArray(),
                DeltaTime = deltaTime,
            };
            var updateHandle = updateJob.Schedule(_count, 64);

            var applyJob = new ApplyTransformDataJob
            {
                Positions = _positions.AsArray(),
                Rotations = _rotations.AsArray(),
            };
            _inFlightHandle = applyJob.Schedule(_transforms, updateHandle);
            _hasInFlight = true;

            // 同期待ちしたいならここで完了（現行踏襲）
            CompleteInFlight();
        }

        public void TickAsync(float deltaTime)
        {
            if (_disposed || _count == 0) return;

            CompleteInFlight();

            var updateJob = new UpdateTransformsJob
            {
                Positions = _positions.AsArray(),
                Velocities = _velocities.AsArray(),
                Rotations = _rotations.AsArray(),
                AngularVelocities = _angularVelocities.AsArray(),
                DeltaTime = deltaTime,
            };
            var updateHandle = updateJob.Schedule(_count, 64);

            var applyJob = new ApplyTransformDataJob
            {
                Positions = _positions.AsArray(),
                Rotations = _rotations.AsArray(),
            };
            _inFlightHandle = applyJob.Schedule(_transforms, updateHandle);
            _hasInFlight = true;
        }

        public void CleanupDestroyed()
        {
            CompleteInFlight();

            // 現行踏襲：receiver が UnityEngine.Object で死んでたら消す
            for (int slotId = _nextSlotId - 1; slotId >= 0; slotId--)
            {
                ref var slot = ref _slots[slotId];
                if (slot.DenseIndex < 0) continue;

                if (slot.Receiver is UnityEngine.Object obj && obj == null)
                {
                    var handle = new TransformHandle(slotId, slot.Generation);
                    Unregister(handle);
                }
            }
        }

        [BurstCompile]
        public struct UpdateTransformsJob : IJobParallelFor
        {
            public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float3> Velocities;
            public NativeArray<float> Rotations;
            [ReadOnly] public NativeArray<float> AngularVelocities;
            public float DeltaTime;

            public void Execute(int index)
            {
                Positions[index] += Velocities[index] * DeltaTime;
                Rotations[index] += AngularVelocities[index] * DeltaTime;
            }
        }

        [BurstCompile]
        public struct ApplyTransformDataJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float> Rotations;

            public void Execute(int index, TransformAccess transform)
            {
                var p = Positions[index];
                transform.position = new Vector3(p.x, p.y, p.z);
                transform.rotation = Quaternion.Euler(0f, 0f, Rotations[index]);
            }
        }
    }
}
#endif
