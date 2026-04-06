#nullable enable
using System.Collections.Generic;
using Game.Channel;
using Game.Commands.VNext;
using UnityEngine;
using VContainer;

namespace Game.SelectRuntime
{
    public static class UserMoveRotateValidationUtility
    {
        static readonly List<SelectableRuntimeMB> SelectableBuffer = new();
        static readonly Collider[] OverlapBuffer = new Collider[64];

        public static bool IsValidPose(UserMoveRotateValidationRequest request, Vector3 position, Quaternion rotation)
        {
            if (!request.IsValid)
                return true;

            if (!PassAreaConstraint(request, position))
                return false;

            if (!PassDistanceConstraint(request, position))
                return false;

            if (!PassColliderConstraint(request, position))
                return false;

            return true;
        }

        public static bool TryFindNearestValidPose(
            UserMoveRotateValidationRequest request,
            Vector3 requestedPosition,
            Quaternion requestedRotation,
            out Vector3 correctedPosition,
            out Quaternion correctedRotation)
        {
            correctedPosition = requestedPosition;
            correctedRotation = requestedRotation;

            if (!request.IsValid)
                return true;

            if (IsValidPose(request, requestedPosition, requestedRotation))
                return true;

            if (TryClampToAreaBoundary(request, requestedPosition, out var boundaryPosition))
            {
                correctedPosition = boundaryPosition;
                correctedRotation = requestedRotation;

                if (IsValidPose(request, correctedPosition, correctedRotation))
                    return true;
            }

            var plane = ResolvePlane(request, correctedPosition);
            const float radiusStep = 0.25f;
            const int ringCount = 16;
            const int samplesPerRing = 24;
            for (int ring = 1; ring <= ringCount; ring++)
            {
                var radius = ring * radiusStep;
                for (int sample = 0; sample < samplesPerRing; sample++)
                {
                    var angle = (sample / (float)samplesPerRing) * Mathf.PI * 2f;
                    var offset2D = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    var candidate = plane == AreaPlane.XZ
                        ? correctedPosition + new Vector3(offset2D.x, 0f, offset2D.y)
                        : correctedPosition + new Vector3(offset2D.x, offset2D.y, 0f);

                    if (!IsValidPose(request, candidate, requestedRotation))
                        continue;

                    correctedPosition = candidate;
                    correctedRotation = requestedRotation;
                    return true;
                }
            }

            return false;
        }

        static bool TryClampToAreaBoundary(UserMoveRotateValidationRequest request, Vector3 worldPosition, out Vector3 correctedPosition)
        {
            correctedPosition = worldPosition;

            if (!TryResolveFirstAreaPlayer(request, out var player, out var basePosition))
                return false;

            var shape = player.Definition.Shape;
            if (shape == null)
                return false;

            var plane = player.Definition.Plane;
            var localOffset = worldPosition - basePosition;
            var local = ToLocal(localOffset, plane);

            switch (shape)
            {
                case CircleAreaShape circle:
                    {
                        var outer = Mathf.Max(0f, circle.Radius);
                        if (outer <= 0f)
                            return false;

                        var inner = Mathf.Clamp(circle.InnerRadius, 0f, outer);
                        var magnitude = local.magnitude;
                        if (magnitude <= Mathf.Epsilon)
                        {
                            local = inner > 0f ? Vector2.right * inner : Vector2.zero;
                        }
                        else if (magnitude < inner)
                        {
                            local = local / magnitude * inner;
                        }
                        else if (magnitude > outer)
                        {
                            local = local / magnitude * outer;
                        }

                        correctedPosition = basePosition + ToPlane(local, plane);
                        return true;
                    }

                case RectAreaShape rect:
                    {
                        var halfSize = rect.Size * 0.5f;
                        if (halfSize.x <= 0f && halfSize.y <= 0f)
                            return false;

                        local.x = Mathf.Clamp(local.x, -halfSize.x, halfSize.x);
                        local.y = Mathf.Clamp(local.y, -halfSize.y, halfSize.y);
                        correctedPosition = basePosition + ToPlane(local, plane);
                        return true;
                    }
            }

            return false;
        }

        public static bool TryProjectPointerPosition(
            UserMoveRotateValidationRequest request,
            Camera? camera,
            Vector2 screenPosition,
            Vector3 currentPosition,
            out Vector3 projectedPosition)
        {
            projectedPosition = currentPosition;
            camera ??= Camera.main;
            if (camera == null)
                return false;

            var plane = ResolvePlane(request, currentPosition);
            var planeOrigin = ResolvePlaneOrigin(request, currentPosition);
            var planeNormal = plane == AreaPlane.XZ ? Vector3.up : Vector3.forward;
            var ray = camera.ScreenPointToRay(screenPosition);
            var worldPlane = new Plane(planeNormal, planeOrigin);
            if (!worldPlane.Raycast(ray, out var enter))
                return false;

            projectedPosition = ray.GetPoint(enter);
            return true;
        }

        static bool PassAreaConstraint(UserMoveRotateValidationRequest request, Vector3 position)
        {
            if (!HasConfiguredAreaTags(request))
                return true;

            var areaTags = request.Editor.AreaTags;
            for (int i = 0; i < areaTags.Count; i++)
            {
                var areaTag = areaTags[i];
                if (!TryResolveAreaPlayer(request, areaTag, out var player, out var basePosition))
                    continue;

                if (player.ContainsPosition(basePosition, position))
                    return true;
            }

            return false;
        }

