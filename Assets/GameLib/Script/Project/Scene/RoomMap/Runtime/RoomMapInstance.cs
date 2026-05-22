#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Game.RoomMap
{
    public sealed class RoomMapInstance
    {
        public readonly struct CellRecord
        {
            public readonly int LayerIndex;
            public readonly string LayerName;
            public readonly int X;
            public readonly int Y;
            public readonly int TileId;
            public readonly Vector3 WorldPos;

            public readonly SpawnedLifetimeHandle Lifetime;

            public IRuntimeResolver? Resolver => Lifetime.Resolver;
            public GameObject? Root => Lifetime.Root;
            public IScopeNode? ScopeNode => Lifetime.ScopeNode;

            public bool IsEmpty => TileId == 0 || Lifetime.IsEmpty;

            public CellRecord(
                int layerIndex,
                string layerName,
                int x,
                int y,
                int tileId,
                Vector3 worldPos,
                SpawnedLifetimeHandle lifetime)
            {
                LayerIndex = layerIndex;
                LayerName = layerName;
                X = x;
                Y = y;
                TileId = tileId;
                WorldPos = worldPos;
                Lifetime = lifetime;
            }

            public CellRecord AsEmpty()
            {
                return new CellRecord(LayerIndex, LayerName, X, Y, 0, WorldPos, SpawnedLifetimeHandle.Empty);
            }
        }

        public readonly struct DynamicRecord
        {
            public readonly int TileId;
            public readonly Vector2Int Cell;
            public readonly Vector3 WorldPos;

            public readonly SpawnedLifetimeHandle Lifetime;

            public IRuntimeResolver? Resolver => Lifetime.Resolver;
            public GameObject? Root => Lifetime.Root;
            public IScopeNode? ScopeNode => Lifetime.ScopeNode;

            public bool IsEmpty => TileId == 0 || Lifetime.IsEmpty;

            public DynamicRecord(
                int tileId,
                Vector2Int cell,
                Vector3 worldPos,
                SpawnedLifetimeHandle lifetime)
            {
                TileId = tileId;
                Cell = cell;
                WorldPos = worldPos;
                Lifetime = lifetime;
            }
        }

        readonly struct LayerData
        {
            public readonly int Width;
            public readonly int Height;
            public readonly string Name;
            public readonly CellRecord[] Cells;

            public LayerData(int width, int height, string name)
            {
                Width = Mathf.Max(1, width);
                Height = Mathf.Max(1, height);
                Name = name ?? string.Empty;
                Cells = new CellRecord[Width * Height];
            }
        }

        readonly LayerData[] _layers;
        readonly int _maxWidth;
        readonly int _maxHeight;

        readonly List<DynamicRecord> _dynamic = new();

        public int LayerCount => _layers.Length;
        public int Width => GetLayerWidth(0);
        public int Height => GetLayerHeight(0);
        public int MaxWidth => _maxWidth;
        public int MaxHeight => _maxHeight;
        public IReadOnlyList<DynamicRecord> DynamicRecords => _dynamic;

        public RoomMapInstance(IReadOnlyList<RoomMapLayoutLayer> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                _layers = new[] { new LayerData(1, 1, "Layer 0") };
                _maxWidth = 1;
                _maxHeight = 1;
                return;
            }

            _layers = new LayerData[layers.Count];
            var maxW = 1;
            var maxH = 1;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var name = layer != null && !string.IsNullOrEmpty(layer.DisplayName) ? layer.DisplayName : $"Layer {i}";
                var width = layer?.Width ?? 1;
                var height = layer?.Height ?? 1;
                _layers[i] = new LayerData(width, height, name);
                maxW = Math.Max(maxW, width);
                maxH = Math.Max(maxH, height);
            }

            _maxWidth = maxW;
            _maxHeight = maxH;
        }

        public bool TryGet(int x, int y, out CellRecord record)
        {
            return TryGet(0, x, y, out record);
        }

        public bool TryGet(int layerIndex, int x, int y, out CellRecord record)
        {
            record = default;
            if (!TryGetLayer(layerIndex, out var layer))
                return false;
            if (x < 0 || x >= layer.Width || y < 0 || y >= layer.Height)
                return false;

            record = layer.Cells[y * layer.Width + x];
            return true;
        }

        public void Set(int x, int y, CellRecord record)
        {
            Set(0, x, y, record);
        }

        public void Set(int layerIndex, int x, int y, CellRecord record)
        {
            if (!TryGetLayer(layerIndex, out var layer))
                return;
            if (x < 0 || x >= layer.Width || y < 0 || y >= layer.Height)
                return;

            layer.Cells[y * layer.Width + x] = record;
        }

        public CellRecord[] GetRawCellsUnsafe() => GetRawCellsUnsafe(0);

        public CellRecord[] GetRawCellsUnsafe(int layerIndex)
        {
            if (!TryGetLayer(layerIndex, out var layer))
                return Array.Empty<CellRecord>();
            return layer.Cells;
        }

        public int GetLayerWidth(int layerIndex)
        {
            return TryGetLayer(layerIndex, out var layer) ? layer.Width : 1;
        }

        public int GetLayerHeight(int layerIndex)
        {
            return TryGetLayer(layerIndex, out var layer) ? layer.Height : 1;
        }

        public string GetLayerName(int layerIndex)
        {
            return TryGetLayer(layerIndex, out var layer) ? layer.Name : string.Empty;
        }

        public bool TryGetLayer(int layerIndex, out (int Width, int Height, string Name, CellRecord[] Cells) layer)
        {
            layer = default;
            if (layerIndex < 0 || layerIndex >= _layers.Length)
                return false;
            var data = _layers[layerIndex];
            layer = (data.Width, data.Height, data.Name, data.Cells);
            return true;
        }

        public void AddDynamic(DynamicRecord record)
        {
            if (record.IsEmpty)
                return;

            _dynamic.Add(record);
        }

        public int DynamicCount => _dynamic.Count;

        public void RemoveDynamicAtSwapBack(int index)
        {
            if (index < 0 || index >= _dynamic.Count)
                return;

            var last = _dynamic.Count - 1;
            _dynamic[index] = _dynamic[last];
            _dynamic.RemoveAt(last);
        }

        public void ForEachInRect(RectInt rect, Action<int, int, CellRecord> visitor)
        {
            ForEachInRect(0, rect, visitor);
        }

        public void ForEachInRect(int layerIndex, RectInt rect, Action<int, int, CellRecord> visitor)
        {
            if (visitor == null)
                return;

            if (!TryGetLayer(layerIndex, out var layer))
                return;

            var xMin = Mathf.Clamp(rect.xMin, 0, layer.Width);
            var xMax = Mathf.Clamp(rect.xMax, 0, layer.Width);
            var yMin = Mathf.Clamp(rect.yMin, 0, layer.Height);
            var yMax = Mathf.Clamp(rect.yMax, 0, layer.Height);

            for (int y = yMin; y < yMax; y++)
            {
                for (int x = xMin; x < xMax; x++)
                {
                    var idx = y * layer.Width + x;
                    visitor(x, y, layer.Cells[idx]);
                }
            }
        }

        public void ClearAllToEmpty()
        {
            for (int l = 0; l < _layers.Length; l++)
            {
                var cells = _layers[l].Cells;
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] = cells[i].AsEmpty();
                }
            }

            _dynamic.Clear();
        }

        public bool HasLiveObjects()
        {
            for (int l = 0; l < _layers.Length; l++)
            {
                var cells = _layers[l].Cells;
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!cells[i].IsEmpty)
                        return true;
                }
            }

            for (int i = 0; i < _dynamic.Count; i++)
            {
                if (!_dynamic[i].IsEmpty)
                    return true;
            }

            return false;
        }
    }
}
