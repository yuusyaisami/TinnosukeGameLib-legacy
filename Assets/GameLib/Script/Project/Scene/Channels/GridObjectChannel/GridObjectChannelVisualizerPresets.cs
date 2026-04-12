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
        [Tooltip("各 item に生成する RuntimeTemplatePreset です。RuntimeTemplateSO へ解決できる必要があります。")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplatePreset;

        [BoxGroup("Visual")]
        [LabelText("Allow Pooling")]
        [Tooltip("true のとき生成済み runtime を pool に返して再利用します。")]
        [SerializeField]
        bool _allowPooling = true;

        [BoxGroup("Visual")]
        [LabelText("Size Source")]
        [Tooltip("layout 計算に使う item size の取得元です。")]
        [SerializeField]
        GridObjectChannelVisualizerSizeSource _sizeSource = GridObjectChannelVisualizerSizeSource.VisualBounds;

        [BoxGroup("Visual")]
        [ShowIf(nameof(UsesFixedSize))]
        [LabelText("Fixed Size")]
        [Tooltip("Size Source が Fixed のときに使う item size です。")]
        [SerializeField]
        Vector2 _fixedSize = new(100f, 100f);

        [BoxGroup("Visual")]
        [LabelText("Delay Between Spawns")]
        [Tooltip("新規 item の spawn 間で待機する秒数です。relayout のみでは使用しません。")]
        [SerializeField]
        DynamicValue<float> _delayBetweenSpawns = DynamicValueExtensions.FromLiteral(0f);

        [BoxGroup("Commands")]
        [LabelText("Spawn Commands")]
        [Tooltip("各 item spawn 時に共通で流す command 群です。")]
        [SerializeField]
        [CommandListFunctionName("GridObjectChannel.Item.OnSpawn")]
        CommandListData _spawnCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Counter Var")]
        [Tooltip("spawn command 実行時に現在 item index を書き込む VarKey です。")]
        [SerializeField]
        VarKeyRef _counterVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [BoxGroup("Commands")]
        [LabelText("Write Spawner To Context")]
        [Tooltip("true のとき channel owner scope を Context slot へ積んでから spawn command を実行します。")]
        [SerializeField]
        bool _writeSpawnerToContext;

        [BoxGroup("Commands")]
        [ShowIf(nameof(_writeSpawnerToContext))]
        [LabelText("Spawner Context Slot")]
        [Tooltip("Write Spawner To Context が true のときに使う context slot です。")]
        [SerializeField]
        CommandLtsSlot _spawnerContextSlot = CommandLtsSlot.ContextA;

        [BoxGroup("Choice")]
        [LabelText("Enable Choice Input")]
        [Tooltip("true のとき GridObjectChoice の選択待機入力をこの preset から解決します。")]
        [SerializeField]
        bool _enableChoiceInput;

        [BoxGroup("Choice")]
        [ShowIf(nameof(_enableChoiceInput))]
        [LabelText("Choice Button Tag")]
        [Tooltip("各選択肢 RuntimeLTS 内の ButtonChannel tag です。")]
        [SerializeField]
        string _choiceButtonChannelTag = "default";

        [BoxGroup("Choice")]
        [ShowIf(nameof(_enableChoiceInput))]
        [LabelText("Decision Phase")]
        [Tooltip("ButtonChannel のどの phase を選択確定として扱うかを指定します。")]
        [SerializeField]
        GridObjectChoiceDecisionPhase _choiceDecisionPhase = GridObjectChoiceDecisionPhase.CompletedWaitingRelease;

        [BoxGroup("Choice")]
        [ShowIf(nameof(_enableChoiceInput))]
        [LabelText("Require Phase Transition")]
        [Tooltip("true のとき、同一 phase の連続更新ではなく phase 遷移時のみ決定判定します。")]
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
        [Tooltip("SO 内に保持する GridObjectChannelVisualizerPreset 本体です。")]
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
