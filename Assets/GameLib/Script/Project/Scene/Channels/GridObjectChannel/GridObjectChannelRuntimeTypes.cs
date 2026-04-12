#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Channel
{
    [Serializable]
    public sealed class GridObjectChannelBindRequest
    {
        [LabelText("Override Player Preset")]
        [Tooltip("true のとき hub 側 default player preset の代わりにここで指定した player preset を使います。")]
        [SerializeField]
        bool _overridePlayerPreset;

        [SerializeField]
        [ShowIf(nameof(_overridePlayerPreset))]
        [Tooltip("bind 時に差し替える player preset です。")]
        DynamicValue<GridObjectChannelPlayerPresetBase> _playerPresetValue =
            DynamicValue<GridObjectChannelPlayerPresetBase>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelPlayerPresetBase>(new GridObjectChannelStandalonePlayerPreset()));

        [LabelText("Override Layout Preset")]
        [Tooltip("true のとき hub 側 default layout preset の代わりにここで指定した layout preset を使います。")]
        [SerializeField]
        bool _overrideLayoutPreset;

        [SerializeField]
        [ShowIf(nameof(_overrideLayoutPreset))]
        [Tooltip("bind 時に差し替える layout preset です。")]
        DynamicValue<GridObjectChannelLayoutPreset> _layoutPresetValue =
            DynamicValue<GridObjectChannelLayoutPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelLayoutPreset>(new GridObjectChannelLayoutPreset()));

        [LabelText("Override Visualizer Preset")]
        [Tooltip("true のとき hub 側 default visualizer preset の代わりにここで指定した visualizer preset を使います。")]
        [SerializeField]
        bool _overrideVisualizerPreset;

        [SerializeField]
        [ShowIf(nameof(_overrideVisualizerPreset))]
        [Tooltip("bind 時に差し替える visualizer preset です。")]
        DynamicValue<GridObjectChannelVisualizerPreset> _visualizerPresetValue =
            DynamicValue<GridObjectChannelVisualizerPreset>.FromSource(
                new ManagedRefLiteralSource<GridObjectChannelVisualizerPreset>(new GridObjectChannelVisualizerPreset()));

        [LabelText("Force Choice Compatible")]
        [Tooltip("true のとき choice session 用に visualizer preset の choice input を有効化します。")]
        [SerializeField]
        bool _forceChoiceCompatible;

        [LabelText("Spawn Commands")]
        [Tooltip("bind 実行時に visualizer preset の SpawnCommands へ追加する command list です。")]
        [SerializeField]
        [CommandListFunctionName("GridObjectChannel.Bind.OnSpawn")]
        CommandListData _spawnCommands = new();

        public bool OverridePlayerPreset
        {
            get => _overridePlayerPreset;
            set => _overridePlayerPreset = value;
        }

        public DynamicValue<GridObjectChannelPlayerPresetBase> PlayerPresetValue
        {
            get => _playerPresetValue;
            set => _playerPresetValue = value;
        }

        public bool OverrideLayoutPreset
        {
            get => _overrideLayoutPreset;
            set => _overrideLayoutPreset = value;
        }

        public DynamicValue<GridObjectChannelLayoutPreset> LayoutPresetValue
        {
            get => _layoutPresetValue;
            set => _layoutPresetValue = value;
        }

        public bool OverrideVisualizerPreset
        {
            get => _overrideVisualizerPreset;
            set => _overrideVisualizerPreset = value;
        }

        public DynamicValue<GridObjectChannelVisualizerPreset> VisualizerPresetValue
        {
            get => _visualizerPresetValue;
            set => _visualizerPresetValue = value;
        }

        public bool ForceChoiceCompatible
        {
            get => _forceChoiceCompatible;
            set => _forceChoiceCompatible = value;
        }

        public CommandListData SpawnCommands
        {
            get => _spawnCommands;
            set => _spawnCommands = value ?? new CommandListData();
        }

        public GridObjectChannelBindRequest Clone()
        {
            return new GridObjectChannelBindRequest
            {
                _overridePlayerPreset = _overridePlayerPreset,
                _playerPresetValue = _playerPresetValue,
                _overrideLayoutPreset = _overrideLayoutPreset,
                _layoutPresetValue = _layoutPresetValue,
                _overrideVisualizerPreset = _overrideVisualizerPreset,
                _visualizerPresetValue = _visualizerPresetValue,
                _forceChoiceCompatible = _forceChoiceCompatible,
                _spawnCommands = _spawnCommands?.CreateRuntimeCopy() ?? new CommandListData(),
            };
        }
    }

    internal enum GridObjectChannelItemKeyKind
    {
        Standalone = 10,
        SourceCell = 20,
    }

    internal readonly struct GridObjectChannelItemKey : IEquatable<GridObjectChannelItemKey>
    {
        public GridObjectChannelItemKey(GridObjectChannelItemKeyKind kind, int valueA, int valueB)
        {
            Kind = kind;
            ValueA = valueA;
            ValueB = valueB;
        }

        public GridObjectChannelItemKeyKind Kind { get; }
        public int ValueA { get; }
        public int ValueB { get; }

        public static GridObjectChannelItemKey Standalone(int listIndex) => new(GridObjectChannelItemKeyKind.Standalone, listIndex, 0);
        public static GridObjectChannelItemKey SourceCell(int row, int column) => new(GridObjectChannelItemKeyKind.SourceCell, row, column);

        public bool Equals(GridObjectChannelItemKey other)
        {
            return Kind == other.Kind &&
                   ValueA == other.ValueA &&
                   ValueB == other.ValueB;
        }

        public override bool Equals(object? obj) => obj is GridObjectChannelItemKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, ValueA, ValueB);
    }

    internal sealed class GridObjectChannelResolvedItem
    {
        public GridObjectChannelItemKey Key;
        public int ListIndex;
        public int Row;
        public int Column;
        public int SourceRow;
        public int SourceColumn;
        public Vector3 TargetLocalPosition;
        public List<GridBlackboardCellSnapshot>? CellValues;

        public void SetCellValues(List<GridBlackboardCellSnapshot> values)
        {
            if (values == null || values.Count == 0)
            {
                CellValues = null;
                return;
            }

            CellValues = new List<GridBlackboardCellSnapshot>(values);
        }
    }

    internal sealed class GridObjectChannelVisualInstance
    {
        public GridObjectChannelVisualInstance(
            GridObjectChannelItemKey key,
            Transform root,
            IScopeNode scope,
            IObjectResolver resolver)
        {
            Key = key;
            Root = root;
            RootRect = root as RectTransform;
            Scope = scope;
            Resolver = resolver;
        }

        public GridObjectChannelItemKey Key { get; private set; }
        public Transform Root { get; }
        public RectTransform? RootRect { get; }
        public IScopeNode Scope { get; }
        public IObjectResolver Resolver { get; }
        public int ListIndex { get; private set; }
        public int Row { get; private set; }
        public int Column { get; private set; }
        public int SourceRow { get; private set; }
        public int SourceColumn { get; private set; }
        public Vector3 TargetLocalPosition { get; private set; }

        public void UpdateFromItem(GridObjectChannelResolvedItem item)
        {
            Key = item.Key;
            ListIndex = item.ListIndex;
            Row = item.Row;
            Column = item.Column;
            SourceRow = item.SourceRow;
            SourceColumn = item.SourceColumn;
            TargetLocalPosition = item.TargetLocalPosition;
        }
    }
}
