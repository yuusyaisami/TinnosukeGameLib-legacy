#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.Trait;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class TraitListChannelDefinitionCommand
    {
        [LabelText("Definition")]
        [Tooltip("この entry が適用される TraitDefinitionSO。list item の trait 定義ごとに個別 command を差し替えます。")]
        [SerializeField]
        TraitDefinitionSO? _definition;

        [LabelText("Commands")]
        [SerializeField]
        [CommandListFunctionName("TraitListChannel.Item.ByDefinition.OnSpawn")]
        [Tooltip("指定 TraitDefinition の item spawn 時に追加で流す command 群です。")]
        CommandListData _commands = new();

        public TraitDefinitionSO? Definition => _definition;
        public CommandListData Commands => _commands;

        internal TraitListChannelDefinitionCommand CreateRuntimeCopy()
        {
            return new TraitListChannelDefinitionCommand
            {
                _definition = _definition,
                _commands = CloneCommandList(_commands),
            };
        }

        static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }
    }

    [Serializable]
    public sealed class TraitListChannelVisualizerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Visual")]
        [LabelText("Runtime Template")]
        [Tooltip("各 list item に生成する RuntimeTemplatePreset です。RuntimeTemplateSO へ解決できる必要があります。")]
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
        TraitListChannelVisualizerSizeSource _sizeSource = TraitListChannelVisualizerSizeSource.VisualBounds;

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
        [SerializeField]
        [CommandListFunctionName("TraitListChannel.Item.OnSpawn")]
        [Tooltip("すべての item spawn 時に共通で流す command 群です。")]
        CommandListData _spawnCommands = new();

        [BoxGroup("Commands")]
        [LabelText("By Definition")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [Tooltip("TraitDefinition ごとの差し替え spawn command 設定です。")]
        [SerializeField]
        List<TraitListChannelDefinitionCommand> _byDefinition = new();

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
        [Tooltip("WriteSpawnerToContext が true のときに使う context slot です。")]
        [SerializeField]
        CommandLtsSlot _spawnerContextSlot = CommandLtsSlot.ContextA;

        bool UsesFixedSize() => _sizeSource == TraitListChannelVisualizerSizeSource.Fixed;

        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePreset => _runtimeTemplatePreset;
        public bool AllowPooling => _allowPooling;
        public TraitListChannelVisualizerSizeSource SizeSource => _sizeSource;
        public Vector2 FixedSize => new(Mathf.Max(0f, _fixedSize.x), Mathf.Max(0f, _fixedSize.y));
        public DynamicValue<float> DelayBetweenSpawns => _delayBetweenSpawns;
        public CommandListData SpawnCommands => _spawnCommands;
        public IReadOnlyList<TraitListChannelDefinitionCommand> ByDefinition => _byDefinition;
        public VarKeyRef CounterVar => _counterVar;
        public bool WriteSpawnerToContext => _writeSpawnerToContext;
        public CommandLtsSlot SpawnerContextSlot => _spawnerContextSlot;

        public bool TryResolveRuntimeTemplate(IDynamicContext context, out BaseRuntimeTemplateSO? runtimeTemplate)
        {
            runtimeTemplate = null;
            if (!_runtimeTemplatePreset.TryGet(context, out BaseRuntimeTemplatePreset? preset) || preset == null)
                return false;

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(preset);
            return runtimeTemplate != null;
        }

        public TraitListChannelVisualizerPreset CreateRuntimeCopy()
        {
            var copy = new TraitListChannelVisualizerPreset
            {
                _runtimeTemplatePreset = _runtimeTemplatePreset,
                _allowPooling = _allowPooling,
                _sizeSource = _sizeSource,
                _fixedSize = _fixedSize,
                _delayBetweenSpawns = _delayBetweenSpawns,
                _spawnCommands = CloneCommandList(_spawnCommands),
                _byDefinition = new List<TraitListChannelDefinitionCommand>(_byDefinition.Count),
                _counterVar = _counterVar,
                _writeSpawnerToContext = _writeSpawnerToContext,
                _spawnerContextSlot = _spawnerContextSlot,
            };

            for (var i = 0; i < _byDefinition.Count; i++)
            {
                var entry = _byDefinition[i];
                if (entry == null)
                    continue;
                copy._byDefinition.Add(entry.CreateRuntimeCopy());
            }

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
        menuName = "Game/UI/TraitListChannel/Visualizer Preset",
        fileName = "TraitListChannelVisualizerPreset")]
    public sealed class TraitListChannelVisualizerPresetSO : ScriptableObject, IDynamicValueAsset<TraitListChannelVisualizerPreset>
    {
        [SerializeReference, InlineProperty, HideLabel]
        [Tooltip("SO 内に保持する TraitListChannelVisualizerPreset 本体です。")]
        TraitListChannelVisualizerPreset? _preset = new();

        public TraitListChannelVisualizerPreset? Preset
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
            _preset ??= new TraitListChannelVisualizerPreset();
        }
    }
}
