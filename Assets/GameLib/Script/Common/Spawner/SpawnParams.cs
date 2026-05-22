#nullable enable

using Game.DI;
using UnityEngine;

namespace Game.Spawn
{
    /// <summary>
    /// Unified spawn parameter struct for all spawners.
    /// LTS spawners use Prefab; Runtime spawners use Template/Identity.
    /// </summary>
    public struct SpawnParams
    {
        public GameObject? Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        /// <summary>
        /// Parent transform in the Unity hierarchy.
        /// - When null, Runtime spawners should parent under their configured root.
        /// - When specified and different from the configured root, pooling must be bypassed.
        /// </summary>
        public Transform? TransformParent;

        /// <summary>
        /// Parent scope for DI/LifetimeScope build.
        /// This is independent from <see cref="TransformParent"/>.
        /// When specified, KernelScopeHost should be built under this scope even if the transform
        /// parent is the spawner root.
        /// </summary>
        public IScopeNode? LifetimeScopeParent;
        public bool WorldSpace;

        /// <summary>
        /// Whether the spawned instance may be pooled. When false, spawners should instantiate/destroy
        /// directly and mark the created KernelScopeHost so it will not be returned to the pool.
        /// Default: true
        /// </summary>
        public bool AllowPooling;

        public BaseRuntimeTemplateSO? Template;
        public RuntimeIdentityData? Identity;

        public static SpawnParams Default => new()
        {
            Prefab = null,
            Position = Vector3.zero,
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = null,
            Identity = null
        };

        public static SpawnParams ForLTS(GameObject prefab, Vector3 position) => new()
        {
            Prefab = prefab,
            Position = position,
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = null,
            Identity = null
        };

        public static SpawnParams ForLTS(GameObject prefab, Vector3 position, Quaternion rotation) => new()
        {
            Prefab = prefab,
            Position = position,
            Rotation = rotation,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = null,
            Identity = null
        };
        public static SpawnParams ForLTS(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Transform? transformParent = null,
            IScopeNode? lifetimeScopeParent = null,
            bool worldSpace = true,
            bool allowPooling = true) => new()
            {
                Prefab = prefab,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                TransformParent = transformParent,
                LifetimeScopeParent = lifetimeScopeParent,
                WorldSpace = worldSpace,
                AllowPooling = allowPooling,
                Template = null,
                Identity = null
            };

        public static SpawnParams ForRuntime(BaseRuntimeObjectTemplate template, Vector3 position, RuntimeIdentityData? identity = null) => new()
        {
            Prefab = null,
            Position = position,
            Rotation = Quaternion.identity,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = template,
            Identity = identity
        };

        public static SpawnParams ForRuntime(BaseRuntimeTemplateSO template, Vector3 position, Quaternion rotation, RuntimeIdentityData? identity = null) => new()
        {
            Prefab = null,
            Position = position,
            Rotation = rotation,
            Scale = Vector3.one,
            TransformParent = null,
            LifetimeScopeParent = null,
            WorldSpace = true,
            AllowPooling = true,
            Template = template,
            Identity = identity
        };

        public static SpawnParams ForRuntime(
            BaseRuntimeTemplateSO template,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            RuntimeIdentityData? identity = null,
            Transform? transformParent = null,
            IScopeNode? lifetimeScopeParent = null,
            bool worldSpace = true,
            bool allowPooling = true) => new()
            {
                Prefab = null,
                Position = position,
                Rotation = rotation,
                Scale = scale,
                TransformParent = transformParent,
                LifetimeScopeParent = lifetimeScopeParent,
                WorldSpace = worldSpace,
                AllowPooling = allowPooling,
                Template = template,
                Identity = identity
            };
    }

    public static class SpawnPoseUtility
    {
        public static bool TryResolveAcquireWorldPose(Transform parent, in SpawnParams spawnParams, out Vector3 worldPosition, out Quaternion worldRotation)
        {
            worldPosition = spawnParams.Position;
            worldRotation = spawnParams.Rotation;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[SpawnPoseUtility] ResolveAcquire parent={DescribeTransform(parent)} worldSpace={spawnParams.WorldSpace} inputPos={spawnParams.Position} inputRot={spawnParams.Rotation.eulerAngles}");
#endif

            if (parent == null)
                return false;

            if (spawnParams.WorldSpace)
            {
                if (parent is RectTransform parentRect &&
                    TryProjectWorldToCanvasPlane(parentRect, spawnParams.Position, out worldPosition))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    //Debug.Log($"[SpawnPoseUtility] ResolveAcquire result=Projected worldPos={worldPosition} worldRot={worldRotation.eulerAngles}");
#endif
                    return true;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                //Debug.Log($"[SpawnPoseUtility] ResolveAcquire result=DirectFallback worldPos={worldPosition} worldRot={worldRotation.eulerAngles}");
#endif
                return parent is RectTransform ? false : true;
            }

            worldPosition = parent.TransformPoint(spawnParams.Position);
            worldRotation = parent.rotation * spawnParams.Rotation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"[SpawnPoseUtility] ResolveAcquire result=LocalToWorld worldPos={worldPosition} worldRot={worldRotation.eulerAngles}");
#endif
            return true;
        }

