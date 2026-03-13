#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LineDraw;
using Game.MaterialFx;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class LineDrawControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.LineDrawControl;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not LineDrawControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "LineDrawControlCommandData is required.");

            ct.ThrowIfCancellationRequested();

            // ターゲットスコープを解決
            var (targetScope, error) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (targetScope == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, error ?? "Target scope could not be resolved.");
            }

            if (targetScope.Resolver == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Target scope resolver is null.");
            }

            // ILineDrawServiceを取得
            if (!targetScope.Resolver.TryResolve<ILineDrawService>(out var lineDrawService) || lineDrawService == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ILineDrawService is not available in the target scope.");
            }

            // ハンドルを取得
            var handles = ResolveHandles(lineDrawService, typed);
            if (handles.Count == 0)
            {
                Debug.LogWarning("[LineDrawControlExecutor] No active handles found.");
                return;
            }

            // 各ハンドルに対して操作を実行
            for (int i = 0; i < handles.Count; i++)
            {
                var handle = handles[i];
                ExecuteOperation(typed, ctx, lineDrawService, handle);
            }
        }

        static List<LineHandle> ResolveHandles(ILineDrawService service, LineDrawControlCommandData typed)
        {
            var result = new List<LineHandle>();
            var activeHandles = service.ActiveHandles;

            if (activeHandles == null || activeHandles.Count == 0)
                return result;

            switch (typed.HandleTarget)
            {
                case LineDrawHandleTarget.All:
                    for (int i = 0; i < activeHandles.Count; i++)
                        result.Add(activeHandles[i]);
                    break;

                case LineDrawHandleTarget.First:
                    if (activeHandles.Count > 0)
                        result.Add(activeHandles[0]);
                    break;

                case LineDrawHandleTarget.ByIndex:
                    if (typed.HandleIndex >= 0 && typed.HandleIndex < activeHandles.Count)
                        result.Add(activeHandles[typed.HandleIndex]);
                    else
                        Debug.LogWarning($"[LineDrawControlExecutor] HandleIndex {typed.HandleIndex} is out of range. ActiveCount={activeHandles.Count}");
                    break;
            }

            return result;
        }

        static void ExecuteOperation(LineDrawControlCommandData typed, CommandContext ctx, ILineDrawService service, LineHandle handle)
        {
            switch (typed.Operation)
            {
                case LineDrawControlOperation.UpdateStyle:
                    service.UpdateStyle(handle, typed.Style);
                    break;

                case LineDrawControlOperation.ApplyMaterialFx:
                    ApplyMaterialFx(typed, ctx, service, handle);
                    break;

                case LineDrawControlOperation.SetPatternOffset:
                    service.UpdatePatternOffset(handle, typed.PatternOffset);
                    break;

                case LineDrawControlOperation.SetPatternOffsetVelocity:
                    service.UpdatePatternOffsetVelocity(handle, typed.PatternOffsetVelocity);
                    break;

                case LineDrawControlOperation.SetBaseWidth:
                    service.UpdateBaseWidth(handle, typed.BaseWidth);
                    break;

                case LineDrawControlOperation.Release:
                    service.Release(handle);
                    break;

                default:
                    Debug.LogWarning($"[LineDrawControlExecutor] Unknown operation: {typed.Operation}");
                    break;
            }
        }

        static void ApplyMaterialFx(LineDrawControlCommandData typed, CommandContext ctx, ILineDrawService service, LineHandle handle)
        {
            var fx = service.TryGetMaterialFx(handle);
            if (fx == null)
            {
                Debug.LogWarning("[LineDrawControlExecutor] MaterialFx is not available for this line.");
                return;
            }

            if (!typed.MaterialFxSource.TryGet(ctx, out var payload) || payload == null)
            {
                Debug.LogWarning("[LineDrawControlExecutor] MaterialFxPayload could not be resolved.");
                return;
            }

            if (!ctx.Resolver.TryResolve<IMaterialFxPropertyRegistry>(out var registry) || registry == null)
            {
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMaterialFxPropertyRegistry is missing.");
            }

            var context = payload.ContextTag ?? string.Empty;
            if (payload.ClearContextFirst)
                fx.ClearContext(context);

            var entries = payload.Entries;
            if (entries == null || entries.Count == 0)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var key = e.Key ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!registry.TryGetValueType(key, out var kind))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"MaterialFx key not found: '{key}'");

                var value = e.Value.ToTypedValue(kind);
                var lifetime = e.LifetimeSeconds;
                if (lifetime == 0f)
                    lifetime = -1f;

                if (e.ApplyWeightFade)
                {
                    fx.SetLayerFade(key, context, value, e.FadeDuration, e.FadeEase, e.BlendMode, payload.Priority, lifetime);
                    continue;
                }

                fx.SetLayer(key, context, value, e.BlendMode, payload.Priority, lifetime);
            }
        }
    }
}
