#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands.VNext;
using Game.Common;
using Game.Input;
using UnityEngine;
using VContainer;

namespace Game.SelectRuntime
{
    public sealed class UserMoveRotateRuntimeService :
        IUserMoveRotateRuntimeService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        enum UserMoveRotateEnterSource
        {
            SelectableLongPress = 10,
            PointerLongPress = 20,
            Command = 30,
        }

        sealed class EditSession
        {
            public UserMoveRotateRuntimeMB? Editor;
            public KernelScopeHost? RuntimeScope;
            public Transform? RootTransform;
            public Transform? MoveTransform;
            public Transform? RotateTransform;
            public int StartedFrame;
            public Vector3 LastValidPosition;
            public Quaternion LastValidRotation = Quaternion.identity;
            public Channel.AreaPlane RotationPlane = Channel.AreaPlane.XY;
            public float RotationDegrees;

            public bool IsActive => Editor != null && RuntimeScope != null && RootTransform != null && MoveTransform != null && RotateTransform != null;

            public void Clear()
            {
                Editor = null;
                RuntimeScope = null;
                RootTransform = null;
                MoveTransform = null;
                RotateTransform = null;
                StartedFrame = 0;
                LastValidPosition = Vector3.zero;
                LastValidRotation = Quaternion.identity;
                RotationPlane = Channel.AreaPlane.XY;
                RotationDegrees = 0f;
            }
        }

        sealed class EntryPressState
        {
            public UserMoveRotateRuntimeMB? Editor;
            public WorldPointerTargetMB? Target;
            public float PressedTime;
            public WorldPointerEventData PressedData;
            public bool EntryCommandsExecuted;

            public bool IsActive => Editor != null && Target != null;

            public void Clear()
            {
                Editor = null;
                Target = null;
                PressedTime = 0f;
                PressedData = default;
                EntryCommandsExecuted = false;
            }
        }

        readonly IScopeNode _owner;
        readonly IWorldPointerRuntimeService _pointerService;
        readonly ISelectRuntimeManagerService _managerService;
        readonly IWorldPointerRuntimeOptions _pointerOptions;
        readonly IPointerService _pointerActivity;

        readonly HashSet<UserMoveRotateRuntimeMB> _editors = new();
        readonly Dictionary<WorldPointerTargetMB, UserMoveRotateRuntimeMB> _editorByTarget = new();
        readonly EditSession _session = new();
        readonly EntryPressState _entryPress = new();
        readonly ExternalFloatBindingRuntime _rotateBinding = new();
        readonly ExternalBoolBindingRuntime _isEditorModeBinding = new();
        WorldPointerTargetMB? _hoveredTarget;
        WorldPointerEventData _hoveredData;
        Vector2 _lastPointerScreen;

        public UserMoveRotateRuntimeService(
            IScopeNode owner,
            IWorldPointerRuntimeService pointerService,
            ISelectRuntimeManagerService managerService,
            IWorldPointerRuntimeOptions pointerOptions,
            IPointerService pointerActivity)
        {
            _owner = owner;
            _pointerService = pointerService;
            _managerService = managerService;
            _pointerOptions = pointerOptions;
            _pointerActivity = pointerActivity;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _managerService.OnLeftLongPressSelectable += HandleLongPressSelectable;
            _managerService.OnSelectionChanged += HandleSelectionChanged;
            _managerService.OnEnabledChanged += HandleEnabledChanged;
            _pointerService.OnHoveredChanged += HandleHoveredChanged;
            _pointerService.OnLeftClicked += HandleLeftClicked;
            _pointerService.OnRightClicked += HandleRightClicked;
            _pointerService.OnFrameUpdated += HandleFrameUpdated;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _managerService.OnLeftLongPressSelectable -= HandleLongPressSelectable;
            _managerService.OnSelectionChanged -= HandleSelectionChanged;
            _managerService.OnEnabledChanged -= HandleEnabledChanged;
            _pointerService.OnHoveredChanged -= HandleHoveredChanged;
            _pointerService.OnLeftClicked -= HandleLeftClicked;
            _pointerService.OnRightClicked -= HandleRightClicked;
            _pointerService.OnFrameUpdated -= HandleFrameUpdated;

            EndSession(runExitCommands: false);
            _rotateBinding.Release();
            _isEditorModeBinding.Release();
            _entryPress.Clear();
            _hoveredTarget = null;
            _hoveredData = default;
            _lastPointerScreen = default;
            _editors.Clear();
            _editorByTarget.Clear();
        }

