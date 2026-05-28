// Game.Profile.ProfileFloatValue.cs
//
// ProfileFloatValue - Scalar 縺ｨ Blackboard 荳｡譁ｹ縺ｫ繝舌う繝ｳ繝牙庄閭ｽ縺ｪ float 蛟､

using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Game.Common;
using Game.Kernel.IR;
using Game.Save;
using Game.Scalar;

namespace Game.Profile
{
    /// <summary>
    /// Scalar 縺ｨ Blackboard 荳｡譁ｹ縺ｫ繝舌う繝ｳ繝牙庄閭ｽ縺ｪ float 繝励Ο繝輔ぃ繧､繝ｫ蛟､縲・
    /// MovementProfile 縺ｮ DefaultSpeed 縺ｪ縺ｩ縲ヾcalar System 縺ｧ邂｡逅・☆繧句､縺ｫ菴ｿ逕ｨ縲・
    /// </summary>
    [Serializable]
    public sealed class ProfileFloatValue : IProfileValueBinding, IScalarDeclarationAuthoring
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public float Value;

        // ================================================================
        // Scalar Binding
        // ================================================================

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Scalar Key")]
        [Tooltip("Inspector setting.")]
        public ScalarKey ScalarKeyValue;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Policy")]
        [Tooltip("Scalar 縺ｸ縺ｮ譖ｸ縺崎ｾｼ縺ｿ繝昴Μ繧ｷ繝ｼ")]
        [ShowIf(nameof(HasScalarKey))]
        public ScalarBindPolicy ScalarPolicyValue;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Effect Mod")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(HasScalarKey))]
        public bool UseEffectMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Round Mod")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(HasScalarKey))]
        public bool UseRoundMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Round Digits")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(ShowRoundSettings))]
        [MinValue(0)]
        [MaxValue(6)]
        public int RoundDigits;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Clamp Mod")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(HasScalarKey))]
        public bool UseClampMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Local Base")]
        [Tooltip("Scalar Runtime 縺ｮ LocalBase 繧定ｨｭ螳壹☆繧九°")]
        [ShowIf(nameof(HasScalarKey))]
        public bool UseLocalBase;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Local Base")]
        [ShowIf(nameof(ShowLocalBaseSettings))]
        public float LocalBaseValue;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Clamp Settings")]
        [ShowIf(nameof(ShowClampSettings))]
        public ScalarClamp Clamp;

