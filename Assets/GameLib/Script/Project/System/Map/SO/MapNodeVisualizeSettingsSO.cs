#nullable enable
using System;
using System.Collections.Generic;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.MapNode
{
    [CreateAssetMenu(
        fileName = "MapNodeVisualizeSettings",
        menuName = "Game/MapNode/Visualize Settings")]
    public sealed class MapNodeVisualizeSettingsSO : ScriptableObject
    {
        [BoxGroup("Layout")]
        public MapNodeSpace Space = MapNodeSpace.World;

        [BoxGroup("Layout")]
        public Vector2 LayerSpacing = new Vector2(0f, -2.0f);

        [BoxGroup("Layout")]
        public Vector2 WidthSpacing = new Vector2(2.0f, 0f);

        [BoxGroup("Layout")]
        public Vector2 AlignOffset = Vector2.zero;

        [BoxGroup("Layout")]
        public bool Centered = true;

        [BoxGroup("Layout")]
        public Vector2 JitterRange = Vector2.zero;

        [BoxGroup("Spawn")]
        public MapNodeSpawnSource SpawnSource = MapNodeSpawnSource.Prefab;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), MapNodeSpawnSource.RuntimeTemplate)]
        [InlineProperty, HideLabel]
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset;

        [BoxGroup("Spawn")]
        [ShowIf(nameof(SpawnSource), MapNodeSpawnSource.Prefab)]
        public GameObject? Prefab;

        [BoxGroup("Spawn")]
        public bool AllowPooling = true;

        [BoxGroup("Line Spawn")]
        [ToggleLeft]
        public bool UseLineSpawn = false;

        [BoxGroup("Line Spawn")]
        [ShowIf(nameof(UseLineSpawn))]
        public MapNodeSpawnSource LineSpawnSource = MapNodeSpawnSource.Prefab;

        [BoxGroup("Line Spawn")]
        [ShowIf(nameof(ShowLineRuntimeTemplate))]
        [InlineProperty, HideLabel]
        public DynamicValue<BaseRuntimeTemplatePreset> LineRuntimeTemplatePreset;

        [BoxGroup("Line Spawn")]
        [ShowIf(nameof(ShowLinePrefab))]
        public GameObject? LinePrefab;

        [BoxGroup("Line Spawn")]
        [ShowIf(nameof(UseLineSpawn))]
        public bool LineAllowPooling = true;

        [BoxGroup("Commands")]
        public MapNodeVisualCommandTable CommandTable = new();

        [BoxGroup("Commands")]
        public MapNodeUIButtonCommandTable UIButtonCommandTable = new();

        [BoxGroup("Commands")]
        public AnimationSpritePreset? DefaultAnimPreset;

        [BoxGroup("Line")]
        public string LineChannelTag = "default";

        [BoxGroup("Line")]
        public MeshLineSettings LineSettings = new();

        bool ShowLineRuntimeTemplate => UseLineSpawn && LineSpawnSource == MapNodeSpawnSource.RuntimeTemplate;
        bool ShowLinePrefab => UseLineSpawn && LineSpawnSource == MapNodeSpawnSource.Prefab;

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!RuntimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

        public bool TryResolveLineRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!LineRuntimeTemplatePreset.TryGet(context, out var preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

    }

    [Serializable]
    public struct MapNodeVisualCommandTable
    {
        public CommandListData Common;
        public List<MapNodeTypeCommand> ByType;
        public List<MapNodeStateCommand> ByState;
    }

    [Serializable]
    public struct MapNodeTypeCommand
    {
        public MapNodeType Type;
        public CommandListData Commands;
    }

    [Serializable]
    public struct MapNodeStateCommand
    {
        public MapNodeState State;
        public CommandListData Commands;
    }

    [Serializable]
    public struct MapNodeUIButtonCommandTable
    {
        public CommandListData SubmitUpAppendCommon;
        public List<MapNodeTypeCommand> SubmitUpAppendByType;
        public List<MapNodeStateCommand> SubmitUpAppendByState;
    }

    [Serializable]
    public struct MeshLineSettings
    {
        public MapNodeLineStyle DefaultStyle;
        public List<LineStyleByNodeType> ByType;
        public List<LineStyleByState> ByState;
    }

    [Serializable]
    public struct LineStyleByNodeType
    {
        public MapNodeType Type;
        public MapNodeLineStyle Style;
    }

    [Serializable]
    public struct LineStyleByState
    {
        public MapNodeState State;
        public MapNodeLineStyle Style;
    }

    [Serializable]
    public sealed class MapNodeLineStyle
    {
        [InlineProperty]
        public MeshLineTrackVisualizerPreset Visualizer = new();

        [InlineProperty]
        public MeshTrackMaterialPreset Material = new();
    }
}
