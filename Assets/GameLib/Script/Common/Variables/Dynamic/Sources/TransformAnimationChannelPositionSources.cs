#nullable enable

using System;
using Game.Channel;
using Game.Commands.VNext;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum TransformAnimationChannelPositionSpace
    {
        World = 0,
        Local = 1,
    }

    public enum TransformAnimationChannelTargetSelectMode
    {
        Self = 0,
        FirstChild = 1,
        ChildPath = 2,
        ChildName = 3,
    }

    [Serializable]
    public sealed class TransformAnimationChannelPosition2Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Target Select")]
        TransformAnimationChannelTargetSelectMode targetSelectMode = TransformAnimationChannelTargetSelectMode.Self;

        [SerializeField, LabelText("Child Path")]
        [ShowIf(nameof(ShowChildPathField))]
        string childPath = "";

        [SerializeField, LabelText("Child Name")]
        [ShowIf(nameof(ShowChildNameField))]
        string childName = "";

        [SerializeField, LabelText("Search Recursive")]
        [ShowIf(nameof(ShowChildNameField))]
        bool childNameRecursive = true;

        [SerializeField, LabelText("Space")]
        TransformAnimationChannelPositionSpace space = TransformAnimationChannelPositionSpace.World;

        [SerializeField, LabelText("Debug Log")]
        bool debugLogEnabled;

        [SerializeField, LabelText("Debug Every N Frames"), MinValue(1)]
        [ShowIf(nameof(debugLogEnabled))]
        int debugLogEveryNFrames = 30;

        [NonSerialized] ActorSourceResolveCache _actorCache;
        [NonSerialized] TransformAnimationChannelTargetResolveCache _targetCache;
        [NonSerialized] int _lastDebugLogFrame = -1;

        public string SourceTypeName => "TransformChannelPos";
        public string GetDebugData => $"{actorSource.Kind}:{channelTag} [{targetSelectMode}] ({space},Vector2)";

        bool ShowChildPathField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildPath;
        bool ShowChildNameField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildName;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TransformAnimationChannelPositionSourceHelper.TryGetTargetTransform(
                    context,
                    actorSource,
                    channelTag,
                    targetSelectMode,
                    childPath,
                    childName,
                    childNameRecursive,
                    ref _actorCache,
                    ref _targetCache,
                    out var resolved,
                    out var root))
            {
                TryLog($"Resolve failed. actor={actorSource.Kind}, tag={channelTag}, mode={targetSelectMode}, path={childPath}, name={childName}");
                return DynamicVariant.FromVector2(Vector2.zero);
            }

            var position = space == TransformAnimationChannelPositionSpace.World
                ? resolved.position
                : resolved.localPosition;
            var value = new Vector2(position.x, position.y);

            TryLog(
                $"tag={channelTag}, actor={actorSource.Kind}, mode={targetSelectMode}, root={TransformAnimationChannelPositionSourceHelper.GetTransformPath(root)}, " +
                $"resolved={TransformAnimationChannelPositionSourceHelper.GetTransformPath(resolved)}, space={space}, world={resolved.position}, local={resolved.localPosition}, out={value}");

            return DynamicVariant.FromVector2(value);
        }

        void TryLog(string message)
        {
            if (!debugLogEnabled)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, debugLogEveryNFrames);
            if (_lastDebugLogFrame >= 0 && frame - _lastDebugLogFrame < interval)
                return;

            _lastDebugLogFrame = frame;
            Debug.Log($"[TransformAnimationChannelPosition2Source] {message}");
        }
    }

    [Serializable]
    public sealed class TransformAnimationChannelPosition3Source : IDynamicSource
    {
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetActorSourceLabel(actorSource)")]
        [SerializeField]
        ActorSource actorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Target Select")]
        TransformAnimationChannelTargetSelectMode targetSelectMode = TransformAnimationChannelTargetSelectMode.Self;

        [SerializeField, LabelText("Child Path")]
        [ShowIf(nameof(ShowChildPathField))]
        string childPath = "";

        [SerializeField, LabelText("Child Name")]
        [ShowIf(nameof(ShowChildNameField))]
        string childName = "";

        [SerializeField, LabelText("Search Recursive")]
        [ShowIf(nameof(ShowChildNameField))]
        bool childNameRecursive = true;

        [SerializeField, LabelText("Space")]
        TransformAnimationChannelPositionSpace space = TransformAnimationChannelPositionSpace.World;

        [SerializeField, LabelText("Debug Log")]
        bool debugLogEnabled;

        [SerializeField, LabelText("Debug Every N Frames"), MinValue(1)]
        [ShowIf(nameof(debugLogEnabled))]
        int debugLogEveryNFrames = 30;

        [NonSerialized] ActorSourceResolveCache _actorCache;
        [NonSerialized] TransformAnimationChannelTargetResolveCache _targetCache;
        [NonSerialized] int _lastDebugLogFrame = -1;

        public string SourceTypeName => "TransformChannelPos";
        public string GetDebugData => $"{actorSource.Kind}:{channelTag} [{targetSelectMode}] ({space},Vector3)";

        bool ShowChildPathField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildPath;
        bool ShowChildNameField() => targetSelectMode == TransformAnimationChannelTargetSelectMode.ChildName;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TransformAnimationChannelPositionSourceHelper.TryGetTargetTransform(
                    context,
                    actorSource,
                    channelTag,
                    targetSelectMode,
                    childPath,
                    childName,
                    childNameRecursive,
                    ref _actorCache,
                    ref _targetCache,
                    out var resolved,
                    out var root))
            {
                TryLog($"Resolve failed. actor={actorSource.Kind}, tag={channelTag}, mode={targetSelectMode}, path={childPath}, name={childName}");
                return DynamicVariant.FromVector3(Vector3.zero);
            }

            var position = space == TransformAnimationChannelPositionSpace.World
                ? resolved.position
                : resolved.localPosition;

            TryLog(
                $"tag={channelTag}, actor={actorSource.Kind}, mode={targetSelectMode}, root={TransformAnimationChannelPositionSourceHelper.GetTransformPath(root)}, " +
                $"resolved={TransformAnimationChannelPositionSourceHelper.GetTransformPath(resolved)}, space={space}, world={resolved.position}, local={resolved.localPosition}, out={position}");

            return DynamicVariant.FromVector3(position);
        }

        void TryLog(string message)
        {
            if (!debugLogEnabled)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, debugLogEveryNFrames);
            if (_lastDebugLogFrame >= 0 && frame - _lastDebugLogFrame < interval)
                return;

            _lastDebugLogFrame = frame;
            Debug.Log($"[TransformAnimationChannelPosition3Source] {message}");
        }
    }

    struct TransformAnimationChannelTargetResolveCache
    {
        public Transform? Root;
        public Transform? Resolved;
        public string ChannelTag;
        public TransformAnimationChannelTargetSelectMode Mode;
        public string ChildPath;
        public string ChildName;
        public bool ChildNameRecursive;
    }

    static class TransformAnimationChannelPositionSourceHelper
    {
        public static bool TryGetTargetTransform(
            IDynamicContext? context,
            ActorSource actorSource,
            string channelTag,
            TransformAnimationChannelTargetSelectMode mode,
            string childPath,
            string childName,
            bool childNameRecursive,
            ref ActorSourceResolveCache actorCache,
            ref TransformAnimationChannelTargetResolveCache targetCache,
            out Transform resolved,
            out Transform root)
        {
            resolved = null!;
            root = null!;

            if (context?.Scope == null || string.IsNullOrWhiteSpace(channelTag))
                return false;

            var scope = ActorSourceFastResolver.ResolveCached(context, actorSource, ref actorCache);
            if (scope?.Resolver == null)
                return false;

            if (!scope.Resolver.TryResolve<ITransformAnimationHubService>(out var hub) || hub == null)
                return false;

            if (!hub.TryGetPlayer(channelTag.Trim(), out var player) || player == null)
                return false;

            root = player.TargetTransform;
            if (root == null)
                return false;

            if (TryUseCache(root, channelTag, mode, childPath, childName, childNameRecursive, ref targetCache, out var cached) && cached != null)
            {
                resolved = cached;
                return true;
            }

            if (!TryResolveFromRoot(root, mode, childPath, childName, childNameRecursive, out var selected) || selected == null)
                return false;

            targetCache.Root = root;
            targetCache.Resolved = selected;
            targetCache.ChannelTag = channelTag;
            targetCache.Mode = mode;
            targetCache.ChildPath = childPath ?? string.Empty;
            targetCache.ChildName = childName ?? string.Empty;
            targetCache.ChildNameRecursive = childNameRecursive;

            resolved = selected;
            return true;
        }

        static bool TryUseCache(
            Transform root,
            string channelTag,
            TransformAnimationChannelTargetSelectMode mode,
            string childPath,
            string childName,
            bool childNameRecursive,
            ref TransformAnimationChannelTargetResolveCache cache,
            out Transform cached)
        {
            cached = null!;

            if (cache.Root == null || cache.Resolved == null)
                return false;

            if (cache.Root != root)
                return false;

            if (!string.Equals(cache.ChannelTag, channelTag, StringComparison.Ordinal))
                return false;

            if (cache.Mode != mode)
                return false;

            if (!string.Equals(cache.ChildPath ?? string.Empty, childPath ?? string.Empty, StringComparison.Ordinal))
                return false;

            if (!string.Equals(cache.ChildName ?? string.Empty, childName ?? string.Empty, StringComparison.Ordinal))
                return false;

            if (cache.ChildNameRecursive != childNameRecursive)
                return false;

            cached = cache.Resolved;
            return cached != null;
        }

        static bool TryResolveFromRoot(
            Transform root,
            TransformAnimationChannelTargetSelectMode mode,
            string childPath,
            string childName,
            bool childNameRecursive,
            out Transform selected)
        {
            var resolved = mode switch
            {
                TransformAnimationChannelTargetSelectMode.Self => root,
                TransformAnimationChannelTargetSelectMode.FirstChild => root.childCount > 0 ? root.GetChild(0) : null,
                TransformAnimationChannelTargetSelectMode.ChildPath => ResolveByPath(root, childPath),
                TransformAnimationChannelTargetSelectMode.ChildName => ResolveByName(root, childName, childNameRecursive),
                _ => root,
            };

            if (resolved == null)
            {
                selected = null!;
                return false;
            }

            selected = resolved;
            return true;
        }

        static Transform? ResolveByPath(Transform root, string childPath)
        {
            if (string.IsNullOrWhiteSpace(childPath))
                return null;

            return root.Find(childPath.Trim());
        }

        static Transform? ResolveByName(Transform root, string childName, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(childName))
                return null;

            var targetName = childName.Trim();

            if (!recursive)
            {
                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (child != null && string.Equals(child.name, targetName, StringComparison.Ordinal))
                        return child;
                }

                return null;
            }

            return FindChildRecursive(root, targetName);
        }

        static Transform? FindChildRecursive(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                    return child;

                var nested = FindChildRecursive(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        public static string GetTransformPath(Transform? t)
        {
            if (t == null)
                return "(null)";

            var path = t.name;
            var parent = t.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
