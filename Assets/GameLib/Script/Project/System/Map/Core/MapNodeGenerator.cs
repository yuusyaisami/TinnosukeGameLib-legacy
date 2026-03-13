#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using UnityEngine;

namespace Game.MapNode
{
    public sealed class MapNodeGenerator
    {
        public MapGraph Generate(MapNodeGenerateSettingsSO settings, IScopeNode scope)
        {
            if (settings == null)
                return new MapGraph(new List<MapNodeLayer>(), new List<MapNode>(), 0, 0);

            var depth = Mathf.Max(1, settings.Depth);
            var context = new SimpleDynamicContext(new VarStore(), scope);

            var layerOverrideMap = BuildLayerOverrideMap(settings.LayerOverrides);

            var layerWidths = new int[depth];
            var maxWidth = 1;
            for (int i = 0; i < depth; i++)
            {
                var width = ResolveLayerWidth(settings.WidthPerLayer, context, layerOverrideMap, i);
                layerWidths[i] = width;
                maxWidth = Mathf.Max(maxWidth, width);
            }

            var rng = settings.Deterministic ? new System.Random(settings.Seed) : new System.Random(Environment.TickCount);

            var layers = new List<MapNodeLayer>(depth);
            var nodes = new List<MapNode>(depth * maxWidth);

            var typeOverrideMap = BuildTypeOverrideMap(settings.TypeOverrides);
            var typeCounts = new Dictionary<MapNodeType, int>();

            for (int layerIndex = 0; layerIndex < depth; layerIndex++)
            {
                var layer = new MapNodeLayer { LayerIndex = layerIndex };
                var width = layerWidths[layerIndex];
                var layerOverride = TryGetLayerOverride(layerOverrideMap, layerIndex);
                var weights = ResolveWeights(settings.DefaultTypeWeights, layerOverride);

                for (int widthIndex = 0; widthIndex < width; widthIndex++)
                {
                    var node = new MapNode
                    {
                        Id = nodes.Count,
                        LayerIndex = layerIndex,
                        WidthIndex = widthIndex,
                        Type = ResolveNodeType(weights, typeOverrideMap, typeCounts, rng, layerIndex),
                        State = MapNodeState.None
                    };

                    nodes.Add(node);
                    layer.NodeIds.Add(node.Id);
                    IncrementCount(typeCounts, node.Type);
                }

                layers.Add(layer);
            }

            BuildConnections(nodes, layers, layerWidths, settings.ConnectionRules, layerOverrideMap, rng);

            return new MapGraph(layers, nodes, depth, maxWidth);
        }

        static Dictionary<int, MapNodeLayerOverride> BuildLayerOverrideMap(List<MapNodeLayerOverride> overrides)
        {
            var map = new Dictionary<int, MapNodeLayerOverride>();
            if (overrides == null)
                return map;

            for (int i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                if (entry == null)
                    continue;
                map[entry.LayerIndex] = entry;
            }

            return map;
        }

        static Dictionary<MapNodeType, MapNodeTypeOverride> BuildTypeOverrideMap(List<MapNodeTypeOverride> overrides)
        {
            var map = new Dictionary<MapNodeType, MapNodeTypeOverride>();
            if (overrides == null)
                return map;

            for (int i = 0; i < overrides.Count; i++)
            {
                var entry = overrides[i];
                if (entry == null)
                    continue;
                map[entry.Type] = entry;
            }

            return map;
        }

        static MapNodeLayerOverride? TryGetLayerOverride(Dictionary<int, MapNodeLayerOverride> map, int layerIndex)
        {
            return map != null && map.TryGetValue(layerIndex, out var value) ? value : null;
        }

        static int ResolveLayerWidth(
            DynamicValue<int> defaultWidth,
            IDynamicContext context,
            Dictionary<int, MapNodeLayerOverride> layerOverrides,
            int layerIndex)
        {
            if (layerOverrides != null
                && layerOverrides.TryGetValue(layerIndex, out var layerOverride)
                && layerOverride != null
                && layerOverride.UseWidthOverride)
            {
                return Mathf.Max(1, layerOverride.OverrideWidth);
            }

            return Mathf.Max(1, defaultWidth.GetOrDefault(context, 1));
        }

        static List<MapNodeTypeWeight> ResolveWeights(List<MapNodeTypeWeight> defaults, MapNodeLayerOverride? layerOverride)
        {
            if (layerOverride != null && layerOverride.OverrideWeights != null && layerOverride.OverrideWeights.Count > 0)
                return layerOverride.OverrideWeights;

            return defaults ?? new List<MapNodeTypeWeight>();
        }

        static MapNodeType ResolveNodeType(
            List<MapNodeTypeWeight> weights,
            Dictionary<MapNodeType, MapNodeTypeOverride> typeOverrides,
            Dictionary<MapNodeType, int> typeCounts,
            System.Random rng,
            int layerIndex)
        {
            var candidates = FilterCandidates(weights, typeOverrides, typeCounts, layerIndex, requireMin: false);
            if (candidates.Count == 0)
                return MapNodeType.Default;

            var forced = FilterCandidates(weights, typeOverrides, typeCounts, layerIndex, requireMin: true);
            var targetList = forced.Count > 0 ? forced : candidates;

            return PickWeighted(targetList, rng);
        }

