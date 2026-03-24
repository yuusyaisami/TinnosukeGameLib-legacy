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
        sealed class EditSession
        {
            public UserMoveRotateRuntimeMB? Editor;
            public RuntimeLifetimeScope? RuntimeScope;
            public Transform? RootTransform;
            public int StartedFrame;
            public Vector3 LastValidPosition;
            public Quaternion LastValidRotation = Quaternion.identity;

            public bool IsActive => Editor != null && RuntimeScope != null && RootTransform != null;

            public void Clear()
            {
                Editor = null;
                RuntimeScope = null;
                RootTransform = null;
                StartedFrame = 0;
                LastValidPosition = Vector3.zero;
                LastValidRotation = Quaternion.identity;
            }
        }

        sealed class EntryPressState
        {
            public UserMoveRotateRuntimeMB? Editor;
            public WorldPointerTargetMB? Target;
            public float PressedTime;
            public WorldPointerEventData PressedData;

            public bool IsActive => Editor != null && Target != null;

            public void Clear()
            {
                Editor = null;
                Target = null;
                PressedTime = 0f;
                PressedData = default;
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

            TryEnterEditorMode(editor, runtimeScopeOverride: null, fromSelectableLongPress: true, _hoveredData);
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
            if (!enabled)
                EndSession(runExitCommands: true);
        }

        void HandleHoveredChanged(WorldPointerHoverChangedEventData eventData)
        {
            _hoveredTarget = eventData.CurrentTarget;
            _hoveredData = eventData.EventData;

            if (_entryPress.IsActive && !ReferenceEquals(_entryPress.Target, _hoveredTarget))
                _entryPress.Clear();
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
            if (!_session.IsActive || _session.Editor == null || _session.RuntimeScope == null || _session.RootTransform == null)
                return;

            if (!_managerService.EvaluateIsEnabled())
            {
                EndSession(runExitCommands: true);
                return;
            }

            var request = UserMoveRotateValidationRequest.Create(_session.Editor, _session.RuntimeScope);
            var currentPosition = _session.RootTransform.position;
            var currentRotation = _session.RootTransform.rotation;
            var candidatePosition = currentPosition;
            var candidateRotation = currentRotation;

            ApplyRotation(frame, request, currentPosition, ref candidateRotation);
            ApplyMovement(frame, request, currentPosition, currentRotation, ref candidatePosition);

            _lastPointerScreen = frame.PointerScreen;

            if (candidatePosition == currentPosition && candidateRotation == currentRotation)
                return;

            if (!UserMoveRotateValidationUtility.IsValidPose(request, candidatePosition, candidateRotation))
                return;

            _session.RootTransform.SetPositionAndRotation(candidatePosition, candidateRotation);
            _session.LastValidPosition = candidatePosition;
            _session.LastValidRotation = candidateRotation;
            SyncRotateBinding(request, candidateRotation);
        }

        void HandleEditorEntryFrame(InputFrame frame)
        {
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
                _entryPress.Clear();
                return;
            }

            if (!frame.PointerLeft.Held)
            {
                if (frame.PointerLeft.Up)
                    _entryPress.Clear();
                return;
            }

            if (!ReferenceEquals(_entryPress.Target, _hoveredTarget))
            {
                LogEditorEntryFailure(editor, "Pointer long press canceled: target changed before threshold.");
                _entryPress.Clear();
                return;
            }

            if (Time.unscaledTime - _entryPress.PressedTime < editor.EditorLongPressSeconds)
                return;

            if (!TryEnterEditorMode(editor, runtimeScopeOverride: null, fromSelectableLongPress: false, _entryPress.PressedData))
                LogEditorEntryFailure(editor, "Pointer long press reached threshold but editor mode entry failed.");
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

            _entryPress.Editor = editor;
            _entryPress.Target = _hoveredTarget;
            _entryPress.PressedTime = Time.unscaledTime;
            _entryPress.PressedData = _hoveredData.Target != null ? _hoveredData : new WorldPointerEventData(_hoveredTarget, _hoveredData.ScreenPosition, _hoveredData.WorldPosition, _hoveredData.HitNormal, _hoveredData.HitCollider);

        }

        bool TryEnterEditorMode(
            UserMoveRotateRuntimeMB editor,
            RuntimeLifetimeScope? runtimeScopeOverride,
            bool fromSelectableLongPress,
            WorldPointerEventData eventData)
        {
            if (editor == null)
                return false;

            if (ReferenceEquals(_session.Editor, editor))
                return true;

            if (fromSelectableLongPress && !CanEnterFromSelectable(editor))
            {
                LogEditorEntryFailure(editor, $"Selectable entry blocked by source {editor.EditorEntrySource}.");
                return false;
            }

            if (!fromSelectableLongPress && !CanEnterFromPointer(editor))
            {
                LogEditorEntryFailure(editor, $"Pointer entry blocked by source {editor.EditorEntrySource}.");
                return false;
            }

            if (!editor.TryResolveActorScope(out var scope) || scope is not RuntimeLifetimeScope runtimeScope)
            {
                LogEditorEntryFailure(editor, "Could not resolve RuntimeLifetimeScope from editor actor scope.");
                return false;
            }

            if (runtimeScopeOverride != null && !ReferenceEquals(runtimeScope, runtimeScopeOverride))
                runtimeScope = runtimeScopeOverride;

            EndSession(runExitCommands: true);

            var rootTransform = runtimeScope.Identity?.SelfTransform != null
                ? runtimeScope.Identity.SelfTransform
                : runtimeScope.transform;
            _session.Editor = editor;
            _session.RuntimeScope = runtimeScope;
            _session.RootTransform = rootTransform;
            _session.StartedFrame = Time.frameCount;
            _session.LastValidPosition = rootTransform.position;
            _session.LastValidRotation = rootTransform.rotation;

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

        static string DescribeRuntime(RuntimeLifetimeScope? runtime)
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

        void ApplyRotation(InputFrame frame, UserMoveRotateValidationRequest request, Vector3 currentPosition, ref Quaternion candidateRotation)
        {
            if (request.Editor == null || frame.Scroll == Vector2.zero)
                return;

            var degrees = frame.Scroll.y * request.Editor.RotateDegreesPerScroll;
            if (Mathf.Approximately(degrees, 0f))
                return;

            var currentDegrees = ExtractRotationDegrees(request, currentPosition, candidateRotation);
            candidateRotation = ApplyRotationDegrees(request, currentPosition, candidateRotation, currentDegrees + degrees);
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

        void BindRotateExternal(UserMoveRotateRuntimeMB editor, RuntimeLifetimeScope runtimeScope)
        {
            _rotateBinding.Acquire(runtimeScope, editor.RotateBinding, HandleRotateBindingChanged);

            var request = UserMoveRotateValidationRequest.Create(editor, runtimeScope);
            if (_rotateBinding.TryRead(out var externalDegrees))
            {
                if (TryApplyExternalRotation(request, externalDegrees))
                    return;
            }

            SyncRotateBinding(request, request.RootTransform.rotation);
        }

        void BindIsEditorModeExternal(UserMoveRotateRuntimeMB editor, RuntimeLifetimeScope runtimeScope)
        {
            _isEditorModeBinding.Acquire(runtimeScope, editor.IsEditorModeBinding);
        }

        void SyncIsEditorModeBinding(bool isEditing)
        {
            if (!_isEditorModeBinding.HasBinding)
                return;

            _isEditorModeBinding.Write(isEditing);
        }

        void HandleRotateBindingChanged(float value)
        {
            if (!_session.IsActive || _session.Editor == null || _session.RuntimeScope == null)
                return;

            var request = UserMoveRotateValidationRequest.Create(_session.Editor, _session.RuntimeScope);
            TryApplyExternalRotation(request, value);
        }

        bool TryApplyExternalRotation(UserMoveRotateValidationRequest request, float absoluteDegrees)
        {
            if (!_session.IsActive || _session.RootTransform == null)
                return false;

            var currentPosition = _session.RootTransform.position;
            var currentRotation = _session.RootTransform.rotation;
            var candidateRotation = ApplyRotationDegrees(request, currentPosition, currentRotation, absoluteDegrees);
            if (Quaternion.Angle(currentRotation, candidateRotation) <= 0.0001f)
                return true;

            if (!UserMoveRotateValidationUtility.IsValidPose(request, currentPosition, candidateRotation))
                return false;

            _session.RootTransform.SetPositionAndRotation(currentPosition, candidateRotation);
            _session.LastValidPosition = currentPosition;
            _session.LastValidRotation = candidateRotation;
            return true;
        }

        void SyncRotateBinding(UserMoveRotateValidationRequest request, Quaternion rotation)
        {
            if (!_rotateBinding.HasBinding)
                return;

            var degrees = ExtractRotationDegrees(request, request.RootTransform.position, rotation);
            _rotateBinding.Write(degrees);
        }

        static float ExtractRotationDegrees(UserMoveRotateValidationRequest request, Vector3 currentPosition, Quaternion rotation)
        {
            var euler = rotation.eulerAngles;
            var plane = UserMoveRotateValidationUtility.ResolvePlane(request, currentPosition);
            var degrees = plane == Channel.AreaPlane.XZ ? euler.y : euler.z;
            return Mathf.Repeat(degrees, 360f);
        }

        static Quaternion ApplyRotationDegrees(
            UserMoveRotateValidationRequest request,
            Vector3 currentPosition,
            Quaternion currentRotation,
            float absoluteDegrees)
        {
            var euler = currentRotation.eulerAngles;
            var plane = UserMoveRotateValidationUtility.ResolvePlane(request, currentPosition);
            var normalizedDegrees = Mathf.Repeat(absoluteDegrees, 360f);
            if (plane == Channel.AreaPlane.XZ)
                euler.y = normalizedDegrees;
            else
                euler.z = normalizedDegrees;

            return Quaternion.Euler(euler);
        }
    }
}
