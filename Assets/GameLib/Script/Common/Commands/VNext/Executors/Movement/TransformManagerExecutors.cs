#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.TransformSystem;
using VContainer;

namespace Game.Commands.VNext
{
    static class TransformManagerExecutorUtility
    {
        public static async UniTask<ITransformManagerService?> ResolveManagerAsync(ActorSource source, CommandContext ctx, CancellationToken ct)
        {
            var (scope, resolveError) = await ActorScopeResolver.ResolveAsync(source, ctx, ct);
            if (scope == null)
                throw new CommandExecutionException(CommandRunFailureKind.ResolveFailed, $"TransformManager target resolve failed: {resolveError}");

            for (var current = scope; current != null; current = current.Parent)
            {
                EnsureScopeBuiltIfNeeded(current);
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<ITransformManagerService>(out var manager) && manager != null)
                    return manager;
            }

            return null;
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            ScopeFeatureInstallerUtility.EnsureScopeBuiltIfNeeded(scope);
        }
    }

    public sealed class TransformManagerMovementExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TransformManagerMovement;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TransformManagerMovementCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TransformManagerMovementCommandData is required.");

            var manager = await TransformManagerExecutorUtility.ResolveManagerAsync(typed.Target, ctx, ct);
            if (manager == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITransformManagerService is missing.");

            if (typed.Operation == TransformManagerEntryOperation.Remove)
            {
                if (string.IsNullOrWhiteSpace(typed.NormalizedEntryId))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Entry Id is required for remove operation.");

                if (!manager.RemoveMovement(typed.NormalizedEntryId))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Movement entry '{typed.NormalizedEntryId}' was not found.");

                return;
            }

            if (!typed.TryBuildSettings(out var settings))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Movement entry settings are invalid.");

            var entry = new TransformManagerMovementEntry(settings, typed.Velocity.GetOrDefault(ctx, UnityEngine.Vector2.zero));
            if (!manager.UpsertMovement(entry))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Movement entry '{settings.EntryId}' upsert failed.");
        }
    }

    public sealed class TransformManagerRotateExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TransformManagerRotate;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TransformManagerRotateCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TransformManagerRotateCommandData is required.");

            var manager = await TransformManagerExecutorUtility.ResolveManagerAsync(typed.Target, ctx, ct);
            if (manager == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITransformManagerService is missing.");

            if (typed.Operation == TransformManagerEntryOperation.Remove)
            {
                if (string.IsNullOrWhiteSpace(typed.NormalizedEntryId))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Entry Id is required for remove operation.");

                if (!manager.RemoveRotate(typed.NormalizedEntryId))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Rotate entry '{typed.NormalizedEntryId}' was not found.");

                return;
            }

            if (!typed.TryBuildSettings(out var settings))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Rotate entry settings are invalid.");

            var entry = new TransformManagerRotateEntry(
                settings,
                typed.OffsetDegrees.GetOrDefault(ctx, 0f),
                typed.AngularVelocity.GetOrDefault(ctx, 0f));
            if (!manager.UpsertRotate(entry))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Rotate entry '{settings.EntryId}' upsert failed.");
        }
    }

    public sealed class TransformManagerScaleExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.TransformManagerScale;

        public async UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not TransformManagerScaleCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "TransformManagerScaleCommandData is required.");

            var manager = await TransformManagerExecutorUtility.ResolveManagerAsync(typed.Target, ctx, ct);
            if (manager == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "ITransformManagerService is missing.");

            if (typed.Operation == TransformManagerEntryOperation.Remove)
            {
                if (string.IsNullOrWhiteSpace(typed.NormalizedEntryId))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Entry Id is required for remove operation.");

                if (!manager.RemoveScale(typed.NormalizedEntryId))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Scale entry '{typed.NormalizedEntryId}' was not found.");

                return;
            }

            if (!typed.TryBuildSettings(out var settings))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "Scale entry settings are invalid.");

            var entry = new TransformManagerScaleEntry(settings, typed.LocalScale.GetOrDefault(ctx, UnityEngine.Vector3.one));
            if (!manager.UpsertScale(entry))
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"Scale entry '{settings.EntryId}' upsert failed.");
        }
    }
}
