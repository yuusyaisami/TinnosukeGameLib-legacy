// Game.Channel.TransformAnimationChannelPlayer.cs
#nullable enable
using VContainer;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.TransformSystem;
using UnityEngine;

namespace Game.Channel
{
    public enum TransformAnimationRunMode
    {
        Idle = 0,
        Preset = 1,
        SingleStep = 2,
        FollowTransform = 3,
        FollowPosition = 4,
        RotateSpeed = 5,
        RotateAngle = 6,
    }

    public readonly struct TransformAnimationChannelTelemetrySnapshot
    {
        public readonly string Tag;
        public readonly string TargetName;
        public readonly bool TargetExists;
        public readonly bool IsPlaying;
        public readonly bool IsFollowing;
        public readonly bool IsShaking;
        public readonly bool HasOutput;
        public readonly TransformAnimationRunMode RunMode;
        public readonly string CurrentOperation;
        public readonly int StepIndex;
        public readonly int StepCount;
        public readonly int LoopIndex;
        public readonly int LoopCount;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 LocalPosition;
        public readonly Vector3 LocalEulerAngles;
        public readonly Vector3 LocalScale;
        public readonly bool FollowUseTransformTarget;
        public readonly string FollowTargetName;
        public readonly Vector3 FollowTargetPosition;
        public readonly Vector3 FollowSmoothVelocity;
        public readonly Vector3 FollowCurrentDirection;
        public readonly bool FollowHasTransformControllerVelocitySource;
        public readonly bool FollowHasRigidbody2DVelocitySource;
        public readonly TransformFollowOptions FollowOptions;

        public TransformAnimationChannelTelemetrySnapshot(
            string tag,
            string targetName,
            bool targetExists,
            bool isPlaying,
            bool isFollowing,
            bool isShaking,
            bool hasOutput,
            TransformAnimationRunMode runMode,
            string currentOperation,
            int stepIndex,
            int stepCount,
            int loopIndex,
            int loopCount,
            Vector3 worldPosition,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            bool followUseTransformTarget,
            string followTargetName,
            Vector3 followTargetPosition,
            Vector3 followSmoothVelocity,
            Vector3 followCurrentDirection,
            bool followHasTransformControllerVelocitySource,
            bool followHasRigidbody2DVelocitySource,
            TransformFollowOptions followOptions)
        {
            Tag = tag;
            TargetName = targetName;
            TargetExists = targetExists;
            IsPlaying = isPlaying;
            IsFollowing = isFollowing;
            IsShaking = isShaking;
            HasOutput = hasOutput;
            RunMode = runMode;
            CurrentOperation = currentOperation;
            StepIndex = stepIndex;
            StepCount = stepCount;
            LoopIndex = loopIndex;
            LoopCount = loopCount;
            WorldPosition = worldPosition;
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
            FollowUseTransformTarget = followUseTransformTarget;
            FollowTargetName = followTargetName;
            FollowTargetPosition = followTargetPosition;
            FollowSmoothVelocity = followSmoothVelocity;
            FollowCurrentDirection = followCurrentDirection;
            FollowHasTransformControllerVelocitySource = followHasTransformControllerVelocitySource;
            FollowHasRigidbody2DVelocitySource = followHasRigidbody2DVelocitySource;
            FollowOptions = followOptions;
        }
    }

    public interface ITransformAnimationChannelTelemetry
    {
        TransformAnimationChannelTelemetrySnapshot GetTelemetrySnapshot();
    }

    public interface ITransformAnimationChannelPlayer
    {
        string Tag { get; }
        Transform TargetTransform { get; }
        void Tick(float deltaTime);

        UniTask PlayPresetAsync(
            ITransformAnimationPreset preset,
            IVarStore variables,
            TransformPresetExecutionPolicy policy = TransformPresetExecutionPolicy.StopPrevious,
            CancellationToken ct = default);

        UniTask PlayFollowAsync(Transform target, TransformFollowOptions options, CancellationToken ct = default);
        UniTask PlayFollowAsync(Vector3 target, TransformFollowOptions options, CancellationToken ct = default);
        void PlayShake(in TransformShakeSettings settings);
        void StopShake();

