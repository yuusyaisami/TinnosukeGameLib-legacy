#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Channel;
using Game.Commands.VNext;
using Game.Common;
using Game.Spawn;
using UnityEngine;
using VContainer;

namespace Game.UI
{
    internal static class TraitListChannelRuntimeHelpers
    {
        public static TraitListChannelEnvironmentKind ResolveEnvironment(Transform ownerTransform, out Canvas? canvas)
        {
            canvas = ownerTransform != null ? ownerTransform.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null &&
                (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera))
            {
                return TraitListChannelEnvironmentKind.ScreenUI;
            }

            canvas = null;
            return TraitListChannelEnvironmentKind.World;
        }

        public static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? "default" : tag.Trim();
        }

        public static IVarStore ResolveVars(IScopeNode? scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }

        public static void EnsureScopeBuiltIfNeeded(IScopeNode? scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
                runtimeScope.EnsureScopeBuilt();
        }

        public static bool TryResolveVisualBounds(TraitListChannelVisualInstance instance, out VisualBoundsOutput output)
        {
            output = default;
            if (instance.Resolver == null)
                return false;

            if (!instance.Resolver.TryResolve<IVisualBoundsService>(out var boundsService) || boundsService == null)
                return false;

            return boundsService.TryGetLastOutput(out output) && output.HasBounds;
        }

        public static Vector3 ResolvePlacementLocalPosition(
            TraitListChannelVisualInstance instance,
            Vector3 targetLocalPosition,
            TraitListChannelHorizontalAlignment horizontalAlignment,
            TraitListChannelVerticalAlignment verticalAlignment)
        {
            if (!TryResolveVisualBounds(instance, out var bounds))
                return targetLocalPosition;

            var anchor = new Vector3(
                ResolveHorizontalAnchor(bounds.LocalRect, horizontalAlignment),
                ResolveVerticalAnchor(bounds.LocalRect, verticalAlignment),
                0f);
            return targetLocalPosition - anchor;
        }

        public static void SetLocalPosition(
            TraitListChannelVisualInstance instance,
            Vector3 localPosition,
            TraitListChannelEnvironmentKind environmentKind)
        {
            if (environmentKind == TraitListChannelEnvironmentKind.ScreenUI && instance.RootRect != null)
            {
                var parentRect = instance.RootRect.parent as RectTransform;
                if (parentRect != null)
                {
                    var reference = ResolveAnchorReference(instance.RootRect, parentRect);
                    instance.RootRect.anchoredPosition3D = new Vector3(
                        localPosition.x - reference.x,
                        localPosition.y - reference.y,
                        localPosition.z);
                }
                else
                {
                    instance.RootRect.anchoredPosition3D = localPosition;
                }

                return;
            }

            instance.Root.localPosition = localPosition;
        }

        public static async UniTask ReleaseSpawnedInstanceAsync(
            Transform? root,
            IScopeNode? scope,
            IObjectResolver? resolver)
        {
            if (resolver == null)
                return;

            await UniTask.SwitchToMainThread();

            try
            {
                if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
                {
                    if (runtimeScope.Resolver != null &&
                        runtimeScope.Resolver.TryResolve<IRuntimeLifetimeScopePool>(out var pool) &&
                        pool != null)
                    {
                        pool.Release(runtimeScope);
                        return;
                    }

                    if (root != null)
                        Object.Destroy(root.gameObject);
                    else
                        Object.Destroy(runtimeScope.gameObject);
                    return;
                }

                if (scope is BaseLifetimeScope baseScope)
                {
                    await baseScope.DespawnAsync(CancellationToken.None);
                    return;
                }

                if (root != null)
                    Object.Destroy(root.gameObject);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TraitListChannel] Release failed: {ex.Message}");
            }
        }

        public static void ExtractSpawnedInfo(
            IObjectResolver? resolver,
            out Transform? root,
            out IScopeNode? scopeNode)
        {
            root = null;
            scopeNode = null;
            if (resolver == null)
                return;

            if (resolver.TryResolve<RuntimeLifetimeScope>(out var runtimeScope) && runtimeScope != null)
            {
                root = runtimeScope.transform;
                scopeNode = runtimeScope;
                return;
            }

            if (resolver.TryResolve<BaseLifetimeScope>(out var baseScope) && baseScope != null)
            {
                root = baseScope.transform;
                scopeNode = baseScope;
                return;
            }
        }

        public static bool TryResolveTransformAnimationPlayer(
            TraitListChannelVisualInstance instance,
            string channelTag,
            out ITransformAnimationChannelPlayer? player)
        {
            player = null;
            if (instance.Resolver == null)
                return false;

            if (!instance.Resolver.TryResolve<ITransformAnimationHubService>(out var hub) || hub == null)
                return false;

            return hub.TryGetPlayer(channelTag, out player) && player != null;
        }

        static float ResolveHorizontalAnchor(Rect localRect, TraitListChannelHorizontalAlignment alignment)
        {
            return alignment switch
            {
                TraitListChannelHorizontalAlignment.Left => localRect.xMin,
                TraitListChannelHorizontalAlignment.Right => localRect.xMax,
                TraitListChannelHorizontalAlignment.Center => localRect.center.x,
                _ => localRect.xMin
            };
        }

        static float ResolveVerticalAnchor(Rect localRect, TraitListChannelVerticalAlignment alignment)
        {
            return alignment switch
            {
                TraitListChannelVerticalAlignment.Top => localRect.yMax,
                TraitListChannelVerticalAlignment.Bottom => localRect.yMin,
                TraitListChannelVerticalAlignment.Center => localRect.center.y,
                _ => localRect.yMax
            };
        }

        static Vector2 ResolveAnchorReference(RectTransform rectTransform, RectTransform parent)
        {
            var parentSize = parent.rect.size;
            var parentPivot = parent.pivot;
            var anchorMin = rectTransform.anchorMin;
            var anchorMax = rectTransform.anchorMax;
            var pivot = rectTransform.pivot;
            var normalized = new Vector2(
                Mathf.Lerp(anchorMin.x, anchorMax.x, pivot.x),
                Mathf.Lerp(anchorMin.y, anchorMax.y, pivot.y));

            return new Vector2(
                (normalized.x - parentPivot.x) * parentSize.x,
                (normalized.y - parentPivot.y) * parentSize.y);
        }
    }
}
