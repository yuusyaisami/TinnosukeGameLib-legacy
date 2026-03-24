#nullable enable
using System;
using System.Collections.Generic;
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

        readonly IScopeNode _owner;
        readonly IWorldPointerRuntimeService _pointerService;
        readonly ISelectRuntimeManagerService _managerService;
        readonly IWorldPointerRuntimeOptions _pointerOptions;
        readonly IPointerService _pointerActivity;

        readonly HashSet<UserMoveRotateRuntimeMB> _editors = new();
        readonly Dictionary<WorldPointerTargetMB, UserMoveRotateRuntimeMB> _editorByTarget = new();
        readonly EditSession _session = new();
        readonly ExternalFloatBindingRuntime _rotateBinding = new();

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
            _pointerService.OnLeftClicked += HandleLeftClicked;
            _pointerService.OnRightClicked += HandleRightClicked;
            _pointerService.OnFrameUpdated += HandleFrameUpdated;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _managerService.OnLeftLongPressSelectable -= HandleLongPressSelectable;
            _managerService.OnSelectionChanged -= HandleSelectionChanged;
            _managerService.OnEnabledChanged -= HandleEnabledChanged;
            _pointerService.OnLeftClicked -= HandleLeftClicked;
            _pointerService.OnRightClicked -= HandleRightClicked;
            _pointerService.OnFrameUpdated -= HandleFrameUpdated;

            EndSession(runExitCommands: false);
            _rotateBinding.Release();
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
                return;

            if (!editor.TryResolveActorScope(out var scope) || scope is not RuntimeLifetimeScope runtimeScope)
                return;

            if (ReferenceEquals(_session.Editor, editor))
                return;

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

            if (_managerService.Current != null)
                SelectRuntimeCommandUtility.Execute(_managerService.Current, _owner, selected: true, hovered: ReferenceEquals(_managerService.Hovered, _managerService.Current), editing: true, editor.OnEditorEnterCommands);
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
            ApplyMovement(frame, request, currentPosition, ref candidatePosition);

            if (candidatePosition == currentPosition && candidateRotation == currentRotation)
                return;

            if (!UserMoveRotateValidationUtility.IsValidPose(request, candidatePosition, candidateRotation))
                return;

            _session.RootTransform.SetPositionAndRotation(candidatePosition, candidateRotation);
            _session.LastValidPosition = candidatePosition;
            _session.LastValidRotation = candidateRotation;
            SyncRotateBinding(request, candidateRotation);
        }

        void ApplyMovement(InputFrame frame, UserMoveRotateValidationRequest request, Vector3 currentPosition, ref Vector3 candidatePosition)
        {
            if (_session.Editor == null)
                return;

            var moveMode = _session.Editor.MoveSourceMode;
            var usePointer = moveMode == UserMoveSourceMode.PointerFollow
                || (moveMode == UserMoveSourceMode.Hybrid && _pointerActivity.HasRecentPointerActivity());

            if (usePointer)
            {
                if (UserMoveRotateValidationUtility.TryProjectPointerPosition(
                        request,
                        _pointerOptions.WorldCamera,
                        frame.PointerScreen,
                        currentPosition,
                        out var projectedPosition))
                {
                    candidatePosition = projectedPosition;
                    return;
                }
            }

            if (moveMode == UserMoveSourceMode.PointerFollow)
                return;

            if (frame.Move == Vector2.zero)
                return;

            var moveDelta = frame.Move * _session.Editor.InputMoveSpeed * frame.DeltaTime;
            candidatePosition = request.Editor.FallbackPlane == Channel.AreaPlane.XZ
                ? currentPosition + new Vector3(moveDelta.x, 0f, moveDelta.y)
                : currentPosition + new Vector3(moveDelta.x, moveDelta.y, 0f);
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
                _session.Clear();
                return;
            }

            if (runExitCommands && _managerService.Current != null)
            {
                SelectRuntimeCommandUtility.Execute(
                    _managerService.Current,
                    _owner,
                    selected: true,
                    hovered: ReferenceEquals(_managerService.Hovered, _managerService.Current),
                    editing: false,
                    _session.Editor.OnEditorExitCommands);
            }

            _rotateBinding.Release();
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