        public bool IsEditing(UserMoveRotateRuntimeMB editor)
        {
            return editor != null && ReferenceEquals(_session.Editor, editor);
        }

        public bool TryEnterEditorMode(UserMoveRotateRuntimeMB editor)
        {
            return TryEnterEditorMode(
                editor,
                runtimeScopeOverride: null,
                enterSource: UserMoveRotateEnterSource.Command,
                eventData: default,
                ignoreEntrySource: true);
        }

        public bool TryExitEditorMode(UserMoveRotateRuntimeMB editor, bool runExitCommands)
        {
            if (editor == null || !_session.IsActive || !ReferenceEquals(_session.Editor, editor))
                return false;

            EndSession(runExitCommands);
            return true;
        }

        public void RegisterEditor(UserMoveRotateRuntimeMB editor)
        {
            if (editor == null || !_editors.Add(editor))
                return;

            var target = editor.ResolveTarget();
            if (target != null)
                _editorByTarget[target] = editor;
        }

        public void UnregisterEditor(UserMoveRotateRuntimeMB editor)
        {
            if (editor == null || !_editors.Remove(editor))
                return;

            var target = editor.ResolveTarget();
            if (target != null && _editorByTarget.TryGetValue(target, out var mapped) && ReferenceEquals(mapped, editor))
                _editorByTarget.Remove(target);

            if (ReferenceEquals(_session.Editor, editor))
                EndSession(runExitCommands: true);
        }

        void HandleLongPressSelectable(SelectableRuntimeMB selectable)
        {
            if (selectable == null)
                return;

            var target = selectable.ResolveTarget();
            if (target == null || !_editorByTarget.TryGetValue(target, out var editor) || editor == null)
            {
                LogEditorEntryFailure(selectable, "Selectable long press ignored: editor target is not registered.");
                return;
            }

            if (!CanEnterFromSelectable(editor))
            {
                LogEditorEntryFailure(editor, $"Selectable long press ignored: entry source is {editor.EditorEntrySource}.");
                return;
            }

            if (_session.IsActive && ReferenceEquals(_session.Editor, editor))
                return;

            if (_entryPress.IsActive && ReferenceEquals(_entryPress.Editor, editor) && editor.EditorEntrySource == UserMoveRotateEditorEntrySource.Both)
            {
                // Pointer long-press path has progress updates; in Both mode prefer it to keep Entry timing deterministic.
                return;
            }

            ExecuteLongPressEntryCommands(editor);
            TryEnterEditorMode(
                editor,
                runtimeScopeOverride: null,
                enterSource: UserMoveRotateEnterSource.SelectableLongPress,
                eventData: _hoveredData,
                ignoreEntrySource: false);
        }

        void HandleSelectionChanged(SelectRuntimeSelectionChangedEvent eventData)
        {
            if (!_session.IsActive)
                return;

            if (ReferenceEquals(eventData.Current, _managerService.Current))
            {
                var target = _session.Editor?.ResolveTarget();
                if (target != null && eventData.Current != null && ReferenceEquals(eventData.Current.ResolveTarget(), target))
                    return;
            }

            EndSession(runExitCommands: true);
        }

        void HandleEnabledChanged(bool enabled)
        {
            if (!enabled && _entryPress.IsActive)
                CancelEntryPress(_entryPress.Editor, "Pointer long press canceled: select runtime disabled.", runCancelCommands: true);

            if (!enabled)
                EndSession(runExitCommands: true);
        }

