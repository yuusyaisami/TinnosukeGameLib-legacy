#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Collision
{
    [Serializable]
    public sealed class CollisionNamedIdEntry
    {
        [SerializeField] string displayName = string.Empty;
        [SerializeField, TextArea(1, 3)] string description = string.Empty;
        [SerializeField] byte value;

        public string DisplayName => displayName;
        public string Description => description;
        public byte Value => value;

        public CollisionNamedIdEntry(string displayName, string description, byte value)
        {
            this.displayName = displayName ?? string.Empty;
            this.description = description ?? string.Empty;
            this.value = value;
        }
    }

    [Serializable]
    public struct StaticToDynamicSetMapping
    {
        [SerializeField] StaticColliderKind staticKind;
        [SerializeField] byte dynamicSetValue;

        public StaticColliderKind StaticKind => staticKind;
        public byte DynamicSetValue => dynamicSetValue;

        public StaticToDynamicSetMapping(StaticColliderKind staticKind, DynamicColliderSetId dynamicSetId)
        {
            this.staticKind = staticKind;
            dynamicSetValue = (byte)dynamicSetId;
        }
    }

    [CreateAssetMenu(
        fileName = "CollisionIdCatalog",
        menuName = "Game/Collision/Collision Id Catalog")]
    public sealed class CollisionIdCatalogSO : ScriptableObject
    {
        [SerializeField] List<CollisionNamedIdEntry> dynamicSets = new();
        [SerializeField] List<CollisionNamedIdEntry> staticKinds = new();
        [SerializeField] List<StaticToDynamicSetMapping> staticProxyMappings = new();

        public IReadOnlyList<CollisionNamedIdEntry> DynamicSets => dynamicSets;
        public IReadOnlyList<CollisionNamedIdEntry> StaticKinds => staticKinds;
        public IReadOnlyList<StaticToDynamicSetMapping> StaticProxyMappings => staticProxyMappings;

        public bool EnsureDefaultsIfNeeded()
        {
            var changed = false;

            if (dynamicSets == null || dynamicSets.Count == 0)
            {
                dynamicSets = CreateDefaultDynamicEntries();
                changed = true;
            }

            if (staticKinds == null || staticKinds.Count == 0)
            {
                staticKinds = CreateDefaultStaticEntries();
                changed = true;
            }

            if (staticProxyMappings == null || staticProxyMappings.Count == 0)
            {
                staticProxyMappings = CreateDefaultProxyMappings();
                changed = true;
            }

            return changed;
        }

        public bool TryGetDynamicName(byte value, out string displayName)
        {
            for (int i = 0; i < dynamicSets.Count; i++)
            {
                var entry = dynamicSets[i];
                if (entry != null && entry.Value == value)
                {
                    displayName = entry.DisplayName;
                    return true;
                }
            }

            displayName = string.Empty;
            return false;
        }

        public bool TryGetStaticName(byte value, out string displayName)
        {
            for (int i = 0; i < staticKinds.Count; i++)
            {
                var entry = staticKinds[i];
                if (entry != null && entry.Value == value)
                {
                    displayName = entry.DisplayName;
                    return true;
                }
            }

            displayName = string.Empty;
            return false;
        }

        public bool TryResolveStaticProxySet(StaticColliderKind kind, out DynamicColliderSetId setId)
        {
            for (int i = 0; i < staticProxyMappings.Count; i++)
            {
                var mapping = staticProxyMappings[i];
                if (mapping.StaticKind == kind)
                {
                    setId = (DynamicColliderSetId)mapping.DynamicSetValue;
                    return true;
                }
            }

            setId = default;
            return false;
        }

        public void ResetToDefault()
        {
            dynamicSets = CreateDefaultDynamicEntries();
            staticKinds = CreateDefaultStaticEntries();
            staticProxyMappings = CreateDefaultProxyMappings();
        }

        internal static List<CollisionNamedIdEntry> CreateDefaultDynamicEntries()
        {
            return new List<CollisionNamedIdEntry>
            {
                new("None", "Unassigned", (byte)DynamicColliderSetId.None),
                new("Ball", "Legacy ball collider set.", (byte)DynamicColliderSetId.Ball),
                new("Nail", "Legacy nail collider set.", (byte)DynamicColliderSetId.Nail),
                new("Obstacle Box", "Legacy moving obstacle collider set.", (byte)DynamicColliderSetId.ObstacleBox),
                new("Player Hurtbox", "Player body / hurtbox.", (byte)DynamicColliderSetId.PlayerHurtbox),
                new("Enemy Hurtbox", "Enemy body / hurtbox.", (byte)DynamicColliderSetId.EnemyHurtbox),
                new("Player Bullet", "Projectile owned by player.", (byte)DynamicColliderSetId.PlayerBullet),
                new("Enemy Bullet", "Projectile owned by enemy.", (byte)DynamicColliderSetId.EnemyBullet),
                new("Obstacle", "Generic obstacle proxy set.", (byte)DynamicColliderSetId.Obstacle),
            };
        }

        internal static List<CollisionNamedIdEntry> CreateDefaultStaticEntries()
        {
            return new List<CollisionNamedIdEntry>
            {
                new("Stage Geometry", "Default static stage geometry.", (byte)StaticColliderKind.StageGeometry),
                new("Boundary", "Out-of-bounds or screen boundary.", (byte)StaticColliderKind.Boundary),
                new("Living Wall", "Temporary wall for living state.", (byte)StaticColliderKind.LivingWall),
                new("Necro Wall", "Temporary wall for necro state.", (byte)StaticColliderKind.NecroWall),
            };
        }

        internal static List<StaticToDynamicSetMapping> CreateDefaultProxyMappings()
        {
            return new List<StaticToDynamicSetMapping>
            {
                new(StaticColliderKind.StageGeometry, DynamicColliderSetId.Obstacle),
                new(StaticColliderKind.Boundary, DynamicColliderSetId.Obstacle),
                new(StaticColliderKind.LivingWall, DynamicColliderSetId.Obstacle),
                new(StaticColliderKind.NecroWall, DynamicColliderSetId.Obstacle),
            };
        }
    }
}
