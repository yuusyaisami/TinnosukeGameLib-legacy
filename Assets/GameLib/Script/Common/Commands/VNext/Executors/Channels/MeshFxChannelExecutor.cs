#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.MaterialFx;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class MeshFxAnimationChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.MeshFxAnimationChannel;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not MeshFxAnimationChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshFxAnimationChannelCommandData is required.");

            if (!ctx.Resolver.TryResolve<IMeshFxAnimationService>(out var service) || service == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMeshFxAnimationService is missing.");

            if (!typed.ApplyAnimation && !typed.ApplyMaterialPreset)
                return UniTask.CompletedTask;

            var runTask = ExecuteAsync(service, typed, ct);
            if (typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion)
            {
                if (typed.Loop && typed.LoopCount < 0)
                {
                    Debug.LogWarning($"[MeshFxAnimationExecutor] Infinite loop cannot be awaited. Switching to RunInBackground. tag={typed.ChannelTag}");
                    RunInBackground(runTask);
                    return UniTask.CompletedTask;
                }

                return runTask;
            }

            RunInBackground(runTask);
            return UniTask.CompletedTask;
        }

        static async UniTask ExecuteAsync(
            IMeshFxAnimationService service,
            MeshFxAnimationChannelCommandData typed,
            CancellationToken ct)
        {
            var channelTag = typed.ChannelTag;
            var animationPayload = typed.AnimationPayload;
            var materialPayload = typed.MaterialPayload;

            List<MeshFxMaterialAnimationEntry>? materialEntries = null;
            if (typed.ApplyMaterialPreset && materialPayload.Entries.Count > 0)
            {
                materialEntries = ConvertMaterialPresetEntries(materialPayload.Entries);
            }

            var maxWaitSeconds = Mathf.Max(
                typed.ApplyAnimation ? ComputeParameterWaitSeconds(animationPayload.Entries) : 0f,
                typed.ApplyMaterialPreset ? ComputeMaterialWaitSeconds(materialPayload.Entries) : 0f);

            if (!typed.Loop)
            {
                ApplyOnce(service, channelTag, typed, materialEntries);
                await WaitIfNeeded(maxWaitSeconds, ct, forceYieldWhenZero: false);
                return;
            }

            if (typed.LoopCount < 0)
            {
                while (!ct.IsCancellationRequested)
                {
                    ApplyOnce(service, channelTag, typed, materialEntries);
                    await WaitIfNeeded(maxWaitSeconds, ct, forceYieldWhenZero: true);
                }
                return;
            }

            var loopCount = Mathf.Max(1, typed.LoopCount);
            for (int i = 0; i < loopCount && !ct.IsCancellationRequested; i++)
            {
                ApplyOnce(service, channelTag, typed, materialEntries);
                await WaitIfNeeded(maxWaitSeconds, ct, forceYieldWhenZero: true);
            }
        }

        static void ApplyOnce(
            IMeshFxAnimationService service,
            string channelTag,
            MeshFxAnimationChannelCommandData typed,
            IReadOnlyList<MeshFxMaterialAnimationEntry>? materialEntries)
        {
            if (typed.ApplyAnimation && typed.AnimationPayload.Entries.Count > 0)
            {
                var payload = typed.AnimationPayload;
                service.Play(
                    channelTag,
                    payload.ContextTag,
                    payload.Entries,
                    null,
                    payload.ClearContextFirst,
                    materialBasePriority: 0);
            }

            if (typed.ApplyMaterialPreset && materialEntries != null && materialEntries.Count > 0)
            {
                var payload = typed.MaterialPayload;
                service.Play(
                    channelTag,
                    payload.ContextTag,
                    null,
                    materialEntries,
                    payload.ClearContextFirst,
                    payload.Priority);
            }
        }

        static List<MeshFxMaterialAnimationEntry> ConvertMaterialPresetEntries(IReadOnlyList<MeshFxMaterialPresetCommandEntry> entries)
        {
            var result = new List<MeshFxMaterialAnimationEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var src = entries[i].Entry;
                if (string.IsNullOrWhiteSpace(src.Key))
                    continue;

                var duration = src.ApplyWeightFade ? Mathf.Max(0f, src.FadeDuration) : 0f;
                var lifetime = src.LifetimeSeconds;
                if (Mathf.Approximately(lifetime, 0f))
                    lifetime = -1f;

                result.Add(new MeshFxMaterialAnimationEntry
                {
                    Key = src.Key,
                    Value = src.Value,
                    BlendMode = src.BlendMode,
                    DurationSeconds = duration,
                    Easing = src.FadeEase,
                    PriorityOffset = 0,
                    LifetimeSeconds = lifetime
                });
            }

            return result;
        }

        static float ComputeParameterWaitSeconds(IReadOnlyList<MeshFxParameterAnimationEntry> entries)
        {
            var max = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry.WaitForCompletion)
                    continue;

                max = Mathf.Max(max, Mathf.Max(0f, entry.DurationSeconds));
            }

            return max;
        }

        static float ComputeMaterialWaitSeconds(IReadOnlyList<MeshFxMaterialPresetCommandEntry> entries)
        {
            var max = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var wrapped = entries[i];
                if (!wrapped.WaitForCompletion)
                    continue;

                var entry = wrapped.Entry;
                if (!entry.ApplyWeightFade)
                    continue;

                max = Mathf.Max(max, Mathf.Max(0f, entry.FadeDuration));
            }

            return max;
        }

        static async UniTask WaitIfNeeded(float waitSeconds, CancellationToken ct, bool forceYieldWhenZero)
        {
            if (waitSeconds > 0f)
            {
                var ms = Mathf.CeilToInt(waitSeconds * 1000f);
                await UniTask.Delay(ms, cancellationToken: ct);
                return;
            }

            if (forceYieldWhenZero)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        static void RunInBackground(UniTask task)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception)
                {
                }
            });
        }
    }

    public sealed class MeshFxChannelControlExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.MeshFxChannelControl;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not MeshFxChannelControlCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "MeshFxChannelControlCommandData is required.");

            if (!ctx.Resolver.TryResolve<IMeshFxChannelHubService>(out var hub) || hub == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMeshFxChannelHubService is missing.");

            if (typed.Mode == MeshFxChannelControlMode.Remove)
            {
                if (string.IsNullOrWhiteSpace(typed.RemoveTag))
                    return UniTask.CompletedTask;

                hub.UnregisterChannel(typed.RemoveTag.Trim());
                return UniTask.CompletedTask;
            }

            var def = typed.CreateDef;
            if (!string.IsNullOrWhiteSpace(typed.CreateTagOverride))
            {
                SetChannelTag(def, typed.CreateTagOverride.Trim());
            }

            var ownerTransform = ctx.Scope.Identity?.SelfTransform;
            if (ownerTransform != null)
            {
                def.EnsureIntegrity(ownerTransform);
            }

            var registered = hub.RegisterChannel(def, overwrite: typed.OverwriteIfExists);
            if (!registered && typed.FailIfCannotCreate)
            {
                throw new CommandExecutionException(
                    CommandRunFailureKind.InvalidArgs,
                    $"MeshFx channel create failed. tag='{def.Tag}', overwrite={typed.OverwriteIfExists}.");
            }

            return UniTask.CompletedTask;
        }

        static void SetChannelTag(ChannelDefBase def, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = typeof(ChannelDefBase).GetField("tag", flags);
            if (field == null)
                return;

            field.SetValue(def, tag);
        }
    }
}