        void HandleHoveredChanged(WorldPointerHoverChangedEventData eventData)
        {
            _hoveredTarget = eventData.CurrentTarget;
            _hoveredData = eventData.EventData;

            if (_entryPress.IsActive && !ReferenceEquals(_entryPress.Target, _hoveredTarget))
                CancelEntryPress(_entryPress.Editor, "Pointer long press canceled: hover target left.", runCancelCommands: true);
        }

        void HandleLeftClicked(WorldPointerEventData eventData)
        {
            if (!_session.IsActive || Time.frameCount <= _session.StartedFrame)
                return;

            EndSession(runExitCommands: true);
        }

        void HandleRightClicked(WorldPointerEventData eventData)
        {
            if (!_session.IsActive || Time.frameCount <= _session.StartedFrame)
                return;

            EndSession(runExitCommands: true);
        }

        void HandleFrameUpdated(InputFrame frame)
        {
            if (_session.IsActive)
            {
                HandleEditingFrame(frame);
                return;
            }

            HandleEditorEntryFrame(frame);
        }

        void HandleEditingFrame(InputFrame frame)
        {
            if (!_session.IsActive || _session.Editor == null || _session.RuntimeScope == null || _session.RootTransform == null || _session.MoveTransform == null || _session.RotateTransform == null)
                return;

            if (!_managerService.EvaluateIsEnabled())
            {
                EndSession(runExitCommands: true);
                return;
            }

            if (!EvaluateEditorCondition(_session.Editor, _session.RuntimeScope))
            {
                EndSession(runExitCommands: true);
                return;
            }

            var request = UserMoveRotateValidationRequest.Create(_session.Editor, _session.RuntimeScope);
            var currentPosition = _session.MoveTransform!.position;
            var currentRotation = _session.RotateTransform!.rotation;
            var candidatePosition = currentPosition;
            var candidateRotation = currentRotation;
            var candidateRotationDegrees = _session.RotationDegrees;

            ApplyRotation(frame, request, _session.RotationDegrees, ref candidateRotation, out candidateRotationDegrees);
            ApplyMovement(frame, request, currentPosition, currentRotation, ref candidatePosition);

            _lastPointerScreen = frame.PointerScreen;

            if (candidatePosition == currentPosition && candidateRotation == currentRotation)
                return;

            if (!UserMoveRotateValidationUtility.IsValidPose(request, candidatePosition, candidateRotation))
                return;

            ApplySessionPose(candidatePosition, candidateRotation);
            _session.LastValidPosition = candidatePosition;
            _session.LastValidRotation = candidateRotation;
            _session.RotationDegrees = candidateRotationDegrees;
            SyncRotateBinding();
        }

        void HandleEditorEntryFrame(InputFrame frame)
        {
            if (!_managerService.EvaluateIsEnabled())
            {
                if (_entryPress.IsActive)
                    CancelEntryPress(_entryPress.Editor, "Pointer long press canceled: manager is disabled.", runCancelCommands: true);
                return;
            }

            if (frame.PointerLeft.Down)
            {
                TryBeginPointerEntry();
            }

            if (!_entryPress.IsActive)
                return;

            var editor = _entryPress.Editor;
            if (editor == null || !CanEnterFromPointer(editor))
            {
                if (editor != null)
                    LogEditorEntryFailure(editor, $"Pointer long press ignored: entry source is {editor.EditorEntrySource}.");
                CancelEntryPress(editor, "Pointer long press canceled: entry source mismatch.", runCancelCommands: true);
                return;
            }

            if (!frame.PointerLeft.Held)
            {
                if (frame.PointerLeft.Up)
                    _entryPress.Clear();
                else
                    CancelEntryPress(editor, "Pointer long press canceled: hold interrupted.", runCancelCommands: true);
                return;
            }

            if (!TryResolveRuntimeScope(editor, runtimeScopeOverride: null, out var conditionScope) || !EvaluateEditorCondition(editor, conditionScope))
            {
                CancelEntryPress(editor, "Pointer long press canceled: editor condition is false.", runCancelCommands: true);
                return;
            }

            if (!ReferenceEquals(_entryPress.Target, _hoveredTarget))
            {
                CancelEntryPress(editor, "Pointer long press canceled: target changed before threshold.", runCancelCommands: true);
                return;
            }

            var elapsed = Time.unscaledTime - _entryPress.PressedTime;
            var progress = editor.EditorLongPressSeconds > 0f
                ? Mathf.Clamp01(elapsed / editor.EditorLongPressSeconds)
                : 1f;
            if (!_entryPress.EntryCommandsExecuted && progress >= editor.LongPressEntryProgress)
            {
                ExecuteLongPressEntryCommands(editor);
                _entryPress.EntryCommandsExecuted = true;
            }

            if (elapsed < editor.EditorLongPressSeconds)
                return;

            var entered = TryEnterEditorMode(
                editor,
                runtimeScopeOverride: null,
                enterSource: UserMoveRotateEnterSource.PointerLongPress,
                eventData: _entryPress.PressedData,
                ignoreEntrySource: false);
            if (!entered)
            {
                LogEditorEntryFailure(editor, "Pointer long press reached threshold but editor mode entry failed.");
                CancelEntryPress(editor, "Pointer long press canceled: enter failed at threshold.", runCancelCommands: true);
                return;
            }

            _entryPress.Clear();
        }

