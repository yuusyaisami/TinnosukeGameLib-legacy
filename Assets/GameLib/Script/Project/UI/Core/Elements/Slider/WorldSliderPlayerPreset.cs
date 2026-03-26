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
    public sealed class WorldSliderTransitionSettings
    {
        [MinValue(0f)]
        [LabelText("Delay Seconds")]
        [Tooltip("値更新を受けてから easing を開始するまでの待機時間です。")]
        [SerializeField]
        float _delaySeconds;

        [MinValue(0f)]
        [LabelText("Duration Seconds")]
        [Tooltip("表示値が現在値から目標値へ移動する時間です。0 の場合は即時反映されます。")]
        [SerializeField]
        float _durationSeconds = 0.15f;

        [LabelText("Ease")]
        [Tooltip("表示値が目標値へ向かうときの easing 種別です。")]
        [SerializeField]
        Ease _ease = Ease.Linear;

        public float DelaySeconds => Mathf.Max(0f, _delaySeconds);
        public float DurationSeconds => Mathf.Max(0f, _durationSeconds);
        public Ease Ease => _ease;

        internal WorldSliderTransitionSettings CreateRuntimeCopy()
        {
            return new WorldSliderTransitionSettings
            {
                _delaySeconds = _delaySeconds,
                _durationSeconds = _durationSeconds,
                _ease = _ease,
            };
        }

        internal void CopyFrom(WorldSliderTransitionSettings other)
        {
            if (other == null)
                return;

            _delaySeconds = other._delaySeconds;
            _durationSeconds = other._durationSeconds;
            _ease = other._ease;
        }
    }

    [System.Serializable]
    public sealed class WorldSliderPlayerBindingEntry
    {
        [BoxGroup("Entry")]
        [LabelText("Condition")]
        [Tooltip("true のときだけこの binding entry を採用候補にします。すべて false なら slider は非表示になります。")]
        [SerializeField]
        DynamicValue<bool> _condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("Entry")]
        [LabelText("Order")]
        [Tooltip("Condition を満たす entry が複数ある場合の優先度です。高いほど優先されます。")]
        [SerializeField]
        int _order;

        [BoxGroup("Binding")]
        [LabelText("Use Scalar Binding")]
        [Tooltip("ScalarService から値を読む場合に有効にします。")]
        [SerializeField]
        bool _useScalarBinding = true;

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useScalarBinding))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Scalar Source\", _scalarBindingSource)")]
        [Tooltip("ScalarService を解決する actor / scope です。")]
        [SerializeField]
        ActorSource _scalarBindingSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useScalarBinding))]
        [LabelText("Scalar Key")]
        [Tooltip("監視する scalar key です。")]
        [SerializeField]
        ScalarKey _scalarKey;

        [BoxGroup("Binding")]
        [LabelText("Use Blackboard Binding")]
        [Tooltip("Blackboard から値を読む場合に有効にします。")]
        [SerializeField]
        bool _useBlackboardBinding;

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useBlackboardBinding))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Blackboard Source\", _blackboardBindingSource)")]
        [Tooltip("BlackboardService を解決する actor / scope です。")]
        [SerializeField]
        ActorSource _blackboardBindingSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Binding")]
        [ShowIf(nameof(_useBlackboardBinding))]
        [LabelText("Blackboard Key")]
        [Tooltip("監視する blackboard var key です。")]
        [SerializeField]
        VarKeyRef _blackboardKey;

        [BoxGroup("Binding")]
        [LabelText("Binding Priority")]
        [Tooltip("Scalar と Blackboard の両方が使えるとき、どちらを優先して target 値に採用するかを決めます。")]
        [SerializeField]
        WorldSliderBindingPriority _bindingPriority = WorldSliderBindingPriority.Scalar;

        public DynamicValue<bool> Condition => _condition;
        public int Order => _order;
        public bool UseScalarBinding => _useScalarBinding;
        public ActorSource ScalarBindingSource => _scalarBindingSource;
        public ScalarKey ScalarKey => _scalarKey;
        public bool UseBlackboardBinding => _useBlackboardBinding;
        public ActorSource BlackboardBindingSource => _blackboardBindingSource;
        public VarKeyRef BlackboardKey => _blackboardKey;
        public WorldSliderBindingPriority BindingPriority => _bindingPriority;

        internal bool EvaluateCondition(IDynamicContext? context)
        {
            if (context != null)
                return _condition.GetOrDefault(context, true);

            return _condition.GetOrDefaultWithoutContext(true);
        }

        internal WorldSliderPlayerBindingEntry CreateRuntimeCopy()
        {
            return new WorldSliderPlayerBindingEntry
            {
                _condition = _condition,
                _order = _order,
                _useScalarBinding = _useScalarBinding,
                _scalarBindingSource = _scalarBindingSource,
                _scalarKey = _scalarKey,
                _useBlackboardBinding = _useBlackboardBinding,
                _blackboardBindingSource = _blackboardBindingSource,
                _blackboardKey = _blackboardKey,
                _bindingPriority = _bindingPriority,
            };
        }
    }

    [System.Serializable]
    public sealed class WorldSliderPlayerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Binding")]
        [LabelText("Binding Entries")]
        [Tooltip("Condition=true の entry を候補にし、Order が最も高いものを採用します。候補が 1 件も無い場合は slider を非表示にします。")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<WorldSliderPlayerBindingEntry> _bindingEntries = new() { new() };

        [BoxGroup("Range")]
        [LabelText("Min Value")]
        [Tooltip("Slider の生値の最小値です。normalized 値の 0 に対応します。")]
        [SerializeField]
        DynamicValue<float> _minValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Range")]
        [LabelText("Max Value")]
        [Tooltip("Slider の生値の最大値です。normalized 値の 1 に対応します。")]
        [SerializeField]
        DynamicValue<float> _maxValue = DynamicValueExtensions.FromLiteral(1f);

        [BoxGroup("Range")]
        [LabelText("Initial Value")]
        [Tooltip("binding が取れない場合の初期生値です。")]
        [SerializeField]
        DynamicValue<float> _initialValue = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Transition")]
        [LabelText("Increase Transition")]
        [Tooltip("値が増加したときに使う delay / duration / ease 設定です。")]
        [InlineProperty]
        [SerializeField]
        WorldSliderTransitionSettings _increaseTransition = new();

        [BoxGroup("Transition")]
        [LabelText("Decrease Transition")]
        [Tooltip("値が減少したときに使う delay / duration / ease 設定です。")]
        [InlineProperty]
        [SerializeField]
        WorldSliderTransitionSettings _decreaseTransition = new();

        [BoxGroup("Display")]
        [LabelText("Segment Display Mode")]
        [Tooltip("Segmented 表示時に displayed 値を continuous のまま使うか、到達済み段で止めるかを決めます。")]
        [SerializeField]
        WorldSliderSegmentDisplayMode _segmentDisplayMode = WorldSliderSegmentDisplayMode.Continuous;

        [BoxGroup("Commands")]
        [LabelText("On Target Value Changed")]
        [Tooltip("target 値が変わったときだけ実行される command list です。displayed の easing 更新ごとには実行されません。")]
        [SerializeField]
        [CommandListFunctionName("WorldSlider.Player.OnTargetChanged")]
        CommandListData _onTargetValueChangedCommands = new();

        public IReadOnlyList<WorldSliderPlayerBindingEntry> BindingEntries => _bindingEntries;
        public DynamicValue<float> MinValue => _minValue;
        public DynamicValue<float> MaxValue => _maxValue;
        public DynamicValue<float> InitialValue => _initialValue;
        public WorldSliderTransitionSettings IncreaseTransition => _increaseTransition;
        public WorldSliderTransitionSettings DecreaseTransition => _decreaseTransition;
        public WorldSliderSegmentDisplayMode SegmentDisplayMode => _segmentDisplayMode;
        public CommandListData OnTargetValueChangedCommands => _onTargetValueChangedCommands;

        internal WorldSliderPlayerPreset CreateRuntimeCopy()
        {
            var bindingEntries = new List<WorldSliderPlayerBindingEntry>(_bindingEntries.Count);
            for (int i = 0; i < _bindingEntries.Count; i++)
            {
                var entry = _bindingEntries[i];
                bindingEntries.Add(entry?.CreateRuntimeCopy() ?? new WorldSliderPlayerBindingEntry());
            }

            return new WorldSliderPlayerPreset
            {
                _bindingEntries = bindingEntries,
                _minValue = _minValue,
                _maxValue = _maxValue,
                _initialValue = _initialValue,
                _increaseTransition = _increaseTransition?.CreateRuntimeCopy() ?? new WorldSliderTransitionSettings(),
                _decreaseTransition = _decreaseTransition?.CreateRuntimeCopy() ?? new WorldSliderTransitionSettings(),
                _segmentDisplayMode = _segmentDisplayMode,
                _onTargetValueChangedCommands = CloneCommandList(_onTargetValueChangedCommands),
            };
        }

        internal void ApplyMutation(
            WorldSliderPlayerRuntimeMutation mutation,
            ICommandListRuntimeMutationService? mutationService)
        {
            if (mutation == null)
                return;

            if (mutation.ApplyBinding)
            {
                _bindingEntries = new List<WorldSliderPlayerBindingEntry>(mutation.BindingEntries.Count);
                for (int i = 0; i < mutation.BindingEntries.Count; i++)
                {
                    var entry = mutation.BindingEntries[i];
                    _bindingEntries.Add(entry?.CreateRuntimeCopy() ?? new WorldSliderPlayerBindingEntry());
                }

                if (_bindingEntries.Count == 0)
                    _bindingEntries.Add(new WorldSliderPlayerBindingEntry());
            }

            if (mutation.ApplyRange)
            {
                _minValue = mutation.MinValue;
                _maxValue = mutation.MaxValue;
                _initialValue = mutation.InitialValue;
            }

            if (mutation.ApplyIncreaseTransition)
            {
                _increaseTransition ??= new WorldSliderTransitionSettings();
                _increaseTransition.CopyFrom(mutation.IncreaseTransition);
            }

            if (mutation.ApplyDecreaseTransition)
            {
                _decreaseTransition ??= new WorldSliderTransitionSettings();
                _decreaseTransition.CopyFrom(mutation.DecreaseTransition);
            }

            if (mutation.ApplySegmentDisplayMode)
                _segmentDisplayMode = mutation.SegmentDisplayMode;

            if (mutation.ApplyTargetChangedCommands)
            {
                _onTargetValueChangedCommands ??= new CommandListData();
                _onTargetValueChangedCommands.ApplyRuntimeMutation(mutation.TargetChangedCommands, mutationService);
            }
        }

        internal void BindDebugOwners(Object owner, string prefix)
        {
            _onTargetValueChangedCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_onTargetValueChangedCommands)}");
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [CreateAssetMenu(
        menuName = "Game/UI/World Slider/Player Preset",
        fileName = "WorldSliderPlayerPreset")]
    public sealed class WorldSliderPlayerPresetSO : ScriptableObject, IDynamicValueAsset<WorldSliderPlayerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        WorldSliderPlayerPreset? _preset = new();

        public WorldSliderPlayerPreset? Preset
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
            BindDebugOwners();
        }

        void OnValidate()
        {
            EnsurePreset();
            BindDebugOwners();
        }

        void EnsurePreset()
        {
            if (_preset == null)
                _preset = new WorldSliderPlayerPreset();
        }

        void BindDebugOwners()
        {
            _preset?.BindDebugOwners(this, nameof(_preset));
        }
    }
}
