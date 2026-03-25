#nullable enable

using System;
using Game.Commands.VNext;
using Game.Search;
using Game.Targeting;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Common
{
    public enum TargetChannelTargetSelectMode
    {
        First = 0,
        FilterByActorSource = 1,
    }

    [Serializable]
    public sealed class TargetChannelTargetPosition2Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Channel Owner\", channelOwnerActorSource)")]
        ActorSource channelOwnerActorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Select Mode")]
        TargetChannelTargetSelectMode selectMode = TargetChannelTargetSelectMode.First;

        [SerializeField, LabelText("Fallback")]
        Vector2 fallback = Vector2.zero;

        [SerializeField, LabelText("Fallback To First On Miss")]
        [ShowIf(nameof(UseActorFilter))]
        bool fallbackToFirstIfFilterMiss = true;

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Filter Actor\", filterActorSource)")]
        [ShowIf(nameof(UseActorFilter))]
        ActorSource filterActorSource;

        [NonSerialized] ActorSourceResolveCache _channelOwnerCache;
        [NonSerialized] ActorSourceResolveCache _filterActorCache;

        public string SourceTypeName => "TargetChannelPos";
        public string GetDebugData => $"{channelOwnerActorSource.Kind}:{channelTag}:{selectMode} (Vector2)";

        bool UseActorFilter() => selectMode == TargetChannelTargetSelectMode.FilterByActorSource;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TargetChannelTargetPositionSourceHelper.TryResolveTargetHit(
                    context,
                    channelTag,
                    channelOwnerActorSource,
                    ref _channelOwnerCache,
                    selectMode,
                    filterActorSource,
                    fallbackToFirstIfFilterMiss,
                    ref _filterActorCache,
                    out var hit))
            {
                return DynamicVariant.FromVector2(fallback);
            }

            return DynamicVariant.FromVector2(new Vector2(hit.Position.x, hit.Position.y));
        }
    }

    [Serializable]
    public sealed class TargetChannelTargetPosition3Source : IDynamicSource
    {
        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Channel Owner\", channelOwnerActorSource)")]
        ActorSource channelOwnerActorSource;

        [SerializeField, LabelText("Channel Tag")]
        string channelTag = "default";

        [SerializeField, LabelText("Select Mode")]
        TargetChannelTargetSelectMode selectMode = TargetChannelTargetSelectMode.First;

        [SerializeField, LabelText("Fallback")]
        Vector3 fallback = Vector3.zero;

        [SerializeField, LabelText("Fallback To First On Miss")]
        [ShowIf(nameof(UseActorFilter))]
        bool fallbackToFirstIfFilterMiss = true;

        [SerializeField]
        [LabelText("@Game.Commands.VNext.ActorSourceOdinLabelHelper.GetLabel(\"Filter Actor\", filterActorSource)")]
        [ShowIf(nameof(UseActorFilter))]
        ActorSource filterActorSource;

        [NonSerialized] ActorSourceResolveCache _channelOwnerCache;
        [NonSerialized] ActorSourceResolveCache _filterActorCache;

        public string SourceTypeName => "TargetChannelPos";
        public string GetDebugData => $"{channelOwnerActorSource.Kind}:{channelTag}:{selectMode} (Vector3)";

        bool UseActorFilter() => selectMode == TargetChannelTargetSelectMode.FilterByActorSource;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            if (!TargetChannelTargetPositionSourceHelper.TryResolveTargetHit(
                    context,
                    channelTag,
                    channelOwnerActorSource,
                    ref _channelOwnerCache,
                    selectMode,
                    filterActorSource,
                    fallbackToFirstIfFilterMiss,
                    ref _filterActorCache,
                    out var hit))
            {
                return DynamicVariant.FromVector3(fallback);
            }

            var z = TargetChannelTargetPositionSourceHelper.ResolveWorldZ(hit);
            return DynamicVariant.FromVector3(new Vector3(hit.Position.x, hit.Position.y, z));
        }
    }

    static class TargetChannelTargetPositionSourceHelper
    {
        public static bool TryResolveTargetHit(
            IDynamicContext? context,
            string channelTag,
            in ActorSource channelOwnerActorSource,
            ref ActorSourceResolveCache channelOwnerCache,
            TargetChannelTargetSelectMode selectMode,
            in ActorSource filterActorSource,
            bool fallbackToFirstIfFilterMiss,
            ref ActorSourceResolveCache filterActorCache,
            out DynamicSearchHit hit)
        {
            hit = default;

            if (context == null)
                return false;

            var scope = context.Scope;
            if (scope == null)
                return false;

            if (channelOwnerActorSource.Kind == ActorSourceKind.FromUnityObject &&
                channelOwnerActorSource.UnityObject == null)
            {
                Debug.LogWarning("[TargetChannelSource] Channel Owner uses FromUnityObject but UnityObject is null.");
                return false;
            }

            var channelOwnerScope = ActorSourceFastResolver.ResolveCached(
                context,
                channelOwnerActorSource,
                ref channelOwnerCache,
                scope);
            if (channelOwnerScope == null &&
                channelOwnerActorSource.Kind == ActorSourceKind.FromUnityObject &&
                channelOwnerActorSource.UnityObject != null &&
                IsPrefabAssetReference(channelOwnerActorSource.UnityObject) &&
                context?.CommandRootScope != null)
            {
                // In spawned command contexts, FromUnityObject may point to prefab-asset references.
                // In that case, command root scope is the only runtime-resolvable owner.
                channelOwnerScope = context.CommandRootScope;
            }
            if (channelOwnerScope == null &&
                channelOwnerActorSource.Kind == ActorSourceKind.FromUnityObject &&
                channelOwnerActorSource.UnityObject != null)
            {
                // Spawn command contexts can use non-scope child objects as references.
                // If the object belongs to current/command-root scope hierarchy, fall back to that scope.
                if (context?.CommandRootScope != null && IsObjectRelatedToScope(channelOwnerActorSource.UnityObject, context.CommandRootScope))
                {
                    channelOwnerScope = context.CommandRootScope;
                }
                else if (IsObjectRelatedToScope(channelOwnerActorSource.UnityObject, scope))
                {
                    channelOwnerScope = scope;
                }
            }
            if (channelOwnerScope?.Resolver == null)
            {
                if (channelOwnerActorSource.Kind == ActorSourceKind.FromUnityObject &&
                    channelOwnerActorSource.UnityObject != null)
                {
                    var kind = DescribeUnityObjectKind(channelOwnerActorSource.UnityObject);
                    var name = channelOwnerActorSource.UnityObject.name;
                    Debug.LogWarning($"[TargetChannelSource] Channel Owner FromUnityObject could not resolve scope. Object='{name}' Kind={kind}");
                }
                return false;
            }

            var normalizedTag = string.IsNullOrWhiteSpace(channelTag) ? "default" : channelTag.Trim();
            // NOTE:
            // ここは再発しやすい失敗点。
            // FromUnityObject で得た owner scope が TargetChannelHub を持っていない場合があるため、
            // 単一 scope 解決ではなく親チェーン/代替起点(CommandRoot, current scope)まで探索する。
            if (!TryResolveRuntimeFromScopeChain(channelOwnerScope, normalizedTag, out var runtime))
            {
                if (context?.CommandRootScope != null &&
                    !ReferenceEquals(context.CommandRootScope, channelOwnerScope) &&
                    TryResolveRuntimeFromScopeChain(context.CommandRootScope, normalizedTag, out runtime))
                {
                }
                else if (!ReferenceEquals(scope, channelOwnerScope) &&
                         TryResolveRuntimeFromScopeChain(scope, normalizedTag, out runtime))
                {
                }
                else
                {
                    if (channelOwnerActorSource.Kind == ActorSourceKind.FromUnityObject)
                        Debug.LogWarning($"[TargetChannelSource] ITargetChannelHub/runtime not found from owner scope chain. Tag='{normalizedTag}'");
                    return false;
                }
            }

            if (runtime == null)
                return false;

            var hits = runtime.Hits;
            if (hits == null || hits.Count == 0)
            {
                if (channelOwnerActorSource.Kind == ActorSourceKind.FromUnityObject)
                    Debug.LogWarning($"[TargetChannelSource] Target channel has no hits. Tag='{normalizedTag}'");
                return false;
            }

            if (selectMode != TargetChannelTargetSelectMode.FilterByActorSource)
            {
                hit = hits[0];
                return true;
            }

            var filterScope = context != null
                ? ActorSourceFastResolver.ResolveCached(context, filterActorSource, ref filterActorCache, scope)
                : ActorSourceFastResolver.ResolveCached(scope, filterActorSource, ref filterActorCache);
            if (filterScope != null)
            {
                for (var i = 0; i < hits.Count; i++)
                {
                    var candidate = hits[i];
                    if (ReferenceEquals(candidate.Scope, filterScope) || ReferenceEquals(candidate.Identity, filterScope.Identity))
                    {
                        hit = candidate;
                        return true;
                    }
                }
            }

            if (!fallbackToFirstIfFilterMiss)
                return false;

            hit = hits[0];
            return true;
        }

        public static float ResolveWorldZ(in DynamicSearchHit hit)
        {
            var fromIdentity = hit.Identity?.SelfTransform;
            if (fromIdentity != null)
                return fromIdentity.position.z;

            if (hit.Scope is Component component)
                return component.transform.position.z;

            return 0f;
        }

        static bool IsPrefabAssetReference(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
                return false;

            if (unityObject is GameObject go)
                return !go.scene.IsValid() || !go.scene.isLoaded;

            if (unityObject is Component comp)
                return !comp.gameObject.scene.IsValid() || !comp.gameObject.scene.isLoaded;

            return false;
        }

        internal static bool TryResolveRuntimeFromScopeChain(IScopeNode? startScope, string tag, out ITargetChannelRuntime? runtime)
        {
            runtime = null;
            if (startScope == null || string.IsNullOrWhiteSpace(tag))
                return false;

            // StartScope -> Parent を順にたどり、
            // 「Hub が存在し、かつ対象 Tag runtime がある地点」を採用する。
            for (var current = startScope; current != null; current = current.Parent)
            {
                if (!TryResolveHubAtScope(current, out var hub) || hub == null)
                    continue;

                if (hub.TryGetRuntime(tag, out var found) && found != null)
                {
                    runtime = found;
                    return true;
                }
            }

            return false;
        }

        internal static bool TryResolveHubAtScope(IScopeNode scope, out ITargetChannelHub? hub)
        {
            hub = null;
            if (scope == null)
                return false;

            var resolver = scope.Resolver;
            if (resolver != null &&
                resolver.TryResolve<ITargetChannelHub>(out var resolved) &&
                resolved != null)
            {
                hub = resolved;
                return true;
            }

            // Resolver経由で取れないケース向けの保険。
            // MB 自体が ITargetChannelHub を実装している構成でも拾えるようにする。
            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scope, out var scopeTransform) || scopeTransform == null)
                return false;

            var components = scopeTransform.GetComponents<MonoBehaviour>();
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is ITargetChannelHub componentHub && componentHub != null)
                {
                    hub = componentHub;
                    return true;
                }
            }

            return false;
        }

        static bool IsObjectRelatedToScope(UnityEngine.Object unityObject, IScopeNode scope)
        {
            if (unityObject == null || scope == null)
                return false;

            if (!TryGetObjectTransform(unityObject, out var objectTransform) || objectTransform == null)
                return false;

            if (!ActorWorldPositionSourceHelper.TryGetScopeTransform(scope, out var scopeTransform) || scopeTransform == null)
                return false;

            return objectTransform == scopeTransform ||
                   objectTransform.IsChildOf(scopeTransform) ||
                   scopeTransform.IsChildOf(objectTransform);
        }

        static bool TryGetObjectTransform(UnityEngine.Object unityObject, out Transform? transform)
        {
            transform = null;
            if (unityObject == null)
                return false;

            if (unityObject is Component comp && comp != null)
            {
                transform = comp.transform;
                return true;
            }

            if (unityObject is GameObject go && go != null)
            {
                transform = go.transform;
                return true;
            }

            return false;
        }

        static string DescribeUnityObjectKind(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
                return "Null";

            if (unityObject is GameObject go)
            {
                var sceneValid = go.scene.IsValid();
                var sceneLoaded = go.scene.isLoaded;
                return $"GameObject(SceneValid={sceneValid}, SceneLoaded={sceneLoaded})";
            }

            if (unityObject is Component comp)
            {
                var sceneValid = comp.gameObject.scene.IsValid();
                var sceneLoaded = comp.gameObject.scene.isLoaded;
                return $"Component<{comp.GetType().Name}>(SceneValid={sceneValid}, SceneLoaded={sceneLoaded})";
            }

            return unityObject.GetType().Name;
        }
    }
}
