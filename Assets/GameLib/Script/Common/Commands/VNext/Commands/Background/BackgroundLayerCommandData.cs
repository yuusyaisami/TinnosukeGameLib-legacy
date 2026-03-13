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
    /// Background Layer に対する操作種別。
    /// </summary>
    public enum BackgroundLayerOperation
    {
        /// <summary>Layer の ScrollSpeed を設定する。</summary>
        SetScrollSpeed = 0,

        /// <summary>Layer の Offset を設定する。</summary>
        SetOffset = 1,

        /// <summary>Layer の Offset に加算する。</summary>
        AddOffset = 2,

        /// <summary>Layer の一時停止 / 再開。</summary>
        SetPaused = 3,

        /// <summary>AnimationSpritePreset を VarStore に書き込む。</summary>
        WriteSpritePreset = 4,

        /// <summary>Layer 内の全要素に対して MaterialFx SetState を適用する。</summary>
        SetMaterialFx = 5,

        /// <summary>Layer 内の各要素 LTS でコマンドリストを実行する。</summary>
        ExecuteOnElements = 6,

        /// <summary>Layer を有効 / 無効にする（タイルの表示 / 非表示）。</summary>
        SetEnabled = 7,

        /// <summary>システム全体を MarkDirty して再評価させる。</summary>
        MarkDirty = 8,
    }

    /// <summary>
    /// Background Layer を制御するための統合コマンドデータ。
    /// Operation フィールドで操作を切り替え、各操作に必要なパラメータのみ Inspector に表示される。
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

        // ─── Layer 指定 ───────────────────────────────────────────────

        [BoxGroup("Target")]
        [LabelText("Operation")]
        [EnumToggleButtons]
        public BackgroundLayerOperation Operation = BackgroundLayerOperation.SetScrollSpeed;

        [BoxGroup("Target")]
        [LabelText("Use Layer Name")]
        [Tooltip("true の場合、LayerName でレイヤーを特定する。false なら LayerIndex。")]
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
        [Tooltip("true の場合、すべてのレイヤーに対して操作を適用する。")]
        public bool AllLayers;

        // ─── SetScrollSpeed ──────────────────────────────────────────

        [BoxGroup("ScrollSpeed")]
        [LabelText("Speed")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetScrollSpeed")]
        public DynamicValue<Vector2> ScrollSpeed;

        // ─── SetOffset / AddOffset ───────────────────────────────────

        [BoxGroup("Offset")]
        [LabelText("Offset")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetOffset || Operation == BackgroundLayerOperation.AddOffset")]
        public DynamicValue<Vector2> Offset;

        // ─── SetPaused ───────────────────────────────────────────────

        [BoxGroup("Pause")]
        [LabelText("Paused")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetPaused")]
        public DynamicValue<bool> Paused;

        // ─── WriteSpritePreset ───────────────────────────────────────

        [BoxGroup("SpritePreset")]
        [LabelText("Preset Source")]
        [ShowIf("@Operation == BackgroundLayerOperation.WriteSpritePreset")]
        public DynamicValue<AnimationSpritePreset> SpritePresetSource;

        [BoxGroup("SpritePreset")]
        [LabelText("Write VarId")]
        [Tooltip("AnimationSpritePreset を書き込む先の VarId。Blackboard 経由で SpawnCommands から参照する。")]
        [ShowIf("@Operation == BackgroundLayerOperation.WriteSpritePreset")]
        public DynamicValue<int> SpritePresetVarId;

        // ─── SetMaterialFx ──────────────────────────────────────────

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

        // ─── ExecuteOnElements ───────────────────────────────────────

        [BoxGroup("Execute")]
        [LabelText("Commands")]
        [ShowIf("@Operation == BackgroundLayerOperation.ExecuteOnElements")]
        [CommandListFunctionName("Background.LayerExecute")]
        public CommandListData ElementCommands = new();

        // ─── SetEnabled ─────────────────────────────────────────────

        [BoxGroup("Enabled")]
        [LabelText("Enabled")]
        [ShowIf("@Operation == BackgroundLayerOperation.SetEnabled")]
        public DynamicValue<bool> Enabled;
    }
}
