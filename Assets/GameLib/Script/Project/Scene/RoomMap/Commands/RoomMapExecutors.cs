#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;
using UnityEngine;
using Game.Common;
using VContainer;
namespace Game.Commands.VNext
{
    public sealed class BuildRoomMapExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.BuildRoomMap;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not BuildRoomMapCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "BuildRoomMapCommandData is required.");

            // Resolve from VarStore (override) -> fallback to direct field
            if (!typed.ProfileSource.TryResolve(ctx.Vars, out var profile) || profile == null)
            {
                // Missing profile is not fatal to the whole command list: log and skip this command.
                var varId = typed.ProfileSource.VarId;
                var scopeDesc = ctx.Scope != null ? ctx.Scope.GetType().FullName : "null";
                Debug.LogWarning($"[BuildRoomMapExecutor] Profile not found (VarId={varId}) in scope={scopeDesc}. Skipping build.");
                return;
            }

            var svc = RoomMapCommandExecutorUtility.ResolveRoomMapServiceOrThrow(ctx);
            var profileName = profile.name ?? profile.GetType().Name;
            float? delayOverrideSeconds = null;
            if (typed.VisualDelayOverride.HasSource)
            {
                var dynScope = ctx.Scope ?? ctx.Actor;
                if (dynScope != null)
                {
                    var dynCtx = new SimpleDynamicContext(ctx.Vars ?? NullVarStore.Instance, dynScope);
                    if (typed.VisualDelayOverride.TryGet(dynCtx, out var resolvedDelay))
                    {
                        delayOverrideSeconds = resolvedDelay;
                    }
                }
            }
            try
            {
                var targetScope = ctx.Actor ?? ctx.Scope;
                await svc.BuildAsync(profile, targetScope, delayOverrideSeconds, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
        }
    }

    public sealed class ClearRoomMapExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ClearRoomMap;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ClearRoomMapCommandData)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ClearRoomMapCommandData is required.");

            var svc = RoomMapCommandExecutorUtility.ResolveRoomMapServiceOrThrow(ctx);
            await svc.ClearAsync(ct);
        }
    }

    public sealed class RemoveRoomMapRectExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RemoveRoomMapRect;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RemoveRoomMapRectCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RemoveRoomMapRectCommandData is required.");

            var svc = RoomMapCommandExecutorUtility.ResolveRoomMapServiceOrThrow(ctx);
            await svc.RemoveRectAsync(typed.Rect, ct);
        }
    }

    public sealed class ApplyRoomMapVisualExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ApplyRoomMapVisual;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ApplyRoomMapVisualCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ApplyRoomMapVisualCommandData is required.");

            // Resolve from VarStore (override) -> fallback to direct field
            if (!typed.VisualSource.TryResolve(ctx.Vars, out var visual) || visual == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Visual is null (and could not be resolved from vars).");

            var svc = RoomMapCommandExecutorUtility.ResolveRoomMapServiceOrThrow(ctx);
            await svc.ApplyVisualAsync(visual, ct);
        }
    }

    public sealed class GetRoomMapCenterExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.GetRoomMapCenter;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not GetRoomMapCenterCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "GetRoomMapCenterCommandData is required.");

            // Resolve profile from vars or direct field
            if (!typed.ProfileSource.TryResolve(ctx.Vars, out var profile) || profile == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Profile is null (and could not be resolved from vars).");

            // Compute center using layout information only (no instance / build required)
            var center = ComputeRoomCenterFromProfile(profile);

            // Try to set into the nearest IBlackboardService if available, else set into local vars
            var originScope = ctx.Scope;
            var set = false;
            if (originScope != null && originScope.Resolver != null && originScope.Resolver.TryResolve<Game.Common.IBlackboardService>(out var bbSvc) && bbSvc != null)
            {
                bbSvc.TryGlobalSetVariant(Game.Vars.Generated.VarIds.GameLib.RoomMap.roomCenterPos, Game.Common.DynamicVariant.FromVector3(center));
                set = true;
            }

            if (!set && ctx.Vars != null)
            {
                ctx.Vars.TrySetVariant(Game.Vars.Generated.VarIds.GameLib.RoomMap.roomCenterPos, Game.Common.DynamicVariant.FromVector3(center));
                set = true;
            }

            return UniTask.CompletedTask;
        }

        static UnityEngine.Vector3 ComputeRoomCenterFromProfile(Game.RoomMap.RoomMapProfileSO profile)
        {
            if (profile == null)
                return UnityEngine.Vector3.zero;

            var layout = profile.Layout;
            if (layout == null)
                return UnityEngine.Vector3.zero;

            var sum = UnityEngine.Vector3.zero;
            var count = 0;

            for (int layerIndex = 0; layerIndex < layout.LayerCount; layerIndex++)
            {
                if (!layout.TryGetLayer(layerIndex, out var layer) || layer == null)
                    continue;

                var width = layer.Width;
                var height = layer.Height;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var tileId = layer.GetTileId(x, y);
                        if (tileId == 0)
                            continue;

                        var world = Game.RoomMap.RoomMapTransformUtility.CellToWorld(profile, x, y);
                        sum += world;
                        count++;
                    }
                }
            }

            return count == 0 ? UnityEngine.Vector3.zero : sum / count;
        }
    }

    static class RoomMapCommandExecutorUtility
    {
        public static Game.RoomMap.IRoomMapSystemService ResolveRoomMapServiceOrThrow(CommandContext ctx)
        {
            if (ctx == null)
                throw new CommandExecutionException(CommandRunFailureKind.Exception, "CommandContext is null.");

            var origin = ctx.Scope;
            if (origin == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "Scope is null.");

            // Try candidate scope kinds in order of preference: Scene -> Field -> Project -> origin
            var candidates = new System.Collections.Generic.List<LifetimeScopeKind>();
            candidates.Add(LifetimeScopeKind.Scene);
            candidates.Add(LifetimeScopeKind.Field);
            candidates.Add(LifetimeScopeKind.Project);

            foreach (var kind in candidates)
            {
                try
                {
                    var node = ScopeNodeHierarchy.FindNearestAncestorByKind(origin, kind, includeSelf: true);
                    if (node == null) continue;
                    var resolver = node.Resolver;
                    if (resolver == null) continue;
                    if (resolver.TryResolve<Game.RoomMap.IRoomMapSystemService>(out var svc) && svc != null)
                        return svc;
                }
                catch (ArgumentException ex)
                {
                    Debug.LogException(ex);
                }
            }

            // Finally try the origin resolver directly
            var originResolver = origin.Resolver;
            if (originResolver != null && originResolver.TryResolve<Game.RoomMap.IRoomMapSystemService>(out var originSvc) && originSvc != null)
                return originSvc;

            throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, "IRoomMapSystemService is not registered in the nearest Scene/Field/Project/Scope. Add RoomMapSystemMB to the appropriate scope (Scene or Field).");
        }

        private static string DescribeScope(object? origin)
        {
            if (origin == null) return "null";
            try
            {
                var type = origin.GetType();
                string? name = null;
                var nameProp = type.GetProperty("Name");
                if (nameProp != null)
                    name = nameProp.GetValue(origin)?.ToString();
                if (string.IsNullOrEmpty(name))
                {
                    var goProp = type.GetProperty("gameObject");
                    if (goProp != null)
                    {
                        var go = goProp.GetValue(origin);
                        var goNameProp = go?.GetType().GetProperty("name");
                        if (goNameProp != null)
                            name = goNameProp.GetValue(go)?.ToString();
                    }
                }
                return $"{type.FullName}{(string.IsNullOrEmpty(name) ? string.Empty : $" (name={name})")}";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return origin.ToString() ?? "null";
            }
        }
    }
}
