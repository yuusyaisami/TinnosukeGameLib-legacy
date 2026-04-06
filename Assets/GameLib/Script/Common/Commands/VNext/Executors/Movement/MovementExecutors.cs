#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Movement;
using Game.Scalar;
using Game.Scalar.Generated;
using Game.TransformSystem;
using VContainer;
using UnityEngine;
using Game.DI;

namespace Game.Commands.VNext
{
    public sealed class SetVelocityExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetVelocity;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetVelocityCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetVelocityCommandData is required.");

            if (string.IsNullOrEmpty(typed.ChannelKey))
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IMovementChannelHub>(out var hub))
                return UniTask.CompletedTask;

            if (!MovementCommandExecutorUtility.TryGetOrCreateChannel(hub, typed.ChannelKey, typed.AutoCreateChannelIfMissing, typed.AutoCreateSettings, out var handle))
                return UniTask.CompletedTask;

            var velocity = typed.Velocity.Resolve(ctx);
            if (typed.Immediate)
                handle.SetImmediateVelocity(velocity);
            else
                handle.Velocity = velocity;
            return UniTask.CompletedTask;
        }
    }

    public sealed class AddForceExecutor : ICommandExecutor
    {
        const int ResolveRetryFrames = 5;

        public int CommandId => CommandIds.AddForce;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not AddForceCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AddForceCommandData is required.");

            var force = typed.Force.Resolve(ctx);
            switch (typed.TargetKind)
            {
                case AddForceTargetKind.MovementChannel:
                    ApplyToMovementChannel(ctx, typed, force);
                    break;
                case AddForceTargetKind.TransformChannelRigidbody2D:
                    await ApplyToTransformChannelRigidbody2DAsync(ctx, typed, force, typed.WriteMode, typed.ForceMode, ct);
                    break;
            }
        }

        static void ApplyToMovementChannel(CommandContext ctx, AddForceCommandData typed, Vector2 force)
        {
            if (string.IsNullOrEmpty(typed.ChannelKey))
            {
                Debug.LogWarning("[AddForceExecutor] ChannelKey is null or empty.");
                return;
            }

            if (!ctx.Resolver.TryResolve<IMovementChannelHub>(out var hub))
            {
                Debug.LogWarning($"[AddForceExecutor] IMovementChannelHub could not be resolved. channel='{typed.ChannelKey}'");
                return;
            }

            if (!hub.TryGetChannel(typed.ChannelKey, out var handle))
            {
                Debug.LogWarning($"[AddForceExecutor] Movement channel was not found. channel='{typed.ChannelKey}'");
                return;
            }

            if (typed.WriteMode == AddForceWriteMode.OverrideVelocity)
            {
                handle.SetImmediateVelocity(force);
                return;
            }

            handle.AddForce(force);
        }

        static async UniTask ApplyToTransformChannelRigidbody2DAsync(
            CommandContext ctx,
            AddForceCommandData typed,
            Vector2 force,
            AddForceWriteMode writeMode,
            ForceMode2D forceMode,
            CancellationToken ct)
        {
            var (resolvedScope, resolveError) = await ActorScopeResolver.ResolveAsync(typed.Target, ctx, ct);
            if (resolvedScope == null)
            {
                Debug.LogWarning($"[AddForceExecutor] TransformChannel target resolve failed: {resolveError}");
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"AddForce target resolve failed: {resolveError}");
            }

            ITransformChannelRuntime? runtime = null;
            for (var attempt = 0; attempt <= ResolveRetryFrames; attempt++)
            {
                if (TryResolveTransformChannelRuntime(resolvedScope, typed.TransformChannelTag, out runtime) && runtime != null)
                    break;

                if (attempt == ResolveRetryFrames)
                    break;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (runtime != null)
            {
                if (writeMode == AddForceWriteMode.OverrideVelocity)
                {
                    if (runtime.TryApplyRigidbody2DSettings(
                            applySimulated: false,
                            simulated: false,
                            applyGravityScale: false,
                            gravityScale: 0f,
                            applyFreezeRotation: false,
                            freezeRotation: false,
                            applyLinearVelocity: true,
                            linearVelocity: force,
                            applyAngularVelocity: false,
                            angularVelocity: 0f))
                    {
                        return;
                    }
                }
                else if (runtime.TryAddForceToRigidbody2D(force, forceMode))
                {
                    return;
                }
            }

            var directRoot = resolvedScope.Identity?.SelfTransform;
            var directRigidbody = directRoot != null
                ? directRoot.GetComponent<Rigidbody2D>() ?? directRoot.GetComponentInChildren<Rigidbody2D>(true)
                : null;
            if (directRigidbody != null)
            {
                if (writeMode == AddForceWriteMode.OverrideVelocity)
                    directRigidbody.linearVelocity = force;
                else
                    directRigidbody.AddForce(force, forceMode);
                return;
            }

            var scopeId = resolvedScope.Identity?.Id ?? "(no id)";
            var scopeKind = resolvedScope.Identity?.Kind.ToString() ?? resolvedScope.Kind.ToString();
            var channelTag = string.IsNullOrWhiteSpace(typed.TransformChannelTag) ? "default" : typed.TransformChannelTag.Trim();
            Debug.LogWarning($"[AddForceExecutor] Failed to apply force. WriteMode={writeMode}, TransformChannel runtime(tag={channelTag}) and Rigidbody2D were both unavailable. target={scopeKind}:{scopeId}");
        }

        static bool TryResolveTransformChannelRuntime(IScopeNode scope, string channelTag, out ITransformChannelRuntime? runtime)
        {
            runtime = null;

            var normalizedTag = string.IsNullOrWhiteSpace(channelTag)
                ? TransformChannelTagUtility.DefaultTag
                : channelTag.Trim();

            for (var current = scope; current != null; current = current.Parent)
            {
                var resolver = current.Resolver;
                if (resolver == null)
                    continue;

                if (!resolver.TryResolve<ITransformChannelHubService>(out var hub) || hub == null)
                    continue;

                if (hub.TryGetRuntime(normalizedTag, out var byTag) && byTag != null)
                {
                    runtime = byTag;
                    return true;
                }

                if (hub.TryGetRuntime(TransformChannelTagUtility.DefaultTag, out var byDefault) && byDefault != null)
                {
                    runtime = byDefault;
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class SetChannelEnabledExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetChannelEnabled;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetChannelEnabledCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetChannelEnabledCommandData is required.");

            if (string.IsNullOrEmpty(typed.ChannelKey) || string.IsNullOrEmpty(typed.LayerKey))
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IMovementChannelHub>(out var hub))
                return UniTask.CompletedTask;

            if (!hub.TryGetChannel(typed.ChannelKey, out var handle))
                return UniTask.CompletedTask;

            handle.SetEnabled(typed.LayerKey, typed.Enabled.Resolve(ctx));
            return UniTask.CompletedTask;
        }
    }

    public sealed class SetAllChannelsEnabledExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetAllChannelsEnabled;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetAllChannelsEnabledCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetAllChannelsEnabledCommandData is required.");

            if (string.IsNullOrEmpty(typed.LayerKey))
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<MovementChannelHubService>(out var hub))
                return UniTask.CompletedTask;

            hub.SetAllEnabled(typed.LayerKey, typed.Enabled.Resolve(ctx));
            return UniTask.CompletedTask;
        }
    }

    public sealed class SetChannelInfluenceExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetChannelInfluence;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetChannelInfluenceCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetChannelInfluenceCommandData is required.");

            if (string.IsNullOrEmpty(typed.ChannelKey))
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IMovementChannelHub>(out var hub))
                return UniTask.CompletedTask;

            if (!hub.TryGetChannel(typed.ChannelKey, out var handle))
                return UniTask.CompletedTask;

            handle.Influence = typed.Influence.Resolve(ctx);
            return UniTask.CompletedTask;
        }
    }

    public sealed class ResetAllVelocitiesExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.ResetAllVelocities;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not ResetAllVelocitiesCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "ResetAllVelocitiesCommandData is required.");

            if (!ctx.Resolver.TryResolve<MovementChannelHubService>(out var hub))
                return UniTask.CompletedTask;

            hub.ResetAllVelocities();
            return UniTask.CompletedTask;
        }
    }

    public sealed class CreateMovementChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.CreateMovementChannel;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not CreateMovementChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "CreateMovementChannelCommandData is required.");

            if (string.IsNullOrEmpty(typed.ChannelKey))
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IMovementChannelHub>(out var hub))
                return UniTask.CompletedTask;

            if (typed.SkipIfExists && hub.ContainsChannel(typed.ChannelKey))
                return UniTask.CompletedTask;

            if (!typed.SkipIfExists && hub.ContainsChannel(typed.ChannelKey))
                hub.UnregisterChannel(typed.ChannelKey);

            hub.RegisterChannel(typed.ChannelKey, MovementCommandExecutorUtility.BuildChannelDef(typed.ChannelKey, typed.Settings));
            return UniTask.CompletedTask;
        }
    }

    public sealed class RemoveMovementChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.RemoveMovementChannel;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not RemoveMovementChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "RemoveMovementChannelCommandData is required.");

            if (string.IsNullOrEmpty(typed.ChannelKey))
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IMovementChannelHub>(out var hub))
                return UniTask.CompletedTask;

            hub.UnregisterChannel(typed.ChannelKey);
            return UniTask.CompletedTask;
        }
    }

    public sealed class SetMovementModuleExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetMovementModule;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetMovementModuleCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetMovementModuleCommandData is required.");

            switch (typed.Target)
            {
                case MovementModuleTarget.Motion:
                    ApplyMotion(ctx, typed);
                    break;
                case MovementModuleTarget.Homing:
                    ApplyHoming(ctx, typed);
                    break;
            }

            return UniTask.CompletedTask;
        }

        static void ApplyMotion(CommandContext ctx, SetMovementModuleCommandData typed)
        {
            var motion = ResolveMotion(ctx);
            if (motion == null)
                return;

            var preset = typed.ResolveMotionPreset();
            if (preset != null)
            {
                motion.SetMotion(preset);
                return;
            }

            if (typed.ClearMotionIfNull)
                motion.ClearMotion();
        }

        static void ApplyHoming(CommandContext ctx, SetMovementModuleCommandData typed)
        {
            var homing = ResolveHoming(ctx);
            if (homing == null)
                return;

            if (!string.IsNullOrEmpty(typed.HomingLayerKey))
            {
                var enabled = typed.HomingEnabled.Resolve(ctx);
                homing.HomingLayer.Set(typed.HomingLayerKey, enabled);
            }

            if (typed.ApplyBlendParams && homing is IHomingMovementConfigurable configurable)
                configurable.SetBlendParams(typed.BlendParams ?? HomingBlendParams.Default);
        }

        static IMotionMovement? ResolveMotion(CommandContext ctx)
        {
            if (ctx.Resolver.TryResolve<IInputMovementService>(out var inputSvc) && inputSvc?.Motion != null)
                return inputSvc.Motion;

            if (ctx.Resolver.TryResolve<IMotionMovement>(out var motion))
                return motion;

            return null;
        }

        static IHomingMovement? ResolveHoming(CommandContext ctx)
        {
            if (ctx.Resolver.TryResolve<IInputMovementService>(out var inputSvc) && inputSvc?.Homing != null)
                return inputSvc.Homing;

            if (ctx.Resolver.TryResolve<IHomingMovement>(out var homing))
                return homing;

            return null;
        }
    }

    public sealed class SetInputMovementExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetInputMovement;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetInputMovementCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetInputMovementCommandData is required.");

            if (typed.ApplyEnabled)
                ApplyEnabled(ctx, typed);
            if (typed.ApplyAcceleration)
                ApplyAcceleration(ctx, typed);
            if (typed.ApplyMotion)
                ApplyMotion(ctx, typed);
            if (typed.ApplyHoming)
                ApplyHoming(ctx, typed);
            if (typed.ApplySpeed)
                ApplySpeed(ctx, typed);

            return UniTask.CompletedTask;
        }

        static void ApplyEnabled(CommandContext ctx, SetInputMovementCommandData typed)
        {
            if (!ctx.Resolver.TryResolve<IInputMovementService>(out var inputService) || inputService == null)
                return;

            if (inputService is not IEnabledService enabledService)
                return;

            enabledService.SetEnabled(typed.Enabled.Resolve(ctx));
        }

        static void ApplySpeed(CommandContext ctx, SetInputMovementCommandData typed)
        {
            if (!ctx.Resolver.TryResolve<IBaseScalarService>(out var scalar) || scalar == null)
                return;

            var speedMul = Mathf.Max(0f, typed.SpeedMultiplier.Resolve(ctx));
            scalar.SetRuntimeBaseline(ScalarKeys.GameLib.Movement.SpeedMultiplier, speedMul);
        }

        static void ApplyMotion(CommandContext ctx, SetInputMovementCommandData typed)
        {
            var motion = ResolveMotion(ctx);
            if (motion == null)
                return;

            var preset = typed.ResolveMotionPreset();
            if (preset != null)
            {
                motion.SetMotion(preset);
                return;
            }

            if (typed.ClearMotionIfNull)
                motion.ClearMotion();
        }

        static void ApplyHoming(CommandContext ctx, SetInputMovementCommandData typed)
        {
            var homing = ResolveHoming(ctx);
            if (homing == null)
                return;

            if (!string.IsNullOrEmpty(typed.HomingLayerKey))
            {
                var enabled = typed.HomingEnabled.Resolve(ctx);
                homing.HomingLayer.Set(typed.HomingLayerKey, enabled);
            }

            if (typed.ApplyBlendParams && homing is IHomingMovementConfigurable configurable)
                configurable.SetBlendParams(typed.BlendParams ?? HomingBlendParams.Default);
        }

        static void ApplyAcceleration(CommandContext ctx, SetInputMovementCommandData typed)
        {
            var settings = new InputAccelerationSettings
            {
                Enabled = typed.AccelerationEnabled.Resolve(ctx),
                Accel = Mathf.Max(0f, typed.Accel.Resolve(ctx)),
                Decel = Mathf.Max(0f, typed.Decel.Resolve(ctx))
            };

            if (ctx.Resolver.TryResolve<IReadOnlyList<IInputDirectionSettingsAdapter>>(out var adapters) &&
                adapters != null &&
                adapters.Count > 0)
            {
                for (int i = 0; i < adapters.Count; i++)
                {
                    var adapter = adapters[i];
                    if (adapter == null)
                        continue;

                    var next = adapter.CurrentSettings;
                    next.Acceleration = settings;
                    adapter.ApplySettings(next);
                }
                return;
            }

            if (ctx.Resolver.TryResolve<IInputDirectionSettingsAdapter>(out var singleAdapter) && singleAdapter != null)
            {
                var next = singleAdapter.CurrentSettings;
                next.Acceleration = settings;
                singleAdapter.ApplySettings(next);
            }
        }

        static IMotionMovement? ResolveMotion(CommandContext ctx)
        {
            if (ctx.Resolver.TryResolve<IInputMovementService>(out var inputSvc) && inputSvc?.Motion != null)
                return inputSvc.Motion;

            if (ctx.Resolver.TryResolve<IMotionMovement>(out var motion))
                return motion;

            return null;
        }

        static IHomingMovement? ResolveHoming(CommandContext ctx)
        {
            if (ctx.Resolver.TryResolve<IInputMovementService>(out var inputSvc) && inputSvc?.Homing != null)
                return inputSvc.Homing;

            if (ctx.Resolver.TryResolve<IHomingMovement>(out var homing))
                return homing;

            return null;
        }
    }

    static class MovementCommandExecutorUtility
    {
        public static bool TryGetOrCreateChannel(
            IMovementChannelHub hub,
            string channelKey,
            bool autoCreate,
            MovementChannelCreateSettings? settings,
            out IMovementChannelHandle handle)
        {
            if (hub.TryGetChannel(channelKey, out handle))
                return true;

            if (!autoCreate)
                return false;

            handle = hub.RegisterChannel(channelKey, BuildChannelDef(channelKey, settings));
            return handle != null;
        }

        public static MovementChannelDef BuildChannelDef(string channelKey, MovementChannelCreateSettings? settings)
        {
            var resolved = settings ?? new MovementChannelCreateSettings();
            var tag = string.IsNullOrWhiteSpace(resolved.Tag) ? channelKey : resolved.Tag;
            return new MovementChannelDef
            {
                Tag = tag,
                Priority = resolved.Priority,
                BlendOp = resolved.BlendOp,
                Influence = Mathf.Clamp01(resolved.Influence),
                EnabledByDefault = resolved.EnabledByDefault,
                SmoothingLambda = Mathf.Max(0f, resolved.SmoothingLambda),
                DecelerationLambda = Mathf.Max(0f, resolved.DecelerationLambda),
            };
        }
    }
}