        void TryBeginPointerEntry()
        {
            if (_hoveredTarget == null)
                return;

            if (!_editorByTarget.TryGetValue(_hoveredTarget, out var editor) || editor == null)
            {
                if (_hoveredTarget != null)
                    LogEditorEntryFailure(_hoveredTarget, "Pointer down ignored: no editor is registered for hovered target.");
                return;
            }

            if (!CanEnterFromPointer(editor))
            {
                LogEditorEntryFailure(editor, $"Pointer down ignored: entry source is {editor.EditorEntrySource}.");
                return;
            }

            if (!TryResolveRuntimeScope(editor, runtimeScopeOverride: null, out var runtimeScope) || !EvaluateEditorCondition(editor, runtimeScope))
            {
                LogEditorEntryFailure(editor, "Pointer down ignored: editor condition is false.");
                return;
            }

            _entryPress.Editor = editor;
            _entryPress.Target = _hoveredTarget;
            _entryPress.PressedTime = Time.unscaledTime;
            _entryPress.PressedData = _hoveredData.Target != null ? _hoveredData : new WorldPointerEventData(_hoveredTarget, _hoveredData.ScreenPosition, _hoveredData.WorldPosition, _hoveredData.HitNormal, _hoveredData.HitCollider);
            _entryPress.EntryCommandsExecuted = false;

        }

        bool TryEnterEditorMode(
            UserMoveRotateRuntimeMB editor,
            KernelScopeHost? runtimeScopeOverride,
            UserMoveRotateEnterSource enterSource,
            WorldPointerEventData eventData,
            bool ignoreEntrySource)
        {
            if (editor == null)
                return false;

            _ = eventData;

            if (ReferenceEquals(_session.Editor, editor))
                return true;

            if (!ignoreEntrySource &&
                ((enterSource == UserMoveRotateEnterSource.SelectableLongPress && !CanEnterFromSelectable(editor)) ||
                 (enterSource == UserMoveRotateEnterSource.PointerLongPress && !CanEnterFromPointer(editor))))
            {
                LogEditorEntryFailure(editor, $"Editor entry blocked by source {editor.EditorEntrySource}.");
                return false;
            }

            if (!TryResolveRuntimeScope(editor, runtimeScopeOverride, out var runtimeScope))
                return false;

            if (!EvaluateEditorCondition(editor, runtimeScope))
            {
                LogEditorEntryFailure(editor, "Editor entry blocked by condition=false.");
                return false;
            }

            EndSession(runExitCommands: true);

            var rootTransform = runtimeScope.Identity?.SelfTransform != null
                ? runtimeScope.Identity.SelfTransform
                : runtimeScope.transform;
            var moveTransform = editor.ApplyOverrideTargetTransform && editor.MoveTargetTransform != null
                ? editor.MoveTargetTransform
                : rootTransform;
            var rotateTransform = editor.ApplyOverrideTargetTransform && editor.RotateTargetTransform != null
                ? editor.RotateTargetTransform
                : rootTransform;
            _session.Editor = editor;
            _session.RuntimeScope = runtimeScope;
            _session.RootTransform = rootTransform;
            _session.MoveTransform = moveTransform;
            _session.RotateTransform = rotateTransform;
            _session.StartedFrame = Time.frameCount;
            _session.LastValidPosition = moveTransform.position;
            _session.LastValidRotation = rotateTransform.rotation;

            var initRequest = UserMoveRotateValidationRequest.Create(editor, runtimeScope);
            _session.RotationPlane = UserMoveRotateValidationUtility.ResolvePlane(initRequest, moveTransform.position);
            _session.RotationDegrees = ExtractRotationDegrees(_session.RotationPlane, rotateTransform.rotation);

            BindRotateExternal(editor, runtimeScope);
            BindIsEditorModeExternal(editor, runtimeScope);
            SyncIsEditorModeBinding(true);

            ExecuteEditorCommands(editor, editor.OnEditorEnterCommands, selected: true, hovered: false, editing: true);

            return true;
        }

