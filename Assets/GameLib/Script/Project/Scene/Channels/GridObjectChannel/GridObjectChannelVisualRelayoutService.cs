#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Commands.VNext;
using Game.UI;
using UnityEngine;

namespace Game.Channel
{
    internal sealed class GridObjectChannelVisualRelayoutService
    {
        readonly string _tag;

        public GridObjectChannelVisualRelayoutService(string tag)
        {
            _tag = tag;
        }

        public async UniTask RelayoutInstanceAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelVisualInstance instance,
            GridObjectChannelResolvedItem item,
            CancellationToken ct)
        {
            instance.UpdateFromItem(item);
            TransformGridSharedUtility.RefreshLayoutAndBounds(instance.Resolver);
            var targetLocal = TransformGridSharedUtility.ResolvePlacementLocalPosition(
                instance.Resolver,
                instance.Root,
                instance.RootRect,
                item.TargetLocalPosition,
                (int)state.ResolvedLayoutPreset.ItemHorizontalAlignment,
                (int)state.ResolvedLayoutPreset.ItemVerticalAlignment);
            await AnimateInstanceAsync(state, instance, targetLocal, state.ResolvedLayoutPreset.RelayoutMotion, ct);
        }

        public async UniTask AnimateInstanceAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelVisualInstance instance,
            Vector3 targetLocal,
            GridObjectChannelMotionPreset motion,
            CancellationToken ct)
        {
            if (motion == null || motion.DurationSeconds <= 0f)
            {
                TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, state.EnvironmentKind);
                return;
            }

            if (motion.UseTransformAnimation &&
                TransformGridSharedUtility.TryResolveTransformAnimationPlayer(instance.Resolver, motion.TransformAnimationChannelTag, out var player) &&
                player != null)
            {
                var playerTarget = player.TargetTransform;
                if (playerTarget != null &&
                    (ReferenceEquals(playerTarget, instance.Root) || ReferenceEquals(playerTarget, instance.RootRect)))
                {
                    var motionTarget = TransformGridSharedUtility.ResolveMotionTargetPosition(
                        instance.RootRect,
                        targetLocal,
                        state.EnvironmentKind);
                    var step = new TransformAnimationPresetStep
                    {
                        operation = state.EnvironmentKind == TransformGridEnvironmentKind.ScreenUI && instance.RootRect != null
                            ? TransformAnimationOperation.AnchoredPosition
                            : TransformAnimationOperation.LocalPosition,
                        duration = Game.Common.DynamicValueExtensions.FromLiteral(motion.DurationSeconds),
                        ease = motion.Ease,
                        relative = false,
                        fireAndForget = false,
                    };

                    if (motion.WaitForCompletion)
                    {
                        await player.PlayStepAsync(motionTarget, step);
                        TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, state.EnvironmentKind);
                        return;
                    }

                    UniTask.Void(async () =>
                    {
                        try
                        {
                            await player.PlayStepAsync(motionTarget, step);
                            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, state.EnvironmentKind);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[GridObjectChannel] TransformAnimation fallback triggered after channel failure. Tag='{_tag}' Message={ex.Message}");
                            await RunFallbackTweenAsync(state, instance, targetLocal, motion, CancellationToken.None);
                        }
                    });
                    return;
                }
            }

            await RunFallbackTweenAsync(state, instance, targetLocal, motion, ct);
        }

        async UniTask RunFallbackTweenAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelVisualInstance instance,
            Vector3 targetLocal,
            GridObjectChannelMotionPreset motion,
            CancellationToken ct)
        {
            var start = instance.RootRect != null && state.EnvironmentKind == TransformGridEnvironmentKind.ScreenUI
                ? instance.RootRect.anchoredPosition3D
                : instance.Root.localPosition;
            var duration = motion.DurationSeconds;
            if (duration <= 0f)
            {
                TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, state.EnvironmentKind);
                return;
            }

            if (!motion.WaitForCompletion)
            {
                UniTask.Void(async () =>
                {
                    try
                    {
                        await RunFallbackTweenCoreAsync(state, instance, start, targetLocal, duration, motion.Ease, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GridObjectChannel] Detached fallback tween failed. Tag='{_tag}' Message={ex.Message}");
                    }
                });
                return;
            }

            await RunFallbackTweenCoreAsync(state, instance, start, targetLocal, duration, motion.Ease, ct);
        }

        static async UniTask RunFallbackTweenCoreAsync(
            GridObjectChannelRuntimeState state,
            GridObjectChannelVisualInstance instance,
            Vector3 start,
            Vector3 targetLocal,
            float duration,
            Ease ease,
            CancellationToken ct)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = DOVirtual.EasedValue(0f, 1f, t, ease);
                var next = Vector3.LerpUnclamped(start, targetLocal, eased);
                TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, next, state.EnvironmentKind);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            TransformGridSharedUtility.SetLocalPosition(instance.Root, instance.RootRect, targetLocal, state.EnvironmentKind);
        }
    }
}
