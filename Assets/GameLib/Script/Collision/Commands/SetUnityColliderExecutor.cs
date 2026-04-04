#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Collision;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public sealed class SetUnityColliderExecutor : ICommandExecutor
    {
        public int CommandId => CommandIds.SetUnityCollider;

        public UniTask Execute(ICommandData data, CommandContext ctx, CancellationToken ct)
        {
            if (data is not SetUnityColliderCommandData typed)
                throw new CommandExecutionException(CommandRunFailureKind.InvalidArgs, "SetUnityColliderCommandData is required.");

            var actor = ctx.Actor ?? ctx.Scope;
            if (actor == null)
                return UniTask.CompletedTask;

            var root = actor.Identity?.SelfTransform;
            if (root == null)
                return UniTask.CompletedTask;

            var resolver = actor.Resolver;
            UnityColliderObjectService? unityService = null;
            if (resolver != null && resolver.TryResolve<UnityColliderObjectService>(out var resolvedService))
                unityService = resolvedService;

            var unityColliderMb = root.GetComponentInChildren<UnityColliderObjectMB>(includeInactive: true);

            var targetColliders = new List<Collider2D>(8);
            var targetTags = new List<string>(8);
            ResolveTargets(root, unityColliderMb, typed.TargetTag, targetColliders, targetTags);
            if (targetColliders.Count == 0)
                return UniTask.CompletedTask;

            for (var i = 0; i < targetColliders.Count; i++)
            {
                var sourceCollider = targetColliders[i];
                if (sourceCollider == null)
                    continue;

                var colliderTag = i < targetTags.Count
                    ? NormalizeTag(targetTags[i])
                    : UnityColliderObjectMB.DefaultColliderTag;

                var enabledBeforeReconfigure = sourceCollider.enabled;
                var triggerBeforeReconfigure = sourceCollider.isTrigger;

                var configuredCollider = EnsureModeCollider(sourceCollider.gameObject, sourceCollider, typed.Mode);
                if (configuredCollider == null)
                    continue;

                if (!ReferenceEquals(configuredCollider, sourceCollider))
                {
                    unityColliderMb?.ReplaceCollider(sourceCollider, configuredCollider, colliderTag);
                    unityService?.NotifyColliderReplaced(sourceCollider, configuredCollider, colliderTag);
                    targetColliders[i] = configuredCollider;
                }

                var finalEnabled = typed.ApplyEnabled
                    ? typed.Enabled.GetOrDefault(ctx, true)
                    : enabledBeforeReconfigure;
                var finalIsTrigger = typed.ApplyIsTrigger
                    ? typed.IsTrigger.GetOrDefault(ctx, triggerBeforeReconfigure)
                    : triggerBeforeReconfigure;

                if (typed.ApplyOffset)
                {
                    var offset = typed.Offset.GetOrDefault(ctx, configuredCollider.offset);
                    configuredCollider.offset = offset;
                }

                if (typed.ApplySize)
                {
                    var size = typed.Size.GetOrDefault(ctx, Vector2.one);
                    ApplyGenericSize(configuredCollider, size);
                }

                ApplyModeSpecificSettings(configuredCollider, typed, ctx);

                if (typed.ApplySharedMaterial)
                    configuredCollider.sharedMaterial = typed.SharedMaterial;

                configuredCollider.isTrigger = finalIsTrigger;
                if (unityService != null)
                {
                    unityService.SetTrigger(configuredCollider, finalIsTrigger);
                    unityService.SetEnabled(configuredCollider, finalEnabled, colliderTag);
                }
                else
                {
                    configuredCollider.enabled = finalEnabled;
                }
            }

            if (typed.ApplyLayerId)
            {
                var layerDefault = targetColliders[0] != null ? targetColliders[0].gameObject.layer : 0;
                var layerId = Mathf.Clamp(typed.LayerId.GetOrDefault(ctx, layerDefault), 0, 31);

                for (var i = 0; i < targetColliders.Count; i++)
                {
                    var collider = targetColliders[i];
                    if (collider != null)
                        collider.gameObject.layer = layerId;
                }

                unityColliderMb?.SetLayerId(layerId);
                unityService?.SetLayerId(layerId);
            }

            if (typed.ApplyHitMask)
            {
                var hitMaskRaw = typed.HitMask.GetOrDefault(ctx, unchecked((int)~0u));
                var hitMask = unchecked((uint)hitMaskRaw);
                unityColliderMb?.SetHitMask(hitMask);
                unityService?.SetHitMask(hitMask);
            }

            if (typed.ApplySetId)
            {
                var setIdRaw = typed.SetId.GetOrDefault(ctx, (int)DynamicColliderSetId.EnemyBullet);
                var setId = (DynamicColliderSetId)setIdRaw;
                unityColliderMb?.SetSetId(setId);
                unityService?.SetSetId(setId);
            }

            if (typed.ApplyUserData)
            {
                var userData = typed.UserData.GetOrDefault(ctx, 0);
                unityColliderMb?.SetUserData(userData);
                unityService?.SetUserData(userData);
            }

            return UniTask.CompletedTask;
        }

        static void ResolveTargets(
            Transform root,
            UnityColliderObjectMB? unityColliderMb,
            string rawTargetTag,
            List<Collider2D> colliders,
            List<string> tags)
        {
            colliders.Clear();
            tags.Clear();

            var normalizedTargetTag = NormalizeSelectorTag(rawTargetTag);
            if (normalizedTargetTag == null)
            {
                var primary = ResolvePrimaryCollider(root, unityColliderMb);
                if (primary == null)
                    return;

                colliders.Add(primary);
                if (unityColliderMb != null && unityColliderMb.TryGetTagForCollider(primary, out var tag))
                    tags.Add(tag);
                else
                    tags.Add(UnityColliderObjectMB.DefaultColliderTag);
                return;
            }

            if (unityColliderMb == null)
                return;

            unityColliderMb.FillConfiguredColliders(colliders, tags);
            FilterTargetsByTag(colliders, tags, normalizedTargetTag);
        }

        static void FilterTargetsByTag(List<Collider2D> colliders, List<string> tags, string targetTag)
        {
            for (var i = colliders.Count - 1; i >= 0; i--)
            {
                var tag = i < tags.Count ? tags[i] : UnityColliderObjectMB.DefaultColliderTag;
                if (string.Equals(tag, targetTag, StringComparison.Ordinal))
                    continue;

                colliders.RemoveAt(i);
                if (i < tags.Count)
                    tags.RemoveAt(i);
            }
        }

        static Collider2D? ResolvePrimaryCollider(Transform root, UnityColliderObjectMB? unityColliderMb)
        {
            var viaMb = unityColliderMb?.Collider;
            if (viaMb != null)
                return viaMb;

            if (root.TryGetComponent<Collider2D>(out var own) && own != null)
                return own;

            var found = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
            for (var i = 0; i < found.Length; i++)
            {
                var collider = found[i];
                if (collider != null)
                    return collider;
            }

            return null;
        }

        static Collider2D? EnsureModeCollider(GameObject host, Collider2D? current, UnityColliderShapeMode mode)
        {
            return mode switch
            {
                UnityColliderShapeMode.Circle => EnsureTypedCollider<CircleCollider2D>(host, current),
                UnityColliderShapeMode.Box => EnsureTypedCollider<BoxCollider2D>(host, current),
                UnityColliderShapeMode.Capsule => EnsureTypedCollider<CapsuleCollider2D>(host, current),
                _ => current ?? host.GetComponent<Collider2D>(),
            };
        }

        static T EnsureTypedCollider<T>(GameObject host, Collider2D? current) where T : Collider2D
        {
            if (current is T typedCurrent)
                return typedCurrent;

            var added = host.AddComponent<T>();
            if (current != null && !ReferenceEquals(current, added))
                current.enabled = false;
            return added;
        }

        static void ApplyGenericSize(Collider2D collider, Vector2 size)
        {
            var safeSize = new Vector2(Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));

            switch (collider)
            {
                case CircleCollider2D circle:
                    circle.radius = Mathf.Max(0.0005f, Mathf.Max(safeSize.x, safeSize.y) * 0.5f);
                    break;
                case BoxCollider2D box:
                    box.size = safeSize;
                    break;
                case CapsuleCollider2D capsule:
                    capsule.size = safeSize;
                    break;
            }
        }

        static void ApplyModeSpecificSettings(Collider2D collider, SetUnityColliderCommandData typed, CommandContext ctx)
        {
            switch (typed.Mode)
            {
                case UnityColliderShapeMode.Circle:
                    if (collider is CircleCollider2D circle)
                    {
                        var radius = typed.CircleRadius.GetOrDefault(ctx, circle.radius);
                        circle.radius = Mathf.Max(0.0005f, radius);
                    }
                    break;
                case UnityColliderShapeMode.Box:
                    if (collider is BoxCollider2D box)
                    {
                        var edgeRadius = typed.BoxEdgeRadius.GetOrDefault(ctx, box.edgeRadius);
                        box.edgeRadius = Mathf.Max(0f, edgeRadius);
                    }
                    break;
                case UnityColliderShapeMode.Capsule:
                    if (collider is CapsuleCollider2D capsule)
                        capsule.direction = typed.CapsuleDirection;
                    break;
            }
        }

        static string NormalizeTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return UnityColliderObjectMB.DefaultColliderTag;

            return tag.Trim();
        }

        static string? NormalizeSelectorTag(string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            return tag.Trim();
        }
    }
}
