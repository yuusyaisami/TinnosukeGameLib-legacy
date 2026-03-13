#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Direction;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SetDirectionExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetDirection;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetDirectionCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetDirectionCommandData is required.");

            if (string.IsNullOrEmpty(typed.LayerTag))
                return UniTask.CompletedTask;

            if (!ctx.Resolver.TryResolve<IDirectionChannelHub>(out var hub) || hub == null)
                return UniTask.CompletedTask;

            if (!hub.TryGetLayer(typed.LayerTag, out var handle))
            {
                if (!typed.AutoCreateLayerIfMissing)
                    return UniTask.CompletedTask;

                var def = new DirectionLayerDef(
                    typed.LayerTag,
                    typed.CreateIfMissing.InitialDirection,
                    typed.CreateIfMissing.Priority,
                    typed.CreateIfMissing.BlendMode,
                    typed.CreateIfMissing.Influence,
                    typed.CreateIfMissing.TransitionSpeedOverride,
                    typed.CreateIfMissing.EnabledByDefault);
                handle = hub.RegisterLayer(typed.LayerTag, def);
            }

            if (handle != null)
            {
                var dir = ResolveDirection(typed, ctx);
                if (typed.Normalize)
                    dir = dir.normalized;

                handle.TrySetDirection(dir);
            }

            return UniTask.CompletedTask;
        }

        static Vector2 ResolveDirection(SetDirectionCommandData typed, CommandContext ctx)
        {
            switch (typed.InputMode)
            {
                case SetDirectionInputMode.DynamicVector2:
                    if (!typed.DynamicDirection2.HasSource)
                        return typed.Direction;
                    return typed.DynamicDirection2.GetOrDefault(ctx, typed.Direction);

                case SetDirectionInputMode.DynamicVector3:
                    if (!typed.DynamicDirection3.HasSource)
                        return typed.Direction;
                    var fallback3 = new Vector3(typed.Direction.x, typed.Direction.y, 0f);
                    var resolved3 = typed.DynamicDirection3.GetOrDefault(ctx, fallback3);
                    return new Vector2(resolved3.x, resolved3.y);

                case SetDirectionInputMode.LiteralVector2:
                default:
                    return typed.Direction;
            }
        }
    }
}
