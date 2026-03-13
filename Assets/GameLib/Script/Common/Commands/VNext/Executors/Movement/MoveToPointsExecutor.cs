#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Movement;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class MoveToPointsExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.MoveToPoints;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not MoveToPointsCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MoveToPointsCommandData is required.");

            if (typed.Points == null || typed.Points.Count == 0)
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IMoveToInputPointService>(out var moveTo) || moveTo == null)
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<MoveToInputPointProfileSO>(out var profile) || profile == null)
                return UniTask.CompletedTask;

            var runTask = RunSequenceAsync(typed, moveTo, profile, ctx, ct);
            return typed.Sequence.AwaitMode == MoveToPointAwaitMode.WaitForCompletion
                ? runTask
                : RunInBackground(runTask);
        }

        static UniTask RunInBackground(UniTask task)
        {
            UniTask.Void(async () =>
            {
                try { await task; }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (Exception e) { Debug.LogException(e); }
            });
            return UniTask.CompletedTask;
        }

        static async UniTask RunSequenceAsync(
            MoveToPointsCommandData typed,
            IMoveToInputPointService moveTo,
            MoveToInputPointProfileSO profile,
            CommandContext ctx,
            CancellationToken ct)
        {
            if (typed.Sequence.CancelExistingTarget)
                moveTo.ClearTarget(clearPath: true);

            try
            {
                if (typed.Sequence.FinishMode == MoveToPointFinishMode.FinishOnLastPoint)
                {
                    for (int i = 0; i < typed.Points.Count; i++)
                        await RunSinglePointAsync(typed, moveTo, profile, ctx, typed.Points[i], ct);
                    return;
                }

                var idx = 0;
                var dir = 1;
                var count = typed.Points.Count;

                while (true)
                {
                    await RunSinglePointAsync(typed, moveTo, profile, ctx, typed.Points[idx], ct);
                    ct.ThrowIfCancellationRequested();

                    if (typed.Sequence.FinishMode == MoveToPointFinishMode.Loop)
                    {
                        idx = (idx + 1) % count;
                        continue;
                    }

                    if (count <= 1)
                        continue;

                    if (dir > 0)
                    {
                        if (idx >= count - 1)
                        {
                            dir = -1;
                            idx = count - 2;
                        }
                        else
                        {
                            idx++;
                        }
                    }
                    else
                    {
                        if (idx <= 0)
                        {
                            dir = 1;
                            idx = 1;
                        }
                        else
                        {
                            idx--;
                        }
                    }
                }
            }
            finally
            {
                if (ct.IsCancellationRequested && typed.Sequence.ClearTargetOnCancel)
                {
                    try { moveTo.ClearTarget(clearPath: true); }
                    catch (ObjectDisposedException) { }
                }
                else if (!ct.IsCancellationRequested && typed.Sequence.ClearTargetOnComplete)
                {
                    try { moveTo.ClearTarget(clearPath: true); }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        static async UniTask RunSinglePointAsync(
            MoveToPointsCommandData typed,
            IMoveToInputPointService moveTo,
            MoveToInputPointProfileSO profile,
            CommandContext ctx,
            MoveToPointEntry entry,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var world = ResolvePoint(entry, ctx);

            var baseReq = profile.ToRequest(
                overrideSpeed: typed.Request.OverrideSpeed ? typed.Request.Speed.Resolve(ctx) : null,
                arcSeed: typed.Request.ArcSeed);

            var req = new MoveToInputPointRequest(
                speed: baseReq.Speed,
                arrivalDistance: baseReq.ArrivalDistance,
                waypointAdvanceDistance: baseReq.WaypointAdvanceDistance,
                lookAheadDistance: baseReq.LookAheadDistance,
                repathIntervalFrames: baseReq.RepathIntervalFrames,
                stuckSpeedEpsilon: baseReq.StuckSpeedEpsilon,
                stuckFramesToRepath: baseReq.StuckFramesToRepath,
                stopOnArrive: baseReq.StopOnArrive,
                allowArcOnClearLine: baseReq.AllowArcOnClearLine,
                arcMaxOffset: baseReq.ArcMaxOffset,
                arcOffsetFactor: baseReq.ArcOffsetFactor,
                arcSeed: baseReq.ArcSeed,
                obstacleMask: baseReq.ObstacleMask,
                useTriggers: baseReq.UseTriggers,
                debugFlags: baseReq.DebugFlags,
                inputType: typed.Request.InputType);

            try
            {
                moveTo.SetTarget(world, req, forceRepath: entry.ForceRepath);
            }
            catch (ObjectDisposedException ex)
            {
                throw new OperationCanceledException("MoveToInputPointService was disposed.", ex, ct);
            }

            await UniTask.WaitUntil(() => !moveTo.HasTarget, cancellationToken: ct);

            var list = entry.OnArriveCommands;
            if (list == null || list.Count == 0)
                return;

            var runner = ctx.Runner;
            if (runner == null)
                return;

            var result = await runner.ExecuteListAsync(list, ctx, ct, ctx.Options);
            if (result.Status == CommandRunStatus.Canceled)
                throw new OperationCanceledException();
            if (result.Status == CommandRunStatus.Error)
                throw new CommandExecutionException(result.FailureKind, result.Message);
        }

        static Vector2 ResolvePoint(in MoveToPointEntry entry, CommandContext ctx)
        {
            var p = entry.Point.Resolve(ctx);
            if (entry.Space == MoveToPointSpace.World)
                return p;

            if (ctx.Scope is Component c)
                return (Vector2)c.transform.position + p;

            return p;
        }
    }
}
