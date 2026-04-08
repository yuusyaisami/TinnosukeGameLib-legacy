#nullable enable
using System;
using System.Collections.Generic;
using Game.Commands.VNext;
using Game.Common;
using Game.DI;
using Game.SelectRuntime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    public enum TooltipChannelSpaceKind
    {
        Unknown = 0,
        UIScreen = 10,
        World = 20,
    }

    public enum TooltipChannelRenderSpaceKind
    {
        Auto = 0,
        UIScreen = 10,
        World = 20,
    }

    public enum TooltipChannelInputMode
    {
        AutoByInputService = 10,
        Pointer = 20,
        Navigation = 30,
        PointerNavigation = 40,
    }

    public enum TooltipChannelSpawnMode
    {
        FollowPointer = 10,
        FixedOffset = 20,
    }

    public enum TooltipChannelAnchorX
    {
        Left = 10,
        Center = 20,
        Right = 30,
    }

    public enum TooltipChannelAnchorY
    {
        Up = 10,
        Center = 20,
        Down = 30,
    }

    public enum TooltipChannelStackDirection
    {
        Up = 10,
        Down = 20,
        Left = 30,
        Right = 40,
    }

    public enum TooltipChannelOverrideMode
    {
        None = 0,
        ForceShow = 10,
        ForceHide = 20,
    }

    public enum TooltipChannelVisibilityState
    {
        Hidden = 0,
        Spawning = 10,
        Active = 20,
        Closing = 30,
    }

    public enum TooltipChannelHubControlOperation
    {
        RegisterOrReplace = 10,
        Unregister = 20,
        ClearAll = 30,
        SwapHubPreset = 40,
    }

    public enum TooltipChannelOperation
    {
        ForceShow = 10,
        ForceHide = 20,
        ClearForceOverride = 30,
    }

    public enum TooltipChannelPlayerControlOperation
    {
        SwapPlayerPreset = 10,
        SwapCommandsPreset = 20,
        ResetRuntimeOverrides = 30,
    }

    public enum TooltipHitTestTargetKind
    {
        OwnerRectTransform = 10,
        OwnerSpriteRenderer = 20,
        ActorRectTransform = 30,
        ActorSpriteRenderer = 40,
        OwnerWorldPointerTarget = 50,
        ActorWorldPointerTarget = 60,
        OwnerSelectablePointerTarget = 70,
        ActorSelectablePointerTarget = 80,
    }

    public interface ITooltipChannelPlayer
    {
        string Tag { get; }
        bool IsConditionEnabled { get; }
        bool IsAutoTriggered { get; }
        bool IsVisibleRequested { get; }
        bool IsSpawned { get; }
        int Priority { get; }
        TooltipChannelOverrideMode OverrideMode { get; }
        TooltipChannelVisibilityState VisibilityState { get; }
    }

    public interface ITooltipChannelCommandService
    {
        bool ForceShow();
        bool ForceHide();
        bool ClearForceOverride();
    }

    public interface ITooltipChannelControlService
    {
        bool SwapPlayerPreset(TooltipPlayerPreset? preset);
        bool SwapCommandsPreset(TooltipCommandsPreset? preset);
        bool ResetRuntimeOverrides(bool resetPlayer, bool resetCommands, bool resetForceOverride);
    }

    public interface ITooltipChannelHubService
    {
        int ChannelCount { get; }
        TooltipChannelSpaceKind SpaceKind { get; }
        bool Contains(string tag);
        bool TryGetPlayer(string tag, out ITooltipChannelPlayer? player);
        bool TryGetCommand(string tag, out ITooltipChannelCommandService? command);
        bool TryGetControl(string tag, out ITooltipChannelControlService? control);
        bool RegisterOrReplace(string tag, TooltipPlayerPreset preset);
        bool Unregister(string tag);
        void ClearAll();
        bool SwapHubPreset(TooltipHubPreset? preset);
        void GetTags(List<string> output);
    }

    public interface ITooltipChannelOptions
    {
        DynamicValue<TooltipPlayerPreset> PresetValue { get; }
    }

    [Serializable]
    public sealed class TooltipHitTestTarget
    {
        [BoxGroup("Target")]
        [LabelText("Kind")]
        [Tooltip("Owner の RectTransform/SpriteRenderer を使うか、ActorSource から別 scope を解決して使うかを選びます。")]
        [SerializeField]
        TooltipHitTestTargetKind _kind = TooltipHitTestTargetKind.OwnerRectTransform;

        [BoxGroup("Target")]
        [ShowIf(nameof(UsesActorSource))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Actor Source\", _actorSource)")]
        [Tooltip("Actor 系 target を使うときの解決先です。RectTransform/SpriteRenderer/WorldPointerTarget はその scope の SelfTransform から取得します。")]
        [SerializeField]
        ActorSource _actorSource = new() { Kind = ActorSourceKind.Current };

        public TooltipHitTestTargetKind Kind => _kind;
        public ActorSource ActorSource => _actorSource;

        internal static TooltipHitTestTarget Create(TooltipHitTestTargetKind kind)
        {
            return new TooltipHitTestTarget
            {
                _kind = kind,
            };
        }

        public TooltipHitTestTarget CreateRuntimeCopy()
        {
            return new TooltipHitTestTarget
            {
                _kind = _kind,
                _actorSource = _actorSource,
            };
        }

        bool UsesActorSource =>
            _kind == TooltipHitTestTargetKind.ActorRectTransform ||
            _kind == TooltipHitTestTargetKind.ActorSpriteRenderer ||
            _kind == TooltipHitTestTargetKind.ActorWorldPointerTarget ||
            _kind == TooltipHitTestTargetKind.ActorSelectablePointerTarget;
    }

    [Serializable]
    public sealed class TooltipHitTestPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Hit Test")]
        [LabelText("Targets")]
        [Tooltip("auto trigger 判定に使う hit target 群です。空なら player 側では未指定扱いになり、hub default hit test を使用します。")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [SerializeField]
        List<TooltipHitTestTarget> _targets = new();

        public IReadOnlyList<TooltipHitTestTarget> Targets => _targets;
        public bool HasAnyTarget => _targets != null && _targets.Count > 0;

        public TooltipHitTestPreset CreateRuntimeCopy()
        {
            var copy = new TooltipHitTestPreset();
            if (_targets == null || _targets.Count == 0)
                return copy;

            copy._targets = new List<TooltipHitTestTarget>(_targets.Count);
            for (var i = 0; i < _targets.Count; i++)
                copy._targets.Add(_targets[i]?.CreateRuntimeCopy() ?? new TooltipHitTestTarget());
            return copy;
        }

        public static TooltipHitTestPreset CreateOwnerDefault()
        {
            var preset = new TooltipHitTestPreset();
            preset._targets = new List<TooltipHitTestTarget>
            {
                TooltipHitTestTarget.Create(TooltipHitTestTargetKind.OwnerRectTransform),
                TooltipHitTestTarget.Create(TooltipHitTestTargetKind.OwnerSpriteRenderer),
            };
            return preset;
        }
    }

    [Serializable]
    public sealed class TooltipCommandsPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Context")]
        [LabelText("Write Tooltip Owner To Context")]
        [Tooltip("true のとき、tooltip を生成した owner scope を command context slot へ書き込みます。")]
        [SerializeField]
        bool _writeTooltipOwnerToContext = true;

        [BoxGroup("Context")]
        [ShowIf(nameof(_writeTooltipOwnerToContext))]
        [LabelText("Tooltip Owner Context Slot")]
        [Tooltip("tooltip 生成者 owner を書き込む ContextA-D slot です。")]
        [SerializeField]
        CommandLtsSlot _tooltipOwnerContextSlot = CommandLtsSlot.ContextA;

        [BoxGroup("Commands")]
        [LabelText("Show Commands")]
        [Tooltip("spawn 完了直後に tooltip runtime 側で実行する commands です。")]
        [CommandListFunctionName("Tooltip.Channel.Show")]
        [SerializeField]
        CommandListData _showCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Hide Commands")]
        [Tooltip("close 開始時に tooltip runtime 側で実行する commands です。")]
        [CommandListFunctionName("Tooltip.Channel.Hide")]
        [SerializeField]
        CommandListData _hideCommands = new();

        [BoxGroup("Commands")]
        [LabelText("Self Despawn")]
        [Tooltip("hide commands の最後に実行する self despawn command です。")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        SelfDespawnCommandData _selfDespawn = new();

        public bool WriteTooltipOwnerToContext => _writeTooltipOwnerToContext;
        public CommandLtsSlot TooltipOwnerContextSlot => _tooltipOwnerContextSlot;
        public CommandListData ShowCommands => _showCommands;
        public CommandListData HideCommands => _hideCommands;
        public SelfDespawnCommandData SelfDespawn => _selfDespawn;

        public TooltipCommandsPreset CreateRuntimeCopy()
        {
            return new TooltipCommandsPreset
            {
                _writeTooltipOwnerToContext = _writeTooltipOwnerToContext,
                _tooltipOwnerContextSlot = _tooltipOwnerContextSlot,
                _showCommands = CloneCommandList(_showCommands),
                _hideCommands = CloneCommandList(_hideCommands),
                _selfDespawn = CloneSelfDespawn(_selfDespawn),
            };
        }

        public void BindDebugOwner(UnityEngine.Object owner, string prefix)
        {
            _showCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_showCommands)}");
            _hideCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_hideCommands)}");
            _selfDespawn?.BeforeDespawnCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_selfDespawn)}.{nameof(SelfDespawnCommandData.BeforeDespawnCommands)}");
            _selfDespawn?.OnReacquireCommands?.BindDebugOwner(owner, $"{prefix}.{nameof(_selfDespawn)}.{nameof(SelfDespawnCommandData.OnReacquireCommands)}");
        }

        internal static CommandListData CloneCommandList(CommandListData? source)
        {
            var clone = new CommandListData();
            if (source != null)
                clone.SetCommands(source);
            return clone;
        }

        static SelfDespawnCommandData CloneSelfDespawn(SelfDespawnCommandData? source)
        {
            if (source == null)
                return new SelfDespawnCommandData();

            return new SelfDespawnCommandData
            {
                DelaySeconds = source.DelaySeconds,
                BeforeDespawnCommands = CloneCommandList(source.BeforeDespawnCommands),
                OnReacquireCommands = CloneCommandList(source.OnReacquireCommands),
            };
        }
    }

    [Serializable]
    public sealed class TooltipPlayerPreset : IDynamicManagedRefValue
    {
        [BoxGroup("State")]
        [LabelText("Condition")]
        [Tooltip("true のときだけ auto trigger 対象になります。false のときは force show がない限り表示しません。")]
        [SerializeField]
        DynamicValue<bool> _condition = DynamicValueExtensions.FromLiteral(true);

        [BoxGroup("State")]
        [LabelText("Priority")]
        [Tooltip("複数 tooltip が同時可視になったときの stack/placement 優先度です。高いほど先に配置されます。")]
        [SerializeField]
        int _priority;

        [BoxGroup("Placement")]
        [LabelText("Apply Placement Override")]
        [Tooltip("true のときだけ Placement 設定をこの preset で上書きします。false のときは TooltipSystem の defaults を使います。")]
        [SerializeField]
        bool _applyPlacementOverride;

        [BoxGroup("Placement")]
        [ShowIf(nameof(ShowsPlacementOverrideFields))]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Anchor Actor\", _anchorActorSource)")]
        [Tooltip("FixedOffset の基準位置に使う actor です。Current の場合は owner の SelfTransform を使います。")]
        [SerializeField]
        ActorSource _anchorActorSource = new() { Kind = ActorSourceKind.Current };

        [BoxGroup("Runtime")]
        [LabelText("Apply Runtime Override")]
        [Tooltip("true のときだけ Runtime 設定をこの preset で上書きします。false のときは TooltipSystem の defaults を使います。")]
        [SerializeField]
        bool _applyRuntimeOverride;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowsRuntimeOverrideFields))]
        [LabelText("Runtime Template")]
        [Tooltip("tooltip 本体として spawn する RuntimeTemplate preset です。")]
        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> _runtimeTemplatePreset;

        [BoxGroup("Runtime")]
        [ShowIf(nameof(ShowsRuntimeOverrideFields))]
        [LabelText("Spawner Tag")]
        [Tooltip("spawn に使う spawner tag です。空なら tag fallback を許可します。")]
        [SerializeField]
        string _spawnerTag = string.Empty;

        [BoxGroup("Input")]
        [LabelText("Apply Input Override")]
        [Tooltip("true のときだけ Input 設定をこの preset で上書きします。false のときは TooltipSystem の defaults を使います。")]
        [SerializeField]
        bool _applyInputOverride;

        [BoxGroup("Input")]
        [ShowIf(nameof(ShowsInputOverrideFields))]
        [LabelText("Enable Pointer Hover")]
        [Tooltip("pointer hover による auto trigger を許可します。")]
        [SerializeField]
        bool _enablePointerHover = true;

        [BoxGroup("Input")]
        [ShowIf(nameof(ShowsInputOverrideFields))]
        [LabelText("Enable Selection Hover")]
        [Tooltip("UI selection による auto trigger を許可します。")]
        [SerializeField]
        bool _enableSelectionHover = true;

        [BoxGroup("Input")]
        [ShowIf(nameof(ShowsInputOverrideFields))]
        [LabelText("Hover Delay Seconds")]
        [MinValue(0d)]
        [Tooltip("pointer hover から表示開始までの待機秒です。")]
        [SerializeField]
        float _hoverDelaySeconds = 0.4f;

        [BoxGroup("Input")]
        [ShowIf(nameof(ShowsInputOverrideFields))]
        [LabelText("Selection Delay Seconds")]
        [MinValue(0d)]
        [Tooltip("selection hover から表示開始までの待機秒です。")]
        [SerializeField]
        float _selectionDelaySeconds = 0.3f;

        [BoxGroup("Input")]
        [ShowIf(nameof(ShowsInputOverrideFields))]
        [LabelText("Pointer Move Threshold")]
        [MinValue(0d)]
        [Tooltip("hover delay 計測中に pointer がこの距離以上動いたら待機をやり直します。")]
        [SerializeField]
        float _pointerMoveThreshold = 2f;

        [BoxGroup("Placement")]
        [ShowIf(nameof(ShowsPlacementOverrideFields))]
        [LabelText("Spawn Mode")]
        [Tooltip("pointer follow で出すか、anchor + fixed offset で出すかを選びます。")]
        [SerializeField]
        TooltipChannelSpawnMode _spawnMode = TooltipChannelSpawnMode.FollowPointer;

        [BoxGroup("Placement")]
        [ShowIf(nameof(ShowsFollowPointerFields))]
        [LabelText("Follow Pointer Direction Offset")]
        [Tooltip("pointer follow の基準方向オフセットです。UI では TooltipRoot 基準の anchored/local 座標、World では world 座標です。X/Y は最終 anchor 方向に応じて符号が決まり、Z は固定加算です。")]
        [SerializeField]
        Vector3 _followPointerDirectionOffset = Vector3.zero;

        [BoxGroup("Placement")]
        [ShowIf(nameof(ShowsFollowPointerFields))]
        [LabelText("Follow Pointer Move Scale")]
        [Tooltip("spawn 後に pointer が動いたとき、その追従量をスケールします。")]
        [SerializeField]
        Vector2 _followPointerMoveScale = Vector2.one;

        [BoxGroup("Placement")]
        [ShowIf(nameof(ShowsFixedOffsetFields))]
        [LabelText("Fixed Direction Offset")]
        [Tooltip("anchor actor からの固定方向オフセットです。UI では TooltipRoot 基準の anchored/local 座標、World では world 座標です。X/Y は最終 anchor 方向に応じて符号が決まり、Z は固定加算です。")]
        [SerializeField]
        Vector3 _fixedDirectionOffset = Vector3.zero;

        [BoxGroup("Placement")]
        [ShowIf(nameof(ShowsPlacementOverrideFields))]
        [LabelText("Anchor X")]
        [Tooltip("tooltip 矩形の横方向アンカーです。")]
        [SerializeField]
        TooltipChannelAnchorX _anchorX = TooltipChannelAnchorX.Right;

        [BoxGroup("Placement")]
        [ShowIf(nameof(ShowsPlacementOverrideFields))]
        [LabelText("Anchor Y")]
        [Tooltip("tooltip 矩形の縦方向アンカーです。")]
        [SerializeField]
        TooltipChannelAnchorY _anchorY = TooltipChannelAnchorY.Up;

        [BoxGroup("Hit Test")]
        [LabelText("Hit Test")]
        [Tooltip("player 個別 hit test preset です。空なら hub default hit test を使います。")]
        [SerializeField]
        DynamicValue<TooltipHitTestPreset> _hitTestValue =
            DynamicValue<TooltipHitTestPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipHitTestPreset>(new TooltipHitTestPreset()));

        [BoxGroup("Commands")]
        [LabelText("Commands Preset")]
        [Tooltip("show/hide/self-despawn を束ねた commands bundle preset です。")]
        [SerializeField]
        DynamicValue<TooltipCommandsPreset> _commandsPresetValue =
            DynamicValue<TooltipCommandsPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipCommandsPreset>(new TooltipCommandsPreset()));

        public DynamicValue<bool> Condition => _condition;
        public int Priority => _priority;
        public bool ApplyRuntimeOverride => _applyRuntimeOverride;
        public bool ApplyInputOverride => _applyInputOverride;
        public bool ApplyPlacementOverride => _applyPlacementOverride;
        public ActorSource AnchorActorSource => _anchorActorSource;
        public DynamicValue<BaseRuntimeTemplatePreset> RuntimeTemplatePresetValue => _runtimeTemplatePreset;
        public string SpawnerTag => _spawnerTag;
        public bool EnablePointerHover => _enablePointerHover;
        public bool EnableSelectionHover => _enableSelectionHover;
        public float HoverDelaySeconds => Mathf.Max(0f, _hoverDelaySeconds);
        public float SelectionDelaySeconds => Mathf.Max(0f, _selectionDelaySeconds);
        public float PointerMoveThreshold => Mathf.Max(0f, _pointerMoveThreshold);
        public TooltipChannelSpawnMode SpawnMode => _spawnMode;
        public Vector3 FollowPointerDirectionOffset => _followPointerDirectionOffset;
        public Vector2 FollowPointerMoveScale => _followPointerMoveScale;
        public Vector3 FixedDirectionOffset => _fixedDirectionOffset;
        public TooltipChannelAnchorX AnchorX => _anchorX;
        public TooltipChannelAnchorY AnchorY => _anchorY;
        public DynamicValue<TooltipHitTestPreset> HitTestValue => _hitTestValue;
        public DynamicValue<TooltipCommandsPreset> CommandsPresetValue => _commandsPresetValue;

        public TooltipPlayerPreset CreateRuntimeCopy()
        {
            return new TooltipPlayerPreset
            {
                _condition = _condition,
                _priority = _priority,
                _applyPlacementOverride = _applyPlacementOverride,
                _anchorActorSource = _anchorActorSource,
                _applyRuntimeOverride = _applyRuntimeOverride,
                _runtimeTemplatePreset = _runtimeTemplatePreset,
                _spawnerTag = _spawnerTag,
                _applyInputOverride = _applyInputOverride,
                _enablePointerHover = _enablePointerHover,
                _enableSelectionHover = _enableSelectionHover,
                _hoverDelaySeconds = _hoverDelaySeconds,
                _selectionDelaySeconds = _selectionDelaySeconds,
                _pointerMoveThreshold = _pointerMoveThreshold,
                _spawnMode = _spawnMode,
                _followPointerDirectionOffset = _followPointerDirectionOffset,
                _followPointerMoveScale = _followPointerMoveScale,
                _fixedDirectionOffset = _fixedDirectionOffset,
                _anchorX = _anchorX,
                _anchorY = _anchorY,
                _hitTestValue = _hitTestValue,
                _commandsPresetValue = _commandsPresetValue,
            };
        }

        bool ShowsRuntimeOverrideFields => _applyRuntimeOverride;
        bool ShowsInputOverrideFields => _applyInputOverride;
        bool ShowsPlacementOverrideFields => _applyPlacementOverride;
        bool ShowsFollowPointerFields => _applyPlacementOverride && _spawnMode == TooltipChannelSpawnMode.FollowPointer;
        bool ShowsFixedOffsetFields => _applyPlacementOverride && _spawnMode == TooltipChannelSpawnMode.FixedOffset;
    }

    [Serializable]
    public sealed class TooltipHubPreset : IDynamicManagedRefValue
    {
        [BoxGroup("Render")]
        [LabelText("Render Space")]
        [Tooltip("tooltip を実際に spawn / placement する空間です。Trigger の hit test 空間とは別です。既定は UIScreen です。")]
        [SerializeField]
        TooltipChannelRenderSpaceKind _renderSpace = TooltipChannelRenderSpaceKind.UIScreen;

        [BoxGroup("Camera")]
        [LabelText("Camera Location Tag")]
        [Tooltip("camera 解決に使う CameraLocation tag です。空白の場合は default を使用します。")]
        [SerializeField]
        string _cameraLocationTag = "default";

        [BoxGroup("Input")]
        [LabelText("Input Mode")]
        [Tooltip("tooltip の auto trigger 判定に使う入力モードです。")]
        [SerializeField]
        TooltipChannelInputMode _inputMode = TooltipChannelInputMode.PointerNavigation;

        [BoxGroup("Hit Test")]
        [LabelText("Default Hit Test")]
        [Tooltip("player 側 hit test が空のときに使う既定 hit test です。")]
        [SerializeField]
        DynamicValue<TooltipHitTestPreset> _defaultHitTestValue =
            DynamicValue<TooltipHitTestPreset>.FromSource(
                new ManagedRefLiteralSource<TooltipHitTestPreset>(TooltipHitTestPreset.CreateOwnerDefault()));

        [BoxGroup("Stack")]
        [LabelText("Stack Direction")]
        [Tooltip("重なり回避時にずらす方向です。")]
        [SerializeField]
        TooltipChannelStackDirection _stackDirection = TooltipChannelStackDirection.Up;

        [BoxGroup("Stack")]
        [LabelText("Stack Gap")]
        [MinValue(0d)]
        [Tooltip("重なり回避時の各 tooltip 間隔です。")]
        [SerializeField]
        float _stackGap = 8f;

        [BoxGroup("Clamp")]
        [LabelText("Enable Clamp")]
        [Tooltip("画面外へ出ないよう配置を clamp します。")]
        [SerializeField]
        bool _enableClamp = true;

        [BoxGroup("Clamp")]
        [ShowIf(nameof(_enableClamp))]
        [LabelText("Flip Threshold X")]
        [MinValue(0d)]
        [Tooltip("左右 overflow 比率がこの値を超えたら AnchorX を反転します。")]
        [SerializeField]
        float _flipThresholdX = 0.2f;

        [BoxGroup("Clamp")]
        [ShowIf(nameof(_enableClamp))]
        [LabelText("Flip Threshold Y")]
        [MinValue(0d)]
        [Tooltip("上下 overflow 比率がこの値を超えたら AnchorY を反転します。")]
        [SerializeField]
        float _flipThresholdY = 0.2f;

        [BoxGroup("Spawn")]
        [LabelText("Spawn Warmup Frames")]
        [MinValue(0)]
        [Tooltip("spawn 後に VisualBounds を安定させるため、画面外退避したまま待つフレーム数です。")]
        [SerializeField]
        int _spawnWarmupFrames = 2;

        public TooltipChannelRenderSpaceKind RenderSpace => _renderSpace;
        public string CameraLocationTag => string.IsNullOrWhiteSpace(_cameraLocationTag) ? "default" : _cameraLocationTag.Trim();
        public TooltipChannelInputMode InputMode => _inputMode;
        public DynamicValue<TooltipHitTestPreset> DefaultHitTestValue => _defaultHitTestValue;
        public TooltipChannelStackDirection StackDirection => _stackDirection;
        public float StackGap => Mathf.Max(0f, _stackGap);
        public bool EnableClamp => _enableClamp;
        public float FlipThresholdX => Mathf.Max(0f, _flipThresholdX);
        public float FlipThresholdY => Mathf.Max(0f, _flipThresholdY);
        public int SpawnWarmupFrames => Mathf.Max(0, _spawnWarmupFrames);

        public TooltipHubPreset CreateRuntimeCopy()
        {
            return new TooltipHubPreset
            {
                _renderSpace = _renderSpace,
                _cameraLocationTag = _cameraLocationTag,
                _inputMode = _inputMode,
                _defaultHitTestValue = _defaultHitTestValue,
                _stackDirection = _stackDirection,
                _stackGap = _stackGap,
                _enableClamp = _enableClamp,
                _flipThresholdX = _flipThresholdX,
                _flipThresholdY = _flipThresholdY,
                _spawnWarmupFrames = _spawnWarmupFrames,
            };
        }
    }

}
