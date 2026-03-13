#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MapNode
{
    [CreateAssetMenu(
        fileName = "MapNodeGenerateSettings",
        menuName = "Game/MapNode/Generate Settings")]
    public sealed class MapNodeGenerateSettingsSO : ScriptableObject
    {
        [BoxGroup("Graph")]
        [MinValue(1)]
        public int Depth = 5;

        [BoxGroup("Graph")]
        public DynamicValue<int> WidthPerLayer;

        [BoxGroup("Type Weights")]
        public List<MapNodeTypeWeight> DefaultTypeWeights = new();

        [BoxGroup("Overrides")]
        public List<MapNodeLayerOverride> LayerOverrides = new();

        [BoxGroup("Overrides")]
        public List<MapNodeTypeOverride> TypeOverrides = new();

        [BoxGroup("Connection")]
        public ConnectionRuleSet ConnectionRules = ConnectionRuleSet.Default;

        [BoxGroup("Random")]
        public bool Deterministic = true;

        [BoxGroup("Random")]
        [ShowIf(nameof(Deterministic))]
        public int Seed = 0;
    }

    [Serializable]
    public struct MapNodeTypeWeight
    {
        public MapNodeType Type;
        [MinValue(0)]
        public int Weight;
    }

    [Serializable]
    public sealed class MapNodeLayerOverride
    {
        public int LayerIndex;
        [ToggleLeft]
        public bool UseWidthOverride;
        [ShowIf(nameof(UseWidthOverride))]
        [MinValue(1)]
        public int OverrideWidth = 1;
        public List<MapNodeTypeWeight> OverrideWeights = new();
        public ConnectionRuleSet OverrideConnection = ConnectionRuleSet.Default;
    }

    [Serializable]
    public sealed class MapNodeTypeOverride
    {
        public MapNodeType Type;
        [MinValue(0)]
        public int MinCount;
        [MinValue(0)]
        public int MaxCount;
        public List<int> AllowedLayers = new();
    }

    [Serializable]
    public struct ConnectionRuleSet
    {
        public bool SameIndexGuaranteed;
        public int NeighborMin;
        public int NeighborMax;
        [Range(0f, 1f)]
        public float ExtraConnectionChance;
        public bool EnsureInbound;

        public static ConnectionRuleSet Default => new ConnectionRuleSet
        {
            SameIndexGuaranteed = true,
            NeighborMin = -1,
            NeighborMax = 1,
            ExtraConnectionChance = 0.15f,
            EnsureInbound = true
        };
    }
}