        void ApplyRotateSpeed(Vector3 speed, bool add, float fadeSeconds, float dampingRate);
        void StopRotateSpeed(bool immediate, float fadeSeconds);

        void ApplyRotateAngle(Vector3 targetEulerAngles, float smoothTime, float maxSpeed);
        void StopRotateAngle(bool immediate, float fadeSeconds);

        UniTask PlayStepAsync(Vector3 to, ITransformAnimationStep step);
        bool TrySnapToCurrentFollowTarget();

        void Stop();
    }

    public interface ITransformAnimationChannelLifecycle
    {
        void OnAcquire();
        void OnRelease();
    }

    /// <summary>
    /// Thin facade: command 受付 → director / track へ委譲。
    /// 演出ロジックの本体は各 track が持つ。
    /// </summary>
    public sealed class TransformAnimationChannelPlayer : ITransformAnimationChannelPlayer, ITransformAnimationChannelLifecycle, ITransformAnimationChannelTelemetry
    {
        public string Tag => _def.Tag;

        readonly TransformChannelDef _def;
        readonly IScopeNode _scope;
        readonly bool _enableDebugLog;

        ITransformAnimationTargetRegistry? _targetRegistry;
        ITransformTargetDirector? _director;

        // active tracks
        readonly List<TransformPresetTrack> _presetTracks = new();
        TransformFollowTrack? _followTrack;
        TransformShakeTrack? _shakeTrack;
        TransformRotateTrack? _rotateSpeedTrack;
        TransformAngleConvergeTrack? _rotateAngleTrack;

        readonly List<CancellationTokenSource> _presetCtsList = new();
        CancellationTokenSource? _followCts;
        CancellationTokenSource? _spawnOnAcquireCts;

        TransformAnimationRunMode _debugRunMode;
        TransformAnimationOperation? _debugCurrentOperation;
        int _debugStepIndex = -1;
        int _debugStepCount;
        int _debugLoopIndex = -1;
        int _debugLoopCount;

        public TransformAnimationChannelPlayer(TransformChannelDef def, IScopeNode scope, bool enableDebugLog = false)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _enableDebugLog = enableDebugLog;

            if (_scope.Resolver != null)
                _scope.Resolver.TryResolve(out _targetRegistry);
        }

        public Transform TargetTransform => _def.TargetTransformOrRectTransform;

        ITransformTargetDirector? ResolveDirector()
        {
            TryResolveTargetRegistry();

            var target = TargetTransform;
            if (target == null)
                return null;

            if (_targetRegistry != null)
            {
                var resolvedDirector = _targetRegistry.GetOrCreateDirector(target);
                _director = resolvedDirector;
                return resolvedDirector;
            }

            if (_director != null)
                return _director;

            // fallback: registry を使えない場合は player 自身が director を tick する
            ITransformAnimationOutputRegistry? outputRegistry = null;
            _scope.Resolver?.TryResolve(out outputRegistry);
            TransformAnimationOutput? output = null;
            if (outputRegistry != null && outputRegistry.TryGetSink(target, out var sink))
                output = sink.AnimationOutput;
            output ??= new TransformAnimationOutput();
            var director = new TransformTargetDirector(target, output, applyDirectly: outputRegistry == null);
            _director = director;
            return _director;
        }

        void ResetCachedDirector()
        {
            _director = null;
        }

        // ===== Preset =====