        public static void ApplySpawnPose(Transform target, SpawnParams spawnParams)
        {
            if (target == null)
                return;

            var scale = spawnParams.Scale == default ? Vector3.one : spawnParams.Scale;

            if (target is RectTransform rect)
            {
                if (TryApplyRectTransformPose(rect, spawnParams))
                {
                    rect.localScale = scale;
                    return;
                }
            }

            if (spawnParams.WorldSpace)
            {
                target.SetPositionAndRotation(spawnParams.Position, spawnParams.Rotation);
            }
            else
            {
                target.localPosition = spawnParams.Position;
                target.localRotation = spawnParams.Rotation;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var canvas = target.GetComponentInParent<Canvas>(includeInactive: true);
            //Debug.Log($"[SpawnPoseUtility] Apply target={DescribeTransform(target)} canvas={DescribeCanvas(canvas)} worldSpace={spawnParams.WorldSpace} inputPos={spawnParams.Position} inputRot={spawnParams.Rotation.eulerAngles} finalWorldPos={target.position} finalLocalPos={target.localPosition} finalScale={target.localScale}");
#endif

            target.localScale = scale;
        }

        static bool TryApplyRectTransformPose(RectTransform rect, SpawnParams spawnParams)
        {
            if (rect == null)
                return false;

            var canvas = rect.GetComponentInParent<Canvas>(includeInactive: true);
            if (canvas == null)
            {
                ApplyRectAsTransform(rect, spawnParams);
                return true;
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                ApplyRectAsTransform(rect, spawnParams);
                return true;
            }

            if (!spawnParams.WorldSpace)
            {
                rect.localPosition = spawnParams.Position;
                rect.localRotation = spawnParams.Rotation;
                rect.anchoredPosition3D = spawnParams.Position;
                rect.anchoredPosition = new Vector2(spawnParams.Position.x, spawnParams.Position.y);
                return true;
            }

            if (!TryResolveProjectionCamera(canvas, out var projectionCamera) || projectionCamera == null)
            {
                // Fall back to the raw world pose when a projection camera is not available.
                ApplyRectAsTransform(rect, spawnParams);
                return true;
            }

            var screenCamera = projectionCamera;
            var screenPoint = screenCamera.WorldToScreenPoint(spawnParams.Position);

            if (!IsFinite(screenPoint.x) || !IsFinite(screenPoint.y))
            {
                ApplyRectAsTransform(rect, spawnParams);
                return true;
            }

            var parentRect = rect.parent as RectTransform ?? canvas.transform as RectTransform;
            if (parentRect == null)
            {
                ApplyRectAsTransform(rect, spawnParams);
                return true;
            }

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    parentRect,
                    new Vector2(screenPoint.x, screenPoint.y),
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : projectionCamera,
                    out var worldPoint))
            {
                rect.position = worldPoint;
                rect.rotation = spawnParams.Rotation;
                return true;
            }

            ApplyRectAsTransform(rect, spawnParams);
            return true;
        }

        static bool TryProjectWorldToCanvasPlane(RectTransform rect, Vector3 worldPosition, out Vector3 canvasWorldPoint)
        {
            canvasWorldPoint = default;

            var canvas = rect.GetComponentInParent<Canvas>(includeInactive: true);
            if (canvas == null)
                return false;

            if (canvas.renderMode == RenderMode.WorldSpace)
                return false;

            var projectionCamera = canvas.worldCamera != null
                ? canvas.worldCamera
                : Camera.main;

            if (projectionCamera == null)
                return false;

            var screenPoint = projectionCamera.WorldToScreenPoint(worldPosition);

            if (!IsFinite(screenPoint.x) || !IsFinite(screenPoint.y))
                return false;

            var parentRect = rect.parent as RectTransform ?? canvas.transform as RectTransform;
            if (parentRect == null)
                return false;

            return RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parentRect,
                new Vector2(screenPoint.x, screenPoint.y),
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : projectionCamera,
                out canvasWorldPoint);
        }

        static void ApplyRectAsTransform(RectTransform rect, SpawnParams spawnParams)
        {
            if (spawnParams.WorldSpace)
            {
                rect.SetPositionAndRotation(spawnParams.Position, spawnParams.Rotation);
            }
            else
            {
                rect.localPosition = spawnParams.Position;
                rect.localRotation = spawnParams.Rotation;
            }
        }

        static bool TryResolveProjectionCamera(Canvas canvas, out Camera? camera)
        {
            camera = null;
            if (canvas != null && canvas.worldCamera != null)
            {
                camera = canvas.worldCamera;
                return true;
            }

            camera = Camera.main;
            return camera != null;
        }

        static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static string DescribeTransform(Transform? t)
        {
            if (t == null)
                return "<null>";

            return $"{t.name} pos={t.position} local={t.localPosition}";
        }

        static string DescribeCanvas(Canvas? canvas)
        {
            if (canvas == null)
                return "<null>";

            return $"{canvas.name} mode={canvas.renderMode}";
        }
#endif
    }
}



