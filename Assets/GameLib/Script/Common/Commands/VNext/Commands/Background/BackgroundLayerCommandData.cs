#nullable enable
using System;
using System.Collections.Generic;
using Game.Background;
using Game.Channel;
using Game.Common;
using Game.MaterialFx;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Commands.VNext
{
    /// <summary>
    /// Background Layer 縺ｫ蟇ｾ縺吶ｋ謫堺ｽ懃ｨｮ蛻･縲・
    /// </summary>
    public enum BackgroundLayerOperation
    {
        /// <summary>Layer 縺ｮ ScrollSpeed 繧定ｨｭ螳壹☆繧九・/summary>
        SetScrollSpeed = 0,

        /// <summary>Layer 縺ｮ Offset 繧定ｨｭ螳壹☆繧九・/summary>
        SetOffset = 1,

        /// <summary>Layer 縺ｮ Offset 縺ｫ蜉邂励☆繧九・/summary>
        AddOffset = 2,

        /// <summary>Layer 縺ｮ荳譎ょ●豁｢ / 蜀埼幕縲・/summary>
        SetPaused = 3,

        /// <summary>AnimationSpritePreset 繧・VarStore 縺ｫ譖ｸ縺崎ｾｼ繧縲・/summary>
        WriteSpritePreset = 4,

        /// <summary>Layer 蜀・・蜈ｨ隕∫ｴ縺ｫ蟇ｾ縺励※ MaterialFx SetState 繧帝←逕ｨ縺吶ｋ縲・/summary>
        SetMaterialFx = 5,

        /// <summary>Layer 蜀・・蜷・ｦ∫ｴ LTS 縺ｧ繧ｳ繝槭Φ繝峨Μ繧ｹ繝医ｒ螳溯｡後☆繧九・/summary>
        ExecuteOnElements = 6,

        /// <summary>Layer 繧呈怏蜉ｹ / 辟｡蜉ｹ縺ｫ縺吶ｋ・医ち繧､繝ｫ縺ｮ陦ｨ遉ｺ / 髱櫁｡ｨ遉ｺ・峨・/summary>
        SetEnabled = 7,

        /// <summary>繧ｷ繧ｹ繝・Β蜈ｨ菴薙ｒ MarkDirty 縺励※蜀崎ｩ穂ｾ｡縺輔○繧九・/summary>
        MarkDirty = 8,
    }

    /// <summary>
    /// Background Layer 繧貞宛蠕｡縺吶ｋ縺溘ａ縺ｮ邨ｱ蜷医さ繝槭Φ繝峨ョ繝ｼ繧ｿ縲・
    /// Operation 繝輔ぅ繝ｼ繝ｫ繝峨〒謫堺ｽ懊ｒ蛻・ｊ譖ｿ縺医∝推謫堺ｽ懊↓蠢・ｦ√↑繝代Λ繝｡繝ｼ繧ｿ縺ｮ縺ｿ Inspector 縺ｫ陦ｨ遉ｺ縺輔ｌ繧九・
    /// </summary>
    [Serializable]
    public sealed class BackgroundLayerCommandData : ICommandData
    {
        public int CommandId => CommandIds.BackgroundLayer;

        public string DebugData
        {
            get
            {
                var layerLabel = UseLayerName ? $"name={LayerName}" : $"index={LayerIndex}";
                return $"Op={Operation} {layerLabel}";
            }
        }

        // 笏笏笏 Layer 謖・ｮ・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public BackgroundLayerOperation Operation = BackgroundLayerOperation.SetScrollSpeed;

        [BoxGroup("Target")]
        [LabelText("Use Layer Name")]
        [Tooltip("Inspector setting.")]
        public bool UseLayerName;

        [BoxGroup("Target")]
        [LabelText("Layer Index")]
        [ShowIf("@!UseLayerName")]
        public DynamicValue<int> LayerIndex;

        [BoxGroup("Target")]
        [LabelText("Layer Name")]
        [ShowIf(nameof(UseLayerName))]
        public DynamicValue<string> LayerName;

        [BoxGroup("Target")]
        [LabelText("All Layers")]
        [Tooltip("Inspector setting.")]
        public bool AllLayers;

        // 笏笏笏 SetScrollSpeed 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("ScrollSpeed")]
        [LabelText("Speed")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetScrollSpeed")]
        public DynamicValue<Vector2> ScrollSpeed;

        // 笏笏笏 SetOffset / AddOffset 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Offset")]
        [LabelText("Offset")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetOffset || Operation == BackgroundLayerOperation.AddOffset")]
        public DynamicValue<Vector2> Offset;

        // 笏笏笏 SetPaused 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Pause")]
        [LabelText("Paused")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetPaused")]
        public DynamicValue<bool> Paused;

        // 笏笏笏 WriteSpritePreset 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("SpritePreset")]
        [LabelText("Preset Source")]
        [ShowIf("@Operation == BackgroundLayerOperation.WriteSpritePreset")]
        public DynamicValue<AnimationSpritePreset> SpritePresetSource;

        [BoxGroup("SpritePreset")]
        [LabelText("Write VarId")]
        [Tooltip("Inspector setting.")]
        [ShowIf("@Operation == BackgroundLayerOperation.WriteSpritePreset")]
        public DynamicValue<int> SpritePresetVarId;

        // 笏笏笏 SetMaterialFx 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("MaterialFx")]
        [LabelText("MaterialFx Payload")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetMaterialFx")]
        public DynamicValue<MaterialFxPayload> MaterialFxSource;

        [BoxGroup("MaterialFx")]
        [LabelText("Visual Selector")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetMaterialFx")]
        public VisualSelectorSpec VisualSelector = VisualSelectorSpec.All();

        [BoxGroup("MaterialFx")]
        [LabelText("Clear Missing Keys")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetMaterialFx")]
        public bool ClearMissingKeys = true;

        [BoxGroup("MaterialFx")]
        [LabelText("Base Priority")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetMaterialFx")]
        public int BasePriority;

        // 笏笏笏 ExecuteOnElements 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Execute")]
        [LabelText("Commands")]
        [ShowIf("@Operation == BackgroundLayerOperation.ExecuteOnElements")]
        [CommandListFunctionName("Background.LayerExecute")]
        public CommandListData ElementCommands = new();

        // 笏笏笏 SetEnabled 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [BoxGroup("Enabled")]
        [LabelText("Enabled")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetEnabled")]
        public DynamicValue<bool> Enabled;
    }
}
