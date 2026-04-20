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
        [Tooltip("Inspector setting.")]
        [SerializeField]
        TraitDefinitionSO? _definition;

        [LabelText("Commands")]
        [SerializeField]
        [CommandListFunctionName("TraitListChannel.Item.ByDefinition.OnSpawn")]
        [Tooltip("Inspector setting.")]
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
        TraitListChannelVisualizerSizeSource _sizeSource = TraitListChannelVisualizerSizeSource.VisualBounds;

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
        [SerializeField]
        [CommandListFunctionName("TraitListChannel.Item.OnSpawn")]
        [Tooltip("Inspector setting.")]
        CommandListData _spawnCommands = new();

        [BoxGroup("Commands")]
        [LabelText("By Definition")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        List<TraitListChannelDefinitionCommand> _byDefinition = new();

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
        [Tooltip("Inspector setting.")]
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
