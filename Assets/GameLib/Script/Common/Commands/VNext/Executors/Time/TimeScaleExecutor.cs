#nullable enable
using System.Collections.Generic;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands;
using Game.Times;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SetTimeScaleExecutor : ICommandExecutor
    {
        static readonly Dictionary<TimeScaleKind, CancellationTokenSource> s_temporaryByKind = new();

        public int CommandId => CommandIds.SetTimeScale;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TimeScaleCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TimeScaleCommandData is required.");

            if (!TryResolveTimeService(ctx, out var timeService))
            {
                Debug.LogWarning("[SetTimeScaleExecutor] TimeService not found in scope hierarchy.");
                return UniTask.CompletedTask;
            }

            var scale = typed.Scale.GetOrDefault(ctx, 1f);
            var previous = timeService.GetBaseScale(typed.Kind);
            if (typed.Mode == TimeScaleCommandMode.Animate)
            {
                var duration = typed.Duration.GetOrDefault(ctx, 0f);
                timeService.AnimateBaseScale(typed.Kind, scale, duration, typed.Ease);
            }
            else
            {
                timeService.SetBaseScale(typed.Kind, scale);
            }

            if (typed.UseTemporaryDuration)
            {
                var temporarySeconds = typed.TemporaryDurationSeconds.GetOrDefault(ctx, 0f);
                var restoreDuration = typed.TemporaryRestoreDurationSeconds.GetOrDefault(ctx, 0f);
                var restoreScale = ResolveRestoreScale(typed, previous, ctx);
                StartTemporaryRestore(timeService, typed, restoreScale, temporarySeconds, restoreDuration);
            }

            return UniTask.CompletedTask;
        }

        static void StartTemporaryRestore(
            ITimeService timeService,
            TimeScaleCommandData command,
            float restoreScale,
            float seconds,
            float restoreDuration)
        {
            if (timeService == null)
                return;

            var kind = command.Kind;
            if (s_temporaryByKind.TryGetValue(kind, out var prevCts) && prevCts != null)
            {
                if (!prevCts.IsCancellationRequested)
                    prevCts.Cancel();
                prevCts.Dispose();
            }

            var duration = Mathf.Max(0f, seconds);
            if (duration <= 0f)
            {
                ApplyRestore(timeService, command, restoreScale, restoreDuration);
                s_temporaryByKind.Remove(kind);
                return;
            }

            var cts = new CancellationTokenSource();
            s_temporaryByKind[kind] = cts;
            RestoreAfterDelayAsync(timeService, command, restoreScale, restoreDuration, duration, cts).Forget();
        }

        static async UniTaskVoid RestoreAfterDelayAsync(
            ITimeService timeService,
            TimeScaleCommandData command,
            float restoreScale,
            float restoreDuration,
            float seconds,
            CancellationTokenSource cts)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(seconds),
                    DelayType.Realtime,
                    PlayerLoopTiming.Update,
                    cts.Token);

                if (!cts.IsCancellationRequested)
                    ApplyRestore(timeService, command, restoreScale, restoreDuration);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                var kind = command.Kind;
                if (s_temporaryByKind.TryGetValue(kind, out var current) && ReferenceEquals(current, cts))
                    s_temporaryByKind.Remove(kind);

                cts.Dispose();
            }
        }

        static void ApplyRestore(
            ITimeService timeService,
            TimeScaleCommandData command,
            float restoreScale,
            float restoreDuration)
        {
            if (command.TemporaryRestoreMode == TimeScaleTemporaryRestoreMode.Animate && restoreDuration > 0f)
            {
                timeService.AnimateBaseScale(command.Kind, restoreScale, restoreDuration, command.TemporaryRestoreEase);
            }
            else
            {
                timeService.SetBaseScale(command.Kind, restoreScale);
            }
        }

        static float ResolveRestoreScale(TimeScaleCommandData command, float previous, CommandContext ctx)
        {
            if (command.TemporaryRestoreTarget == TimeScaleTemporaryRestoreTargetMode.Custom)
            {
                return Mathf.Max(0f, command.TemporaryRestoreScale.GetOrDefault(ctx, previous));
            }

            return Mathf.Max(0f, previous);
        }

        static bool TryResolveTimeService(CommandContext ctx, out ITimeService timeService)
        {
            timeService = null!;
            if (ctx == null)
                return false;

            var origin = ctx.Scope ?? ctx.Actor;
            if (origin != null && origin.Resolver != null && origin.Resolver.TryResolve<ITimeService>(out var svc) && svc != null)
            {
                timeService = svc;
                return true;
            }

            if (ctx.Scope == null)
                return false;

            var candidates = new List<LifetimeScopeKind>
            {
                LifetimeScopeKind.Project,
                LifetimeScopeKind.Global,
                LifetimeScopeKind.Platform,
            };

            for (int i = 0; i < candidates.Count; i++)
            {
                var node = ScopeNodeHierarchy.FindNearestAncestorByKind(ctx.Scope, candidates[i], includeSelf: true);
                if (node == null) continue;
                var resolver = node.Resolver;
                if (resolver == null) continue;
                if (resolver.TryResolve<ITimeService>(out var svc2) && svc2 != null)
                {
                    timeService = svc2;
                    return true;
                }
            }

            return false;
        }
    }
}
