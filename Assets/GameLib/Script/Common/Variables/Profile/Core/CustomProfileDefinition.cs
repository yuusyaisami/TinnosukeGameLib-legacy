// Game.Profile.CustomProfileDefinition.cs
//
// Custom profile definition with dynamic bindings list.

using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Common;
using Game.Save;
using Game.Scalar;
using Sirenix.OdinInspector;

namespace Game.Profile
{
    [Serializable]
    public sealed class CustomProfileDefinition : BaseProfileData
    {
        [BoxGroup("Profile")]
        [LabelText("Profile Name")]
        [SerializeField]
        string _profileName = "CustomProfile";

        [BoxGroup("Profile")]
        [LabelText("Bindings")]
        [SerializeReference]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, CustomAddFunction = nameof(AddBindingInternal), ListElementLabelName = "ProfileBindingListLabel")]
        List<IProfileValueBinding> _bindings = new();

        public string ProfileName => _profileName;

        public override Type ProfileType => typeof(CustomProfileDefinition);

        public override string ToString() => string.IsNullOrEmpty(_profileName) ? nameof(CustomProfileDefinition) : _profileName;

        public override IEnumerable<IProfileValueBinding> EnumerateBindings()
        {
            if (_bindings == null)
                yield break;

            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding != null)
                    yield return binding;
            }
        }

        public override void CollectBindings(List<IProfileValueBinding> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (_bindings == null)
                return;

            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding != null)
                    output.Add(binding);
            }
        }

        public override int GetBindingCount()
        {
            if (_bindings == null)
                return 0;

            int count = 0;
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i] != null)
                    count++;
            }
            return count;
        }

        public void AddBindingInternal()
        {
            _bindings ??= new List<IProfileValueBinding>();
            _bindings.Add(new ProfileDynamicValue());
        }
    }

    public enum ProfileDynamicValueKind
    {
        Float = 10,
        Int = 20,
        Bool = 30,
        String = 40,
        Vector2 = 50,
        Vector3 = 60,
        Color = 70,
        UnityObject = 80,
    }

    [Serializable]
    public sealed class ProfileDynamicValue : IProfileValueBinding
    {
        [BoxGroup("Value")]
        [LabelText("Kind")]
        [SerializeField]
        ProfileDynamicValueKind _kind = ProfileDynamicValueKind.Float;

        [BoxGroup("Value")]
        [LabelText("Float Value")]
        [ShowIf(nameof(IsFloat))]
        [SerializeField]
        float _floatValue;

        [BoxGroup("Value")]
        [LabelText("Int Value")]
        [ShowIf(nameof(IsInt))]
        [SerializeField]
        int _intValue;

        [BoxGroup("Value")]
        [LabelText("Bool Value")]
        [ShowIf(nameof(IsBool))]
        [SerializeField]
        bool _boolValue;

        [BoxGroup("Value")]
        [LabelText("String Value")]
        [ShowIf(nameof(IsString))]
        [SerializeField]
        string _stringValue = string.Empty;

        [BoxGroup("Value")]
        [LabelText("Vector2 Value")]
        [ShowIf(nameof(IsVector2))]
        [SerializeField]
        Vector2 _vector2Value;

        [BoxGroup("Value")]
        [LabelText("Vector3 Value")]
        [ShowIf(nameof(IsVector3))]
        [SerializeField]
        Vector3 _vector3Value;

        [BoxGroup("Value")]
        [LabelText("Color Value")]
        [ShowIf(nameof(IsColor))]
        [SerializeField]
        Color _colorValue = Color.white;

        [BoxGroup("Value")]
        [LabelText("Object Value")]
        [ShowIf(nameof(IsUnityObject))]
        [SerializeField]
        UnityEngine.Object _unityObjectValue;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Scalar Key")]
        [ShowIf(nameof(CanBindScalar))]
        [SerializeField]
        ScalarKey _scalarKey;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Policy")]
        [ShowIf(nameof(ShowScalarSettings))]
        [SerializeField]
        ScalarBindPolicy _scalarPolicy = ScalarBindPolicy.UpdateBaseline;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Effect Mod")]
        [ShowIf(nameof(ShowScalarSettings))]
        [SerializeField]
        bool _useEffectMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Round Mod")]
        [ShowIf(nameof(ShowScalarSettings))]
        [SerializeField]
        bool _useRoundMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Round Digits")]
        [ShowIf(nameof(ShowRoundSettings))]
        [MinValue(0)]
        [MaxValue(6)]
        [SerializeField]
        int _roundDigits;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Clamp Mod")]
        [ShowIf(nameof(ShowScalarSettings))]
        [SerializeField]
        bool _useClampMod;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Use Local Base")]
        [ShowIf(nameof(ShowScalarSettings))]
        [SerializeField]
        bool _useLocalBase;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Local Base")]
        [ShowIf(nameof(ShowLocalBaseSettings))]
        [SerializeField]
        float _localBaseValue;

        [FoldoutGroup("$ScalarBindingGroupName")]
        [LabelText("Clamp")]
        [ShowIf(nameof(ShowClampSettings))]
        [SerializeField]
        ScalarClamp _clamp;

        [FoldoutGroup("$ScalarBindingSaveGroupName")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(ShowScalarSettings))]
        [SerializeField]
        bool _scalarSaveEnabled;

        [FoldoutGroup("$ScalarBindingSaveGroupName")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowScalarSaveLayer))]
        [SerializeField]
        SaveLayer _scalarSaveLayer;

        [FoldoutGroup("$BlackboardBindingGroupName")]
        [LabelText("Blackboard VarId")]
        [SerializeField, VarIdDropdown]
        int _blackboardKey = 0;

        [FoldoutGroup("$BlackboardBindingGroupName")]
        [LabelText("Policy")]
        [ShowIf(nameof(HasBlackboardKey))]
        [SerializeField]
        BlackboardBindPolicy _blackboardPolicy = BlackboardBindPolicy.Overwrite;

        [FoldoutGroup("$BlackboardBindingSaveGroupName")]
        [LabelText("Save Enabled")]
        [ShowIf(nameof(HasBlackboardKey))]
        [SerializeField]
        bool _blackboardSaveEnabled;

        [FoldoutGroup("$BlackboardBindingSaveGroupName")]
        [LabelText("Save Layer")]
        [ShowIf(nameof(ShowBlackboardSaveLayer))]
        [SerializeField]
        SaveLayer _blackboardSaveLayer;

        bool HasScalarKey => _scalarKey.Id != 0;
        bool HasBlackboardKey => _blackboardKey != 0;
        bool IsFloat => _kind == ProfileDynamicValueKind.Float;
        bool IsInt => _kind == ProfileDynamicValueKind.Int;
        bool IsBool => _kind == ProfileDynamicValueKind.Bool;
        bool IsString => _kind == ProfileDynamicValueKind.String;
        bool IsVector2 => _kind == ProfileDynamicValueKind.Vector2;
        bool IsVector3 => _kind == ProfileDynamicValueKind.Vector3;
        bool IsColor => _kind == ProfileDynamicValueKind.Color;
        bool IsUnityObject => _kind == ProfileDynamicValueKind.UnityObject;
        bool CanBindScalar => IsFloat;
        bool ShowScalarSettings => CanBindScalar && HasScalarKey;
        bool ShowRoundSettings => ShowScalarSettings && _useRoundMod;
        bool ShowClampSettings => ShowScalarSettings && _useClampMod;
        bool ShowLocalBaseSettings => ShowScalarSettings && _useLocalBase;
        bool ShowScalarSaveLayer => ShowScalarSettings && _scalarSaveEnabled;
        bool ShowBlackboardSaveLayer => HasBlackboardKey && _blackboardSaveEnabled;
        string ScalarBindingGroupName => $"Scalar Binding ({GetScalarBindingLabel()})";
        string ScalarBindingSaveGroupName => $"{ScalarBindingGroupName}/Save";
        string BlackboardBindingGroupName => $"Blackboard Binding ({GetBlackboardBindingLabel()})";
        string BlackboardBindingSaveGroupName => $"{BlackboardBindingGroupName}/Save";

        public string ProfileBindingListLabel => ProfileBindingInspectorLabelUtility.BuildLabel(
            nameof(ProfileDynamicValue),
            GetValueLabel(),
            CanBindScalar && HasScalarKey ? _scalarKey.Name : string.Empty,
            _blackboardKey);

        public override string ToString()
        {
            var valueLabel = GetValueLabel();
            var scalarLabel = CanBindScalar && HasScalarKey ? GetLeafLabel(_scalarKey.Name) : string.Empty;
            var blackboardLabel = HasBlackboardKey ? GetLeafLabel(GetBlackboardBindingLabel()) : string.Empty;

            if (!string.IsNullOrEmpty(scalarLabel) && !string.IsNullOrEmpty(blackboardLabel))
                return $"{_kind}:{valueLabel} [{scalarLabel}/{blackboardLabel}]";

            if (!string.IsNullOrEmpty(scalarLabel))
                return $"{_kind}:{valueLabel} [{scalarLabel}]";

            if (!string.IsNullOrEmpty(blackboardLabel))
                return $"{_kind}:{valueLabel} [{blackboardLabel}]";

            return $"{_kind}:{valueLabel}";
        }

        int IProfileValueBinding.BlackboardKey => _blackboardKey;
        ScalarKey IProfileValueBinding.ScalarKey => _scalarKey;
        BlackboardBindPolicy IProfileValueBinding.BlackboardPolicy => _blackboardPolicy;
        ScalarBindPolicy IProfileValueBinding.ScalarPolicy => _scalarPolicy;
        bool IProfileValueBinding.HasAnyBinding => HasScalarKey || HasBlackboardKey;

        bool IProfileValueBinding.ScalarSaveEnabled => _scalarSaveEnabled && HasScalarKey && _kind == ProfileDynamicValueKind.Float;
        SaveLayer IProfileValueBinding.ScalarSaveLayer => _scalarSaveLayer;
        bool IProfileValueBinding.BlackboardSaveEnabled => _blackboardSaveEnabled && HasBlackboardKey;
        SaveLayer IProfileValueBinding.BlackboardSaveLayer => _blackboardSaveLayer;

        void IProfileValueBinding.CollectSaveEntries(List<BindingSaveEntry> entries, string scopeIdentity, string profileTypeName)
        {
            if (string.IsNullOrEmpty(scopeIdentity))
                return;

            if (_scalarSaveEnabled && HasScalarKey && _kind == ProfileDynamicValueKind.Float)
            {
                entries.Add(BindingSaveEntry.ForScalar(_scalarKey.Name, _scalarSaveLayer, scopeIdentity, profileTypeName));
            }

            if (_blackboardSaveEnabled && HasBlackboardKey)
            {
                entries.Add(BindingSaveEntry.ForBlackboard(_blackboardKey, _blackboardSaveLayer, scopeIdentity, profileTypeName));
            }
        }

        void IProfileValueBinding.WriteToBlackboard(IBlackboardService blackboard)
        {
            if (!HasBlackboardKey || blackboard == null)
                return;

            var vars = blackboard.LocalVars;
            var varId = _blackboardKey;

            switch (_blackboardPolicy)
            {
                case BlackboardBindPolicy.Overwrite:
                    WriteToVars(vars, varId);
                    break;

                case BlackboardBindPolicy.SkipIfExists:
                case BlackboardBindPolicy.RespectExistingNoOverwrite:
                    if (!vars.Contains(varId))
                        WriteToVars(vars, varId);
                    break;
            }
        }

        void IProfileValueBinding.WriteToScalar(IBaseScalarService scalar)
        {
            if (!HasScalarKey || scalar == null)
                return;
            if (_kind != ProfileDynamicValueKind.Float)
                return;

            switch (_scalarPolicy)
            {
                case ScalarBindPolicy.UpdateBaseline:
                    if (scalar.TryGetRuntime(_scalarKey, out var runtime))
                    {
                        runtime.SetBaseline(_floatValue);
                        if (_useLocalBase)
                            runtime.SetLocalBase(_localBaseValue);
                    }
                    else
                    {
                        scalar.EnsureRuntime(_scalarKey, CreateRuntimeConfig());
                        if (_useLocalBase && scalar.TryGetRuntime(_scalarKey, out runtime))
                            runtime.SetLocalBase(_localBaseValue);
                    }
                    break;

                case ScalarBindPolicy.ReplaceRuntime:
                    scalar.EnsureRuntime(_scalarKey, CreateRuntimeConfig());
                    if (_useLocalBase && scalar.TryGetRuntime(_scalarKey, out var runtimeReplace))
                        runtimeReplace.SetLocalBase(_localBaseValue);
                    break;

                case ScalarBindPolicy.SkipIfExists:
                    if (!scalar.TryGetRuntime(_scalarKey, out _))
                        scalar.EnsureRuntime(_scalarKey, CreateRuntimeConfig());
                    if (_useLocalBase && scalar.TryGetRuntime(_scalarKey, out var runtimeSkip))
                        runtimeSkip.SetLocalBase(_localBaseValue);
                    break;
            }
        }

        void WriteToVars(IVarStore vars, int varId)
        {
            switch (_kind)
            {
                case ProfileDynamicValueKind.Float:
                    vars.TrySetVariant(varId, DynamicVariant.FromFloat(_floatValue));
                    break;
                case ProfileDynamicValueKind.Int:
                    vars.TrySetVariant(varId, DynamicVariant.FromInt(_intValue));
                    break;
                case ProfileDynamicValueKind.Bool:
                    vars.TrySetVariant(varId, DynamicVariant.FromBool(_boolValue));
                    break;
                case ProfileDynamicValueKind.String:
                    vars.TrySetVariant(varId, DynamicVariant.FromString(_stringValue ?? string.Empty));
                    break;
                case ProfileDynamicValueKind.Vector2:
                    vars.TrySetVariant(varId, DynamicVariant.FromVector2(_vector2Value));
                    break;
                case ProfileDynamicValueKind.Vector3:
                    vars.TrySetVariant(varId, DynamicVariant.FromVector3(_vector3Value));
                    break;
                case ProfileDynamicValueKind.Color:
                    vars.TrySetVariant(varId, DynamicVariant.FromColor(_colorValue));
                    break;
                case ProfileDynamicValueKind.UnityObject:
                    vars.TrySetManagedRef(varId, _unityObjectValue);
                    break;
            }
        }

        ScalarRuntimeConfig CreateRuntimeConfig()
        {
            var useClamp = _useClampMod;
            var clamp = _clamp;

            if (useClamp && !clamp.TryCreateLiteralClamp(out clamp))
            {
                Debug.LogError($"[CustomProfileDefinition] SCALAR_CLAMP_DYNAMIC_UNSUPPORTED key={_scalarKey.Id} name={_scalarKey.Name ?? string.Empty}");
                useClamp = false;
                clamp = default;
            }

            return new ScalarRuntimeConfig
            {
                BaseValue = _floatValue,
                UseEffectMod = _useEffectMod,
                UseRoundMod = _useRoundMod,
                RoundDigits = Mathf.Clamp(_roundDigits, 0, 6),
                UseClampMod = useClamp,
                Clamp = clamp
            };
        }

        string GetScalarBindingLabel()
        {
            if (!CanBindScalar)
                return "Unavailable";

            if (!HasScalarKey)
                return "Unbound";

            return _scalarKey.FormatLabel(includeId: false);
        }

        string GetBlackboardBindingLabel()
        {
            if (!HasBlackboardKey)
                return "Unbound";

            if (VarIdResolver.TryGetStableKey(_blackboardKey, out var stableKey) && !string.IsNullOrEmpty(stableKey))
                return stableKey;

            return $"varId:{_blackboardKey}";
        }

        string GetValueLabel()
        {
            return _kind switch
            {
                ProfileDynamicValueKind.Float => _floatValue.ToString("0.###"),
                ProfileDynamicValueKind.Int => _intValue.ToString(),
                ProfileDynamicValueKind.Bool => _boolValue ? "true" : "false",
                ProfileDynamicValueKind.String => string.IsNullOrEmpty(_stringValue) ? "\"\"" : $"\"{_stringValue}\"",
                ProfileDynamicValueKind.Vector2 => _vector2Value.ToString(),
                ProfileDynamicValueKind.Vector3 => _vector3Value.ToString(),
                ProfileDynamicValueKind.Color => _colorValue.ToString(),
                ProfileDynamicValueKind.UnityObject => _unityObjectValue != null ? _unityObjectValue.name : "null",
                _ => "<none>"
            };
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
    }
}