        static List<MapNodeTypeWeight> FilterCandidates(
            List<MapNodeTypeWeight> weights,
            Dictionary<MapNodeType, MapNodeTypeOverride> typeOverrides,
            Dictionary<MapNodeType, int> typeCounts,
            int layerIndex,
            bool requireMin)
        {
            var list = new List<MapNodeTypeWeight>();
            if (weights == null)
                return list;

            for (int i = 0; i < weights.Count; i++)
            {
                var entry = weights[i];
                if (entry.Weight <= 0)
                    continue;

                if (typeOverrides != null && typeOverrides.TryGetValue(entry.Type, out var rule))
                {
                    if (rule.AllowedLayers != null && rule.AllowedLayers.Count > 0 && !rule.AllowedLayers.Contains(layerIndex))
                        continue;

                    var count = typeCounts.TryGetValue(entry.Type, out var value) ? value : 0;
                    if (rule.MaxCount > 0 && count >= rule.MaxCount)
                        continue;

                    if (requireMin && rule.MinCount > 0 && count >= rule.MinCount)
                        continue;

                    if (requireMin && rule.MinCount <= 0)
                        continue;
                }
                else if (requireMin)
                {
                    continue;
                }

                list.Add(entry);
            }

            return list;
        }

        static MapNodeType PickWeighted(List<MapNodeTypeWeight> weights, System.Random rng)
        {
            if (weights == null || weights.Count == 0)
                return MapNodeType.Default;

            var total = 0;
            for (int i = 0; i < weights.Count; i++)
                total += Mathf.Max(0, weights[i].Weight);

            if (total <= 0)
                return weights[0].Type;

            var roll = rng.Next(total);
            for (int i = 0; i < weights.Count; i++)
            {
                roll -= Mathf.Max(0, weights[i].Weight);
                if (roll < 0)
                    return weights[i].Type;
            }

            return weights[weights.Count - 1].Type;
        }

        static void IncrementCount(Dictionary<MapNodeType, int> counts, MapNodeType type)
        {
            if (counts.TryGetValue(type, out var value))
                counts[type] = value + 1;
            else
                counts[type] = 1;
        }

        static void BuildConnections(
            List<MapNode> nodes,
            List<MapNodeLayer> layers,
            int[] layerWidths,
            ConnectionRuleSet defaultRules,
            Dictionary<int, MapNodeLayerOverride> layerOverrides,
            System.Random rng)
        {
            if (nodes == null || layers == null || layers.Count <= 1)
                return;

            for (int layerIndex = 0; layerIndex < layers.Count - 1; layerIndex++)
            {
                var fromLayer = layers[layerIndex];
                var toLayer = layers[layerIndex + 1];
                if (fromLayer == null || toLayer == null)
                    continue;

                var rules = ResolveConnectionRules(defaultRules, layerOverrides, layerIndex);
                var toWidth = layerWidths[layerIndex + 1];

                for (int i = 0; i < fromLayer.NodeIds.Count; i++)
                {
                    var fromNodeId = fromLayer.NodeIds[i];
                    if (fromNodeId < 0 || fromNodeId >= nodes.Count)
                        continue;

                    var fromNode = nodes[fromNodeId];
                    var connected = false;

                    if (rules.SameIndexGuaranteed)
                    {
                        var sameIndex = Mathf.Clamp(fromNode.WidthIndex, 0, toWidth - 1);
                        var toNodeId = toLayer.NodeIds[sameIndex];
                        if (TryAddConnection(nodes, fromNode, toNodeId))
                            connected = true;
                    }

                    var minIndex = Mathf.Clamp(fromNode.WidthIndex + rules.NeighborMin, 0, toWidth - 1);
                    var maxIndex = Mathf.Clamp(fromNode.WidthIndex + rules.NeighborMax, 0, toWidth - 1);

                    for (int idx = minIndex; idx <= maxIndex; idx++)
                    {
                        if (rules.SameIndexGuaranteed && idx == fromNode.WidthIndex)
                            continue;

                        if (rng.NextDouble() <= rules.ExtraConnectionChance)
                        {
                            var toNodeId = toLayer.NodeIds[idx];
                            if (TryAddConnection(nodes, fromNode, toNodeId))
                                connected = true;
                        }
                    }

                    if (!connected)
                    {
                        var idx = minIndex <= maxIndex ? rng.Next(minIndex, maxIndex + 1) : Mathf.Clamp(fromNode.WidthIndex, 0, toWidth - 1);
                        var toNodeId = toLayer.NodeIds[idx];
                        TryAddConnection(nodes, fromNode, toNodeId);
                    }
                }

                if (rules.EnsureInbound)
                {
                    for (int i = 0; i < toLayer.NodeIds.Count; i++)
                    {
                        var toNodeId = toLayer.NodeIds[i];
                        if (toNodeId < 0 || toNodeId >= nodes.Count)
                            continue;

                        var toNode = nodes[toNodeId];
                        if (toNode.PrevIds.Count > 0)
                            continue;

                        var fallbackFromId = fromLayer.NodeIds[rng.Next(fromLayer.NodeIds.Count)];
                        if (fallbackFromId < 0 || fallbackFromId >= nodes.Count)
                            continue;

                        TryAddConnection(nodes, nodes[fallbackFromId], toNodeId);
                    }
                }
            }
        }

        static ConnectionRuleSet ResolveConnectionRules(
            ConnectionRuleSet defaultRules,
            Dictionary<int, MapNodeLayerOverride> layerOverrides,
            int layerIndex)
        {
            if (layerOverrides != null && layerOverrides.TryGetValue(layerIndex, out var layerOverride) && layerOverride != null)
                return layerOverride.OverrideConnection;
            return defaultRules;
        }

        static bool TryAddConnection(List<MapNode> nodes, MapNode fromNode, int toNodeId)
        {
            if (toNodeId < 0 || toNodeId >= nodes.Count)
                return false;

            if (!fromNode.NextIds.Contains(toNodeId))
                fromNode.NextIds.Add(toNodeId);

            var toNode = nodes[toNodeId];
            if (!toNode.PrevIds.Contains(fromNode.Id))
                toNode.PrevIds.Add(fromNode.Id);

            return true;
        }
    }
}
