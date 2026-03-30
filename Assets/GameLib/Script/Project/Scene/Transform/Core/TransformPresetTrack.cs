#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Channel;
using Game.Common;
using Game.Times;
using UnityEngine;
using VContainer;
using VNext = Game.Commands.VNext;

namespace Game.TransformSystem
{
    /// <summary>
    /// Preset / Clip / Step 再生 track。
    /// DOTween.To で内部 state を更新し、合成器経由で pose を出す。
    /// Transform への直接書き込みは行わない。
    /// </summary>
    public sealed class TransformPresetTrack : ITransformModifierTrack
    {
        readonly Transform _ownerTransform;
        readonly RectTransform? _rectTransform;
        readonly IScopeNode _scope;
        readonly bool _enableDebugLog;

        TimeScaleBehavior CurrentTimeScaleBehavior => _scope.Identity?.TimeScaleBehavior ?? TimeScaleBehavior.Scaled;

        bool _stopped;
        bool _playing;
        int _activeTweenCount;
        TransformContributionMask _activeMask;

        // current state (DOTween.To で更新される)
        Vector3 _worldPosition;
        Vector3 _localPosition;
        Vector3 _localEulerAngles;
        Vector3 _localScale = Vector3.one;
        Vector2 _anchoredPosition;
        Vector2 _sizeDelta;
        Vector2 _pivot;

        // active flags per property
        bool _worldPositionActive;
        bool _localPositionActive;
        bool _localRotationActive;
        bool _localScaleActive;
        bool _anchoredPositionActive;
        bool _sizeDeltaActive;
        bool _pivotActive;

        CancellationTokenSource? _cts;

        public bool IsAlive => !_stopped && (_playing || _activeTweenCount > 0);
        public int Priority => 50;
        public TransformContributionMask ContributedProperties => _activeMask;

        public TransformPresetTrack(Transform ownerTransform, IScopeNode scope, bool enableDebugLog = false)
        {
            _ownerTransform = ownerTransform;
            _rectTransform = ownerTransform as RectTransform;
            _scope = scope;
            _enableDebugLog = enableDebugLog;
        }