        public async UniTask PlayPresetAsync(
            ITransformAnimationPreset preset,
            IVarStore variables,
            TransformPresetExecutionPolicy policy = TransformPresetExecutionPolicy.StopPrevious,
            CancellationToken ct = default)
        {
            PruneDeadPresetTracks();
            if (policy != TransformPresetExecutionPolicy.Parallel)
                Stop();

            if (preset == null) return;
            var steps = preset.Steps;
            if (steps == null || steps.Count == 0) return;
            var t = TargetTransform;
            if (!t) return;

            var director = ResolveDirector();
            if (director == null) return;

            var track = new TransformPresetTrack(t, _scope, _enableDebugLog);
            _presetTracks.Add(track);
            director.AddTrack(track);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _presetCtsList.Add(linkedCts);

            _debugRunMode = TransformAnimationRunMode.Preset;
            _debugStepCount = steps.Count;

            try
            {
                await track.PlayPresetAsync(preset, variables, linkedCts.Token);
                director.Tick(0f);
            }
            finally
            {
                if (!track.IsAlive)
                {
                    director.RemoveTrack(track);
                    _presetTracks.Remove(track);
                }

                _presetCtsList.Remove(linkedCts);
                linkedCts.Dispose();
                PruneDeadPresetTracks();
                if (_presetTracks.Count == 0)
                    SetDebugIdle();
            }
        }

        void StopPreset()
        {
            PruneDeadPresetTracks();
            for (int i = _presetTracks.Count - 1; i >= 0; i--)
            {
                var track = _presetTracks[i];
                track.Stop();
                _director?.RemoveTrack(track);
            }
            _presetTracks.Clear();

            for (int i = 0; i < _presetCtsList.Count; i++)
            {
                var cts = _presetCtsList[i];
                if (!cts.IsCancellationRequested) cts.Cancel();
                cts.Dispose();
            }
            _presetCtsList.Clear();
        }

        void PruneDeadPresetTracks()
        {

            for (int i = _presetTracks.Count - 1; i >= 0; i--)
            {
                var track = _presetTracks[i];
                if (track.IsAlive)
                    continue;

                _director?.RemoveTrack(track);
                _presetTracks.RemoveAt(i);
            }
        }

        // ===== Single Step =====

        public async UniTask PlayStepAsync(Vector3 to, ITransformAnimationStep step)
        {
            Stop();
            if (step == null) return;
            var t = TargetTransform;
            if (!t) return;

            var director = ResolveDirector();
            if (director == null) return;

            var track = new TransformPresetTrack(t, _scope, _enableDebugLog);
            _presetTracks.Add(track);
            director.AddTrack(track);
            _debugRunMode = TransformAnimationRunMode.SingleStep;

            try
            {
                await track.PlaySingleStepAsync(step, to);
            }
            finally
            {
                director.RemoveTrack(track);
                _presetTracks.Remove(track);
                SetDebugIdle();
            }
        }

        // ===== Follow =====

        public UniTask PlayFollowAsync(Transform target, TransformFollowOptions options, CancellationToken ct = default)
        {
            return PlayFollowInternalAsync(target, Vector3.zero, useTransformTarget: true, options, ct);
        }

        public UniTask PlayFollowAsync(Vector3 target, TransformFollowOptions options, CancellationToken ct = default)
        {
            return PlayFollowInternalAsync(null, target, useTransformTarget: false, options, ct);
        }

        async UniTask PlayFollowInternalAsync(
            Transform? targetTransform,
            Vector3 targetPosition,
            bool useTransformTarget,
            TransformFollowOptions options,
            CancellationToken ct)
        {
            Stop();

            var t = TargetTransform;
            if (!t) return;

            var director = ResolveDirector();
            if (director == null) return;

            var track = new TransformFollowTrack(t, options);
            if (useTransformTarget)
            {
                if (!targetTransform) return;
                track.SetTransformTarget(targetTransform, options);
            }
            else
            {
                track.SetPositionTarget(targetPosition);
            }

            _followTrack = track;
            director.AddTrack(track);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _followCts = linkedCts;

            _debugRunMode = useTransformTarget
                ? TransformAnimationRunMode.FollowTransform
                : TransformAnimationRunMode.FollowPosition;

            try
            {
                // Follow は ct がキャンセルされるまで tick で動く（director.Tick が更新する）
                while (!linkedCts.Token.IsCancellationRequested && track.IsAlive)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (ReferenceEquals(_followTrack, track))
                {
                    director.RemoveTrack(track);
                    _followTrack = null;
                }

                if (ReferenceEquals(_followCts, linkedCts))
                {
                    _followCts?.Dispose();
                    _followCts = null;
                    SetDebugIdle();
                }
            }
        }

