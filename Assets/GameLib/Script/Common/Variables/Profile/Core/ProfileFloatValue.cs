// Game.Profile.ProfileFloatValue.cs
//
// ProfileFloatValue - Scalar と Blackboard 両方にバインド可能な float 値

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
    /// Scalar と Blackboard 両方にバインド可能な float プロファイル値。
    /// MovementProfile の DefaultSpeed など、Scalar System で管理する値に使用。
    /// </summary>
    [Serializable]
    public struct ProfileFloatValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Default Value")]
        public float Value;

        // ================================================================
        // Scalar Binding
        // ================================================================

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Scalar Key")]
        [Tooltip("Scalar に登録するキー。default の場合は Scalar にバインドしない。")]
        public ScalarKey ScalarKeyValue;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Policy")]
        [Tooltip("Scalar への書き込みポリシー")]
        [ShowIf(nameof(HasScalarKey))]
        public ScalarBindPolicy ScalarPolicyValue;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Effect Mod")]
        [Tooltip("Scalar Runtime で EffectMod を使用するか")]
        [ShowIf(nameof(HasScalarKey))]
        public bool UseEffectMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Clamp Mod")]
        [Tooltip("Scalar Runtime で ClampMod を使用するか")]
        [ShowIf(nameof(HasScalarKey))]
        public bool UseClampMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Local Base")]
        [Tooltip("Scalar Runtime の LocalBase を設定するか")]
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
        [Tooltip("Scalar 値を Save 対象にするか")]
        [ShowIf(nameof(HasScalarKey))]
        public bool ScalarSaveEnabledValue;

        [FoldoutGroup("$ScalarBindingSaveGroupName")]
        [LabelText("Save Layer")]
        [Tooltip("Scalar の Save レイヤー")]
        [ShowIf(nameof(ShowScalarSaveLayer))]
        public SaveLayer ScalarSaveLayerValue;

        // ================================================================
        // Blackboard Binding
        // ================================================================

        [FoldoutGroup("$BlackboardBindingGroupName")]
        [LabelText("Blackboard VarId")]
        [Tooltip("Blackboard に登録する VarId。0 の場合は Blackboard にバインドしない。")]
        [VarIdDropdown]
        public int BlackboardVarId;

        [FoldoutGroup("$BlackboardBindingGroupName")]
        [LabelText("Policy")]
        [Tooltip("Blackboard への書き込みポリシー")]
        [ShowIf(nameof(HasBlackboardKey))]
        public BlackboardBindPolicy BlackboardPolicyValue;

        [FoldoutGroup("$BlackboardBindingSaveGroupName")]
        [LabelText("Save Enabled")]
        [Tooltip("Blackboard 値を Save 対象にするか")]
        [ShowIf(nameof(HasBlackboardKey))]
        public bool BlackboardSaveEnabledValue;

        [FoldoutGroup("$BlackboardBindingSaveGroupName")]
        [LabelText("Save Layer")]
        [Tooltip("Blackboard の Save レイヤー")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        public SaveLayer BlackboardSaveLayerValue;

        // ================================================================
        // Inspector Conditions
        // ================================================================

        bool HasScalarKey => ScalarKeyValue.Id != 0;
        bool HasBlackboardKey => BlackboardVarId != 0;
        bool ShowClampSettings => HasScalarKey && UseClampMod;
        bool ShowLocalBaseSettings => HasScalarKey && UseLocalBase;
        bool ShowScalarSaveLayer => HasScalarKey && ScalarSaveEnabledValue;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && BlackboardSaveEnabledValue;
        string ScalarBindingGroupName => $"Scalar Binding ({GetScalarBindingLabel()})";
        string ScalarBindingSaveGroupName => $"{ScalarBindingGroupName}/Save";
        string BlackboardBindingGroupName => $"Blackboard Binding ({GetBlackboardBindingLabel()})";
        string BlackboardBindingSaveGroupName => $"{BlackboardBindingGroupName}/Save";

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
            // ScopeIdentity が空の場合は Save 対象外
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
                    // Runtime が存在すれば Baseline のみ更新
                    if (scalar.TryGetRuntime(ScalarKeyValue, out var runtime))
                    {
                        runtime.SetBaseline(Value);
                        if (UseLocalBase)
                            runtime.SetLocalBase(LocalBaseValue);
                    }
                    else
                    {
                        // Runtime がなければ作成
                        var cfg = CreateRuntimeConfig();
                        scalar.EnsureRuntime(ScalarKeyValue, cfg);
                        if (UseLocalBase && scalar.TryGetRuntime(ScalarKeyValue, out runtime))
                            runtime.SetLocalBase(LocalBaseValue);
                    }
                    break;

                case ScalarBindPolicy.ReplaceRuntime:
                    // 常に RuntimeConfig ごと置き換え
                    {
                        var cfg = CreateRuntimeConfig();
                        scalar.EnsureRuntime(ScalarKeyValue, cfg);
                        if (UseLocalBase && scalar.TryGetRuntime(ScalarKeyValue, out var runtimeReplace))
                            runtimeReplace.SetLocalBase(LocalBaseValue);
                    }
                    break;

                case ScalarBindPolicy.SkipIfExists:
                    // 既に存在すればスキップ
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

        ScalarRuntimeConfig CreateRuntimeConfig()
        {
            return new ScalarRuntimeConfig
            {
                BaseValue = Value,
                UseEffectMod = UseEffectMod,
                UseClampMod = UseClampMod,
                Clamp = Clamp
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
        /// 値だけを設定するファクトリ
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
        /// Scalar キーと値を設定するファクトリ
        /// </summary>
        public static ProfileFloatValue WithScalar(
            float value,
            ScalarKey key,
            ScalarBindPolicy policy = ScalarBindPolicy.UpdateBaseline,
            bool useEffectMod = false,
            bool useClampMod = false,
            bool saveEnabled = false,
            SaveLayer saveLayer = SaveLayer.Global) => new()
            {
                Value = value,
                ScalarKeyValue = key,
                ScalarPolicyValue = policy,
                UseEffectMod = useEffectMod,
                UseClampMod = useClampMod,
                Clamp = default,
                ScalarSaveEnabledValue = saveEnabled,
                ScalarSaveLayerValue = saveLayer,
                BlackboardVarId = 0,
                BlackboardPolicyValue = BlackboardBindPolicy.Overwrite
            };

        /// <summary>
        /// Blackboard キーと値を設定するファクトリ
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
        /// Scalar と Blackboard 両方にバインドするファクトリ
        /// </summary>
        public static ProfileFloatValue WithBoth(
            float value,
            ScalarKey scalarKey,
            int blackboardVarId,
            ScalarBindPolicy scalarPolicy = ScalarBindPolicy.UpdateBaseline,
            BlackboardBindPolicy blackboardPolicy = BlackboardBindPolicy.Overwrite) => new()
            {
                Value = value,
                ScalarKeyValue = scalarKey,
                ScalarPolicyValue = scalarPolicy,
                UseEffectMod = false,
                UseClampMod = false,
                Clamp = default,
                BlackboardVarId = blackboardVarId,
                BlackboardPolicyValue = blackboardPolicy
            };

        /// <summary>
        /// 暗黙的な float からの変換
        /// </summary>
        public static implicit operator float(ProfileFloatValue pv) => pv.Value;
    }
}
