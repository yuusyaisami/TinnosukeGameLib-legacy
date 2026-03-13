#nullable enable
using System;
using System.Collections.Generic;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.RoomMap
{
    [Serializable]
    public sealed class RoomMapDynamicSpawnEntry
    {
        [HorizontalGroup("Row", Width = 0.55f)]
        [LabelText("Tile")]
        [MinValue(0)]
        [RoomMapTileIdDropdown]
        public int TileId;

        [HorizontalGroup("Row", Width = 0.45f)]
        [LabelText("Tile Name")]
        [ReadOnly]
        public string TileName = string.Empty;

        [BoxGroup("Placement")]
        [LabelText("Cell")]
        public Vector2Int Cell;

        [BoxGroup("Placement")]
        [LabelText("Local Offset")]
        public Vector3 LocalOffset;

        [BoxGroup("Placement")]
        [LabelText("Rotation ΔZ")]
        public float RotationDegZ;

        [BoxGroup("Placement")]
        public Vector3 Scale = Vector3.one;

        public void RefreshTileName(RoomMapTileRegistry? registry)
        {
            if (registry == null)
            {
                TileName = string.Empty;
                return;
            }

            if (TileId <= 0)
            {
                TileName = string.Empty;
                return;
            }

            if (registry.TryGetDisplayPath(TileId, out var path) && !string.IsNullOrEmpty(path))
                TileName = path;
            else
                TileName = "(Unknown TileId)";
        }
    }

    [CreateAssetMenu(
        fileName = "NewRoomMapDynamicLayout",
        menuName = "Game/RoomMap/Dynamic Layout",
        order = 115)]
    public sealed class RoomMapDynamicLayoutSO : ScriptableObject
    {
        [BoxGroup("Size")]
        [MinValue(1)]
        [SerializeField] int width = 1;

        [BoxGroup("Size")]
        [MinValue(1)]
        [SerializeField] int height = 1;

        [BoxGroup("Entries")]
        [TableList(AlwaysExpanded = true, IsReadOnly = false)]
        [SerializeField]
        List<RoomMapDynamicSpawnEntry> entries = new();
        public IReadOnlyList<RoomMapDynamicSpawnEntry> Entries => entries;
        public int Width => width;
        public int Height => height;

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

        void OnValidateImpl()
        {
            if (width < 1) width = 1;
            if (height < 1) height = 1;

            if (entries == null)
                entries = new List<RoomMapDynamicSpawnEntry>();

#if UNITY_EDITOR
            var registry = RoomMapTileRegistryLocator.GetOrCreate();
#else
            RoomMapTileRegistry? registry = null;
#endif

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i] ??= new RoomMapDynamicSpawnEntry();
                entries[i].RefreshTileName(registry);
            }
        }

        public IEnumerable<RoomMapDynamicSpawnEntry> EnumerateValidEntries()
        {
            if (entries == null)
                yield break;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;
                if (e.TileId <= 0)
                    continue;

                yield return e;
            }
        }
    }
}
