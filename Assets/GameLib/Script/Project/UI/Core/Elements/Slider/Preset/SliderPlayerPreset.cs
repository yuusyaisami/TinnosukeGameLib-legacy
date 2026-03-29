#nullable enable
using System.Collections.Generic;
using DG.Tweening;
using Game.Commands.VNext;
using Game.Common;
using Game.Scalar;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [System.Serializable]
    public sealed class SliderTransitionSettings
    {
        [MinValue(0f)]
        [LabelText("Delay Seconds")]
        [SerializeField]
        float _delaySeconds;

        [MinValue(0f)]
        [LabelText("Duration Seconds")]
        [SerializeField]
        float _durationSeconds = 0.15f;

        [LabelText("Ease")]
        [SerializeField]
        Ease _ease = Ease.Linear;

        public float DelaySeconds => Mathf.Max(0f, _delaySeconds);
        public float DurationSeconds => Mathf.Max(0f, _durationSeconds);
        public Ease Ease => _ease;

        internal SliderTransitionSettings CreateRuntimeCopy()
        {
            return new SliderTransitionSettings
            {
                _delaySeconds = _delaySeconds,
                _durationSeconds = _durationSeconds,
                _ease = _ease,
            };
        }

        internal void CopyFrom(SliderTransitionSettings? other)
        {
            if (other == null)
                return;

            _delaySeconds = other._delaySeconds;
            _durationSeconds = other._durationSeconds;
            _ease = other._ease;
        }
    }

    [System.Serializable]
    public sealed class SliderPlayerBindingEntry
    {
        [BoxGroup("Entry")]
        [LabelText("Condition")]
        [SerializeField]
        DynamicValue<bool> _condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Entry")]
        [LabelText("Order")]
        [SerializeField]
        int _order;

        [BoxGroup("Commands")]
        [LabelText("On True")]
        [SerializeField]
        [CommandListFunctionName("Slider.Player.Binding.OnTrue")]
        CommandListData _onConditionBecameTrueCommands = new();

        [BoxGroup("Commands")]
        [LabelText("On False")]
        [SerializeField]
        [CommandListFunctionName("Slider.Player.Binding.OnFalse")]
        CommandListData _onConditionBecameFalseCommands = new();

        [BoxGroup("Binding")]
        [LabelText("Use Scalar Binding")]
        [SerializeField]
        bool _useScalarBinding = true;

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useScalarBinding))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Scalar Source\", _scalarBindingSource)")]
        [SerializeField]
        ActorSource _scalarBindingSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useScalarBinding))]
        [LabelText("Scalar Key")]
        [SerializeField]
        ScalarKey _scalarKey;

        [BoxGroup("Binding")]
        [LabelText("Use Blackboard Binding")]
        [SerializeField]
        bool _useBlackboardBinding;

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useBlackboardBinding))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Blackboard Source\", _blackboardBindingSource)")]
        [SerializeField]
        ActorSource _blackboardBindingSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useBlackboardBinding))]
        [LabelText("Blackboard Key")]
        [SerializeField]
        VarKeyRef _blackboardKey;

        [BoxGroup("Binding")]
        [LabelText("Binding Priority")]
        [SerializeField]
        SliderBindingPriority _bindingPriority = SliderBindingPriority.Scalar;

        public DynamicValue<bool> Condition => _condition;
        public int Order => _order;
        public CommandListData OnConditionBecameTrueCommands => _onConditionBecameTrueCommands;
        public CommandListData OnConditionBecameFalseCommands => _onConditionBecameFalseCommands;
        public bool UseScalarBinding => _useScalarBinding;
        public ActorSource ScalarBindingSource => _scalarBindingSource;
        public ScalarKey ScalarKey => _scalarKey;
        public bool UseBlackboardBinding => _useBlackboardBinding;
        public ActorSource BlackboardBindingSource => _blackboardBindingSource;
        public VarKeyRef BlackboardKey => _blackboardKey;
        public SliderBindingPriority BindingPriority => _bindingPriority;

        internal bool EvaluateCondition(IDynamicContext? context)
        {
            if (context != null)
                return _condition.GetOrDefault(context, true);

            return _condition.GetOrDefaultWithoutContext(true);
        }

        internal SliderPlayerBindingEntry CreateRuntimeCopy()
        {
            return new SliderPlayerBindingEntry
            {
                _condition = _condition,
                _order = _order,
                _onConditionBecameTrueCommands = SliderPlayerPreset.CloneCommandList(_onConditionBecameTrueCommands),
                _onConditionBecameFalseCommands = SliderPlayerPreset.CloneCommandList(_onConditionBecameFalseCommands),
                _useScalarBinding = _useScalarBinding,
                _scalarBindingSource = _scalarBindingSource,
                _scalarKey = _scalarKey,
                _useBlackboardBinding = _useBlackboardBinding,
                _blackboardBindingSource = _blackboardBindingSource,
                _blackboardKey = _blackboardKey,
                _bindingPriority = _bindingPriority,
            };
        }

        internal void BindDebugOwner(UnityEngine.Object owner, string prefix)
        {
            _onConditionBecameTrueCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onConditionBecameTrueCommands)}");
            _onConditionBecameFalseCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onConditionBecameFalseCommands)}");
        }
    }

    [System.Serializable]
    public sealed class SliderUserInputSettings
    {
        [BoxGroup("Input")]
        [LabelText("Enabled")]
        [SerializeField]
        bool _enabled;

        [BoxGroup("Input UI")]
        [ShowIf(nameof(_enabled))]
        [LabelText("UI Input Mode")]
        [SerializeField]
        SliderUIInputMode _uiInputMode = SliderUIInputMode.PointerCapture;

        [BoxGroup("Input World")]
        [ShowIf(nameof(_enabled))]
        [LabelText("World Trigger Button")]
        [SerializeField]
        SliderWorldTriggerButton _worldTriggerButton = SliderWorldTriggerButton.Left;

        [BoxGroup("Input Repeat")]
        [ShowIf(nameof(_enabled))]
        [MinValue(0f)]
        [LabelText("Navigate Repeat Delay")]
        [SerializeField]
        float _navigateRepeatDelay = 0.25f;

        [BoxGroup("Input Repeat")]
        [ShowIf(nameof(_enabled))]
        [MinValue(0.001f)]
        [LabelText("Navigate Repeat Interval")]
        [SerializeField]
        float _navigateRepeatInterval = 0.08f;

        [BoxGroup("Input Repeat")]
        [ShowIf(nameof(_enabled))]
        [MinValue(0f)]
        [LabelText("Scroll Repeat Delay")]
        [SerializeField]
        float _scrollRepeatDelay = 0.1f;

        [BoxGroup("Input Repeat")]
        [ShowIf(nameof(_enabled))]
        [MinValue(0.001f)]
        [LabelText("Scroll Repeat Interval")]
        [SerializeField]
        float _scrollRepeatInterval = 0.05f;

        [BoxGroup("Input Pointer")]
        [ShowIf(nameof(_enabled))]
        [MinValue(0f)]
        [LabelText("Padding Start")]
        [SerializeField]
        float _paddingStart;

        [BoxGroup("Input Pointer")]
        [ShowIf(nameof(_enabled))]
        [MinValue(0f)]
        [LabelText("Padding End")]
        [SerializeField]
        float _paddingEnd;

        public bool Enabled => _enabled;
        public SliderUIInputMode UIInputMode => _uiInputMode;
        public SliderWorldTriggerButton WorldTriggerButton => _worldTriggerButton;
        public float NavigateRepeatDelay => Mathf.Max(0f, _navigateRepeatDelay);
        public float NavigateRepeatInterval => Mathf.Max(0.001f, _navigateRepeatInterval);
        public float ScrollRepeatDelay => Mathf.Max(0f, _scrollRepeatDelay);
        public float ScrollRepeatInterval => Mathf.Max(0.001f, _scrollRepeatInterval);
        public float PaddingStart => Mathf.Max(0f, _paddingStart);
        public float PaddingEnd => Mathf.Max(0f, _paddingEnd);

        internal SliderUserInputSettings CreateRuntimeCopy()
        {
            return new SliderUserInputSettings
            {
                _enabled = _enabled,
                _uiInputMode = _uiInputMode,
                _worldTriggerButton = _worldTriggerButton,
                _navigateRepeatDelay = _navigateRepeatDelay,
                _navigateRepeatInterval = _navigateRepeatInterval,
                _scrollRepeatDelay = _scrollRepeatDelay,
                _scrollRepeatInterval = _scrollRepeatInterval,
                _paddingStart = _paddingStart,
                _paddingEnd = _paddingEnd,
            };
        }

        internal void CopyFrom(SliderUserInputSettings? other)
        {
            if (other == null)
                return;

            _enabled = other._enabled;
            _uiInputMode = other._uiInputMode;
            _worldTriggerButton = other._worldTriggerButton;
            _navigateRepeatDelay = other._navigateRepeatDelay;
            _navigateRepeatInterval = other._navigateRepeatInterval;
            _scrollRepeatDelay = other._scrollRepeatDelay;
            _scrollRepeatInterval = other._scrollRepeatInterval;
            _paddingStart = other._paddingStart;
            _paddingEnd = other._paddingEnd;
        }
    }

    [System.Serializable]
    public sealed class SliderPlayerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Binding")]
        [LabelText("Binding Entries")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<SliderPlayerBindingEntry> _bindingEntries = new() { new() };

        [BoxGroup("Range")]
        [LabelText("Min Value")]
        [SerializeField]
        DynamicValue<float> _minValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Range")]
        [LabelText("Max Value")]
        [SerializeField]
        DynamicValue<float> _maxValue = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Range")]
        [LabelText("Initial Value")]
        [SerializeField]
        DynamicValue<float> _initialValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Transition")]
        [LabelText("Increase Transition")]
        [InlineProperty]
        [SerializeField]
        SliderTransitionSettings _increaseTransition = new();

        [BoxGroup("Transition")]
        [LabelText("Decrease Transition")]
        [InlineProperty]
        [SerializeField]
        SliderTransitionSettings _decreaseTransition = new();

        [BoxGroup("Display")]
        [LabelText("Segment Display Mode")]
        [SerializeField]
        SliderSegmentDisplayMode _segmentDisplayMode = SliderSegmentDisplayMode.Continuous;

        [BoxGroup("Input")]
        [InlineProperty]
        [SerializeField]
        SliderUserInputSettings _userInput = new();

        [BoxGroup("Commands")]
        [LabelText("On Target Value Changed")]
        [SerializeField]
        [CommandListFunctionName("Slider.Player.OnTargetChanged")]
        CommandListData _onTargetValueChangedCommands = new();

        public IReadOnlyList<SliderPlayerBindingEntry> BindingEntries => _bindingEntries;
        public DynamicValue<float> MinValue => _minValue;
        public DynamicValue<float> MaxValue => _maxValue;
        public DynamicValue<float> InitialValue => _initialValue;
        public SliderTransitionSettings IncreaseTransition => _increaseTransition;
        public SliderTransitionSettings DecreaseTransition => _decreaseTransition;
        public SliderSegmentDisplayMode SegmentDisplayMode => _segmentDisplayMode;
        public SliderUserInputSettings UserInput => _userInput;
        public CommandListData OnTargetValueChangedCommands => _onTargetValueChangedCommands;

        internal SliderPlayerPreset CreateRuntimeCopy()
        {
            var bindingEntries = new List<SliderPlayerBindingEntry>(_bindingEntries.Count);
            for (var i = 0; i < _bindingEntries.Count; i++)
            {
                var entry = _bindingEntries[i];
                bindingEntries.Add(entry?.CreateRuntimeCopy() ?? new SliderPlayerBindingEntry());
            }

            return new SliderPlayerPreset
            {
                _bindingEntries = bindingEntries,
                _minValue = _minValue,
                _maxValue = _maxValue,
                _initialValue = _initialValue,
                _increaseTransition = _increaseTransition?.CreateRuntimeCopy() ?? new SliderTransitionSettings(),
                _decreaseTransition = _decreaseTransition?.CreateRuntimeCopy() ?? new SliderTransitionSettings(),
                _segmentDisplayMode = _segmentDisplayMode,
                _userInput = _userInput?.CreateRuntimeCopy() ?? new SliderUserInputSettings(),
                _onTargetValueChangedCommands = CloneCommandList(_onTargetValueChangedCommands),
            };
        }

        internal void ApplyMutation(
            SliderPlayerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyBinding)
            {
                _bindingEntries = new List<SliderPlayerBindingEntry>(mutation.BindingEntries.Count);
                for (var i = 0; i < mutation.BindingEntries.Count; i++)
                {
                    var entry = mutation.BindingEntries[i];
                    _bindingEntries.Add(entry?.CreateRuntimeCopy() ?? new SliderPlayerBindingEntry());
                }

                if (_bindingEntries.Count == 0)
                    _bindingEntries.Add(new SliderPlayerBindingEntry());
            }

            if (mutation.ApplyRange)
            {
                _minValue = mutation.MinValue;
                _maxValue = mutation.MaxValue;
                _initialValue = mutation.InitialValue;
            }

            if (mutation.ApplyIncreaseTransition)
            {
                _increaseTransition ??= new SliderTransitionSettings();
                _increaseTransition.CopyFrom(mutation.IncreaseTransition);
            }

            if (mutation.ApplyDecreaseTransition)
            {
                _decreaseTransition ??= new SliderTransitionSettings();
                _decreaseTransition.CopyFrom(mutation.DecreaseTransition);
            }

            if (mutation.ApplySegmentDisplayMode)
                _segmentDisplayMode = mutation.SegmentDisplayMode;

            if (mutation.ApplyUserInput)
            {
                _userInput ??= new SliderUserInputSettings();
                _userInput.CopyFrom(mutation.UserInput);
            }

            if (mutation.ApplyTargetChangedCommands)
            {
                _onTargetValueChangedCommands ??= new CommandListData();
                _onTargetValueChangedCommands.ApplyRuntimeMutation(mutation.TargetChangedCommands, mutationService);
            }
        }

        internal static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }

        internal void BindDebugOwner(UnityEngine.Object owner, string prefix)
        {
            _onTargetValueChangedCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onTargetValueChangedCommands)}");
            if (_bindingEntries == null)
                return;

            for (var i = 0; i < _bindingEntries.Count; i++)
                _bindingEntries[i]?.BindDebugOwner(owner, $"{prefix}.{nameof(_bindingEntries)}[{i}]");
        }
    }

    [CreateAssetMenu(
        menuName = "Game/UI/Slider/Player Preset",
        fileName = "SliderPlayerPreset")]
    public sealed class SliderPlayerPresetSO : ScriptableObject, IDynamicValueAsset<SliderPlayerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        SliderPlayerPreset? _preset = new();

        public SliderPlayerPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable()
        {
            EnsurePreset();
            BindDebugOwner();
        }

        void OnValidate()
        {
            EnsurePreset();
            BindDebugOwner();
        }

        void EnsurePreset()
        {
            _preset ??= new SliderPlayerPreset();
        }

        void BindDebugOwner()
        {
            _preset?.BindDebugOwner(this, nameof(_preset));
        }
    }

    [System.Serializable]
    public sealed class SliderPlayerRuntimeMutation
    {
        [BoxGroup("Binding")]
        [ToggleLeft]
        [LabelText("Apply Binding")]
        public bool ApplyBinding;

        [BoxGroup("Binding")]
        [ShowIf(nameof(ApplyBinding))]
        [LabelText("Binding Entries")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        public List<SliderPlayerBindingEntry> BindingEntries = new() { new() };

        [BoxGroup("Range")]
        [ToggleLeft]
        [LabelText("Apply Range")]
        public bool ApplyRange;

        [BoxGroup("Range")]
        [ShowIf(nameof(ApplyRange))]
        [LabelText("Min Value")]
        public DynamicValue<float> MinValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Range")]
        [ShowIf(nameof(ApplyRange))]
        [LabelText("Max Value")]
        public DynamicValue<float> MaxValue = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Range")]
        [ShowIf(nameof(ApplyRange))]
        [LabelText("Initial Value")]
        public DynamicValue<float> InitialValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Transition")]
        [ToggleLeft]
        [LabelText("Apply Increase Transition")]
        public bool ApplyIncreaseTransition;

        [BoxGroup("Transition")]
        [ShowIf(nameof(ApplyIncreaseTransition))]
        [InlineProperty]
        [LabelText("Increase Transition")]
        public SliderTransitionSettings IncreaseTransition = new();

        [BoxGroup("Transition")]
        [ToggleLeft]
        [LabelText("Apply Decrease Transition")]
        public bool ApplyDecreaseTransition;

        [BoxGroup("Transition")]
        [ShowIf(nameof(ApplyDecreaseTransition))]
        [InlineProperty]
        [LabelText("Decrease Transition")]
        public SliderTransitionSettings DecreaseTransition = new();

        [BoxGroup("Display")]
        [ToggleLeft]
        [LabelText("Apply Segment Display Mode")]
        public bool ApplySegmentDisplayMode;

        [BoxGroup("Display")]
        [ShowIf(nameof(ApplySegmentDisplayMode))]
        [LabelText("Segment Display Mode")]
        public SliderSegmentDisplayMode SegmentDisplayMode = SliderSegmentDisplayMode.Continuous;

        [BoxGroup("Input")]
        [ToggleLeft]
        [LabelText("Apply User Input")]
        public bool ApplyUserInput;

        [BoxGroup("Input")]
        [ShowIf(nameof(ApplyUserInput))]
        [InlineProperty]
        [LabelText("User Input")]
        public SliderUserInputSettings UserInput = new();

        [BoxGroup("Commands")]
        [ToggleLeft]
        [LabelText("Apply Target Changed Commands")]
        public bool ApplyTargetChangedCommands;

        [BoxGroup("Commands")]
        [ShowIf(nameof(ApplyTargetChangedCommands))]
        [InlineProperty]
        [HideLabel]
        public CommandListMutationStep TargetChangedCommands = new()
        {
            Operation = CommandListMutationOperation.Override,
        };

        public bool HasAnyMutation()
        {
            return ApplyBinding ||
                   ApplyRange ||
                   ApplyIncreaseTransition ||
                   ApplyDecreaseTransition ||
                   ApplySegmentDisplayMode ||
                   ApplyUserInput ||
                   ApplyTargetChangedCommands;
        }
    }
}
