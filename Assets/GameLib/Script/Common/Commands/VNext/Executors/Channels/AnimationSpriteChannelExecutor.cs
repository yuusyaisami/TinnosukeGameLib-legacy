#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Channel;
using Game.MaterialFx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class AnimationSpriteChannelExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.AnimationSpriteChannel;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not AnimationSpriteChannelCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "AnimationSpriteChannelCommandData is required.");

            if (!TryResolveAnimationHub(ctx, out var hub))
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IAnimationSpriteHubService is missing.");

            if (!hub.TryGetPlayer(typed.ChannelTag, out var player) || player == null)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"AnimationSpriteChannel '{typed.ChannelTag}' not found.");

            if (typed.ApplyMaterialFx)
            {
                if (typed.MaterialFxSource.TryGet(ctx, out var payload) && payload != null)
                    ApplyMaterialFxPayload(payload, ctx, player);
            }

            if (typed.ApplyPlaybackSpeed)
            {
                if (typed.PlaybackSpeedSource.TryGet(ctx, out var speed))
                    player.SetPlaybackSpeedMultiplier(speed);
            }

            if (typed.ApplyFlipX)
            {
                if (typed.FlipXMode == AnimationSpriteFlipControlMode.Trigger)
                {
                    player.TriggerFlipX();
                }
                else if (typed.FlipXAngleSource.TryGet(ctx, out var angle))
                {
                    player.SetFlipXAngle(angle);
                }
            }

            if (typed.ApplySortingOrder)
            {
                if (typed.SortingOrderSource.TryGet(ctx, out var sortingOrder))
                    player.SetSortingOrder(sortingOrder);
            }

            if (typed.ApplyVisualType)
            {
                ApplyVisualTypeSettings(typed, ctx, player);
            }

            if (!typed.ApplyAnimation)
                return UniTask.CompletedTask;

            // Animation preset: prefer var -> fallback to inline preset
            if (!typed.PresetSource.TryGet(ctx, out var preset) || preset == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (IsToastDebugContext(ctx))
                {
                    var variant = typed.PresetSource.Evaluate(ctx);
                    var managedType = variant.Kind == Game.Common.ValueKind.ManagedRef && variant.AsManagedRef != null
                        ? variant.AsManagedRef.GetType().Name
                        : "<null>";
                    Debug.LogWarning(
                        $"[AnimationSpriteChannelExecutor] PresetSource resolve failed or null. " +
                        $"tag={typed.ChannelTag} sourceType={typed.PresetSource.SourceTypeName} source={typed.PresetSource.SourceDebugData} " +
                        $"variantKind={variant.Kind} managedType={managedType}");
                }
#endif
                return UniTask.CompletedTask;
            }

            var runTask = player.PlayPresetAsync(preset, ct);

            // IMPORTANT:
            // AnimationSpriteChannelPlayer.PlayPresetAsync returns a task that only completes when playback ends.
            // Loop / OnceToLoop / CrossFade play modes never end by design, so awaiting would hang forever.
            var playMode = preset.playMode;
            var isLoopingMode = playMode != Game.Channel.AnimationPlayMode.Once;
            if (isLoopingMode)
                return RunInBackground(runTask);

            // Once: allow designers to choose whether to wait for completion.
            var shouldWait = typed.WaitForOnceCompletion && typed.AwaitMode == FlowRunAwaitMode.WaitForCompletion;
            return shouldWait ? runTask : RunInBackground(runTask);
        }

        static UniTask RunInBackground(UniTask task)
        {
            UniTask.Void(async () =>
            {
                try { await task; }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) { }
                catch (Exception) { }
            });
            return UniTask.CompletedTask;
        }

        static bool TryResolveAnimationHub(CommandContext ctx, out IAnimationSpriteHubService hub)
        {
            var origin = ctx.Actor ?? ctx.Scope;
            if (origin == null)
            {
                hub = null!;
                return false;
            }

            // Prefer hubs in actor/scope subtree so spawned runtime elements resolve their local channels.
            foreach (var node in ScopeNodeHierarchy.EnumerateSubtree(origin, includeSelf: true))
            {
                var resolver = node?.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<IAnimationSpriteHubService>(out var childHub) && childHub != null)
                {
                    if (!IsHubOwnedByNode(childHub, node))
                        continue;

                    hub = childHub;
                    return true;
                }
            }

            if (ctx.Resolver.TryResolve<IAnimationSpriteHubService>(out var directHub) && directHub != null)
            {
                if (IsHubOwnedByNode(directHub, ctx.Scope) || IsHubOwnedByNode(directHub, origin))
                {
                    hub = directHub;
                    return true;
                }
            }

            foreach (var node in origin.EnumerateAncestors(includeSelf: false))
            {
                var resolver = node?.Resolver;
                if (resolver == null)
                    continue;

                if (resolver.TryResolve<IAnimationSpriteHubService>(out var foundHub) && foundHub != null)
                {
                    if (!IsHubOwnedByNode(foundHub, node))
                        continue;

                    hub = foundHub;
                    return true;
                }
            }

            hub = null!;
            return false;
        }

        static bool IsHubOwnedByNode(IAnimationSpriteHubService hub, IScopeNode? node)
        {
            if (hub is AnimationSpriteHubService typed)
                return ReferenceEquals(typed.OwnerScope, node);

            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";
            if (scope is UnityEngine.Object unityObj && !unityObj)
                return "<destroyed>";
            var id = scope.Identity?.Id;
            if (!string.IsNullOrEmpty(id))
                return $"{id} ({scope.Kind})";
            return scope.GetType().Name;
        }

        static bool IsToastDebugContext(CommandContext ctx)
        {
            return ContainsToastMarker(ctx.Actor) || ContainsToastMarker(ctx.Scope);
        }

        static bool ContainsToastMarker(IScopeNode? scope)
        {
            foreach (var node in scope?.EnumerateAncestors(includeSelf: true) ?? System.Array.Empty<IScopeNode>())
            {
                var id = node?.Identity?.Id;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (id.IndexOf("UIToast", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
#endif

        static void ApplyMaterialFxPayload(MaterialFxPayload payload, CommandContext ctx, IAnimationSpriteChannelPlayer player)
        {
            var fx = player.MaterialFx;
            if (fx == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "MaterialFx is not available on this channel.");

            if (!ctx.Resolver.TryResolve<IMaterialFxPropertyRegistry>(out var registry) || registry == null)
                throw new CommandExecutionException(CommandRunFailureKind.ExecutorMissing, "IMaterialFxPropertyRegistry is missing.");

            if (payload == null)
                return;

            var context = payload.ContextTag ?? string.Empty;
            if (payload.ClearContextFirst)
                fx.ClearContext(context);

            var entries = payload.Entries;
            if (entries == null || entries.Count == 0)
            {
                // Ensure clear-only commands are reflected immediately even when global MaterialFx tick is unavailable.
                fx.Tick(0f);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var key = e.Key ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!registry.TryGetValueType(key, out var kind))
                    throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, $"MaterialFx key not found: '{key}'");

                var value = e.Value.ToTypedValue(kind, ctx);
                var lifetime = e.LifetimeSeconds;
                if (lifetime == 0f)
                    lifetime = -1f;

                if (e.ApplyWeightFade)
                {
                    // NOTE:
                    // In practice, designers expect "Fade" to mean "fade the value itself" (e.g., Amount 1 -> 0),
                    // not fading the layer contribution weight.
                    // For backward compatibility, ApplyWeightFade is interpreted as Value Fade.

                    fx.SetLayerFade(key, context, value, e.ResolveFadeDuration(ctx), e.FadeEase, e.BlendMode, payload.Priority, lifetime);
                    continue;
                }

                fx.SetLayer(key, context, value, e.BlendMode, payload.Priority, lifetime);
            }

            // Command-driven MaterialFx should be visible immediately without waiting for the next system LateTick.
            fx.Tick(0f);
        }

        static void ApplyVisualTypeSettings(
            AnimationSpriteChannelCommandData command,
            CommandContext ctx,
            IAnimationSpriteChannelPlayer player)
        {
            if (player.SpriteRenderer != null)
                ApplySpriteRendererType(command.SpriteRendererType, ctx, player.SpriteRenderer);

            if (player.Image != null)
                ApplyImageType(command.ImageType, ctx, player.Image);
        }

        static void ApplySpriteRendererType(
            AnimationSpriteRendererTypePayload payload,
            CommandContext ctx,
            SpriteRenderer renderer)
        {
            if (renderer == null || payload == null)
                return;

            renderer.drawMode = ConvertRendererType(payload.Type);
            if (renderer.drawMode != SpriteDrawMode.Sliced &&
                renderer.drawMode != SpriteDrawMode.Tiled)
            {
                return;
            }

            if (!payload.SizeSource.TryGet(ctx, out var size))
                return;

            renderer.size = new Vector2(
                Mathf.Max(0f, size.x),
                Mathf.Max(0f, size.y));
        }

        static void ApplyImageType(
            AnimationSpriteImageTypePayload payload,
            CommandContext ctx,
            Image image)
        {
            if (image == null || payload == null)
                return;

            image.type = ConvertImageType(payload.Type);
            image.preserveAspect = payload.PreserveAspect;

            if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled)
            {
                image.fillCenter = payload.FillCenter;
                if (payload.PixelsPerUnitMultiplierSource.TryGet(ctx, out var pixelsPerUnitMultiplier))
                    image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, pixelsPerUnitMultiplier);
            }

            if (image.type == Image.Type.Filled)
            {
                image.fillMethod = ConvertFillMethod(payload.FillMethod);
                if (payload.FillOriginSource.TryGet(ctx, out var fillOrigin))
                    image.fillOrigin = fillOrigin;
                image.fillClockwise = payload.FillClockwise;
                if (payload.FillAmountSource.TryGet(ctx, out var fillAmount))
                    image.fillAmount = Mathf.Clamp01(fillAmount);
            }

            if ((image.type == Image.Type.Simple ||
                 image.type == Image.Type.Sliced ||
                 image.type == Image.Type.Tiled) &&
                image.rectTransform != null &&
                payload.SizeDeltaSource.TryGet(ctx, out var sizeDelta))
            {
                image.rectTransform.sizeDelta = new Vector2(
                    Mathf.Max(0f, sizeDelta.x),
                    Mathf.Max(0f, sizeDelta.y));
            }
        }

        static SpriteDrawMode ConvertRendererType(AnimationSpriteRendererTypeMode mode)
        {
            return mode switch
            {
                AnimationSpriteRendererTypeMode.Sliced => SpriteDrawMode.Sliced,
                AnimationSpriteRendererTypeMode.Tiled => SpriteDrawMode.Tiled,
                _ => SpriteDrawMode.Simple,
            };
        }

        static Image.Type ConvertImageType(AnimationSpriteImageTypeMode mode)
        {
            return mode switch
            {
                AnimationSpriteImageTypeMode.Sliced => Image.Type.Sliced,
                AnimationSpriteImageTypeMode.Tiled => Image.Type.Tiled,
                AnimationSpriteImageTypeMode.Filled => Image.Type.Filled,
                _ => Image.Type.Simple,
            };
        }

        static Image.FillMethod ConvertFillMethod(AnimationSpriteImageFillMethodMode mode)
        {
            return mode switch
            {
                AnimationSpriteImageFillMethodMode.Vertical => Image.FillMethod.Vertical,
                AnimationSpriteImageFillMethodMode.Radial90 => Image.FillMethod.Radial90,
                AnimationSpriteImageFillMethodMode.Radial180 => Image.FillMethod.Radial180,
                AnimationSpriteImageFillMethodMode.Radial360 => Image.FillMethod.Radial360,
                _ => Image.FillMethod.Horizontal,
            };
        }
    }
}