        public async UniTask PlayPresetAsync(
            ITransformAnimationPreset preset,
            IVarStore variables,
            CancellationToken ct = default)
        {
            if (preset == null || _stopped)
                return;

            var steps = preset.Steps;
            if (steps == null || steps.Count == 0)
                return;

            var t = _ownerTransform;
            if (!t)
                return;

            var linkedCts = ct.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : new CancellationTokenSource();
            _cts = linkedCts;
            var token = linkedCts.Token;
            _playing = true;

            try
            {
                if (preset.Loop && preset.LoopCount < 0)
                {
                    while (!token.IsCancellationRequested && !_stopped)
                    {
                        for (int i = 0; i < steps.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            if (!t || _stopped) return;
                            await PlayStepInternalAsync(steps[i], t, variables, token);
                        }
                    }
                }
                else
                {
                    var count = !preset.Loop ? 1 : Mathf.Max(1, preset.LoopCount);
                    for (int k = 0; k < count && !token.IsCancellationRequested && !_stopped; k++)
                    {
                        for (int i = 0; i < steps.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            if (!t || _stopped) return;
                            await PlayStepInternalAsync(steps[i], t, variables, token);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (MissingReferenceException) { }
            finally
            {
                _playing = false;
                if (ReferenceEquals(_cts, linkedCts))
                {
                    _cts?.Dispose();
                    _cts = null;
                }
            }
        }

        public async UniTask PlaySingleStepAsync(ITransformAnimationStep step, Vector3 to, CancellationToken ct = default)
        {
            if (step == null || _stopped)
                return;

            var t = _ownerTransform;
            if (!t)
                return;

            _playing = true;
            try
            {
                await PlayStepInternalAsync(step, t, NullVarStore.Instance, ct, to);
            }
            catch (OperationCanceledException) { }
            catch (MissingReferenceException) { }
            finally
            {
                _playing = false;
            }
        }

        async UniTask PlayStepInternalAsync(
            ITransformAnimationStep step,
            Transform t,
            IVarStore variables,
            CancellationToken ct,
            Vector3? overrideTo = null)
        {
            if (!t || _stopped)
                return;

            var duration = step.Duration.Get(_scope, variables, 0f);
            duration = Mathf.Max(0f, duration);
            var rect = _rectTransform;

            bool useOverride = overrideTo.HasValue;
            var overrideValue = overrideTo.GetValueOrDefault();
            Tween? tween = null;

            switch (step.Operation)
            {
                case TransformAnimationOperation.Wait:
                    break;

                case TransformAnimationOperation.WorldPosition:
                    {
                        var current = t.position;
                        Vector3 target;
                        if (step.PositionPathMode == TransformPositionPathMode.Poly)
                        {
                            var axis = useOverride ? overrideValue : step.Vector3Value.Get(_scope, variables, Vector3.right);
                            target = current + axis;
                        }
                        else
                        {
                            target = useOverride ? overrideValue : step.Vector3Value.Get(_scope, variables, step.Relative ? Vector3.zero : current);
                            if (step.Relative) target = current + target;
                        }

                        _worldPosition = current;
                        _worldPositionActive = true;
                        _activeMask |= TransformContributionMask.WorldPosition;

                        if (duration <= 0f)
                        {
                            t.position = target;
                            _worldPosition = target;
                            return;
                        }

                        if (step.PositionPathMode != TransformPositionPathMode.Linear)
                        {
                            var path = TransformAnimationChannelPlayer.BuildPositionPath(current, target, step);
                            var progress = 0f;
                            tween = DOTween.To(() => progress, x =>
                            {
                                progress = x;
                                _worldPosition = TransformAnimationChannelPlayer.EvaluatePositionPath(path, progress);
                            }, 1f, duration)
                                .SetId(this)
                                .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        }
                        else
                        {
                            var value = current;
                            tween = DOTween.To(() => value, x => { value = x; _worldPosition = x; }, target, duration)
                                .SetId(this)
                                .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        }
                        break;
                    }

                case TransformAnimationOperation.LocalPosition:
                    {
                        var current = t.localPosition;
                        Vector3 target;
                        if (step.PositionPathMode == TransformPositionPathMode.Poly)
                        {
                            var axis = useOverride ? overrideValue : step.Vector3Value.Get(_scope, variables, Vector3.right);
                            target = current + axis;
                        }
                        else
                        {
                            target = useOverride ? overrideValue : step.Vector3Value.Get(_scope, variables, step.Relative ? Vector3.zero : current);
                            if (step.Relative) target = current + target;
                        }

                        _localPosition = current;
                        _localPositionActive = true;
                        _activeMask |= TransformContributionMask.LocalPosition;

                        if (duration <= 0f)
                        {
                            t.localPosition = target;
                            _localPosition = target;
                            return;
                        }

                        if (step.PositionPathMode != TransformPositionPathMode.Linear)
                        {
                            var path = TransformAnimationChannelPlayer.BuildPositionPath(current, target, step);
                            var progress = 0f;
                            tween = DOTween.To(() => progress, x =>
                            {
                                progress = x;
                                _localPosition = TransformAnimationChannelPlayer.EvaluatePositionPath(path, progress);
                            }, 1f, duration)
                                .SetId(this)
                                .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        }
                        else
                        {
                            var value = current;
                            tween = DOTween.To(() => value, x => { value = x; _localPosition = x; }, target, duration)
                                .SetId(this)
                                .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        }
                        break;
                    }

                case TransformAnimationOperation.LocalRotate:
                    {
                        var current = t.localEulerAngles;
                        var target = useOverride ? overrideValue : step.Vector3Value.Get(_scope, variables, step.Relative ? Vector3.zero : current);
                        if (step.Relative)
                            target = current + target;
                        else if (step.EnsureShortestLocalRotatePath)
                            target = ResolveShortestEulerTarget(current, target);

                        _localEulerAngles = current;
                        _localRotationActive = true;
                        _activeMask |= TransformContributionMask.LocalRotation;

                        if (duration <= 0f)
                        {
                            t.localEulerAngles = target;
                            _localEulerAngles = target;
                            return;
                        }

                        var value = current;
                        tween = DOTween.To(() => value, x => { value = x; _localEulerAngles = x; }, target, duration)
                            .SetId(this)
                            .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        break;
                    }

                case TransformAnimationOperation.LocalScale:
                    {
                        var current = t.localScale;
                        var target = useOverride ? overrideValue : step.Vector3Value.Get(_scope, variables, step.Relative ? Vector3.zero : current);
                        if (step.Relative)
                            target = Vector3.Scale(current, Vector3.one + target);

                        _localScale = current;
                        _localScaleActive = true;
                        _activeMask |= TransformContributionMask.LocalScale;

                        if (duration <= 0f)
                        {
                            t.localScale = target;
                            _localScale = target;
                            return;
                        }

                        var value = current;
                        tween = DOTween.To(() => value, x => { value = x; _localScale = x; }, target, duration)
                            .SetId(this)
                            .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        break;
                    }

                case TransformAnimationOperation.AnchoredPosition:
                    {
                        if (!rect) break;
                        var current = rect.anchoredPosition;
                        var raw = useOverride
                            ? new Vector2(overrideValue.x, overrideValue.y)
                            : step.Vector2Value.Get(_scope, variables, step.Relative ? Vector2.zero : current);

                        Vector2 target;
                        if (step.AnchoredInputMode == AnchoredPositionInputMode.LeftTop)
                        {
                            target = step.Relative
                                ? current + new Vector2(raw.x, -raw.y)
                                : ConvertLeftTopToAnchored(rect, raw);
                        }
                        else
                        {
                            target = raw;
                            if (step.Relative) target = current + target;
                        }

                        _anchoredPosition = current;
                        _anchoredPositionActive = true;
                        _activeMask |= TransformContributionMask.AnchoredPosition;

                        if (duration <= 0f)
                        {
                            rect.anchoredPosition = target;
                            _anchoredPosition = target;
                            return;
                        }

                        var value = current;
                        tween = DOTween.To(() => value, x => { value = x; _anchoredPosition = x; }, target, duration)
                            .SetId(this)
                            .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        break;
                    }

                case TransformAnimationOperation.DeltaSize:
                    {
                        if (!rect) break;
                        var current = rect.sizeDelta;
                        var target = useOverride
                            ? new Vector2(overrideValue.x, overrideValue.y)
                            : step.Vector2Value.Get(_scope, variables, step.Relative ? Vector2.zero : current);
                        if (step.Relative) target = current + target;

                        _sizeDelta = current;
                        _sizeDeltaActive = true;
                        _activeMask |= TransformContributionMask.SizeDelta;

                        if (duration <= 0f)
                        {
                            rect.sizeDelta = target;
                            _sizeDelta = target;
                            return;
                        }

                        var value = current;
                        tween = DOTween.To(() => value, x => { value = x; _sizeDelta = x; }, target, duration)
                            .SetId(this)
                            .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        break;
                    }

                case TransformAnimationOperation.Pivot:
                    {
                        if (!rect) break;
                        var currentPivot = rect.pivot;
                        var targetPivot = useOverride
                            ? new Vector2(overrideValue.x, overrideValue.y)
                            : step.Vector2Value.Get(_scope, variables, step.Relative ? Vector2.zero : currentPivot);
                        if (step.Relative) targetPivot = currentPivot + targetPivot;

                        var basePivot = currentPivot;
                        var baseAnchored = rect.anchoredPosition;
                        var size = rect.rect.size;

                        _pivot = currentPivot;
                        _anchoredPosition = baseAnchored;
                        _pivotActive = true;
                        _anchoredPositionActive = true;
                        _activeMask |= TransformContributionMask.Pivot | TransformContributionMask.AnchoredPosition;

                        if (duration <= 0f)
                        {
                            rect.pivot = targetPivot;
                            var deltaPos = new Vector2(
                                (targetPivot.x - basePivot.x) * size.x,
                                (targetPivot.y - basePivot.y) * size.y);
                            rect.anchoredPosition = baseAnchored + deltaPos;
                            _pivot = targetPivot;
                            _anchoredPosition = baseAnchored + deltaPos;
                            return;
                        }

                        var value = currentPivot;
                        tween = DOTween.To(() => value, newPivot =>
                        {
                            value = newPivot;
                            _pivot = newPivot;
                            var deltaPos = new Vector2(
                                (newPivot.x - basePivot.x) * size.x,
                                (newPivot.y - basePivot.y) * size.y);
                            _anchoredPosition = baseAnchored + deltaPos;
                        }, targetPivot, duration)
                            .SetId(this)
                            .SetUpdateFromBehavior(CurrentTimeScaleBehavior);
                        break;
                    }

                case TransformAnimationOperation.Command:
                    {
                        var commands = step.Commands;
                        if (commands == null || commands.Count == 0)
                            return;

                        if (_scope.Resolver == null || !_scope.Resolver.TryResolve<VNext.ICommandRunner>(out var runner) || runner == null)
                            return;

                        var runOptions = VNext.CommandRunOptions.Default;
                        var runCtx = new VNext.CommandContext(_scope, variables ?? NullVarStore.Instance, runner, _scope, runOptions);
                        var runTask = runner.ExecuteListAsync(commands, runCtx, ct, runOptions);

                        if (step.CommandAwaitMode == VNext.FlowRunAwaitMode.WaitForCompletion)
                            await runTask;
                        else
                            runTask.Forget(ex => { if (ex is not OperationCanceledException) Debug.LogException(ex); });
                        return;
                    }

                case TransformAnimationOperation.Scroll:
                    {
                        // Scroll は TransformScrollTrack として director に追加すべきだが、
                        // Preset 内 step として inline 実行もサポートする
                        var velocity = useOverride ? overrideValue : step.Vector3Value.Get(_scope, variables, Vector3.zero);
                        var useLocal = step.Relative;

                        if (duration <= 0f) return;

                        _worldPosition = t.position;
                        _worldPositionActive = true;
                        _activeMask |= TransformContributionMask.WorldPosition;

                        var scrollDuration = duration;

                        if (step.FireAndForget)
                        {
                            _activeTweenCount++;
                            RunScrollInline(t, velocity, scrollDuration, useLocal, ct).Forget(ex =>
                            {
                                _activeTweenCount = Mathf.Max(0, _activeTweenCount - 1);
                                if (ex is not OperationCanceledException) Debug.LogException(ex);
                            });
                            return;
                        }

                        await RunScrollInline(t, velocity, scrollDuration, useLocal, ct);
                        return;
                    }
            }

            if (tween == null)
            {
                if (!step.FireAndForget && duration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
                return;
            }

            tween.SetEase(step.Ease);

            if (step.FireAndForget)
            {
                TrackTweenLifetime(tween);
                tween.Play();
                return;
            }

            tween.Play();

            if (duration <= 0f || !tween.active || tween.IsComplete())
                return;

            // DOTween 完了待機
            CancellationTokenRegistration ctr = default;
            if (ct.CanBeCanceled)
                ctr = ct.Register(() => { if (tween.active) tween.Kill(); });

            try
            {
                while (tween.active && !tween.IsComplete())
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            finally
            {
                ctr.Dispose();
            }
        }

        async UniTask RunScrollInline(Transform t, Vector3 velocity, float scrollDuration, bool useLocal, CancellationToken ct)
        {
            var elapsed = 0f;
            while (elapsed < scrollDuration && !_stopped)
            {
                ct.ThrowIfCancellationRequested();
                if (!t) return;

                var dt = Mathf.Min(Time.deltaTime, scrollDuration - elapsed);
                if (dt <= 0f)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    continue;
                }

                var worldVelocity = useLocal ? t.TransformVector(velocity) : velocity;
                _worldPosition += worldVelocity * dt;
                elapsed += dt;

                if (elapsed < scrollDuration)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        public void Tick(float deltaTime)
        {
            // state は DOTween.To のコールバックで更新されるので Tick では何もしない
        }

        public void WriteContribution(ref TransformPoseAccumulator accumulator)
        {
            if (_stopped)
                return;

            if (_worldPositionActive)
            {
                accumulator.Apply(TransformPoseContribution.WorldPosition(
                    _worldPosition, TransformComposeMode.Replace, Priority));
            }

            if (_localPositionActive)
            {
                accumulator.Apply(TransformPoseContribution.LocalPosition(
                    _localPosition, TransformComposeMode.Replace, Priority));
            }

            if (_localRotationActive)
            {
                accumulator.Apply(TransformPoseContribution.LocalRotation(
                    _localEulerAngles, TransformComposeMode.Replace, Priority));
            }

            if (_localScaleActive)
            {
                accumulator.Apply(TransformPoseContribution.LocalScale(
                    _localScale, TransformComposeMode.Replace, Priority));
            }

            if (_anchoredPositionActive)
            {
                accumulator.Apply(TransformPoseContribution.AnchoredPosition(
                    _anchoredPosition, TransformComposeMode.Replace, Priority));
            }

            if (_sizeDeltaActive)
            {
                accumulator.Apply(TransformPoseContribution.SizeDelta(
                    _sizeDelta, TransformComposeMode.Replace, Priority));
            }

            if (_pivotActive)
            {
                accumulator.Apply(TransformPoseContribution.Pivot(
                    _pivot, TransformComposeMode.Replace, Priority));
            }
        }

        public void Stop()
        {
            _stopped = true;
            var cts = _cts;
            _cts = null;
            if (cts != null)
            {
                if (!cts.IsCancellationRequested) cts.Cancel();
                cts.Dispose();
            }
            DOTween.Kill(this);
            ClearActiveFlags();
        }

        public void Reset()
        {
            _stopped = false;
            _playing = false;
            _activeTweenCount = 0;
            ClearActiveFlags();
        }

        void ClearActiveFlags()
        {
            _activeMask = TransformContributionMask.None;
            _worldPositionActive = false;
            _localPositionActive = false;
            _localRotationActive = false;
            _localScaleActive = false;
            _anchoredPositionActive = false;
            _sizeDeltaActive = false;
            _pivotActive = false;
            _localScale = Vector3.one;
        }

        static Vector3 ResolveShortestEulerTarget(in Vector3 currentEuler, in Vector3 targetEuler)
        {
            return new Vector3(
                currentEuler.x + Mathf.DeltaAngle(currentEuler.x, targetEuler.x),
                currentEuler.y + Mathf.DeltaAngle(currentEuler.y, targetEuler.y),
                currentEuler.z + Mathf.DeltaAngle(currentEuler.z, targetEuler.z));
        }

        static Vector2 ConvertLeftTopToAnchored(RectTransform rect, Vector2 leftTop)
        {
            var parent = rect.parent as RectTransform;
            if (!parent) return leftTop;
            if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one) return leftTop;

            var parentSize = parent.rect.size;
            var size = rect.rect.size;
            var pivot = rect.pivot;
            var parentTopLeft = new Vector2(-parentSize.x * 0.5f, parentSize.y * 0.5f);
            var pivotOffset = new Vector2(size.x * pivot.x, -size.y * (1f - pivot.y));
            return parentTopLeft + new Vector2(leftTop.x, -leftTop.y) + pivotOffset;
        }

        // Path evaluation - delegates to existing static methods on TransformAnimationChannelPlayer
        static Vector3 EvaluatePositionPath(in TransformAnimationChannelPlayer.PositionPathData path, float progress)
        {
            return TransformAnimationChannelPlayer.EvaluatePositionPath(path, progress);
        }

        void TrackTweenLifetime(Tween tween)
        {
            _activeTweenCount++;
            var ended = false;
            void EndOnce()
            {
                if (ended)
                    return;

                ended = true;
                _activeTweenCount = Mathf.Max(0, _activeTweenCount - 1);
            }

            tween.OnComplete(EndOnce);
            tween.OnKill(EndOnce);
        }
    }
}