        public bool TrySnapToCurrentFollowTarget()
        {
            return _followTrack?.TrySnap() ?? false;
        }

        // ===== Shake =====

        public void PlayShake(in TransformShakeSettings settings)
        {
            StopShake();

            var t = TargetTransform;
            if (!t) return;
            if (settings.AmplitudeX <= 0f && settings.AmplitudeY <= 0f &&
                (!settings.EnableRotation || settings.RotationAmplitudeDeg <= 0f))
                return;

            var director = ResolveDirector();
            if (director == null) return;

            var track = new TransformShakeTrack(settings);
            _shakeTrack = track;
            director.AddTrack(track);
        }

        public void ApplyRotateSpeed(Vector3 speed, bool add, float fadeSeconds, float dampingRate)
        {
            var t = TargetTransform;
            if (!t)
                return;

            var director = ResolveDirector();
            if (director == null)
                return;

            StopRotateAngle(immediate: true, fadeSeconds: 0f);

            var track = _rotateSpeedTrack ??= new TransformRotateTrack(t);
            director.AddTrack(track);
            track.SetSpeed(speed, add, fadeSeconds, dampingRate);
            _debugRunMode = TransformAnimationRunMode.RotateSpeed;
        }

        public void StopRotateSpeed(bool immediate, float fadeSeconds)
        {
            var track = _rotateSpeedTrack;
            if (track == null)
                return;

            track.Stop(immediate, fadeSeconds);
            if (immediate && _director != null)
                _director.RemoveTrack(track);
        }

        public void ApplyRotateAngle(Vector3 targetEulerAngles, float smoothTime, float maxSpeed)
        {
            var t = TargetTransform;
            if (!t)
                return;

            var director = ResolveDirector();
            if (director == null)
                return;

            StopRotateSpeed(immediate: true, fadeSeconds: 0f);

            var track = _rotateAngleTrack ??= new TransformAngleConvergeTrack(t);
            director.AddTrack(track);
            track.SetTarget(targetEulerAngles, smoothTime, maxSpeed);
            _debugRunMode = TransformAnimationRunMode.RotateAngle;
        }

        public void StopRotateAngle(bool immediate, float fadeSeconds)
        {
            var track = _rotateAngleTrack;
            if (track == null)
                return;

            track.Stop(immediate, fadeSeconds);
            if (immediate && _director != null)
                _director.RemoveTrack(track);
        }

        public void StopShake()
        {
            var track = _shakeTrack;
            if (track != null)
            {
                track.Stop();
                _director?.RemoveTrack(track);
                _shakeTrack = null;
            }
        }

        // ===== Lifecycle =====

        public void OnAcquire()
        {
            var preset = _def.TransformPreset;
            if (!_def.PlayOnSpawnPreset || preset == null || preset.Steps.Count <= 0)
                return;

            var delaySeconds = Mathf.Max(0f, _def.PlayOnSpawnDelaySeconds);
            if (delaySeconds <= 0f)
            {
                PlayPresetAsync(preset, new VarStore()).Forget();
                return;
            }

            CancelSpawnPlayTimer();
            var cts = new CancellationTokenSource();
            _spawnOnAcquireCts = cts;
            RunPlayOnSpawnAfterDelayAsync(preset, delaySeconds, cts).Forget();
        }

        public void OnRelease()
        {
            Stop();
            StopShake();
            ResetCachedDirector();
            _targetRegistry = null;
        }