        [FoldoutGroup("$ScalarBindingSaveGroupName")]
        [LabelText("Save Enabled")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(HasScalarKey))]
        public bool ScalarSaveEnabledValue;

        [FoldoutGroup("$ScalarBindingSaveGroupName")]
        [LabelText("Save Layer")]
        [Tooltip("Scalar 縺ｮ Save 繝ｬ繧､繝､繝ｼ")]
        [ShowIf(nameof(ShowScalarSaveLayer))]
        public SaveLayer ScalarSaveLayerValue;

        // ================================================================
        // Blackboard Binding
        // ================================================================

        [FoldoutGroup("$BlackboardBindingGroupName")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Inspector setting.")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("$BlackboardBindingGroupName")]
        [LabelText("Policy")]
        [Tooltip("Blackboard 縺ｸ縺ｮ譖ｸ縺崎ｾｼ縺ｿ繝昴Μ繧ｷ繝ｼ")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("$BlackboardBindingSaveGroupName")]
        [LabelText("Save Enabled")]
        [Tooltip("Inspector setting.")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue;

        [FoldoutGroup("$BlackboardBindingSaveGroupName")]
        [LabelText("Save Layer")]
        [Tooltip("Blackboard 縺ｮ Save 繝ｬ繧､繝､繝ｼ")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue;

        // ================================================================
        // Inspector Conditions
        // ================================================================

        bool HasScalarKey => ScalarKeyValue.Id != 0;
        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowRoundSettings => HasScalarKey && UseRoundMod;
        bool ShowClampSettings => HasScalarKey && UseClampMod;
        bool ShowLocalBaseSettings => HasScalarKey && UseLocalBase;
        bool ShowScalarSaveLayer => HasScalarKey && ScalarSaveEnabledValue;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;
        string ScalarBindingGroupName => $"Scalar Binding ({GetScalarBindingLabel()})";
        string ScalarBindingSaveGroupName => $"{ScalarBindingGroupName}/Save";
        string BlackboardBindingGroupName => $"Blackboard Binding ({GetBlackboardBindingLabel()})";
        string BlackboardBindingSaveGroupName => $"{BlackboardBindingGroupName}/Save";

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileFloatValue),
            Value,
            ScalarKeyValue.Name,
            BlackboardVarId);

        // ================================================================
        // IProfileValueBinding - Base
        // ================================================================

        int IProfileValueBinding.BlackboardKey => BlackboardVarId;
        ScalarKey IProfileValueBinding.ScalarKey => ScalarKeyValue;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => BlackboardPolicyValue;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => ScalarPolicyValue;

        bool IProfileValueBinding.HasAnyBinding => HasScalarKey || HasBlackboardKey;

        // ================================================================
        // IProfileValueBinding - Save Meta
        // ================================================================

        bool IProfileValueBinding.ScalarSaveEnabled => ScalarSaveEnabledValue && HasScalarKey;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => ScalarSaveLayerValue;
        bool IProfileValueBinding.BlackboardSaveEnabled => BlackboardSaveEnabledValue && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => BlackboardSaveLayerValue;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            // ScopeIdentity 縺檎ｩｺ縺ｮ蝣ｴ蜷医・ Save 蟇ｾ雎｡螟・
            if (string.IsNullOrEmpty(scopeIdentity))
                return;

            if (ScalarSaveEnabledValue && HasScalarKey)
            {
                entries.Add(BindingSaveEntry.ForScalar(ScalarKeyValue.Name, ScalarSaveLayerValue, scopeIdentity, profileTypeName));
            }

            if (BlackboardSaveEnabledValue && HasBlackboardKey)
            {
                entries.Add(BindingSaveEntry.ForBlackboard(BlackboardVarId, BlackboardSaveLayerValue, scopeIdentity, profileTypeName));
            }
        }

        // ================================================================
        // IProfileValueBinding - Write
        // ================================================================

        void IProfileValueBinding.WriteToBlackboard(IVarStore blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard;
            var varId = BlackboardVarId;
            if (varId == 0)
                return;

            switch (BlackboardPolicyValue)
            {
                case BlackboardBindPolicy.Overwrite:
                    vars.TrySetVariant(varId, DynamicVariant.FromFloat(Value));
                    break;

                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                    {
                        vars.TrySetVariant(varId, DynamicVariant.FromFloat(Value));
                    }
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar)
        {
            if (!HasScalarKey || scalar == null)
                return;

            switch (ScalarPolicyValue)
            {
                case ScalarBindPolicy.UpdateBaseline:
                    // Runtime 縺悟ｭ伜惠縺吶ｌ縺ｰ Baseline 縺ｮ縺ｿ譖ｴ譁ｰ
                    if (scalar.TryGetRuntime(ScalarKeyValue, out var runtime))
                    {
                        runtime.SetBaseline(Value);
                        if (UseLocalBase)
                            runtime.SetLocalBase(LocalBaseValue);
                    }
                    else
                    {
                        // Runtime 縺後↑縺代ｌ縺ｰ菴懈・
                        var cfg = CreateRuntimeConfig();
                        scalar.EnsureRuntime(ScalarKeyValue, cfg);
                        if (UseLocalBase && scalar.TryGetRuntime(ScalarKeyValue, out runtime))
                            runtime.SetLocalBase(LocalBaseValue);
                    }
                    break;

                case ScalarBindPolicy.ReplaceRuntime:
                    // 蟶ｸ縺ｫ RuntimeConfig 縺斐→鄂ｮ縺肴鋤縺・
                    {
                        var cfg = CreateRuntimeConfig();
                        scalar.EnsureRuntime(ScalarKeyValue, cfg);
                        if (UseLocalBase && scalar.TryGetRuntime(ScalarKeyValue, out var runtimeReplace))
                            runtimeReplace.SetLocalBase(LocalBaseValue);
                    }
                    break;

                case ScalarBindPolicy.SkipIfExists:
                    // 譌｢縺ｫ蟄伜惠縺吶ｌ縺ｰ繧ｹ繧ｭ繝・・
                    if (!scalar.TryGetRuntime(ScalarKeyValue, out _))
                    {
                        var cfg = CreateRuntimeConfig();
                        scalar.EnsureRuntime(ScalarKeyValue, cfg);
                    }
                    if (UseLocalBase && scalar.TryGetRuntime(ScalarKeyValue, out var runtimeSkip))
                        runtimeSkip.SetLocalBase(LocalBaseValue);
                    break;
            }
        }

        bool IScalarDeclarationAuthoring.TryCreateScalarDeclaration(
            ScalarOwnerIdentity owner,
            string profileTypeName,
            SourceLocationIR source,
            out ScalarDeclarationInput declaration,
            out string failureReason)
        {
            if (!HasScalarKey)
            {
                declaration = default;
                failureReason = "ProfileFloatValue requires a verified scalar key to create a scalar declaration.";
                return false;
            }

            return ProfileScalarDeclarationProjection.TryCreateScalarDeclaration(
                owner,
                ScalarKeyValue,
                ScalarPolicyValue,
                Value,
                UseEffectMod,
                UseRoundMod,
                RoundDigits,
                UseClampMod,
                Clamp,
                UseLocalBase,
                LocalBaseValue,
                ScalarSaveEnabledValue && HasScalarKey,
                ScalarSaveLayerValue,
                profileTypeName,
                nameof(ProfileFloatValue),
                source,
                out declaration,
                out failureReason);
        }

        ScalarRuntimeConfig CreateRuntimeConfig()
        {
            var useClamp = UseClampMod;
            var clamp = Clamp;

            if (useClamp && !clamp.TryCreateLiteralClamp(out clamp))
            {
                Debug.LogError($"[ProfileFloatValue] SCALAR_CLAMP_DYNAMIC_UNSUPPORTED key={ScalarKeyValue.Id} name={ScalarKeyValue.Name ?? string.Empty}");
                useClamp = false;
                clamp = default;
            }

            return new ScalarRuntimeConfig
            {
                BaseValue = Value,
                UseEffectMod = UseEffectMod,
                UseRoundMod = UseRoundMod,
                RoundDigits = Mathf.Clamp(RoundDigits, 0, 6),
                UseClampMod = useClamp,
                Clamp = clamp
            };
        }

        string GetScalarBindingLabel()
        {
            if (!HasScalarKey)
                return "Unbound";

            return GetLeafLabel(ScalarKeyValue.Name);
        }

        string GetBlackboardBindingLabel()
        {
            if (!HasBlackboardKey)
                return "Unbound";

            if (VarIdResolver.TryGetStableKey(BlackboardVarId, out var stableKey) && !string.IsNullOrEmpty(stableKey))
                return GetLeafLabel(stableKey);

            return $"varId:{BlackboardVarId}";
        }

        static string GetLeafLabel(string fullKey)
        {
            if (string.IsNullOrEmpty(fullKey))
                return "Unbound";

            var lastDot = fullKey.LastIndexOf('.');
            if (lastDot >= 0 && lastDot + 1 < fullKey.Length)
                return fullKey.Substring(lastDot + 1);

            var lastSlash = fullKey.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash + 1 < fullKey.Length)
                return fullKey.Substring(lastSlash + 1);

            return fullKey;
        }

        // ================================================================
        // Factory Methods
        // ================================================================

        /// <summary>
        /// 蛟､縺縺代ｒ險ｭ螳壹☆繧九ヵ繧｡繧ｯ繝医Μ
        /// </summary>
        public static ProfileFloatValue WithValue(float value) => new()
        {
            Value = value,
            ScalarKeyValue = default,
            ScalarPolicyValue = ScalarBindPolicy.UpdateBaseline,
            BlackboardVarId = 0,
            BlackboardPolicyValue = BlackboardBindPolicy.Overwrite
        };

        /// <summary>
        /// Scalar 繧ｭ繝ｼ縺ｨ蛟､繧定ｨｭ螳壹☆繧九ヵ繧｡繧ｯ繝医Μ
        /// </summary>
        public static ProfileFloatValue WithScalar(
            float value,
            ScalarKey key,
            ScalarBindPolicy policy = ScalarBindPolicy.UpdateBaseline,
            bool useEffectMod = false,
            bool useClampMod = false,
            bool saveEnabled = false,
            SaveLayer saveLayer = SaveLayer.Global,
            bool useRoundMod = false,
            int roundDigits = 0) => new()
            {
                Value = value,
                ScalarKeyValue = key,
                ScalarPolicyValue = policy,
                UseEffectMod = useEffectMod,
                UseRoundMod = useRoundMod,
                RoundDigits = roundDigits,
                UseClampMod = useClampMod,
                Clamp = default,
                ScalarSaveEnabledValue = saveEnabled,
                ScalarSaveLayerValue = saveLayer,
                BlackboardVarId = 0,
                BlackboardPolicyValue = BlackboardBindPolicy.Overwrite
            };

        /// <summary>
        /// Blackboard 繧ｭ繝ｼ縺ｨ蛟､繧定ｨｭ螳壹☆繧九ヵ繧｡繧ｯ繝医Μ
        /// </summary>
        public static ProfileFloatValue WithBlackboard(
            float value,
            int blackboardVarId,
            BlackboardBindPolicy policy = BlackboardBindPolicy.Overwrite,
            bool saveEnabled = false,
            SaveLayer saveLayer = SaveLayer.Global) => new()
            {
                Value = value,
                ScalarKeyValue = default,
                ScalarPolicyValue = ScalarBindPolicy.SkipIfExists,
                BlackboardVarId = blackboardVarId,
                BlackboardPolicyValue = policy,
                BlackboardSaveEnabledValue = saveEnabled,
                BlackboardSaveLayerValue = saveLayer
            };

        /// <summary>
        /// Scalar 縺ｨ Blackboard 荳｡譁ｹ縺ｫ繝舌う繝ｳ繝峨☆繧九ヵ繧｡繧ｯ繝医Μ
        /// </summary>
        public static ProfileFloatValue WithBoth(
            float value,
            ScalarKey scalarKey,
            int blackboardVarId,
            ScalarBindPolicy scalarPolicy = ScalarBindPolicy.UpdateBaseline,
            BlackboardBindPolicy blackboardPolicy = BlackboardBindPolicy.Overwrite,
            bool useRoundMod = false,
            int roundDigits = 0) => new()
            {
                Value = value,
                ScalarKeyValue = scalarKey,
                ScalarPolicyValue = scalarPolicy,
                UseEffectMod = false,
                UseClampMod = false,
                UseRoundMod = useRoundMod,
                RoundDigits = roundDigits,
                Clamp = default,
                BlackboardVarId = blackboardVarId,
                BlackboardPolicyValue = blackboardPolicy
            };

        /// <summary>
        /// 證鈴ｻ咏噪縺ｪ float 縺九ｉ縺ｮ螟画鋤
        /// </summary>
        public static implicit operator float(ProfileFloatValue pv) => pv != null ? pv.Value : default;
    }
}
