#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Chunk.Biome
{
    public enum ChunkAxisSource
    {
        Cell = 0,
        World = 1,
    }

    [Serializable]
    public struct ChunkAxisSettings
    {
        [SerializeField, LabelText("Axis Source")]
        ChunkAxisSource axisSource;
        [SerializeField, LabelText("Axis Scale (Cell)")]
        Vector2 axisScaleCell;
        [SerializeField, LabelText("Axis Scale (World)")]
        Vector2 axisScaleWorld;
        [SerializeField, LabelText("Axis Clamp Min")]
        float axisClampMin;
        [SerializeField, LabelText("Axis Clamp Max")]
        float axisClampMax;
        [SerializeField, LabelText("Invert X")]
        bool axisInvertX;
        [SerializeField, LabelText("Invert Y")]
        bool axisInvertY;
        [SerializeField, LabelText("Normalize 0-1")]
        bool normalize01;

        public ChunkAxisSource AxisSource => axisSource;
        public Vector2 AxisScaleCell => axisScaleCell;
        public Vector2 AxisScaleWorld => axisScaleWorld;
        public float AxisClampMin => axisClampMin;
        public float AxisClampMax => axisClampMax;
        public bool AxisInvertX => axisInvertX;
        public bool AxisInvertY => axisInvertY;
        public bool Normalize01 => normalize01;

        public void EnsureDefaults()
        {
            if (axisScaleCell.x <= 0f) axisScaleCell.x = 128f;
            if (axisScaleCell.y <= 0f) axisScaleCell.y = 128f;
            if (axisClampMax <= axisClampMin)
            {
                axisClampMin = -2f;
                axisClampMax = 2f;
            }
        }
    }

    public sealed class ChunkVarBox
    {
        readonly Dictionary<string, float> _values = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, float> Values => _values;

        public void Set(string key, float value)
        {
            if (string.IsNullOrEmpty(key))
                return;
            _values[key] = value;
        }

        public bool TryGet(string key, out float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = 0f;
                return false;
            }

            return _values.TryGetValue(key, out value);
        }

        public void Clear() => _values.Clear();
    }

    public readonly struct ChunkBiomeResult
    {
        public readonly string BiomeId;
        public readonly ChunkVarBox VarBox;

        public ChunkBiomeResult(string biomeId, ChunkVarBox varBox)
        {
            BiomeId = biomeId ?? string.Empty;
            VarBox = varBox ?? new ChunkVarBox();
        }
    }
}
