// Game.Profile.ProfileValue.cs
//
// 汎用 ProfileValue<T> - Blackboard バインディング専用の値ラッパー

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
    /// Blackboard にバインド可能な汎用プロファイル値。
    /// Inspector で値と Blackboard キーを一緒に設定できる。
    /// </summary>
    /// <typeparam name="T">値の型（int, float, bool, string, Vector2, Vector3, Color）</typeparam>
    [Serializable]
    public struct ProfileValue<T> : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public T Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [Tooltip("Blackboard への書き込みポリシー")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [Tooltip("Blackboard 値を Save 対象にするか")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool SaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [Tooltip("Blackboard の Save レイヤー")]
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
            // ProfileValue<T> は Scalar バインディングをサポートしない
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
    /// ProfileValue&lt;int&gt; 特殊化
    /// </summary>
    [Serializable]
    public struct ProfileIntValue : IProfileValueBinding
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

        public static implicit operator int(ProfileIntValue pv) => pv.Value;
    }

    /// <summary>
    /// ProfileValue&lt;bool&gt; 特殊化
    /// </summary>
    [Serializable]
    public struct ProfileBoolValue : IProfileValueBinding
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

        public static implicit operator bool(ProfileBoolValue pv) => pv.Value;
    }

    /// <summary>
    /// ProfileValue&lt;string&gt; 特殊化
    /// </summary>
    [Serializable]
    public struct ProfileStringValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public string Value;

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

        public static implicit operator string(ProfileStringValue pv) => pv.Value;
    }
}