        static bool CanEnterFromSelectable(UserMoveRotateRuntimeMB editor)
        {
            return editor.EditorEntrySource != UserMoveRotateEditorEntrySource.PointerLongPress;
        }

        static bool CanEnterFromPointer(UserMoveRotateRuntimeMB editor)
        {
            return editor.EditorEntrySource != UserMoveRotateEditorEntrySource.SelectableLongPress;
        }

        void LogEditorEntryFailure(object subject, string message)
        {
            _ = subject;
            _ = message;
        }

        bool TryResolveRuntimeScope(UserMoveRotateRuntimeMB editor, KernelScopeHost? runtimeScopeOverride, out KernelScopeHost runtimeScope)
        {
            runtimeScope = null!;
            if (!editor.TryResolveActorScope(out var scope) || scope is not KernelScopeHost resolved)
            {
                LogEditorEntryFailure(editor, "Could not resolve KernelScopeHost from editor actor scope.");
                return false;
            }

            runtimeScope = runtimeScopeOverride != null ? runtimeScopeOverride : resolved;
            return true;
        }

        bool EvaluateEditorCondition(UserMoveRotateRuntimeMB editor, KernelScopeHost runtimeScope)
        {
            var vars = new VarStore();
            if (runtimeScope.Resolver != null && runtimeScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                blackboard.MergeInto(vars, overwrite: true);

            var dynamicContext = new SimpleDynamicContext(vars, runtimeScope);
            return editor.EditorCondition.TryGet(dynamicContext, out var allowed) && allowed;
        }

        void ExecuteLongPressEntryCommands(UserMoveRotateRuntimeMB editor)
        {
            ExecuteEditorCommands(editor, editor.OnLongPressEntryCommands, selected: false, hovered: true, editing: false);
        }

        void ExecuteLongPressCancelCommands(UserMoveRotateRuntimeMB editor)
        {
            ExecuteEditorCommands(editor, editor.OnLongPressCancelCommands, selected: false, hovered: false, editing: false);
        }

        void CancelEntryPress(UserMoveRotateRuntimeMB? editor, string reason, bool runCancelCommands)
        {
            if (editor != null)
                LogEditorEntryFailure(editor, reason);

            if (runCancelCommands && editor != null)
                ExecuteLongPressCancelCommands(editor);

            _entryPress.Clear();
        }

        static string DescribeEditor(UserMoveRotateRuntimeMB editor)
        {
            if (editor == null)
                return "null";

            return $"{editor.name} target={DescribeTarget(editor.Target)} source={editor.EditorEntrySource} longPress={editor.EditorLongPressSeconds:0.###}";
        }

        static string DescribeTarget(WorldPointerTargetMB? target)
        {
            return target != null ? target.name : "null";
        }

        static string DescribeRuntime(KernelScopeHost? runtime)
        {
            if (runtime == null)
                return "null";

            var id = runtime.Identity?.Id ?? "(no id)";
            return $"{runtime.name}:{id}";
        }

        static string DescribeSelectable(SelectableRuntimeMB? selectable)
        {
            if (selectable == null)
                return "null";

            var target = selectable.ResolveTarget();
            return $"{selectable.name} target={DescribeTarget(target)}";
        }

        void ExecuteEditorCommands(
            UserMoveRotateRuntimeMB editor,
            CommandListData? commands,
            bool selected,
            bool hovered,
            bool editing)
        {
            if (editor == null || commands == null || commands.Count == 0)
                return;

            if (!editor.TryResolveActorScope(out var actorScope) || actorScope == null || actorScope.Resolver == null)
                return;

            ICommandRunner? runner = null;
            if (!actorScope.Resolver.TryResolve<ICommandRunner>(out runner) || runner == null)
            {
                var ownerResolver = _owner.Resolver;
                if (ownerResolver == null || !ownerResolver.TryResolve<ICommandRunner>(out runner) || runner == null)
                    return;
            }

            var vars = new VarStore();
            if (actorScope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                blackboard.MergeInto(vars, overwrite: true);

            SelectRuntimeVarKeys.WriteSelectionState(vars, selected, hovered, editing);

            var context = new CommandContext(actorScope, vars, runner, actorScope, CommandRunOptions.Default, _owner, actorScope, actorScope);
            UniTask.Void(async () =>
            {
                try
                {
                    await runner.ExecuteListAsync(commands, context, CancellationToken.None, context.Options);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UserMoveRotateRuntime] Editor command execution failed: {ex.Message}");
                }
            });
        }

        void ApplyMovement(InputFrame frame, UserMoveRotateValidationRequest request, Vector3 currentPosition, Quaternion currentRotation, ref Vector3 candidatePosition)
        {
            if (_session.Editor == null)
                return;

            var moveMode = _session.Editor.MoveSourceMode;
            var usePointer = ShouldUsePointerFollow(frame, moveMode);

            if (usePointer)
            {
                var pointerScreen = frame.PointerScreen;
                var useRelativePointer = frame.PointerDelta != Vector2.zero && Vector2.Distance(pointerScreen, _lastPointerScreen) <= 0.01f;
                if (useRelativePointer)
                    pointerScreen += frame.PointerDelta;

                if (UserMoveRotateValidationUtility.TryProjectPointerPosition(
                        request,
                        _pointerOptions.WorldCamera,
                        pointerScreen,
                        currentPosition,
                        out var projectedPosition))
                {
                    if (TryClampToNearestValidPosition(request, projectedPosition, currentRotation, out candidatePosition))
                        return;
                }
            }

            if (moveMode == UserMoveSourceMode.PointerFollow)
                return;

            if (frame.Move == Vector2.zero)
                return;

            var moveDelta = frame.Move * _session.Editor.InputMoveSpeed * frame.DeltaTime;
            var movedPosition = request.Editor.FallbackPlane == Channel.AreaPlane.XZ
                ? currentPosition + new Vector3(moveDelta.x, 0f, moveDelta.y)
                : currentPosition + new Vector3(moveDelta.x, moveDelta.y, 0f);

            if (TryClampToNearestValidPosition(request, movedPosition, currentRotation, out candidatePosition))
                return;
        }

        static bool TryClampToNearestValidPosition(
            UserMoveRotateValidationRequest request,
            Vector3 requestedPosition,
            Quaternion requestedRotation,
            out Vector3 correctedPosition)
        {
            correctedPosition = requestedPosition;

            if (!request.IsValid)
                return true;

            if (UserMoveRotateValidationUtility.IsValidPose(request, requestedPosition, requestedRotation))
                return true;

            if (!UserMoveRotateValidationUtility.TryFindNearestValidPose(
                    request,
                    requestedPosition,
                    requestedRotation,
                    out correctedPosition,
                    out _))
            {
                return false;
            }

            return true;
        }

        bool ShouldUsePointerFollow(InputFrame frame, UserMoveSourceMode moveMode)
        {
            if (moveMode == UserMoveSourceMode.PointerFollow)
                return true;

            if (moveMode != UserMoveSourceMode.Hybrid)
                return false;

            if (frame.Scheme == Game.Input.ControlScheme.Mouse || frame.Scheme == Game.Input.ControlScheme.Touch)
                return true;

            return _pointerActivity.HasRecentPointerActivity();
        }

        void ApplyRotation(
            InputFrame frame,
            UserMoveRotateValidationRequest request,
            float currentDegrees,
            ref Quaternion candidateRotation,
            out float nextDegrees)
        {
            nextDegrees = currentDegrees;

            if (request.Editor == null || frame.Scroll == Vector2.zero)
                return;

            var degrees = frame.Scroll.y * request.Editor.RotateDegreesPerScroll;
            if (Mathf.Approximately(degrees, 0f))
                return;

            nextDegrees = Mathf.Repeat(currentDegrees + degrees, 360f);
            candidateRotation = ApplyRotationDegrees(_session.RotationPlane, candidateRotation, nextDegrees);
        }

        void EndSession(bool runExitCommands)
        {
            if (!_session.IsActive || _session.Editor == null)
            {
                _rotateBinding.Release();
                _isEditorModeBinding.Release();
                _session.Clear();
                return;
            }

            if (runExitCommands)
            {
                ExecuteEditorCommands(
                    _session.Editor,
                    _session.Editor.OnEditorExitCommands,
                    selected: false,
                    hovered: false,
                    editing: false);
            }

            SyncIsEditorModeBinding(false);
            _rotateBinding.Release();
            _isEditorModeBinding.Release();
            _session.Clear();
        }

        void BindRotateExternal(UserMoveRotateRuntimeMB editor, KernelScopeHost runtimeScope)
        {
            _rotateBinding.Acquire(runtimeScope, editor.RotateBinding, onValueChanged: null);
            SyncRotateBinding();
        }

        void BindIsEditorModeExternal(UserMoveRotateRuntimeMB editor, KernelScopeHost runtimeScope)
        {
            _isEditorModeBinding.Acquire(runtimeScope, editor.IsEditorModeBinding);
        }

        void SyncIsEditorModeBinding(bool isEditing)
        {
            if (!_isEditorModeBinding.HasBinding)
                return;

            _isEditorModeBinding.Write(isEditing);
        }

        void SyncRotateBinding()
        {
            if (!_rotateBinding.HasBinding)
                return;

            _rotateBinding.Write(_session.RotationDegrees);
        }

        void ApplySessionPose(Vector3 position, Quaternion rotation)
        {
            if (_session.MoveTransform == null || _session.RotateTransform == null)
                return;

            if (ReferenceEquals(_session.MoveTransform, _session.RotateTransform))
            {
                _session.MoveTransform.SetPositionAndRotation(position, rotation);
                return;
            }

            _session.MoveTransform.position = position;
            _session.RotateTransform.rotation = rotation;
        }

        static float ExtractRotationDegrees(Channel.AreaPlane plane, Quaternion rotation)
        {
            var euler = rotation.eulerAngles;
            var degrees = plane == Channel.AreaPlane.XZ ? euler.y : euler.z;
            return Mathf.Repeat(degrees, 360f);
        }

        static Quaternion ApplyRotationDegrees(
            Channel.AreaPlane plane,
            Quaternion currentRotation,
            float absoluteDegrees)
        {
            var euler = currentRotation.eulerAngles;
            var normalizedDegrees = Mathf.Repeat(absoluteDegrees, 360f);
            if (plane == Channel.AreaPlane.XZ)
                euler.y = normalizedDegrees;
            else
                euler.z = normalizedDegrees;

            return Quaternion.Euler(euler);
        }
    }
}


