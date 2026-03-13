#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.CameraSystem;
using Game.Commands;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CameraPostProcessExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CameraPostProcess;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CameraPostProcessCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CameraPostProcessCommandData is required.");

            if (!TryResolve(ctx, out var camera))
            {
                Debug.LogWarning("[CameraPostProcessExecutor] CameraSystemService not found on actor scope.");
                return UniTask.CompletedTask;
            }

            var tag = typed.Tag;
            switch (typed.Action)
            {
                case CameraPostProcessCommandAction.ApplyPreset:
                    HandleApplyPreset(typed, camera, tag);
                    break;
                case CameraPostProcessCommandAction.ClearLayer:
                    camera.PostProcess.ClearLayer(tag);
                    break;
                case CameraPostProcessCommandAction.ClearAll:
                    camera.PostProcess.ClearAllLayers();
                    break;
                case CameraPostProcessCommandAction.Reset:
                    camera.ResetPostProcess();
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void HandleApplyPreset(CameraPostProcessCommandData typed, ICameraSystemService camera, string tag)
        {
            var preset = typed.Preset;

            if (preset == null)
                return;

            preset.Apply(tag, camera.PostProcess);
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
