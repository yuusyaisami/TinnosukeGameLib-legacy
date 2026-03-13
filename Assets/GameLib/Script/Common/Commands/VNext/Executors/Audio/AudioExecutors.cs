#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Audio;
using Game.Common;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class PlayAudioExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.PlayAudio;

        static string DescribeScope(IScopeNode? node)
        {
            if (node == null)
                return "<null>";

            if (node is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";

            if (node is Component component && component.gameObject != null)
                return component.gameObject.name + " (" + component.GetType().Name + ")";

            if (node.Identity != null)
                return node.Identity.Id + " (" + node.Identity.Kind + ")";

            return node.GetType().Name;
        }

        static string DescribeCommand(ICommandData data)
        {
            if (data == null)
                return "<null>";

            var name = data.GetType().Name;
            const string suffix = "CommandData";
            if (name.EndsWith(suffix, System.StringComparison.Ordinal))
                name = name.Substring(0, name.Length - suffix.Length);
            return name + "(Id=" + data.CommandId + ")";
        }

        internal static string BuildContextInfo(CommandContext ctx, ICommandData data)
        {
            var actor = DescribeScope(ctx.Actor);
            var scope = DescribeScope(ctx.Scope);
            var cmd = DescribeCommand(data);
            var debugData = data?.DebugData ?? string.Empty;
            if (string.IsNullOrEmpty(debugData))
                return $"Actor={actor} Scope={scope} Cmd={cmd}";
            return $"Actor={actor} Scope={scope} Cmd={cmd} CmdData={debugData}";
        }

        static bool TryResolveScopeTransform(IScopeNode? scope, out Transform? transform)
        {
            transform = null;
            if (scope == null)
                return false;

            var identityTransform = scope.Identity?.SelfTransform;
            if (identityTransform != null)
            {
                transform = identityTransform;
                return true;
            }

            if (scope is Component component && component != null)
            {
                transform = component.transform;
                return true;
            }

            return false;
        }

        static void ApplyPlaybackPosition(PlayAudioCommandData typed, CommandContext ctx, ref SoundRequest request)
        {
            if (typed.PlaybackMode != AudioPlaybackPositionMode.Local)
                return;

            request.IsLocalPlayback = true;

            var localOffset = typed.LocalPlaybackOffset.GetOrDefault(ctx, Vector3.zero);
            if (!typed.LocalPlaybackOrigin.HasSource &&
                TryResolveScopeTransform(ctx.Actor ?? ctx.Scope, out var fallbackTransform) &&
                fallbackTransform != null)
            {
                request.FollowTarget = fallbackTransform;
                request.LocalOffset = localOffset;
                request.WorldPosition = fallbackTransform.position + localOffset;
                return;
            }

            if (typed.LocalPlaybackOrigin.TryGetSource<ActorWorldPosition3Source>(out var actorPositionSource) &&
                actorPositionSource != null &&
                actorPositionSource.TryResolveTransform(ctx, out var followTarget) &&
                followTarget != null)
            {
                request.FollowTarget = followTarget;
                request.LocalOffset = localOffset;
                request.WorldPosition = followTarget.position + localOffset;
                return;
            }

            var origin = typed.LocalPlaybackOrigin.GetOrDefault(ctx, Vector3.zero);
            request.WorldPosition = origin + localOffset;
            request.LocalOffset = Vector3.zero;
        }

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not PlayAudioCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "PlayAudioCommandData is required.");

            if (!ctx.Resolver.TryResolve<IAudioService>(out var audioService) || audioService == null)
                return UniTask.CompletedTask;

            if (typed.Cue == null)
            {
                Debug.Log($"[PlayAudioExecutor] Cue is null -> Stop. {BuildContextInfo(ctx, data)}");
            }

            var request = new SoundRequest
            {
                VolumeScale = typed.VolumeScale,
                FadeInSeconds = typed.FadeInSeconds.GetOrDefault(ctx, 0f),
                FadeOutSeconds = typed.FadeOutSeconds,
                IgnoreIfAlreadyPlaying = typed.IgnoreIfAlreadyPlaying,
                PlaybackStartOffsetSeconds = typed.PlaybackStartOffsetSeconds.GetOrDefault(ctx, 0f),
            };

            ApplyPlaybackPosition(typed, ctx, ref request);

            if (typed.OverridePlaybackSpeed)
                request.PlaybackSpeedScaleOverride = typed.PlaybackSpeedScale.GetOrDefault(ctx, 1f);

            if (typed.OverrideApplyPlaybackSpeedToPitch)
                request.ApplyPlaybackSpeedToPitchOverride = typed.ApplyPlaybackSpeedToPitch;

            if (!string.IsNullOrEmpty(typed.TagOverride))
                request.Tag = typed.TagOverride;

            if (typed.BusOverride.HasValue)
                request.BusOverride = typed.BusOverride.Value;

            if (typed.SpatializeOverride.HasValue)
                request.SpatializeOverride = typed.SpatializeOverride.Value;

            audioService.PlaySound(typed.Cue, request);
            return UniTask.CompletedTask;
        }
    }

    public sealed class StopAudioExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.StopAudio;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not StopAudioCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "StopAudioCommandData is required.");

            if (!ctx.Resolver.TryResolve<IAudioService>(out var audioService) || audioService == null)
                return UniTask.CompletedTask;

            var tag = typed.Tag ?? string.Empty;
            var tagLabel = string.IsNullOrEmpty(tag) ? "<empty>" : tag;
            var busLabel = typed.BusOverride?.ToString() ?? "<none>";
            Debug.Log($"[StopAudioExecutor] StopAudio. {PlayAudioExecutor.BuildContextInfo(ctx, data)} Tag={tagLabel} Bus={busLabel} FadeOut={typed.FadeOutSeconds:0.###}");

            // Safety: if neither tag nor bus is specified, do nothing (avoid stopping default Sfx bus).
            if (string.IsNullOrEmpty(tag) && !typed.BusOverride.HasValue)
                return UniTask.CompletedTask;

            if (typed.BusOverride.HasValue)
            {
                var request = new SoundRequest
                {
                    Tag = tag,
                    BusOverride = typed.BusOverride.Value,
                    FadeOutSeconds = typed.FadeOutSeconds,
                };
                audioService.PlaySound(cue: null, request);
                return UniTask.CompletedTask;
            }

            // Tag specified but no bus -> try stopping the tag across all buses.
            foreach (AudioBusKind bus in System.Enum.GetValues(typeof(AudioBusKind)))
            {
                var request = new SoundRequest
                {
                    Tag = tag,
                    BusOverride = bus,
                    FadeOutSeconds = typed.FadeOutSeconds,
                };
                audioService.PlaySound(cue: null, request);
            }

            return UniTask.CompletedTask;
        }
    }
}
