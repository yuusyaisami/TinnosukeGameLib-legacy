// Game.StateMachine.StateAnimationRule.cs

using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Channel;
using Game.Common;
using Game.StateMachine.Editor;

namespace Game.StateMachine
{
    /// <summary>
    /// StateAnimation のルール定義。
    /// State の条件にマッチした場合に再生するアニメーションを指定する。
    /// </summary>
    [Serializable]
    public sealed class StateAnimationRule
    {
        public string RuleHeader
        {
            get
            {
                var layerPart = string.IsNullOrWhiteSpace(LayerKey) ? "Any Layer" : LayerKey;
                var statePart = string.IsNullOrWhiteSpace(StateKey) ? "Any State" : StateKey;
                var channelPart = string.IsNullOrWhiteSpace(ChannelTag) ? "default" : ChannelTag;
                return $"{statePart} @ {layerPart} → {channelPart}";
            }
        }
        // ────────────────────────────────────────────────────────────
        //  Condition
        // ────────────────────────────────────────────────────────────

        [TitleGroup("Condition")]
        [Tooltip("対象の StateKey（空なら全 State にマッチ）")]
        [LabelText("State")]
        [StateKeyPicker]
        public string StateKey;

        [TitleGroup("Condition")]
        [Tooltip("対象の LayerKey（空なら全 Layer にマッチ）")]
        [LabelText("Layer")]
        public string LayerKey;

        [TitleGroup("Condition")]
        [Tooltip("条件判定のモード。Legacy は既存の OptionConditions(AND/OR)、DynamicBool は DynamicValue<bool> を使用します。")]
        [EnumToggleButtons]
        [LabelText("Condition Mode")]
        public StateAnimationConditionMode ConditionMode = StateAnimationConditionMode.LegacyOptionConditions;

        [TitleGroup("Condition")]
        [Tooltip("Option 条件の判定方法")]
        [EnumToggleButtons]
        [LabelText("Optionロジック")]
        [ShowIf(nameof(UseLegacyOptionConditions))]
        public OptionConditionEvaluationMode OptionLogic = OptionConditionEvaluationMode.All;

        [TitleGroup("Condition")]
        [InfoBox("OptionCondition は OptionKey が現在立っているかどうかだけを見ます。Value は不要です。", InfoMessageType.Info)]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = true, ShowPaging = false)]
        [ShowIf(nameof(UseLegacyOptionConditions))]
        public List<OptionCondition> OptionConditions = new();

        [TitleGroup("Condition")]
        [InfoBox("DynamicBool では DynamicValue<bool> の評価結果で条件判定します。BoolExpression 等を使用できます。", InfoMessageType.Info)]
        [ShowIf(nameof(UseDynamicBoolCondition))]
        [LabelText("Dynamic Option Condition")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<bool> DynamicOptionCondition = DynamicValueExtensions.FromLiteral(true);

        // ────────────────────────────────────────────────────────────
        //  Animation
        // ────────────────────────────────────────────────────────────
        [TitleGroup("Animation")]
        [InlineProperty]
        [HideLabel]
        [Tooltip("再生設定は AnimationSpritePreset と同一構造で統一。")]
        public AnimationSpritePreset Preset = new();

        [TitleGroup("Animation")]
        [Tooltip("送信先チャネルタグ")]
        public string ChannelTag = "default";

        // ────────────────────────────────────────────────────────────
        //  FlipX
        // ────────────────────────────────────────────────────────────

        [TitleGroup("FlipX")]
        [Tooltip("FlipX を適用するか")]
        public bool ApplyFlipX;

        [TitleGroup("FlipX")]
        [ShowIf("ApplyFlipX")]
        [Tooltip("FlipX = true となる OptionValue（空なら無条件 FlipX=true）")]
        [InfoBox("OptionValue を指定（例: Movement.Direction.Left）。\n対応する OptionKey は自動導出され、Local→Global の順で解決されます。")]
        [OptionKeyPicker]
        public string FlipXTrueOptionValue;

        // ────────────────────────────────────────────────────────────
        //  Priority & Restart
        // ────────────────────────────────────────────────────────────

        [TitleGroup("Priority & Restart")]
        [Tooltip("ルール優先度（高いほど優先）")]
        public int Priority;

        [TitleGroup("Priority & Restart")]
        [Tooltip("再開始モード")]
        public AnimationRestartMode RestartMode = AnimationRestartMode.OnEnterOnly;

        public AnimationSpritePreset GetAnimationPreset()
        {
            Preset ??= new AnimationSpritePreset();
            return Preset;
        }

        bool UseLegacyOptionConditions() => ConditionMode == StateAnimationConditionMode.LegacyOptionConditions;
        bool UseDynamicBoolCondition() => ConditionMode == StateAnimationConditionMode.DynamicBool;
    }

    public enum StateAnimationConditionMode
    {
        [LabelText("Legacy OptionConditions")]
        LegacyOptionConditions = 0,

        [LabelText("DynamicValue<bool>")]
        DynamicBool = 1,
    }

    /// <summary>
    /// Option 条件エントリ。
    /// </summary>
    [Serializable]
    public sealed class OptionCondition
    {
        [HorizontalGroup("Row"), LabelWidth(90)]
        [Tooltip("OptionKey (例: Movement.Direction)")]
        [OptionKeyPicker]
        public string OptionKey;

        [HorizontalGroup("Row"), LabelWidth(90)]
        [Tooltip("OptionKey が立っている/立っていないのどちらを要求するか")]
        [LabelText("状態要求")]
        public OptionConditionPresence Presence = OptionConditionPresence.IsSet;
    }

    /// <summary>
    /// Option 条件リストの評価ロジック。
    /// </summary>
    public enum OptionConditionEvaluationMode
    {
        [LabelText("AND (すべて true)")]
        All,

        [LabelText("OR (いずれか true)")]
        Any,
    }

    /// <summary>
    /// OptionKey の存在チェック。
    /// </summary>
    public enum OptionConditionPresence
    {
        [LabelText("Option が set になっているとき")]
        IsSet,

        [LabelText("Option が clear もしくは unset のとき")]
        IsNotSet,
    }

    /// <summary>
    /// アニメーション再開始モード。
    /// </summary>
    public enum AnimationRestartMode
    {
        /// <summary>State 進入時のみ再開始</summary>
        OnEnterOnly,

        /// <summary>Pulse 発火時も再開始</summary>
        OnPulse,

        /// <summary>再開始しない（既に再生中なら継続）</summary>
        Never,
    }
}
