#nullable enable
using System.Collections.Generic;
using Game;
using UnityEngine;

namespace Game.Chunk.Biome
{
    public sealed class ChunkBiomeService : IChunkBiomeService, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly ChunkBiomeSettingsSO? _settings;
        readonly Dictionary<ChunkCoord, string> _cache = new();

        public ChunkBiomeService(ChunkBiomeSettingsSO? settings = null)
        {
            _settings = settings;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _cache.Clear();
        }

        public ChunkBiomeResult Evaluate(ChunkContext context, int seed)
        {
            var varBox = new ChunkVarBox();
            var settings = _settings;

            if (settings != null)
            {
                var axis = settings.AxisSettings;
                axis.EnsureDefaults();
                FillDefaultAxis(varBox, axis, context);

                var paramDefs = settings.ParamDefinitions;
                if (paramDefs != null)
                {
                    var center = context.WorldCenter;
                    for (int i = 0; i < paramDefs.Length; i++)
                    {
                        var def = paramDefs[i];
                        if (def == null || !def.Enabled)
                            continue;

                        var value = def.Evaluate(center, seed);
                        varBox.Set(def.Key, value);
                    }
                }
            }
            else
            {
                var defaultAxis = new ChunkAxisSettings();
                defaultAxis.EnsureDefaults();
                FillDefaultAxis(varBox, defaultAxis, context);
            }

            var biomeId = ResolveBiomeId(context.Coord, varBox);
            _cache[context.Coord] = biomeId;

            return new ChunkBiomeResult(biomeId, varBox);
        }

        public void Forget(ChunkCoord coord)
        {
            _cache.Remove(coord);
        }

        string ResolveBiomeId(ChunkCoord coord, ChunkVarBox varBox)
        {
            var settings = _settings;
            if (settings == null)
                return "default";

            var defs = settings.BiomeDefinitions;
            if (defs == null || defs.Length == 0)
                return settings.DefaultBiomeId;

            var neighborBias = settings.NeighborBiasWeight;
            var bestScore = float.MinValue;
            var bestPriority = int.MinValue;
            var bestId = settings.DefaultBiomeId;

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null || !def.Enabled)
                    continue;

                if (!def.IsMatch(varBox))
                    continue;

                var score = def.BaseScore + CountNeighborMatches(coord, def.BiomeId) * neighborBias;
                if (def.Priority > bestPriority || (def.Priority == bestPriority && score > bestScore))
                {
                    bestPriority = def.Priority;
                    bestScore = score;
                    bestId = def.BiomeId;
                }
            }

            return bestId;
        }

        int CountNeighborMatches(ChunkCoord coord, string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
                return 0;

            var count = 0;
            if (TryGetNeighbor(coord.X + 1, coord.Y, biomeId)) count++;
            if (TryGetNeighbor(coord.X - 1, coord.Y, biomeId)) count++;
            if (TryGetNeighbor(coord.X, coord.Y + 1, biomeId)) count++;
            if (TryGetNeighbor(coord.X, coord.Y - 1, biomeId)) count++;
            return count;
        }

        bool TryGetNeighbor(int x, int y, string biomeId)
        {
            return _cache.TryGetValue(new ChunkCoord(x, y), out var id) && id == biomeId;
        }

        static void FillDefaultAxis(ChunkVarBox box, ChunkAxisSettings axis, ChunkContext context)
        {
            var cellCenter = new Vector2Int(
                context.CellRect.xMin + (context.CellRect.width / 2),
                context.CellRect.yMin + (context.CellRect.height / 2));

            float xAxis;
            float yAxis;

            if (axis.AxisSource == ChunkAxisSource.World)
            {
                var world = context.WorldCenter - context.OriginSettings.WorldOriginPosition;
                var scaleWorld = axis.AxisScaleWorld;
                if (scaleWorld.x <= 0f)
                    scaleWorld.x = axis.AxisScaleCell.x * context.OriginSettings.CellSize.x;
                if (scaleWorld.y <= 0f)
                    scaleWorld.y = axis.AxisScaleCell.y * context.OriginSettings.CellSize.y;
                xAxis = world.x / scaleWorld.x;
                yAxis = world.y / scaleWorld.y;
            }
            else
            {
                var originCell = context.OriginSettings.WorldOriginCell;
                xAxis = (cellCenter.x - originCell.x) / axis.AxisScaleCell.x;
                yAxis = (cellCenter.y - originCell.y) / axis.AxisScaleCell.y;
            }

            if (axis.AxisInvertX) xAxis = -xAxis;
            if (axis.AxisInvertY) yAxis = -yAxis;

            xAxis = Mathf.Clamp(xAxis, axis.AxisClampMin, axis.AxisClampMax);
            yAxis = Mathf.Clamp(yAxis, axis.AxisClampMin, axis.AxisClampMax);

            if (axis.Normalize01)
            {
                xAxis = Mathf.InverseLerp(axis.AxisClampMin, axis.AxisClampMax, xAxis);
                yAxis = Mathf.InverseLerp(axis.AxisClampMin, axis.AxisClampMax, yAxis);
            }

            box.Set("xAxis", xAxis);
            box.Set("yAxis", yAxis);
        }
    }
}
