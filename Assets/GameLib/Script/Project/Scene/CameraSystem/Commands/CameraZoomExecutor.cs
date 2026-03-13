#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.CameraSystem;
using Game.Commands;
using UnityEngine;
using DG.Tweening;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CameraZoomExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CameraZoom;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CameraZoomCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CameraZoomCommandData is required.");

            if (!TryResolve(ctx, out var camera))
            {
                Debug.LogWarning("[CameraZoomExecutor] CameraSystemService not found on actor scope.");
                return UniTask.CompletedTask;
            }

            var zoom = camera.Zoom;
            var layerTag = typed.LayerTag;
            switch (typed.Action)
            {
                case CameraZoomCommandAction.SetLayer:
                    var target = typed.TargetSize.GetOrDefault(ctx, zoom.Current);
                    // Convert optional numeric lambda to a DG.Tweening.Ease value (default to Linear)
                    var ease = typed.Lambda;
                    zoom.SetLayer(layerTag, target, typed.Priority, ease);
                    break;
                case CameraZoomCommandAction.ClearLayer:
                    zoom.ClearLayer(layerTag);
                    break;
                case CameraZoomCommandAction.ClearAll:
                    zoom.ClearAllLayers();
                    break;
                case CameraZoomCommandAction.Reset:
                    camera.ResetZoom();
                    break;
            }

            return UniTask.CompletedTask;
        }

        static bool TryResolve(CommandContext ctx, out ICameraSystemService camera)
        {
            var owner = ctx.Actor ?? ctx.Scope;
            var resolver = owner.Resolver;
            if (resolver.TryResolve<ICameraSystemService>(out var svc) && svc != null)
            {
                camera = svc;
                return true;
            }

            // Fallback: search LTS registry for an Entity scope with category "Camera".
            var cameraScope = ctx.ResolveOtherScope(new CommandTargetIdentityFilter
            {
                kind = LifetimeScopeKind.Entity,
                id = "Camera",
                category = string.Empty,
                requireActive = true,
                searchScope = CommandTargetSearchScope.All,
            });

            if (cameraScope != null && cameraScope.Resolver != null && cameraScope.Resolver.TryResolve<ICameraSystemService>(out var svc2) && svc2 != null)
            {
                camera = svc2;
                return true;
            }

            camera = null!;
            return false;
        }
    }
}
