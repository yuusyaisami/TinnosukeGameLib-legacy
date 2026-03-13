#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.CameraSystem;
using Game.Commands;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class CameraShakeExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CameraShake;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CameraShakeCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CameraShakeCommandData is required.");

            if (!TryResolve(ctx, out var camera))
            {
                Debug.LogWarning("[CameraShakeExecutor] CameraSystemService not found on actor scope.");
                return UniTask.CompletedTask;
            }

            var fx = camera.Fx;
            switch (typed.Action)
            {
                case CameraShakeCommandAction.Play:
                    if (!CameraShakePresetSource.TryGet(typed.Preset, out var preset))
                    {
                        Debug.LogWarning("[CameraShakeExecutor] Play requires a valid camera shake preset source.");
                        break;
                    }

                    var priority = typed.Priority.GetOrDefault(ctx, 0);
                    fx.PlayShake(preset, priority);
                    break;

                case CameraShakeCommandAction.StopHandle:
                    var handle = typed.Handle.GetOrDefault(ctx, 0);
                    var fadeOut = typed.FadeOutSeconds.GetOrDefault(ctx, 0.1f);
                    if (handle <= 0)
                    {
                        Debug.LogWarning("[CameraShakeExecutor] StopHandle requires a valid handle.");
                        break;
                    }

                    fx.StopShake(handle, fadeOut);
                    break;

                case CameraShakeCommandAction.StopAll:
                    var fadeAll = typed.FadeOutAllSeconds.GetOrDefault(ctx, 0.1f);
                    fx.StopAll(fadeAll);
                    break;

                case CameraShakeCommandAction.SetGlobalIntensity:
                    var intensity = typed.Intensity.GetOrDefault(ctx, 1f);
                    fx.SetGlobalIntensity(intensity);
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
