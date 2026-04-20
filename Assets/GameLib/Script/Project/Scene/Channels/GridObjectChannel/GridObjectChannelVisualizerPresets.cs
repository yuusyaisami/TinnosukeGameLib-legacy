#nullable enable
using System;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.UI;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Channel
{
    [Serializable]
    public sealed class GridObjectChannelVisualizerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Visual")]
        [LabelText("Runtime Template")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplatePreset;

        [BoxGroup("Visual")]
        [LabelText("Allow Pooling")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _allowPooling = true;

        [BoxGroup("Visual")]
        [LabelText("Size Source")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelVisualizerSizeSource _sizeSource = GridObjectChannelVisualizerSizeSource.VisualBounds;

        [BoxGroup("Visual")]
        [ShowIf(nameof(UsesFixedSize))]
        [LabelText("Fixed Size")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        Vector2 _fixedSize = new(100f, 100f);

        [BoxGroup("Visual")]
        [LabelText("Delay Between Spawns")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<float> _delayBetweenSpawns = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Commands")]
        [LabelText("Spawn Commands")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        [CommandListFunctionName("GridObjectChannel.Item.OnSpawn")]
        CommandListData _spawnCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Counter Var")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        VarKeyRef _counterVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [BoxGroup("Commands")]
        [LabelText("Write Spawner To Context")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _writeSpawnerToContext;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_writeSpawnerToContext))]
        [LabelText("Spawner Context Slot")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        CommandLtsSlot _spawnerContextSlot = CommandLtsSlot.ContextA;

        [BoxGroup("Choice")]
        [LabelText("Enable Choice Input")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _enableChoiceInput;

        [BoxGroup("Choice")]
        [ShowIf(nameof(_enableChoiceInput))]
        [LabelText("Choice Button Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _choiceButtonChannelTag = "default";

        [BoxGroup("Choice")]
        [ShowIf(nameof(_enableChoiceInput))]
        [LabelText("Decision Phase")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChoiceDecisionPhase _choiceDecisionPhase = GridObjectChoiceDecisionPhase.CompletedWaitingRelease;

        [BoxGroup("Choice")]
        [ShowIf(nameof(_enableChoiceInput))]
        [LabelText("Require Phase Transition")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool _choiceRequirePhaseTransition = true;

        bool UsesFixedSize() => _sizeSource == GridObjectChannelVisualizerSizeSource.Fixed;

        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset => _runtimeTemplatePreset;
        public bool AllowPooling => _allowPooling;
        public GridObjectChannelVisualizerSizeSource SizeSource => _sizeSource;
        public Vector2 FixedSize => new(Mathf.Max(0f, _fixedSize.x), Mathf.Max(0f, _fixedSize.y));
        public DynamicValue<float> DelayBetweenSpawns => _delayBetweenSpawns;
        public CommandListData SpawnCommands => _spawnCommands;
        public VarKeyRef CounterVar => _counterVar;
        public bool WriteSpawnerToContext => _writeSpawnerToContext;
        public CommandLtsSlot SpawnerContextSlot => _spawnerContextSlot;
        public bool EnableChoiceInput => _enableChoiceInput;
        public string ChoiceButtonChannelTag => string.IsNullOrWhiteSpace(_choiceButtonChannelTag) ? "default" : _choiceButtonChannelTag.Trim();
        public GridObjectChoiceDecisionPhase ChoiceDecisionPhase => _choiceDecisionPhase;
        public bool ChoiceRequirePhaseTransition => _choiceRequirePhaseTransition;

        public bool IsChoiceDecisionPhase(ButtonChannelPhase phase)
        {
            return _choiceDecisionPhase switch
            {
                GridObjectChoiceDecisionPhase.AnyDecision => phase == ButtonChannelPhase.CompletedWaitingRelease ||
                                                            phase == ButtonChannelPhase.Short ||
                                                            phase == ButtonChannelPhase.Long ||
                                                            phase == ButtonChannelPhase.LongMax ||
                                                            phase == ButtonChannelPhase.HoldReached,
                GridObjectChoiceDecisionPhase.CompletedWaitingRelease => phase == ButtonChannelPhase.CompletedWaitingRelease,
                GridObjectChoiceDecisionPhase.Short => phase == ButtonChannelPhase.Short,
                GridObjectChoiceDecisionPhase.Long => phase == ButtonChannelPhase.Long,
                GridObjectChoiceDecisionPhase.LongMax => phase == ButtonChannelPhase.LongMax,
                GridObjectChoiceDecisionPhase.HoldReached => phase == ButtonChannelPhase.HoldReached,
                GridObjectChoiceDecisionPhase.Pressed => phase == ButtonChannelPhase.Pressed,
                _ => false,
            };
        }

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!_runtimeTemplatePreset.TryGet(context, out BaseRuntimeTemplatePreset? preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

        public GridObjectChannelVisualizerPreset CreateRuntimeCopy()
        {
            return new GridObjectChannelVisualizerPreset
            {
                _runtimeTemplatePreset = _runtimeTemplatePreset,
                _allowPooling = _allowPooling,
                _sizeSource = _sizeSource,
                _fixedSize = _fixedSize,
                _delayBetweenSpawns = _delayBetweenSpawns,
                _spawnCommands = CloneCommandList(_spawnCommands),
                _counterVar = _counterVar,
                _writeSpawnerToContext = _writeSpawnerToContext,
                _spawnerContextSlot = _spawnerContextSlot,
                _enableChoiceInput = _enableChoiceInput,
                _choiceButtonChannelTag = _choiceButtonChannelTag,
                _choiceDecisionPhase = _choiceDecisionPhase,
                _choiceRequirePhaseTransition = _choiceRequirePhaseTransition,
            };
        }

        public GridObjectChannelVisualizerPreset CreateChoiceRuntimeCopy()
        {
            var copy = CreateRuntimeCopy();
            copy._enableChoiceInput = true;
            copy._choiceDecisionPhase = GridObjectChoiceDecisionPhase.Pressed;
            copy._choiceRequirePhaseTransition = true;
            return copy;
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
        menuName = "Game/Channel/GridObjectChannel/Visualizer Preset",
        fileName = "GridObjectChannelVisualizerPreset")]
    public sealed class GridObjectChannelVisualizerPresetSO : ScriptableObject, IDynamicValueAsset<GridObjectChannelVisualizerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        GridObjectChannelVisualizerPreset? _preset = new();

        public GridObjectChannelVisualizerPreset? Preset
        {
            get
            {
                EnsurePreset();
                return _preset;
            }
        }

        void OnEnable() => EnsurePreset();
        void OnValidate() => EnsurePreset();

        void EnsurePreset()
        {
            _preset ??= new GridObjectChannelVisualizerPreset();
        }
    }
}
