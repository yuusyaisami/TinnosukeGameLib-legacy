#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Rotation;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class VelocityDrivenRotationExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.VelocityDrivenRotation;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            _ = ct;
            if (data is not VelocityDrivenRotationCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "VelocityDrivenRotationCommandData is required.");

            var resolver = ctx?.Resolver;
            if (resolver == null || !resolver.TryResolve<IVelocityRotationSettingsAdapter>(out var adapter) || adapter == null)
                return UniTask.CompletedTask;

            var settings = adapter.CurrentSettings;

            switch (typed.Mode)
            {
                case VelocityDrivenRotationCommandMode.SetEnabled:
                    settings.Enabled = typed.Enabled;
                    break;
                case VelocityDrivenRotationCommandMode.SetMode:
                    settings.Mode = typed.RotationMode;
                    break;
                case VelocityDrivenRotationCommandMode.SetSpeedScale:
                    settings.SpeedScale = typed.SpeedScale;
                    settings.UseScalarSpeedScale = typed.UseScalarSpeedScale;
                    settings.SpeedScaleScalar = typed.SpeedScaleScalar;
                    break;
                case VelocityDrivenRotationCommandMode.SetSource:
                    settings.Source = typed.Source;
                    settings.SourceTransform = typed.SourceTransform;
                    settings.SourceRigidbody2D = typed.SourceRigidbody2D;
                    break;
                case VelocityDrivenRotationCommandMode.SetRotateChannelKey:
                    settings.RotateChannelKey = typed.RotateChannelKey;
                    break;
                case VelocityDrivenRotationCommandMode.SetTiltSettings:
                    settings.Tilt = typed.Tilt;
                    break;
                case VelocityDrivenRotationCommandMode.SetSpinSettings:
                    settings.Spin = typed.Spin;
                    break;
                case VelocityDrivenRotationCommandMode.SetFacingSettings:
                    settings.Facing = typed.Facing;
                    break;
                case VelocityDrivenRotationCommandMode.ApplySettings:
                default:
                    settings = typed.Settings;
                    break;
            }

            adapter.ApplySettings(settings);
            return UniTask.CompletedTask;
        }
    }
}
