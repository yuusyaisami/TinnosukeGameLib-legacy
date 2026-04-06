#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Input;
using Game.SelectRuntime;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public sealed class PointerTiltSwipeService :
        IScopeAcquireHandler,
        IScopeReleaseHandler,
        ITickable,
        IUIInputPreviewObserver
    {
        const float Epsilon = 0.0001f;
        const float AngleEpsilon = 0.01f;
        const int PreviewPriorityValue = 230;

        readonly IScopeNode _owner;
        readonly PointerTiltSwipeMB _mb;

        IScopeNode? _activeScope;
        ICommandRunner? _commandRunner;

        Transform? _targetTransform;
        ITransformAnimationHubService? _transformHub;
        ITransformAnimationChannelPlayer? _rotationPlayer;
        ITransformAnimationChannelPlayer? _positionPlayer;

        PointerTiltEnvironmentMode _resolvedEnvironmentMode;

        Canvas? _uiCanvas;
        Camera? _uiCamera;
        IUIInputRoutingHub? _uiInputRoutingHub;
        IUISelectionState? _uiSelectionState;

        WorldPointerTargetMB? _worldPointerTarget;
        SelectRuntimeManagerMB? _worldManager;
        IWorldPointerRuntimeService? _worldPointerService;
        IWorldPointerRuntimeOptions? _worldPointerOptions;
        Transform? _lastOwnerParent;
        bool _isWorldHovered;
        WorldPointerEventData _worldHoverData;
        bool _hasWorldHoverData;

        Vector2 _lastPointerScreen;
        bool _hasPointerScreen;
        Vector2 _currentPointerLocal;
        bool _hasPointerLocal;

        bool _submitDownFrame;
        bool _submitHeld;
        bool _submitUpFrame;
        bool _uiHeldSignalReceivedFrame;

        bool _isInteractionActive;
        bool _wasInteractionActive;

        Vector3 _baselineEuler;
        Vector3 _lastTiltTargetEuler;
        bool _hasTiltApplied;

        PointerTiltSwipeState _swipeState = PointerTiltSwipeState.Idle;
        Vector2 _swipePressLocal;
        Vector3 _swipeBaseLocalPosition;

        Transform? _positionFollowTarget;
        CancellationTokenSource? _positionFollowCts;
        bool _positionFollowRunning;

        bool _positionReturnActive;
        float _positionReturnElapsed;
        Vector3 _positionReturnFromLocal;
        Vector3 _positionReturnToLocal;

        bool _warnedChannelCollision;

        public int Priority => PreviewPriorityValue;

        public PointerTiltSwipeService(IScopeNode owner, PointerTiltSwipeMB mb)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;
            _activeScope = scope;
            _targetTransform = _mb.TargetTransform != null
                ? _mb.TargetTransform
                : scope.Identity?.SelfTransform;

            _commandRunner = ResolveCommandRunner(scope);
            ResolveTransformPlayers(scope);
            ResolveEnvironment(scope);

            ResetFrameInputFlags();
            ResetRuntimeState();

            if (_targetTransform == null)
                Debug.LogWarning("[PointerTiltSwipe] Target transform is missing. Set Target Transform or ensure scope identity exists.", _mb);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_owner, scope))
                return;

            _ = isReset;

            UnbindUI();
            UnbindWorld();
            StopPositionFollow();
            DestroyPositionFollowTarget();

            _activeScope = null;
            _commandRunner = null;
            _targetTransform = null;
            _transformHub = null;
            _rotationPlayer = null;
            _positionPlayer = null;
            _worldPointerOptions = null;

            _isInteractionActive = false;
            _wasInteractionActive = false;
            _hasPointerLocal = false;
            _hasPointerScreen = false;
            _swipeState = PointerTiltSwipeState.Idle;
            _isWorldHovered = false;
            _hasWorldHoverData = false;
            _worldHoverData = default;
            _lastOwnerParent = null;
            _warnedChannelCollision = false;

            ResetFrameInputFlags();
        }

        public void Tick()
        {
            if (_activeScope == null || _targetTransform == null)
            {
                FinalizeFrameInputState();
                return;
            }

            if (_resolvedEnvironmentMode == PointerTiltEnvironmentMode.World &&
                _lastOwnerParent != _mb.transform.parent)
            {
                RebindWorld();
            }

            UpdateInteractionActive();

            if (_isInteractionActive)
            {
                if (!_wasInteractionActive)
                    HandleInteractionEnter();

                UpdatePointerLocal();
                UpdateTilt();
            }
            else
            {
                if (_wasInteractionActive)
                    HandleInteractionExit();
            }

            if (_mb.EnableSwipe)
                UpdateSwipe();

            UpdatePositionReturn();

            _wasInteractionActive = _isInteractionActive;
            FinalizeFrameInputState();
        }

        public void Observe(in UIInputEvent inputEvent)
        {
            if (_resolvedEnvironmentMode != PointerTiltEnvironmentMode.ScreenUI)
                return;

            switch (inputEvent.Type)
            {
                case UIInputEventType.PointerMove:
                    _lastPointerScreen = inputEvent.PointerPosition;
                    _hasPointerScreen = true;
                    break;

                case UIInputEventType.SubmitDown:
                    _lastPointerScreen = inputEvent.PointerPosition;
                    _hasPointerScreen = true;
                    _submitDownFrame = true;
                    _submitHeld = true;
                    _uiHeldSignalReceivedFrame = true;
                    break;

                case UIInputEventType.SubmitHeld:
                    _lastPointerScreen = inputEvent.PointerPosition;
                    _hasPointerScreen = true;
                    _submitHeld = true;
                    _uiHeldSignalReceivedFrame = true;
                    break;

                case UIInputEventType.SubmitUp:
                    _lastPointerScreen = inputEvent.PointerPosition;
                    _hasPointerScreen = true;
                    _submitUpFrame = true;
                    _submitHeld = false;
                    _uiHeldSignalReceivedFrame = true;
                    break;

                case UIInputEventType.CancelDown:
                    _submitHeld = false;
                    _submitUpFrame = true;
                    _uiHeldSignalReceivedFrame = true;
                    break;
            }
        }

        void ResolveTransformPlayers(IScopeNode scope)
        {
            _transformHub = null;
            _rotationPlayer = null;
            _positionPlayer = null;

            if (scope.TryResolveInAncestors<ITransformAnimationHubService>(out var ancestorHub) && ancestorHub != null)
            {
                _transformHub = ancestorHub;
            }
            else if (scope.Resolver != null && scope.Resolver.TryResolve<ITransformAnimationHubService>(out var scopedHub) && scopedHub != null)
            {
                _transformHub = scopedHub;
            }

            if (_transformHub == null)
            {
                Debug.LogWarning("[PointerTiltSwipe] ITransformAnimationHubService is not resolved.", _mb);
                return;
            }

            var rotationTag = _mb.RotationChannelTag;
            if (_transformHub.TryGetPlayer(rotationTag, out var resolvedRotationPlayer) && resolvedRotationPlayer != null)
                _rotationPlayer = resolvedRotationPlayer;
            else
                Debug.LogWarning($"[PointerTiltSwipe] Rotation channel '{rotationTag}' is not found.", _mb);

            var positionTag = _mb.PositionChannelTag;
            if (_transformHub.TryGetPlayer(positionTag, out var resolvedPositionPlayer) && resolvedPositionPlayer != null)
                _positionPlayer = resolvedPositionPlayer;
            else if (_mb.EnableSwipe)
                Debug.LogWarning($"[PointerTiltSwipe] Position channel '{positionTag}' is not found.", _mb);

            if (!_warnedChannelCollision &&
                _mb.EnableSwipe &&
                string.Equals(rotationTag, positionTag, StringComparison.Ordinal))
            {
                _warnedChannelCollision = true;
                Debug.LogWarning("[PointerTiltSwipe] Rotation and Position channel tags are identical. Swipe start may momentarily reset rotate track. Using separate tags is recommended.", _mb);
            }
        }

        void ResolveEnvironment(IScopeNode scope)
        {
            var configured = _mb.EnvironmentMode;
            if (configured == PointerTiltEnvironmentMode.Auto)
            {
                var canvas = _mb.transform.GetComponentInParent<Canvas>(true);
                if (canvas != null &&
                    (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera))
                {
                    configured = PointerTiltEnvironmentMode.ScreenUI;
                }
                else
                {
                    configured = PointerTiltEnvironmentMode.World;
                }
            }

            _resolvedEnvironmentMode = configured;

            if (_resolvedEnvironmentMode == PointerTiltEnvironmentMode.ScreenUI)
            {
                BindUI(scope);
                return;
            }

            BindWorld(scope);
        }

        void BindUI(IScopeNode scope)
        {
            _uiCanvas = _mb.transform.GetComponentInParent<Canvas>(true);
            _uiCamera = _uiCanvas != null && _uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _uiCanvas.worldCamera
                : null;

            scope.TryResolveInAncestors<IUIInputRoutingHub>(out _uiInputRoutingHub);
            scope.TryResolveInAncestors<IUISelectionState>(out _uiSelectionState);

            _uiInputRoutingHub?.RegisterPreview(_owner, this);
        }

        void UnbindUI()
        {
            if (_uiInputRoutingHub != null)
                _uiInputRoutingHub.UnregisterPreview(_owner, this);

            _uiInputRoutingHub = null;
            _uiSelectionState = null;
            _uiCanvas = null;
            _uiCamera = null;
        }

        void BindWorld(IScopeNode scope)
        {
            _worldPointerTarget = _mb.ResolveWorldPointerTarget();
            if (_worldPointerTarget == null)
            {
                Debug.LogWarning("[PointerTiltSwipe] World mode requires WorldPointerTargetMB. Could not resolve target.", _mb);
                return;
            }

            scope.TryResolveInAncestors<IWorldPointerRuntimeOptions>(out _worldPointerOptions);
            RebindWorld();
        }

        void RebindWorld()
        {
            var nextManager = SelectRuntimeBridgeResolver.FindNearestManager(_mb.transform);
            if (ReferenceEquals(_worldManager, nextManager) && _worldPointerService != null)
            {
                _lastOwnerParent = _mb.transform.parent;
                return;
            }

            UnbindWorld();

            _worldManager = nextManager;
            _lastOwnerParent = _mb.transform.parent;
            if (!SelectRuntimeBridgeResolver.TryResolvePointerService(_worldManager, out var pointerService) || pointerService == null)
                return;

            _worldPointerService = pointerService;
            _worldPointerService.OnHoveredChanged += HandleWorldHoveredChanged;
            _worldPointerService.OnFrameUpdated += HandleWorldFrameUpdated;

            if (_worldPointerService.TryGetCurrentHover(out var hoverData))
            {
                _worldHoverData = hoverData;
                _hasWorldHoverData = true;
                _isWorldHovered = _worldPointerTarget != null && ReferenceEquals(hoverData.Target, _worldPointerTarget);
            }
            else
            {
                _worldHoverData = default;
                _hasWorldHoverData = false;
                _isWorldHovered = false;
            }
        }

        void UnbindWorld()
        {
            if (_worldPointerService != null)
            {
                _worldPointerService.OnHoveredChanged -= HandleWorldHoveredChanged;
                _worldPointerService.OnFrameUpdated -= HandleWorldFrameUpdated;
            }

            _worldPointerService = null;
            _worldManager = null;
            _worldPointerTarget = null;
            _isWorldHovered = false;
            _hasWorldHoverData = false;
            _worldHoverData = default;
        }

        void HandleWorldHoveredChanged(WorldPointerHoverChangedEventData eventData)
        {
            _worldHoverData = eventData.EventData;
            _hasWorldHoverData = eventData.CurrentTarget != null;

            if (_worldPointerTarget == null)
            {
                _isWorldHovered = false;
                return;
            }

            _isWorldHovered = ReferenceEquals(eventData.CurrentTarget, _worldPointerTarget);
        }

        void HandleWorldFrameUpdated(InputFrame frame)
        {
            _lastPointerScreen = frame.PointerScreen;
            _hasPointerScreen = true;
            _submitDownFrame |= frame.PointerLeft.Down;
            _submitUpFrame |= frame.PointerLeft.Up;
            _submitHeld = frame.PointerLeft.Held;

            if (_worldPointerService != null && _worldPointerService.TryGetCurrentHover(out var hoverData))
            {
                _worldHoverData = hoverData;
                _hasWorldHoverData = true;
                _isWorldHovered = _worldPointerTarget != null && ReferenceEquals(hoverData.Target, _worldPointerTarget);
            }
            else
            {
                _hasWorldHoverData = false;
                _isWorldHovered = false;
            }
        }

        void UpdateInteractionActive()
        {
            if (!_mb.IsEnabled)
            {
                _isInteractionActive = false;
                return;
            }

            switch (_resolvedEnvironmentMode)
            {
                case PointerTiltEnvironmentMode.ScreenUI:
                    _isInteractionActive = _uiSelectionState != null && ReferenceEquals(_uiSelectionState.CurrentElement, _owner);
                    break;
                case PointerTiltEnvironmentMode.World:
                    _isInteractionActive = _worldPointerTarget != null && _isWorldHovered;
                    break;
                default:
                    _isInteractionActive = false;
                    break;
            }
        }

        void HandleInteractionEnter()
        {
            if (_targetTransform == null)
                return;

            _baselineEuler = _targetTransform.localEulerAngles;
        }

        void HandleInteractionExit()
        {
            if (_rotationPlayer != null && _hasTiltApplied)
                _rotationPlayer.ApplyRotateAngle(_baselineEuler, _mb.TiltReturnDuration, 0f);

            _hasTiltApplied = false;

            if (_swipeState != PointerTiltSwipeState.Idle)
                EndSwipeWithReturn();
        }

        void UpdatePointerLocal()
        {
            if (TryResolvePointerLocal(out var local))
            {
                _currentPointerLocal = local;
                _hasPointerLocal = true;
            }
        }

        bool TryResolvePointerLocal(out Vector2 local)
        {
            local = default;
            if (!_hasPointerScreen || _targetTransform == null)
                return false;

            if (_resolvedEnvironmentMode == PointerTiltEnvironmentMode.ScreenUI)
            {
                if (_targetTransform is RectTransform rectTransform &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, _lastPointerScreen, _uiCamera, out local))
                {
                    return true;
                }
            }

            if (_resolvedEnvironmentMode == PointerTiltEnvironmentMode.World &&
                _worldPointerTarget != null &&
                _hasWorldHoverData &&
                ReferenceEquals(_worldHoverData.Target, _worldPointerTarget))
            {
                var local3 = _targetTransform.InverseTransformPoint(_worldHoverData.WorldPosition);
                local = new Vector2(local3.x, local3.y);
                return true;
            }

            var fallbackCamera = ResolveFallbackCamera();
            if (fallbackCamera == null)
                return false;

            Vector3 worldPoint;
            if (fallbackCamera.orthographic)
            {
                worldPoint = fallbackCamera.ScreenToWorldPoint(new Vector3(_lastPointerScreen.x, _lastPointerScreen.y, 0f));
                worldPoint.z = _targetTransform.position.z;
            }
            else
            {
                var depth = Mathf.Abs(_targetTransform.position.z - fallbackCamera.transform.position.z);
                worldPoint = fallbackCamera.ScreenToWorldPoint(new Vector3(_lastPointerScreen.x, _lastPointerScreen.y, depth));
                worldPoint.z = _targetTransform.position.z;
            }

            var fallbackLocal = _targetTransform.InverseTransformPoint(worldPoint);
            local = new Vector2(fallbackLocal.x, fallbackLocal.y);
            return true;
        }

        Camera? ResolveFallbackCamera()
        {
            if (_resolvedEnvironmentMode == PointerTiltEnvironmentMode.ScreenUI)
                return _uiCamera != null ? _uiCamera : Camera.main;

            return _worldPointerOptions?.WorldCamera != null
                ? _worldPointerOptions.WorldCamera
                : Camera.main;
        }

        void UpdateTilt()
        {
            if (!_mb.EnableTilt || _rotationPlayer == null || !_hasPointerLocal)
                return;

            var nx = Mathf.Clamp(_currentPointerLocal.x / _mb.TiltLocalRangeX, -1f, 1f);
            var ny = Mathf.Clamp(_currentPointerLocal.y / _mb.TiltLocalRangeY, -1f, 1f);

            if (_mb.InvertTiltX)
                nx = -nx;
            if (_mb.InvertTiltY)
                ny = -ny;

            var targetEuler = _baselineEuler + new Vector3(
                ny * _mb.MaxTiltAngleX,
                nx * _mb.MaxTiltAngleY,
                0f);

            if (!_hasTiltApplied || HasEulerChanged(targetEuler, _lastTiltTargetEuler, AngleEpsilon))
            {
                _rotationPlayer.ApplyRotateAngle(targetEuler, _mb.TiltApplySmoothTime, 0f);
                _lastTiltTargetEuler = targetEuler;
                _hasTiltApplied = true;
            }
        }

        static bool HasEulerChanged(in Vector3 a, in Vector3 b, float epsilon)
        {
            return Mathf.Abs(Mathf.DeltaAngle(a.x, b.x)) > epsilon ||
                   Mathf.Abs(Mathf.DeltaAngle(a.y, b.y)) > epsilon ||
                   Mathf.Abs(Mathf.DeltaAngle(a.z, b.z)) > epsilon;
        }

        void UpdateSwipe()
        {
            if (!_isInteractionActive)
            {
                if (_swipeState != PointerTiltSwipeState.Idle)
                    EndSwipeWithReturn();
                return;
            }

            if (_submitDownFrame)
                BeginSwipe();

            if (_swipeState != PointerTiltSwipeState.Idle && _submitHeld)
                UpdateSwipeProgress();

            if (_swipeState != PointerTiltSwipeState.Idle && _submitUpFrame)
                EndSwipeWithReturn();

            if (_swipeState != PointerTiltSwipeState.Idle && !_submitHeld && !_submitDownFrame && !_submitUpFrame)
                EndSwipeWithReturn();
        }

        void BeginSwipe()
        {
            if (_targetTransform == null)
                return;

            Vector2 resolvedLocal = default;
            if (!_hasPointerLocal)
            {
                if (!TryResolvePointerLocal(out resolvedLocal))
                    return;
            }

            var pressLocal = _hasPointerLocal ? _currentPointerLocal : resolvedLocal;

            _swipeState = PointerTiltSwipeState.PressedCandidate;
            _swipePressLocal = pressLocal;
            _swipeBaseLocalPosition = _targetTransform.localPosition;

            CancelPositionReturn();
            ExecuteCommandList(_mb.OnSwipeCandidateStartedCommands);

            EnsurePositionFollowStarted();
            SetFollowTargetLocal(_swipeBaseLocalPosition);
        }

        void UpdateSwipeProgress()
        {
            if (_targetTransform == null)
                return;

            Vector2 resolvedLocal = default;
            if (!_hasPointerLocal)
            {
                if (!TryResolvePointerLocal(out resolvedLocal))
                    return;
            }

            var currentLocal = _hasPointerLocal ? _currentPointerLocal : resolvedLocal;
            var delta = currentLocal - _swipePressLocal;

            if (_swipeState == PointerTiltSwipeState.PressedCandidate)
            {
                var threshold = _mb.SwipeThresholdLocalDistance;
                if (threshold <= 0f || delta.sqrMagnitude >= threshold * threshold)
                {
                    _swipeState = PointerTiltSwipeState.ThresholdReached;
                    ExecuteCommandList(_mb.OnSwipeThresholdReachedCommands);
                    BeginPositionReturn();
                    return;
                }

                ApplyPreThresholdOffset(delta);
            }
        }

        void ApplyPreThresholdOffset(Vector2 deltaLocal)
        {
            var offset = new Vector2(
                deltaLocal.x * _mb.PreThresholdOffsetScaleX,
                deltaLocal.y * _mb.PreThresholdOffsetScaleY);

            offset.x = Mathf.Clamp(offset.x, -_mb.PreThresholdOffsetMaxX, _mb.PreThresholdOffsetMaxX);
            offset.y = Mathf.Clamp(offset.y, -_mb.PreThresholdOffsetMaxY, _mb.PreThresholdOffsetMaxY);

            var targetLocal = _swipeBaseLocalPosition + new Vector3(offset.x, offset.y, 0f);
            SetFollowTargetLocal(targetLocal);
        }

        void EndSwipeWithReturn()
        {
            _swipeState = PointerTiltSwipeState.Idle;
            BeginPositionReturn();
        }

        void EnsurePositionFollowStarted()
        {
            if (_positionPlayer == null || _targetTransform == null)
                return;

            EnsurePositionFollowTarget();
            if (_positionFollowTarget == null)
                return;

            if (_positionFollowRunning)
                return;

            var options = new TransformFollowOptions
            {
                SmoothTime = 0f,
                FollowX = true,
                FollowY = true,
                MaxSpeed = 0f,
                UseVelocityOffset = false,
                BaseTargetOffset = Vector3.zero,
                VelocityOffsetScale = Vector2.zero,
                VelocityWeight = TransformFollowVelocityWeightSettings.Default,
                VelocitySourceType = TransformFollowVelocitySourceType.TransformChannel,
                LimitTurnRate = false,
                TurnRate = 0f,
            };

            var cts = new CancellationTokenSource();
            _positionFollowCts = cts;
            _positionFollowRunning = true;

            _positionPlayer.PlayFollowAsync(_positionFollowTarget, options, cts.Token).Forget(ex =>
            {
                if (ex is OperationCanceledException)
                    return;

                Debug.LogException(ex);
            });
        }

        void EnsurePositionFollowTarget()
        {
            if (_positionFollowTarget != null || _targetTransform == null)
                return;

            var helperObject = new GameObject($"{_targetTransform.name}_PointerTiltFollowTarget");
            helperObject.hideFlags = HideFlags.HideAndDontSave;

            var helper = helperObject.transform;
            if (_targetTransform.parent != null)
                helper.SetParent(_targetTransform.parent, false);

            helper.position = _targetTransform.position;
            helper.rotation = _targetTransform.rotation;
            helper.localScale = Vector3.one;
            _positionFollowTarget = helper;
        }

        void SetFollowTargetLocal(Vector3 localPosition)
        {
            if (_positionFollowTarget == null)
                return;

            _positionFollowTarget.localPosition = localPosition;
        }

        void BeginPositionReturn()
        {
            if (!_positionFollowRunning || _positionFollowTarget == null)
                return;

            _positionReturnFromLocal = _positionFollowTarget.localPosition;
            _positionReturnToLocal = _swipeBaseLocalPosition;

            if (_mb.PositionReturnDuration <= Epsilon)
            {
                _positionReturnActive = false;
                SetFollowTargetLocal(_positionReturnToLocal);
                StopPositionFollow();
                return;
            }

            _positionReturnElapsed = 0f;
            _positionReturnActive = true;
        }

        void UpdatePositionReturn()
        {
            if (!_positionReturnActive || _positionFollowTarget == null)
                return;

            var duration = Mathf.Max(Epsilon, _mb.PositionReturnDuration);
            _positionReturnElapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_positionReturnElapsed / duration);

            var local = Vector3.Lerp(_positionReturnFromLocal, _positionReturnToLocal, t);
            SetFollowTargetLocal(local);

            if (t < 1f)
                return;

            _positionReturnActive = false;
            if (_swipeState == PointerTiltSwipeState.Idle && !_submitHeld)
                StopPositionFollow();
        }

        void CancelPositionReturn()
        {
            _positionReturnActive = false;
            _positionReturnElapsed = 0f;
        }

        void StopPositionFollow()
        {
            _positionReturnActive = false;
            _positionReturnElapsed = 0f;

            var cts = _positionFollowCts;
            _positionFollowCts = null;
            _positionFollowRunning = false;

            if (cts == null)
                return;

            if (!cts.IsCancellationRequested)
                cts.Cancel();
            cts.Dispose();
        }

        void DestroyPositionFollowTarget()
        {
            if (_positionFollowTarget == null)
                return;

            var go = _positionFollowTarget.gameObject;
            _positionFollowTarget = null;

            if (go == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }

        void ExecuteCommandList(CommandListData commandList)
        {
            if (commandList == null || commandList.Count == 0 || _activeScope == null)
                return;

            var runner = _commandRunner ?? ResolveCommandRunner(_activeScope);
            if (runner == null)
                return;

            _commandRunner = runner;

            var vars = BuildCommandVars(_activeScope);
            var options = CommandRunOptions.Default;
            var ctx = new CommandContext(_activeScope, vars, runner, _activeScope, options);

            UniTask.Void(async () =>
            {
                try
                {
                    var result = await runner.ExecuteListAsync(commandList, ctx, CancellationToken.None, options);
                    if (result.Status == CommandRunStatus.Error)
                        Debug.LogError($"[PointerTiltSwipe] Command execution failed: {result.Message}", _mb);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PointerTiltSwipe] Command execution exception: {ex.Message}", _mb);
                }
            });
        }

        static ICommandRunner? ResolveCommandRunner(IScopeNode scope)
        {
            if (scope.TryResolveInAncestors<ICommandRunner>(out var ancestorRunner) && ancestorRunner != null)
                return ancestorRunner;

            if (scope.Resolver != null && scope.Resolver.TryResolve<ICommandRunner>(out var localRunner) && localRunner != null)
                return localRunner;

            return null;
        }

        static IVarStore BuildCommandVars(IScopeNode scope)
        {
            var vars = new VarStore();
            if (scope.Resolver != null && scope.Resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                blackboard.MergeInto(vars, overwrite: true);

            return vars;
        }

        void ResetRuntimeState()
        {
            _isInteractionActive = false;
            _wasInteractionActive = false;
            _hasTiltApplied = false;
            _hasPointerLocal = false;
            _swipeState = PointerTiltSwipeState.Idle;
            _positionReturnActive = false;
            _positionReturnElapsed = 0f;
        }

        void ResetFrameInputFlags()
        {
            _submitDownFrame = false;
            _submitHeld = false;
            _submitUpFrame = false;
            _uiHeldSignalReceivedFrame = false;
        }

        void FinalizeFrameInputState()
        {
            if (_resolvedEnvironmentMode == PointerTiltEnvironmentMode.ScreenUI && !_uiHeldSignalReceivedFrame)
                _submitHeld = false;

            _submitDownFrame = false;
            _submitUpFrame = false;
            _uiHeldSignalReceivedFrame = false;
        }
    }
}
