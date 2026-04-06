// Game.Profile.ProfileBlackboardValue.cs
//
// Helper structs that specialize in Blackboard-only bindings for types not covered by
// ProfileFloatValue (Vector/Color/UnityObject). Each struct mirrors the binding
// configuration that the float variant exposes but omits scalar logic.

using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Common;
using Game.Save;
using Game.Scalar;

namespace Game.Profile
{
    abstract class ProfileBlackboardBinding<T> : IProfileValueBinding
    {
        public abstract T Value { get; }
        public abstract DynamicVariant ToVariant(T value);

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId = 0;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue = default;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue = false;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue = default;

        protected bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;

        string IProfileValueBinding.ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            GetType().Name,
            Value,
            string.Empty,
            BlackboardVarId);

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => BlackboardSaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => BlackboardSaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !BlackboardSaveEnabledValue || !HasBlackboardKey)
                return;

            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, BlackboardSaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;

            var variant = ToVariant(Value);
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, variant);
                    break;

                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, variant);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar)
        {
            // Blackboard-only helper
        }
    }

    [Serializable]
    public struct ProfileVector2Value : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public Vector2 Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileVector2Value),
            Value,
            string.Empty,
            BlackboardVarId);

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => BlackboardSaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => BlackboardSaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !BlackboardSaveEnabledValue || !HasBlackboardKey)
                return;

            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, BlackboardSaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;

            var variant = DynamicVariant.FromVector2(Value);
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, variant);
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, variant);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }
    }

    [Serializable]
    public struct ProfileVector3Value : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public Vector3 Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileVector3Value),
            Value,
            string.Empty,
            BlackboardVarId);

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => BlackboardSaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => BlackboardSaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !BlackboardSaveEnabledValue || !HasBlackboardKey)
                return;

            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, BlackboardSaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;

            var variant = DynamicVariant.FromVector3(Value);
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, variant);
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, variant);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }
    }

    [Serializable]
    public struct ProfileVector4Value : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public Vector4 Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileVector4Value),
            Value,
            string.Empty,
            BlackboardVarId);

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => BlackboardSaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => BlackboardSaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !BlackboardSaveEnabledValue || !HasBlackboardKey)
                return;

            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, BlackboardSaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;

            var variant = DynamicVariant.FromVector4(Value);
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, variant);
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, variant);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }
    }

    [Serializable]
    public struct ProfileColorValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public Color Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileColorValue),
            Value,
            string.Empty,
            BlackboardVarId);

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => BlackboardSaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => BlackboardSaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !BlackboardSaveEnabledValue || !HasBlackboardKey)
                return;

            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, BlackboardSaveLayerValue, scopeIdentity, profileTypeName));
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;

            var variant = DynamicVariant.FromColor(Value);
            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, variant);
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetVariant(varId, variant);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }
    }

    [Serializable]
    public struct ProfileUnityObjectValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public UnityEngine.Object Value;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("Blackboard Binding")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue;

        [FoldoutGroup("Blackboard Binding/Save")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue;

        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileUnityObjectValue),
            Value,
            string.Empty,
            BlackboardVarId);

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => default;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarBindPolicy.SkipIfExists;
        bool IProfileValueBinding.HasAnyBinding => HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => false;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => default;
        bool IProfileValueBinding.BlackboardSaveEnabled => BlackboardSaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => BlackboardSaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity) || !BlackboardSaveEnabledValue || !HasBlackboardKey)
                return;

            entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, BlackboardSaveLayerValue, scopeIdentity, profileTypeName));
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
                    vars.TrySetManagedRef(varId, Value);
                    break;
                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        vars.TrySetManagedRef(varId, Value);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar) { }
    }
}