        async UniTaskVoid RunPlayOnSpawnAfterDelayAsync(
            ITransformAnimationPreset preset,
            float delaySeconds,
            CancellationTokenSource delayCts)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: delayCts.Token);
                if (delayCts.IsCancellationRequested || !ReferenceEquals(_spawnOnAcquireCts, delayCts))
                    return;
                _spawnOnAcquireCts = null;
                await PlayPresetAsync(preset, new VarStore());
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex); }
            finally
            {
                if (ReferenceEquals(_spawnOnAcquireCts, delayCts))
                    _spawnOnAcquireCts = null;
                delayCts.Dispose();
            }
        }

        void CancelSpawnPlayTimer()
        {
            var cts = _spawnOnAcquireCts;
            _spawnOnAcquireCts = null;
            if (cts == null) return;
            if (!cts.IsCancellationRequested) cts.Cancel();
            cts.Dispose();
        }

        // ===== Stop =====

        public void Stop()
        {
            CancelSpawnPlayTimer();
            StopPreset();
            StopFollow();
            StopShake();
            StopRotateTracks(immediate: true, fadeSeconds: 0f);
            SetDebugIdle();
        }

        void StopFollow()
        {
            var track = _followTrack;
            if (track != null)
            {
                track.Stop();
                _director?.RemoveTrack(track);
                _followTrack = null;
            }

            var cts = _followCts;
            _followCts = null;
            if (cts != null)
            {
                if (!cts.IsCancellationRequested) cts.Cancel();
                cts.Dispose();
            }
        }

        void TryResolveTargetRegistry()
        {
            if (_targetRegistry == null && _scope.Resolver != null)
                _scope.Resolver.TryResolve(out _targetRegistry);
        }

        public void Tick(float deltaTime)
        {
            var director = _director;
            if (director == null || !director.HasActiveTracks)
                return;

            // track は state を更新するだけで、最終反映は director がまとめて行う。
            // ここで毎フレーム Tick しておかないと、最後の確定値しか出力されない。
            director.Tick(deltaTime);
            PruneDeadRotateTracks();
        }

        // ===== Telemetry =====

        public TransformAnimationChannelTelemetrySnapshot GetTelemetrySnapshot()
        {
            PruneDeadPresetTracks();

            var target = TargetTransform;
            var hasTarget = target != null;
            var targetName = "(null)";
            var world = Vector3.zero;
            var local = Vector3.zero;
            var euler = Vector3.zero;
            var scale = Vector3.one;

            if (target != null)
            {
                targetName = target.name;
                world = target.position;
                local = target.localPosition;
                euler = target.localEulerAngles;
                scale = target.localScale;
            }

            var hasFollow = _followTrack != null && _followTrack.IsAlive;
            var follow = _followTrack?.FollowService;
            var followUseTransformTarget = hasFollow && follow != null && follow.UseTransformTarget;
            var followTargetName = followUseTransformTarget && follow?.TargetTransform != null
                ? follow.TargetTransform.name : string.Empty;
            var followTargetPosition = hasFollow && follow != null
                ? follow.ResolveCurrentTargetPosition(world, _followTrack!.Options)
                : Vector3.zero;
            var followSmoothVelocity = hasFollow && follow != null ? follow.SmoothVelocity : Vector3.zero;
            var followCurrentDirection = hasFollow && follow != null ? follow.CurrentDirection : Vector3.zero;
            var followHasTransformControllerVelocitySource = hasFollow && follow != null && follow.HasTransformControllerVelocitySource;
            var followHasRigidbody2DVelocitySource = hasFollow && follow != null && follow.HasRigidbody2DVelocitySource;

            return new TransformAnimationChannelTelemetrySnapshot(
                tag: Tag ?? string.Empty,
                targetName: targetName,
                targetExists: hasTarget,
                isPlaying: _presetTracks.Count > 0,
                isFollowing: hasFollow,
                isShaking: _shakeTrack != null && _shakeTrack.IsAlive,
                hasOutput: _director != null,
                runMode: _debugRunMode,
                currentOperation: _debugCurrentOperation?.ToString() ?? "-",
                stepIndex: _debugStepIndex,
                stepCount: _debugStepCount,
                loopIndex: _debugLoopIndex,
                loopCount: _debugLoopCount,
                worldPosition: world,
                localPosition: local,
                localEulerAngles: euler,
                localScale: scale,
                followUseTransformTarget: followUseTransformTarget,
                followTargetName: followTargetName,
                followTargetPosition: followTargetPosition,
                followSmoothVelocity: followSmoothVelocity,
                followCurrentDirection: followCurrentDirection,
                followHasTransformControllerVelocitySource: followHasTransformControllerVelocitySource,
                followHasRigidbody2DVelocitySource: followHasRigidbody2DVelocitySource,
                followOptions: _followTrack?.Options ?? default);
        }

        void SetDebugIdle()
        {
            _debugRunMode = TransformAnimationRunMode.Idle;
            _debugCurrentOperation = null;
            _debugStepIndex = -1;
            _debugStepCount = 0;
            _debugLoopIndex = -1;
            _debugLoopCount = 0;
        }

        void StopRotateTracks(bool immediate, float fadeSeconds)
        {
            StopRotateSpeed(immediate, fadeSeconds);
            StopRotateAngle(immediate, fadeSeconds);
        }

        void PruneDeadRotateTracks()
        {
            if (_director == null)
                return;

            var speedTrack = _rotateSpeedTrack;
            if (speedTrack != null && !speedTrack.IsAlive)
                _director.RemoveTrack(speedTrack);

            var angleTrack = _rotateAngleTrack;
            if (angleTrack != null && !angleTrack.IsAlive)
                _director.RemoveTrack(angleTrack);
        }

        // ===== Static path utilities (shared with TransformPresetTrack) =====

        public readonly struct PositionPathData
        {
            public readonly Vector3[] Points;
            public readonly float[] Distances;
            public readonly float TotalDistance;

            public PositionPathData(Vector3[] points, float[] distances, float totalDistance)
            {
                Points = points;
                Distances = distances;
                TotalDistance = totalDistance;
            }
        }

        public static PositionPathData BuildPositionPath(in Vector3 start, in Vector3 end, ITransformAnimationStep step)
        {
            switch (step.PositionPathMode)
            {
                case TransformPositionPathMode.Curve:
                    return BuildCurvePath(start, end, step);
                case TransformPositionPathMode.Poly:
                    return BuildPolyPath(start, end, step);
                default:
                    return BuildLinearPath(start, end);
            }
        }

        static PositionPathData BuildLinearPath(in Vector3 start, in Vector3 end)
        {
            var points = new[] { start, end };
            var len = Vector3.Distance(start, end);
            var distances = new[] { 0f, len };
            return new PositionPathData(points, distances, len);
        }

        static PositionPathData BuildCurvePath(in Vector3 start, in Vector3 end, ITransformAnimationStep step)
        {
            var sampling = Mathf.Max(2, step.CurveSamplingCount);
            var points = new Vector3[sampling + 1];
            var distances = new float[sampling + 1];

            if ((end - start).sqrMagnitude <= 1e-8f)
            {
                for (int i = 0; i <= sampling; i++)
                {
                    points[i] = start;
                    distances[i] = 0f;
                }
                return new PositionPathData(points, distances, 0f);
            }

            var control = ResolveCurveControlPoint(start, end, step);
            var totalDistance = 0f;
            for (int i = 0; i <= sampling; i++)
            {
                var t = i / (float)sampling;
                var point = EvaluatePositionCurve(start, control, end, t, step.UseBezierCurve);
                points[i] = point;

                if (i == 0)
                {
                    distances[i] = 0f;
                    continue;
                }

                totalDistance += Vector3.Distance(points[i - 1], point);
                distances[i] = totalDistance;
            }

            return new PositionPathData(points, distances, totalDistance);
        }

        static PositionPathData BuildPolyPath(in Vector3 start, in Vector3 end, ITransformAnimationStep step)
        {
            var sideCount = Mathf.Max(3, step.PolySides);
            var radius = Mathf.Max(0.01f, step.PolyRadius);
            var dir = end - start;
            var pathForward = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
            var axisX = ResolveCurveNormal(pathForward);
            var axisY = Vector3.Cross(pathForward, axisX).normalized;
            if (axisY.sqrMagnitude <= 1e-8f) axisY = Vector3.up;

            var rot = step.PolyRotationDeg * Mathf.Deg2Rad;
            var fromCenterToStart = (Mathf.Cos(rot) * axisX + Mathf.Sin(rot) * axisY).normalized;
            if (fromCenterToStart.sqrMagnitude <= 1e-8f) fromCenterToStart = axisX;

            var center = start - (fromCenterToStart * radius);
            var directionSign = step.PolyDirection == TransformPolyDirection.Clockwise ? -1f : 1f;

            var points = new Vector3[sideCount + 1];
            points[0] = start;

            for (int i = 1; i < sideCount; i++)
            {
                var angle = rot + (directionSign * (Mathf.PI * 2f) * i / sideCount);
                var offset = (Mathf.Cos(angle) * axisX + Mathf.Sin(angle) * axisY) * radius;
                points[i] = center + offset;
            }
            points[sideCount] = start;

            var distances = new float[points.Length];
            var totalDistance = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                totalDistance += Vector3.Distance(points[i - 1], points[i]);
                distances[i] = totalDistance;
            }

            return new PositionPathData(points, distances, totalDistance);
        }

        public static Vector3 EvaluatePositionPath(in PositionPathData path, float progress)
        {
            var points = path.Points;
            if (points == null || points.Length == 0) return Vector3.zero;
            if (points.Length == 1) return points[0];

            var t = Mathf.Clamp01(progress);
            if (path.TotalDistance <= 1e-5f)
                return Vector3.LerpUnclamped(points[0], points[points.Length - 1], t);

            var targetDistance = path.TotalDistance * t;
            var distances = path.Distances;
            var lastIndex = distances.Length - 1;

            for (int i = 1; i <= lastIndex; i++)
            {
                if (targetDistance > distances[i]) continue;
                var segmentStartDistance = distances[i - 1];
                var segmentDistance = distances[i] - segmentStartDistance;
                if (segmentDistance <= 1e-6f) return points[i];
                var localT = (targetDistance - segmentStartDistance) / segmentDistance;
                return Vector3.LerpUnclamped(points[i - 1], points[i], localT);
            }

            return points[points.Length - 1];
        }

        public static Vector3 ResolveCurveControlPoint(in Vector3 start, in Vector3 end, ITransformAnimationStep step)
        {
            return ResolveCurveBaseControlPoint(start, end, step) + step.CurveControlOffset;
        }

        public static Vector3 ResolveCurveBaseControlPoint(in Vector3 start, in Vector3 end, ITransformAnimationStep step)
        {
            var dir = end - start;
            if (dir.sqrMagnitude <= 1e-8f) return start;
            var midpoint = (start + end) * 0.5f;
            var normal = ResolveCurveNormal(dir);
            var sideSign = step.CurveControlSide == TransformCurveControlSide.Outer ? 1f : -1f;
            return midpoint + normal * (step.CurveHeight * sideSign);
        }

        public static Vector3 EvaluatePositionCurve(in Vector3 start, in Vector3 control, in Vector3 end, float t, bool useBezier)
        {
            var clamped = Mathf.Clamp01(t);
            if (!useBezier)
            {
                if (clamped <= 0.5f)
                    return Vector3.LerpUnclamped(start, control, clamped * 2f);
                return Vector3.LerpUnclamped(control, end, (clamped - 0.5f) * 2f);
            }
            var oneMinus = 1f - clamped;
            return (oneMinus * oneMinus * start) + (2f * oneMinus * clamped * control) + (clamped * clamped * end);
        }

        static Vector3 ResolveCurveNormal(in Vector3 dir)
        {
            var normalizedDir = dir.normalized;
            var referenceAxis = Mathf.Abs(Vector3.Dot(normalizedDir, Vector3.forward)) < 0.95f
                ? Vector3.forward : Vector3.up;

            var normal = Vector3.Cross(referenceAxis, normalizedDir);
            if (normal.sqrMagnitude <= 1e-8f)
            {
                referenceAxis = Vector3.right;
                normal = Vector3.Cross(referenceAxis, normalizedDir);
            }
            if (normal.sqrMagnitude <= 1e-8f) return Vector3.up;
            return normal.normalized;
        }
    }
}
