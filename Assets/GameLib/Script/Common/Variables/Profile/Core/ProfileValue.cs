// Game.Profile.ProfileValue.cs
//
// 豎守畑 ProfileValue<T> - Blackboard 繝舌う繝ｳ繝・ぅ繝ｳ繧ｰ蟆ら畑縺ｮ蛟､繝ｩ繝・ヱ繝ｼ

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Common;
using Game.Save;
using Game.Scalar;

namespace Game.Profile
{
    /// <summary>
    /// Blackboard 縺ｫ繝舌う繝ｳ繝牙庄閭ｽ縺ｪ豎守畑繝励Ο繝輔ぃ繧､繝ｫ蛟､縲・
    /// Inspector 縺ｧ蛟､縺ｨ Blackboard 繧ｭ繝ｼ繧剃ｸ邱偵↓險ｭ螳壹〒縺阪ｋ縲・
    /// </summary>
    /// <typeparam name="T">蛟､縺ｮ蝙具ｼ・nt, float, bool, string, Vector2, Vector3, Color・・/typeparam>
    [Serializable]
    public struct ProfileValue<T> : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public T Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Inspector setting.")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [Tooltip("Blackboard 縺ｸ縺ｮ譖ｸ縺崎ｾｼ縺ｿ繝昴Μ繧ｷ繝ｼ")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool SaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [Tooltip("Blackboard 縺ｮ Save 繝ｬ繧､繝､繝ｼ")]
        [ShowIf(nameof(ShowSaveLayer))]
        public SaveLayer SaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowSaveLayer => HasBlackboardKey && SaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            $"ProfileValue<{typeof(T).Name}>",
            Value,
            string.Empty,
            BlackboardVarId);

        // ================================================================
        // IProfileValueBinding - Base
        // ================================================================

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        // ================================================================
        // IProfileValueBinding - Save Meta
        // ================================================================

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => SaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => SaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity))
                return;

            if (SaveEnabledValue && HasBlackboardKey)
            {
                entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, SaveLayerValue, scopeIdentity, profileTypeName));
            }
        }

        // ================================================================
        // IProfileValueBinding - Write
        // ================================================================

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;

            var boxedValue = (object?)Value;
            static bool TryWrite(IVarStore vars, int varId, object? boxedValue)
            {
                if (boxedValue is int i) return vars.TrySetVariant(varId, DynamicVariant.FromInt(i));
                if (boxedValue is float f) return vars.TrySetVariant(varId, DynamicVariant.FromFloat(f));
                if (boxedValue is bool b) return vars.TrySetVariant(varId, DynamicVariant.FromBool(b));
                if (boxedValue is string s) return vars.TrySetVariant(varId, DynamicVariant.FromString(s));
                if (boxedValue is Vector2 v2) return vars.TrySetVariant(varId, DynamicVariant.FromVector2(v2));
                if (boxedValue is Vector3 v3) return vars.TrySetVariant(varId, DynamicVariant.FromVector3(v3));
                if (boxedValue is Color c) return vars.TrySetVariant(varId, DynamicVariant.FromColor(c));
                if (boxedValue is UnityEngine.Object uo) return vars.TrySetVariant(varId, DynamicVariant.FromUnityObject(uo));
                if (boxedValue == null) return vars.TryUnset(varId);
                return vars.TrySetManagedRef(varId, boxedValue);
            }

            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    TryWrite(vars, varId, boxedValue);
                    break;

                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        TryWrite(vars, varId, boxedValue);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar)
        {
            // ProfileValue<T> 縺ｯ Scalar 繝舌う繝ｳ繝・ぅ繝ｳ繧ｰ繧偵し繝昴・繝医＠縺ｪ縺・
        }

        // ================================================================
        // Factory
        // ================================================================

        public static ProfileValue<T> WithValue(T value) => new()
        {
            Value = value,
            BlackboardVarId = 0,
            BlackboardPolicyValue = BlackboardBindPolicy.Overwrite
        };

        public static ProfileValue<T> WithBlackboard(
            T value,
            int blackboardVarId,
            BlackboardBindPolicy policy = BlackboardBindPolicy.Overwrite,
            bool saveEnabled = false,
            SaveLayer saveLayer = SaveLayer.Global) => new()
            {
                Value = value,
                BlackboardVarId = blackboardVarId,
                BlackboardPolicyValue = policy,
                SaveEnabledValue = saveEnabled,
                SaveLayerValue = saveLayer
            };

        public static implicit operator T(ProfileValue<T> pv) => pv.Value;
    }

    /// <summary>
    /// ProfileValue&lt;int&gt; 迚ｹ谿雁喧
    /// </summary>
    [Serializable]
    public sealed class ProfileIntValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public int Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool SaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowSaveLayer))]
        public SaveLayer SaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowSaveLayer => HasBlackboardKey && SaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileIntValue),
            Value,
            string.Empty,
            BlackboardVarId);

        // IProfileValueBinding
        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => SaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => SaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !SaveEnabledValue || !HasBlackboardKey)
                return;
            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, SaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, DynamicVariant.FromInt(Value));
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, DynamicVariant.FromInt(Value));
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }

        public static implicit operator int(ProfileIntValue pv) => pv != null ? pv.Value : default;
    }

    /// <summary>
    /// ProfileValue&lt;bool&gt; 迚ｹ谿雁喧
    /// </summary>
    [Serializable]
    public sealed class ProfileBoolValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public bool Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool SaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowSaveLayer))]
        public SaveLayer SaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowSaveLayer => HasBlackboardKey && SaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileBoolValue),
            Value,
            string.Empty,
            BlackboardVarId);

        // IProfileValueBinding
        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => SaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => SaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !SaveEnabledValue || !HasBlackboardKey)
                return;
            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, SaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, DynamicVariant.FromBool(Value));
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, DynamicVariant.FromBool(Value));
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }

        public static implicit operator bool(ProfileBoolValue pv) => pv != null && pv.Value;
    }

    /// <summary>
    /// ProfileValue&lt;string&gt; 迚ｹ谿雁喧
    /// </summary>
    [Serializable]
    public sealed class ProfileStringValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public string Value = string.Empty;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool SaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowSaveLayer))]
        public SaveLayer SaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowSaveLayer => HasBlackboardKey && SaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileStringValue),
            Value,
            string.Empty,
            BlackboardVarId);

        // IProfileValueBinding
        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => SaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => SaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !SaveEnabledValue || !HasBlackboardKey)
                return;
            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, SaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, DynamicVariant.FromString(Value));
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, DynamicVariant.FromString(Value));
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }

        public static implicit operator string(ProfileStringValue pv) => pv != null ? pv.Value : string.Empty;
    }
}