        static bool PassDistanceConstraint(UserMoveRotateValidationRequest request, Vector3 position)
        {
            if (request.Editor.MinDistanceToOtherSelectable <= 0f || request.ManagerBridge == null)
                return true;

            if (!SelectRuntimeBridgeResolver.TryResolveManagerService(request.ManagerBridge, out var managerService) || managerService == null)
                return true;

            managerService.GetRegisteredSelectables(SelectableBuffer);
            try
            {
                var plane = ResolvePlane(request, position);
                for (int i = 0; i < SelectableBuffer.Count; i++)
                {
                    var selectable = SelectableBuffer[i];
                    if (selectable == null)
                        continue;

                    if (!selectable.TryResolveActorScope(out var otherScope) || otherScope == null)
                        continue;

                    if (ReferenceEquals(otherScope, request.RuntimeScope))
                        continue;

                    var otherTransform = otherScope.Identity?.SelfTransform;
                    if (otherTransform == null || !otherScope.IsActive || !otherScope.IsVisible)
                        continue;

                    var delta = plane == AreaPlane.XZ
                        ? new Vector2(position.x - otherTransform.position.x, position.z - otherTransform.position.z)
                        : new Vector2(position.x - otherTransform.position.x, position.y - otherTransform.position.y);
                    if (delta.sqrMagnitude < request.Editor.MinDistanceToOtherSelectable * request.Editor.MinDistanceToOtherSelectable)
                        return false;
                }

                return true;
            }
            finally
            {
                SelectableBuffer.Clear();
            }
        }

        static bool PassColliderConstraint(UserMoveRotateValidationRequest request, Vector3 position)
        {
            var blockMask = request.Editor.BlockLayerMask;
            if (blockMask == 0 || request.ValidationColliders == null || request.ValidationColliders.Count == 0)
                return true;

            var origin = request.RootTransform.position;
            var delta = position - origin;
            for (int i = 0; i < request.ValidationColliders.Count; i++)
            {
                var collider = request.ValidationColliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                var bounds = collider.bounds;
                var center = bounds.center + delta;
                var extents = bounds.extents;
                var hitCount = Physics.OverlapBoxNonAlloc(center, extents, OverlapBuffer, Quaternion.identity, blockMask, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < hitCount; j++)
                {
                    var hit = OverlapBuffer[j];
                    if (hit == null || ReferenceEquals(hit, collider) || hit.transform.IsChildOf(request.RootTransform))
                        continue;

                    return false;
                }
            }

            return true;
        }

        static bool HasConfiguredAreaTags(UserMoveRotateValidationRequest request)
        {
            var areaTags = request.Editor.AreaTags;
            if (areaTags == null || areaTags.Count == 0)
                return false;

            for (int i = 0; i < areaTags.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(areaTags[i]))
                    return true;
            }

            return false;
        }

        static bool TryResolveAreaPlayer(
            UserMoveRotateValidationRequest request,
            string areaTag,
            out IAreaChannelPlayer player,
            out Vector3 basePosition)
        {
            player = null!;
            basePosition = default;

            if (string.IsNullOrWhiteSpace(areaTag))
                return false;

            var areaScope = ActorSourceFastResolver.Resolve(request.RuntimeScope, request.Editor.AreaActorSource);
            if (areaScope?.Resolver == null)
                return false;

            if (!areaScope.Resolver.TryResolve<IAreaChannelHubService>(out var hub) || hub == null)
                return false;

            if (!hub.TryGetPlayer(areaTag, out player) || player == null)
                return false;

            basePosition = ResolveAreaBasePosition(player.Definition, areaScope);
            return true;
        }

        static Vector3 ResolveAreaBasePosition(AreaChannelDefinition definition, IScopeNode scope)
        {
            var anchor = definition.Anchor != null ? definition.Anchor : scope.Identity?.SelfTransform;
            return anchor != null ? anchor.position + definition.CenterOffset : definition.CenterOffset;
        }

        static Vector2 ToLocal(Vector3 offset, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector2(offset.x, offset.z)
                : new Vector2(offset.x, offset.y);
        }

        static Vector3 ToPlane(Vector2 offset, AreaPlane plane)
        {
            return plane == AreaPlane.XZ
                ? new Vector3(offset.x, 0f, offset.y)
                : new Vector3(offset.x, offset.y, 0f);
        }

        public static AreaPlane ResolvePlane(UserMoveRotateValidationRequest request, Vector3 currentPosition)
        {
            if (TryResolveFirstAreaPlayer(request, out var player, out _))
                return player.Definition.Plane;

            return request.Editor.FallbackPlane;
        }

        static Vector3 ResolvePlaneOrigin(UserMoveRotateValidationRequest request, Vector3 currentPosition)
        {
            if (TryResolveFirstAreaPlayer(request, out _, out var basePosition))
                return basePosition;

            return currentPosition;
        }

        static bool TryResolveFirstAreaPlayer(
            UserMoveRotateValidationRequest request,
            out IAreaChannelPlayer player,
            out Vector3 basePosition)
        {
            player = null!;
            basePosition = default;

            if (!HasConfiguredAreaTags(request))
                return false;

            var areaTags = request.Editor.AreaTags;
            for (int i = 0; i < areaTags.Count; i++)
            {
                if (TryResolveAreaPlayer(request, areaTags[i], out player, out basePosition))
                    return true;
            }

            return false;
        }
    }
}
