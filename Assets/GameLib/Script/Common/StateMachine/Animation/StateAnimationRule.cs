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
    /// StateAnimation 縺ｮ繝ｫ繝ｼ繝ｫ螳夂ｾｩ縲・
    /// State 縺ｮ譚｡莉ｶ縺ｫ繝槭ャ繝√＠縺溷ｴ蜷医↓蜀咲函縺吶ｋ繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ繧呈欠螳壹☆繧九・
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
                return $"{statePart} @ {layerPart} 竊・{channelPart}";
            }
        }
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        //  Condition
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [TitleGroup("Condition")]
        [Tooltip("Inspector setting.")]
        [LabelText("State")]
        [StateKeyPicker]
        public string StateKey;

        [TitleGroup("Condition")]
        [Tooltip("Inspector setting.")]
        [LabelText("Layer")]
        public string LayerKey;

        [TitleGroup("Condition")]
        [Tooltip("Inspector setting.")]
        [EnumToggleButtons]
        [LabelText("Condition Mode")]
        public StateAnimationConditionMode ConditionMode = StateAnimationConditionMode.LegacyOptionConditions;

        [TitleGroup("Condition")]
        [Tooltip("Inspector setting.")]
        [EnumToggleButtons]
        [LabelText("Option繝ｭ繧ｸ繝・け")]
        [ShowIf(nameof(UseLegacyOptionConditions))]
        public OptionConditionEvaluationMode OptionLogic = OptionConditionEvaluationMode.All;

        [TitleGroup("Condition")]
        [InfoBox("Inspector info.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false, DraggableItems = true, ShowPaging = false)]
        [ShowIf(nameof(UseLegacyOptionConditions))]
        public List<OptionCondition> OptionConditions = new();

        [TitleGroup("Condition")]
        [InfoBox("Inspector info.")]
        [ShowIf(nameof(UseDynamicBoolCondition))]
        [LabelText("Dynamic Option Condition")]
        [InlineProperty]
        [HideLabel]
        public DynamicValue<bool> DynamicOptionCondition = DynamicValueExtensions.FromLiteral(true);

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        //  Animation
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        [TitleGroup("Animation")]
        [InlineProperty]
        [HideLabel]
        [Tooltip("Inspector setting.")]
        public AnimationSpritePreset Preset = new();

        [TitleGroup("Animation")]
        [Tooltip("騾∽ｿ｡蜈医メ繝｣繝阪Ν繧ｿ繧ｰ")]
        public string ChannelTag = "default";

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        //  FlipX
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [TitleGroup("FlipX")]
        [Tooltip("Inspector setting.")]
        public bool ApplyFlipX;

        [TitleGroup("FlipX")]
        [ShowIf("ApplyFlipX")]
        [Tooltip("Inspector setting.")]
        [InfoBox("Inspector info.")]
        [OptionKeyPicker]
        public string FlipXTrueOptionValue;

        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
        //  Priority & Restart
        // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏

        [TitleGroup("Priority & Restart")]
        [Tooltip("Inspector setting.")]
        public int Priority;

        [TitleGroup("Priority & Restart")]
        [Tooltip("Inspector setting.")]
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
    /// Option 譚｡莉ｶ繧ｨ繝ｳ繝医Μ縲・
    /// </summary>
    [Serializable]
    public sealed class OptionCondition
    {
        [HorizontalGroup("Row"), LabelWidth(90)]
        [Tooltip("OptionKey (萓・ Movement.Direction)")]
        [OptionKeyPicker]
        public string OptionKey;

        [HorizontalGroup("Row"), LabelWidth(90)]
        [Tooltip("OptionKey 縺檎ｫ九▲縺ｦ縺・ｋ/遶九▲縺ｦ縺・↑縺・・縺ｩ縺｡繧峨ｒ隕∵ｱゅ☆繧九°")]
        [LabelText("Field")]
        public OptionConditionPresence Presence = OptionConditionPresence.IsSet;
    }

    /// <summary>
    /// Option 譚｡莉ｶ繝ｪ繧ｹ繝医・隧穂ｾ｡繝ｭ繧ｸ繝・け縲・
    /// </summary>
    public enum OptionConditionEvaluationMode
    {
        [LabelText("AND (縺吶∋縺ｦ true)")]
        All,

        [LabelText("OR (縺・★繧後° true)")]
        Any,
    }

    /// <summary>
    /// OptionKey 縺ｮ蟄伜惠繝√ぉ繝・け縲・
    /// </summary>
    public enum OptionConditionPresence
    {
        [LabelText("Field")]
        IsSet,

        [LabelText("Field")]
        IsNotSet,
    }

    /// <summary>
    /// 繧｢繝九Γ繝ｼ繧ｷ繝ｧ繝ｳ蜀埼幕蟋九Δ繝ｼ繝峨・
    /// </summary>
    public enum AnimationRestartMode
    {
        /// <summary>State 騾ｲ蜈･譎ゅ・縺ｿ蜀埼幕蟋・/summary>
        OnEnterOnly,

        /// <summary>Pulse 逋ｺ轣ｫ譎ゅｂ蜀埼幕蟋・/summary>
        OnPulse,

        /// <summary>蜀埼幕蟋九＠縺ｪ縺・ｼ域里縺ｫ蜀咲函荳ｭ縺ｪ繧臥ｶ咏ｶ夲ｼ・/summary>
        Never,
    }
}
