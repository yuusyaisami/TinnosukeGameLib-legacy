#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Collision
{
    [Serializable]
    public struct DynamicColliderSetRef : IEquatable<DynamicColliderSetRef>
    {
        [SerializeField]
        [ValueDropdown(nameof(GetOptions))]
        byte value;

        public DynamicColliderSetId Id => (DynamicColliderSetId)value;
        public byte RawValue => value;

        public DynamicColliderSetRef(DynamicColliderSetId setId)
        {
            value = (byte)setId;
        }

        public void Set(DynamicColliderSetId setId)
        {
            value = (byte)setId;
        }

        public override string ToString() => CollisionIdCatalogLocator.GetDynamicDisplayName(value);
        public bool Equals(DynamicColliderSetRef other) => value == other.value;
        public override bool Equals(object? obj) => obj is DynamicColliderSetRef other && Equals(other);
        public override int GetHashCode() => value;
        public static implicit operator DynamicColliderSetId(DynamicColliderSetRef reference) => reference.Id;
        public static implicit operator byte(DynamicColliderSetRef reference) => reference.value;

        static IEnumerable<ValueDropdownItem<byte>> GetOptions() => CollisionIdCatalogLocator.GetDynamicOptions();
    }

    [Serializable]
    public struct StaticColliderKindRef : IEquatable<StaticColliderKindRef>
    {
        [SerializeField]
        [ValueDropdown(nameof(GetOptions))]
        byte value;

        public StaticColliderKind Id => (StaticColliderKind)value;
        public byte RawValue => value;

        public StaticColliderKindRef(StaticColliderKind kind)
        {
            value = (byte)kind;
        }

        public void Set(StaticColliderKind kind)
        {
            value = (byte)kind;
        }

        public override string ToString() => CollisionIdCatalogLocator.GetStaticDisplayName(value);
        public bool Equals(StaticColliderKindRef other) => value == other.value;
        public override bool Equals(object? obj) => obj is StaticColliderKindRef other && Equals(other);
        public override int GetHashCode() => value;
        public static implicit operator StaticColliderKind(StaticColliderKindRef reference) => reference.Id;
        public static implicit operator byte(StaticColliderKindRef reference) => reference.value;

        static IEnumerable<ValueDropdownItem<byte>> GetOptions() => CollisionIdCatalogLocator.GetStaticOptions();
    }

    public static class CollisionAuthoringArrayUtility
    {
        public static DynamicColliderSetId[] ConvertDynamicSets(DynamicColliderSetRef[]? values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<DynamicColliderSetId>();

            var result = new DynamicColliderSetId[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i].Id;

            return result;
        }

        public static StaticColliderKind[] ConvertStaticKinds(StaticColliderKindRef[]? values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<StaticColliderKind>();

            var result = new StaticColliderKind[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = values[i].Id;

            return result;
        }
    }
}
