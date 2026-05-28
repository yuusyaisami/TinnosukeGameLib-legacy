#nullable enable
using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;

namespace Game.Kernel.Layers
{
    public sealed class SceneKernelEntityLeaseTable
    {
        readonly SceneKernelHandle sceneHandle;
        readonly Dictionary<EntityRef, int> slotIndicesByEntityRef = new Dictionary<EntityRef, int>();
        readonly List<SceneKernelEntityLeaseSlot> slots = new List<SceneKernelEntityLeaseSlot>();
        int activeLeaseCount;

        public SceneKernelEntityLeaseTable(SceneKernelHandle sceneHandle)
        {
            if (sceneHandle.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(sceneHandle), sceneHandle, "SceneKernel entity lease tables require a positive scene handle.");

            this.sceneHandle = sceneHandle;
        }

        public SceneKernelHandle SceneHandle => sceneHandle;

        public int ActiveLeaseCount => activeLeaseCount;

        public void Clear()
        {
            slotIndicesByEntityRef.Clear();
            slots.Clear();
            activeLeaseCount = 0;
        }

        public bool TryAcquire(EntityRef entityRef, out SceneKernelEntityLeaseHandle lease)
        {
            if (!TryAcquireSlot(entityRef, out int slotIndex, out SceneKernelEntityLeaseSlot slot, out lease))
                return false;

            slot.Generation = slot.Generation <= 0 ? 1 : checked(slot.Generation + 1);
            slot.IsActive = true;
            slots[slotIndex] = slot;
            activeLeaseCount++;
            lease = CreateLease(entityRef, slotIndex, slot.Generation);
            return true;
        }

        public bool TryGetLease(EntityRef entityRef, out SceneKernelEntityLeaseHandle lease)
        {
            if (entityRef.IsEmpty || !slotIndicesByEntityRef.TryGetValue(entityRef, out int slotIndex))
            {
                lease = default;
                return false;
            }

            SceneKernelEntityLeaseSlot slot = slots[slotIndex];
            if (!slot.IsActive)
            {
                lease = default;
                return false;
            }

            lease = CreateLease(entityRef, slotIndex, slot.Generation);
            return true;
        }

        public bool ValidateLease(SceneKernelEntityLeaseHandle lease)
        {
            if (!IsLeaseRoutedToThisTable(lease, out int slotIndex))
                return false;

            SceneKernelEntityLeaseSlot slot = slots[slotIndex];
            return slot.IsActive && slot.Generation == lease.Generation;
        }

        public bool TryRelease(SceneKernelEntityLeaseHandle lease)
        {
            if (!IsLeaseRoutedToThisTable(lease, out int slotIndex))
                return false;

            SceneKernelEntityLeaseSlot slot = slots[slotIndex];
            if (!slot.IsActive || slot.Generation != lease.Generation)
                return false;

            slot.IsActive = false;
            slots[slotIndex] = slot;
            activeLeaseCount--;
            return true;
        }

        bool TryAcquireSlot(EntityRef entityRef, out int slotIndex, out SceneKernelEntityLeaseSlot slot, out SceneKernelEntityLeaseHandle lease)
        {
            if (entityRef.IsEmpty)
            {
                slotIndex = -1;
                slot = default;
                lease = default;
                return false;
            }

            if (slotIndicesByEntityRef.TryGetValue(entityRef, out slotIndex))
            {
                slot = slots[slotIndex];
                if (slot.IsActive)
                {
                    lease = default;
                    return false;
                }

                lease = default;
                return true;
            }

            slotIndex = slots.Count;
            slot = new SceneKernelEntityLeaseSlot();
            slots.Add(slot);
            slotIndicesByEntityRef.Add(entityRef, slotIndex);
            lease = default;
            return true;
        }

        bool IsLeaseRoutedToThisTable(SceneKernelEntityLeaseHandle lease, out int slotIndex)
        {
            slotIndex = -1;

            if (!lease.SceneHandle.Equals(sceneHandle) || lease.EntityRef.IsEmpty || lease.LeaseId <= 0)
                return false;

            slotIndex = lease.LeaseId - 1;
            if ((uint)slotIndex >= (uint)slots.Count)
                return false;

            if (!slotIndicesByEntityRef.TryGetValue(lease.EntityRef, out int mappedSlotIndex) || mappedSlotIndex != slotIndex)
                return false;

            return true;
        }

        SceneKernelEntityLeaseHandle CreateLease(EntityRef entityRef, int slotIndex, int generation)
        {
            return new SceneKernelEntityLeaseHandle(sceneHandle, entityRef, slotIndex + 1, generation);
        }

        sealed class SceneKernelEntityLeaseSlot
        {
            public int Generation { get; set; }

            public bool IsActive { get; set; }
        }
    }
}