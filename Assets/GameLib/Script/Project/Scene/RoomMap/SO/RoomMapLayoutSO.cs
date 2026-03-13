#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.RoomMap
{
    [Serializable]
    public sealed class RoomMapLayoutLayer
    {
        [BoxGroup("Layer")]
        [LabelText("Name")]
        [SerializeField] string displayName = "Layer";

        [BoxGroup("Size")]
        [MinValue(1)]
        [SerializeField] int width = 1;

        [BoxGroup("Size")]
        [MinValue(1)]
        [SerializeField] int height = 1;

        [SerializeField, HideInInspector] int cellsWidth = 1;
        [SerializeField, HideInInspector] int cellsHeight = 1;
        [SerializeField, HideInInspector] int[] cells = Array.Empty<int>();

        public string DisplayName => displayName;
        public int Width => width;
        public int Height => height;
        public IReadOnlyList<int> Cells => cells;
        internal int[] CellsUnsafe => cells;

        internal void EnsureName(int index)
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = $"Layer {index}";
        }

        internal void ApplyLegacy(int legacyWidth, int legacyHeight, int legacyCellsWidth, int legacyCellsHeight, int[] legacyCells)
        {
            width = Mathf.Max(1, legacyWidth);
            height = Mathf.Max(1, legacyHeight);
            cellsWidth = Mathf.Max(1, legacyCellsWidth);
            cellsHeight = Mathf.Max(1, legacyCellsHeight);
            cells = legacyCells ?? Array.Empty<int>();
            OnValidateImpl();
        }

        internal void OnValidateImpl()
        {
            if (width < 1) width = 1;
            if (height < 1) height = 1;

            var oldW = Mathf.Max(1, cellsWidth);
            var oldH = Mathf.Max(1, cellsHeight);

            ResizeCellsPreserve(oldW, oldH, width, height);
            cellsWidth = width;
            cellsHeight = height;
        }

        public int GetTileId(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return 0;

            var i = y * width + x;
            if (cells == null || i < 0 || i >= cells.Length)
                return 0;

            return cells[i];
        }

        public void SetTileId(int x, int y, int tileId)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            var i = y * width + x;
            if (cells == null || i < 0 || i >= cells.Length)
                return;

            cells[i] = tileId;
        }

        void ResizeCellsPreserve(int oldW, int oldH, int newWidth, int newHeight)
        {
            var newLen = Math.Max(1, newWidth * newHeight);
            var old = cells ?? Array.Empty<int>();

            if (old.Length != Math.Max(1, oldW * oldH))
            {
                oldW = newWidth;
                oldH = newHeight;
            }

            if (old.Length == newLen && oldW == newWidth && oldH == newHeight)
                return;

            var next = new int[newLen];

            var copyW = Math.Min(oldW, newWidth);
            var copyH = Math.Min(oldH, newHeight);
            for (int y = 0; y < copyH; y++)
                for (int x = 0; x < copyW; x++)
                {
                    var src = y * oldW + x;
                    var dst = y * newWidth + x;
                    if ((uint)src < (uint)old.Length && (uint)dst < (uint)next.Length)
                        next[dst] = old[src];
                }

            cells = next;
        }
    }

    [CreateAssetMenu(
        fileName = "NewRoomMapLayout",
        menuName = "Game/RoomMap/Layout",
        order = 110)]
    public sealed class RoomMapLayoutSO : ScriptableObject
    {
        [SerializeField, HideInInspector] int width = 1;
        [SerializeField, HideInInspector] int height = 1;
        [SerializeField, HideInInspector] int cellsWidth = 1;
        [SerializeField, HideInInspector] int cellsHeight = 1;
        [SerializeField, HideInInspector] int[] cells = Array.Empty<int>();

        [SerializeField]
        List<RoomMapLayoutLayer> layers = new();

        public IReadOnlyList<RoomMapLayoutLayer> Layers => layers;
        public int LayerCount => layers?.Count ?? 0;

        public int Width => GetLayerWidth(0);
        public int Height => GetLayerHeight(0);
        public IReadOnlyList<int> Cells => GetLayerCells(0);
        internal int[] CellsUnsafe => GetLayerCellsUnsafe(0);

        [NonSerialized] bool _pendingValidateFix;

        void OnValidate()
        {
#if UNITY_EDITOR
            if (!_pendingValidateFix)
            {
                _pendingValidateFix = true;
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    _pendingValidateFix = false;
                    if (this == null) return;
                    OnValidateImpl();
                    UnityEditor.EditorUtility.SetDirty(this);
                };
            }
            return;
#else
            OnValidateImpl();
#endif
        }

