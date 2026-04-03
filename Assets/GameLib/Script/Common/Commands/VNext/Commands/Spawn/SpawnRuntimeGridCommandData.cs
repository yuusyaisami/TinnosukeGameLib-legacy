#nullable enable

using System;
using System.Collections.Generic;
using Game.Common;
using Game.DI;
using Game.Spawn;
using Game.Vars.Generated;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Commands.VNext
{
    public enum SpawnGridFillOrder
    {
        RowMajor = 0,
        ColumnMajor = 1,
    }

    public enum SpawnGridHorizontalAlign
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    public enum SpawnGridVerticalAlign
    {
        Top = 0,
        Center = 1,
        Bottom = 2,
    }

    public enum SpawnGridAxisDirection
    {
        Positive = 0,
        Negative = 1,
    }

    [Serializable]
    public sealed class SpawnRuntimeGridCommandData : ICommandData
    {
        public int CommandId => CommandIds.SpawnRuntimeGrid;

        public string DebugData
        {
            get
            {
                var templateName = CommandDebugDataHelper.GetDynamicDebugData(Template, "null");
                var count = CommandDebugDataHelper.GetDynamicDebugData(Count, "1");
                var columns = CommandDebugDataHelper.GetDynamicDebugData(Columns, "1");
                var tag = string.IsNullOrEmpty(SpawnerTag) ? "<none>" : SpawnerTag;
                var sourceLabel = WriteSpawnerToContext ? $" Src={SpawnerContextSlot}" : string.Empty;
                return $"Template={templateName} Count={count} Cols={columns} Fill={FillOrder} Spawner={SpawnerKind} Tag={tag}{sourceLabel}";
            }
        }

        [Header("Template")]
        [SerializeField, Required]
        public DynamicValue<BaseRuntimeTemplatePreset> Template;

        [Header("Spawner")]
        [SerializeField]
        [EnumToggleButtons]
        public SpawnerKind SpawnerKind = SpawnerKind.RuntimeEntity;

        [SerializeField]
        public string SpawnerTag = "";

        [Header("Transform")]
        [SerializeField]
        public bool WorldSpace = true;

        [SerializeField]
        public DynamicValue<Vector3> Position;

        [SerializeField]
        public DynamicValue<Vector3> Offset;

        [SerializeField]
        public DynamicValue<Vector3> RotationEuler = DynamicValueExtensions.FromLiteral(Vector3.zero);

        [SerializeField]
        public DynamicValue<Vector3> Scale = DynamicValueExtensions.FromLiteral(Vector3.one);

        [Header("Count")]
        [SerializeField]
        [MinValue(1)]
        public DynamicValue<int> Count = DynamicValueExtensions.FromLiteral(1);

        [SerializeField]
        [MinValue(1)]
        public DynamicValue<int> Columns = DynamicValueExtensions.FromLiteral(1);

        [SerializeField]
        [MinValue(0)]
        public DynamicValue<int> Rows = DynamicValueExtensions.FromLiteral(0);

        [SerializeField]
        [EnumToggleButtons]
        public SpawnGridFillOrder FillOrder = SpawnGridFillOrder.RowMajor;

        [SerializeField]
        [EnumToggleButtons]
        public SpawnGridAxisDirection HorizontalDirection = SpawnGridAxisDirection.Positive;

        [SerializeField]
        [EnumToggleButtons]
        public SpawnGridAxisDirection VerticalDirection = SpawnGridAxisDirection.Negative;

        [SerializeField]
        [EnumToggleButtons]
        public SpawnGridHorizontalAlign HorizontalAlign = SpawnGridHorizontalAlign.Left;

        [SerializeField]
        [EnumToggleButtons]
        public SpawnGridVerticalAlign VerticalAlign = SpawnGridVerticalAlign.Top;

        [SerializeField]
        public DynamicValue<Vector2> Spacing = DynamicValueExtensions.FromLiteral(Vector2.zero);

        [SerializeField]
        public DynamicValue<Vector2> FallbackItemSize = DynamicValueExtensions.FromLiteral(new Vector2(1f, 1f));

        [Header("GridBlackboard Link")]
        [SerializeField]
        [LabelText("Enable GridBlackboard Link")]
        [FormerlySerializedAs("LinkToGridBlackboard")]
        [PropertyTooltip("有効時、各スポーン結果を計算された row/column に基づいて GridBlackboard へ書き込みます。")]
        public bool EnabledGridBlackboardLink = false;

        [SerializeField, ShowIf(nameof(ShowGridBlackboardLinkFields))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Grid Blackboard Scope\", GridBlackboardActorSource)")]
        [PropertyTooltip("IGridBlackboardService を解決するスコープです。通常は Current を使います。")]
        public ActorSource GridBlackboardActorSource = new() { Kind = ActorSourceKind.Current };

        [SerializeField, ShowIf(nameof(ShowGridBlackboardLinkFields))]
        [LabelText("Grid Row Offset")]
        [PropertyTooltip("スポーン計算された row に加算するオフセットです。例: 3 なら 3 行目から書き込みます。")]
        public DynamicValue<int> GridLinkRowOffset = DynamicValueExtensions.FromLiteral(0);

        [SerializeField, ShowIf(nameof(ShowGridBlackboardLinkFields))]
        [LabelText("Grid Column Offset")]
        [PropertyTooltip("スポーン計算された column に加算するオフセットです。")]
        public DynamicValue<int> GridLinkColumnOffset = DynamicValueExtensions.FromLiteral(0);

        [SerializeField, ShowIf(nameof(ShowGridBlackboardLinkFields))]
        [LabelText("Write Spawned Scope Ref")]
        [PropertyTooltip("生成された IScopeNode 参照を ManagedRef として GridBlackboard に書き込みます。")]
        public bool WriteSpawnedScopeRefToGrid;

        [SerializeField, ShowIf(nameof(ShowGridBlackboardLinkFields))]
        [LabelText("Use Grid Key Filter")]
        [PropertyTooltip("有効時は指定 Grid Key を持つセルだけをリンク対象にします。未指定でも Count/Columns が GridBlackboardColumnCountSource の場合は Grid Key / Row を自動推論して利用します。")]
        public bool UseGridKeyFilter = false;

        [SerializeField, ShowIf(nameof(ShowGridKeyFilter))]
        [LabelText("Grid Key")]
        [PropertyTooltip("リンク対象グリッドを識別する VarKey です。WriteGridData の GridId と同じキーを指定します。")]
        public VarKeyRef GridKey = new();

        [SerializeField, ShowIf(nameof(ShowGridSpawnedScopeVar))]
        [LabelText("Spawned Scope Var")]
        [PropertyTooltip("生成Scope参照を書き込む先の VarKey です。")]
        public VarKeyRef GridSpawnedScopeVar = new();

        [SerializeField, ShowIf(nameof(ShowGridBlackboardLinkFields))]
        [LabelText("Copy Vars From Command Vars")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        [PropertyTooltip("指定した VarKey を現在の Command Vars から取得し、同じグリッドセルへコピーします。")]
        public List<VarKeyRef> GridLinkVarKeys = new();

        [Header("Delay Between Spawns")]
        [SerializeField]
        public DynamicValue<float> DelayBetweenSpawns = DynamicValueExtensions.FromLiteral(0f);

        [Header("Parent")]
        [SerializeField]
        [EnumToggleButtons]
        public SpawnTransformParentPolicy TransformParentPolicy = SpawnTransformParentPolicy.SpawnerRoot;

        [SerializeField, ShowIf(nameof(ShowTransformParent))]
        public Transform? TransformParent;

        [SerializeField, ShowIf(nameof(ShowTransformParentActorSource))]
        [InlineProperty]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Transform Parent\", TransformParentActorSource)")]
        public ActorSource TransformParentActorSource;

        [Header("DI Parent (optional)")]
        [SerializeField]
        public bool OverrideLifetimeScopeParent = false;

        [SerializeField, ShowIf(nameof(OverrideLifetimeScopeParent))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"DI Parent\", LifetimeScopeParent)")]
        public ActorSource LifetimeScopeParent;

        [Header("Pooling")]
        [SerializeField]
        public bool AllowPooling = true;

        [Header("Context")]
        [SerializeField]
        public bool WriteSpawnedScopeToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnedScopeToContext))]
        [LabelText("Spawned Scope Slot")]
        public CommandLtsSlot SpawnedScopeSlot = CommandLtsSlot.ContextA;

        [SerializeField]
        [LabelText("Write Spawner To Context")]
        public bool WriteSpawnerToContext = false;

        [SerializeField, ShowIf(nameof(WriteSpawnerToContext))]
        [LabelText("Spawner Slot")]
        public CommandLtsSlot SpawnerContextSlot = CommandLtsSlot.ContextB;

        [Header("Spawn Commands")]
        [SerializeField]
        public bool RunCommandsOnSpawned = false;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        public VarsPolicy VarsPolicy = VarsPolicy.Inherit;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Counter Var")]
        public VarKeyRef CounterVar = new(VarIds.GameLib.Base.CommandVar.i, "i");

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Await OnSpawned Commands")]
        public bool AwaitOnSpawnedCommands = true;

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        [LabelText("Common Commands")]
        public CommandListData OnSpawnedCommonCommands = new();

        [SerializeField, ShowIf(nameof(RunCommandsOnSpawned))]
        public bool RunConditionalCommands = false;

        [SerializeField, ShowIf(nameof(ShowSpawnCondition))]
        [LabelText("Condition")]
        public DynamicValue<bool> SpawnCondition = DynamicValueExtensions.FromLiteral(true);

        [SerializeField, ShowIf(nameof(ShowSpawnCondition))]
        [LabelText("When True")]
        public CommandListData OnSpawnedWhenTrueCommands = new();

        [SerializeField, ShowIf(nameof(ShowSpawnCondition))]
        [LabelText("When False")]
        public CommandListData OnSpawnedWhenFalseCommands = new();

        bool ShowTransformParent => TransformParentPolicy == SpawnTransformParentPolicy.UseTransform;
        bool ShowTransformParentActorSource => TransformParentPolicy == SpawnTransformParentPolicy.ActorSource;
        bool ShowSpawnCondition => RunCommandsOnSpawned && RunConditionalCommands;
        bool ShowGridBlackboardLinkFields => EnabledGridBlackboardLink;
        bool ShowGridSpawnedScopeVar => EnabledGridBlackboardLink && WriteSpawnedScopeRefToGrid;
        bool ShowGridKeyFilter => EnabledGridBlackboardLink && UseGridKeyFilter;

        public SpawnRuntimeGridCommandData()
        {
            Position = DynamicValueExtensions.FromLiteral(Vector3.zero);
            Offset = DynamicValueExtensions.FromLiteral(Vector3.zero);
        }
    }
}
