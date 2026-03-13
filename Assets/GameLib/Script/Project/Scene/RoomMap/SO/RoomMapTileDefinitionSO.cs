#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.RoomMap
{
    public enum RoomMapSpawnSource
    {
        None = 0,
        Prefab = 1,
        RuntimeTemplate = 2,
    }

    [Serializable]
    public sealed class RoomMapTileSpawnDef
    {
        [EnumToggleButtons]
        [SerializeField] RoomMapSpawnSource source;

        [ShowIf("@source == RoomMapSpawnSource.Prefab")]
        [SerializeField] GameObject? prefab;

        [ShowIf("@source == RoomMapSpawnSource.RuntimeTemplate")]
        [SerializeField, InlineProperty, HideLabel] DynamicValue<BaseRuntimeTemplatePreset> templatePreset;

        [Tooltip("v0.1: Entity / RuntimeEntity のみに限定")]
        [SerializeField] SpawnerKind kind = SpawnerKind.Entity;

        [Tooltip("null/空白/'default' は '' に正規化")]
        [SerializeField] string spawnerTag = string.Empty;

        [Tooltip("v0.1推奨: RuntimeTemplate は false（Pooling 成立条件が厳しい）")]
        [SerializeField] bool allowPooling;

        public RoomMapSpawnSource Source => source;
        public GameObject? Prefab => prefab;
        public DynamicValue<BaseRuntimeTemplatePreset> TemplatePreset => templatePreset;
        public SpawnerKind Kind => kind;
        public string SpawnerTag => spawnerTag;
        public string NormalizedSpawnerTag
        {
            get
            {
                if (string.IsNullOrWhiteSpace(spawnerTag))
                    return string.Empty;
                if (string.Equals(spawnerTag, "default", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                return spawnerTag;
            }
        }
        public bool AllowPooling => allowPooling;

        public bool IsValidForSpawn(out string reason)
        {
            reason = string.Empty;

            switch (source)
            {
                case RoomMapSpawnSource.None:
                    return true;
                case RoomMapSpawnSource.Prefab:
                    if (prefab == null) { reason = "Prefab is null"; return false; }
                    if (kind != SpawnerKind.Entity) { reason = "Prefab requires SpawnerKind.Entity"; return false; }
                    return true;
                case RoomMapSpawnSource.RuntimeTemplate:
                    if (!templatePreset.HasSource) { reason = "Template is null"; return false; }
                    if (kind != SpawnerKind.RuntimeEntity) { reason = "RuntimeTemplate requires SpawnerKind.RuntimeEntity"; return false; }
                    return true;
                default:
                    reason = "Unknown source";
                    return false;
            }
        }

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!templatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

    }

    [CreateAssetMenu(
        fileName = "NewRoomMapTileDefinition",
        menuName = "Game/RoomMap/Tile Definition",
        order = 120)]
    public sealed class RoomMapTileDefinitionSO : ScriptableObject
    {
        [Serializable]
        public sealed class RoomMapTileSpawnDefEntry
        {
            [HorizontalGroup("Row", Width = 0.45f)]
            [LabelText("Tile")]
            [MinValue(0)]
            [RoomMapTileIdDropdown]
            public int TileId;

            [HorizontalGroup("Row", Width = 0.55f)]
            [HideLabel]
            public RoomMapTileSpawnDef Def = new();
        }

        [BoxGroup("Defs")]
        [SerializeField]
        List<RoomMapTileSpawnDefEntry> defs = new();

        public IReadOnlyList<RoomMapTileSpawnDefEntry> Defs => defs;

        public bool TryGetDef(int tileId, out RoomMapTileSpawnDef? def)
        {
            def = null;
            if (tileId <= 0 || defs == null)
                return false;

            for (int i = 0; i < defs.Count; i++)
            {
                var e = defs[i];
                if (e == null)
                    continue;
                if (e.TileId != tileId)
                    continue;
                if (e.Def == null)
                    return false;
                def = e.Def;
                return true;
            }

            return false;
        }

        void OnValidate()
        {
            if (defs == null)
                defs = new List<RoomMapTileSpawnDefEntry>();

            for (int i = 0; i < defs.Count; i++)
                defs[i] ??= new RoomMapTileSpawnDefEntry();
        }
    }
}