#if UNITY_EDITOR
        public void RebuildGrid()
        {
            _pendingValidateFix = false;
            OnValidateImpl();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void RebuildLayer(int layerIndex)
        {
            _pendingValidateFix = false;
            OnValidateImpl();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        void OnValidateImpl()
        {
            EnsureLegacyLayer();

            if (layers == null)
                layers = new List<RoomMapLayoutLayer>();

            for (int i = 0; i < layers.Count; i++)
            {
                layers[i] ??= new RoomMapLayoutLayer();
                layers[i].EnsureName(i);
                layers[i].OnValidateImpl();
            }
        }

        void EnsureLegacyLayer()
        {
            if (layers != null && layers.Count > 0)
                return;

            if (layers == null)
                layers = new List<RoomMapLayoutLayer>();

            var legacy = new RoomMapLayoutLayer();
            legacy.ApplyLegacy(width, height, cellsWidth, cellsHeight, cells);
            legacy.EnsureName(0);
            layers.Add(legacy);
        }

        public int GetTileId(int x, int y)
        {
            return GetTileId(0, x, y);
        }

        public int GetTileId(int layerIndex, int x, int y)
        {
            if (!TryGetLayer(layerIndex, out var layer))
                return 0;
            return layer.GetTileId(x, y);
        }

        public void SetTileId(int x, int y, int tileId)
        {
            SetTileId(0, x, y, tileId);
        }

        public void SetTileId(int layerIndex, int x, int y, int tileId)
        {
            if (!TryGetLayer(layerIndex, out var layer))
                return;
            layer.SetTileId(x, y, tileId);
        }

        public bool TryGetLayer(int layerIndex, [NotNullWhen(true)] out RoomMapLayoutLayer? layer)
        {
            layer = null;
            if (layers == null || layerIndex < 0 || layerIndex >= layers.Count)
                return false;
            layer = layers[layerIndex];
            return layer != null;
        }

        public int GetLayerWidth(int layerIndex)
        {
            return TryGetLayer(layerIndex, out var layer) ? Mathf.Max(1, layer.Width) : 1;
        }

        public int GetLayerHeight(int layerIndex)
        {
            return TryGetLayer(layerIndex, out var layer) ? Mathf.Max(1, layer.Height) : 1;
        }

        public IReadOnlyList<int> GetLayerCells(int layerIndex)
        {
            return TryGetLayer(layerIndex, out var layer) ? layer.Cells : Array.Empty<int>();
        }

        internal int[] GetLayerCellsUnsafe(int layerIndex)
        {
            return TryGetLayer(layerIndex, out var layer) ? layer.CellsUnsafe : Array.Empty<int>();
        }

        public int GetMaxWidth()
        {
            if (layers == null || layers.Count == 0)
                return Mathf.Max(1, width);

            var max = 1;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer != null)
                    max = Math.Max(max, layer.Width);
            }
            return max;
        }

        public int GetMaxHeight()
        {
            if (layers == null || layers.Count == 0)
                return Mathf.Max(1, height);

            var max = 1;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer != null)
                    max = Math.Max(max, layer.Height);
            }
            return max;
        }
    }
}
