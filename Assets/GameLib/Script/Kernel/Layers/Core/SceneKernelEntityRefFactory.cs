#nullable enable

using System;
using Game.DI;
using Game.Kernel.Abstractions;
using Game.Kernel.Authoring;
using UnityEngine;

namespace Game.Kernel.Layers
{
    internal static class SceneKernelEntityRefFactory
    {
        public static EntityRef Create(SceneKernelHandle sceneHandle, SceneKernelSpawnRouteId routeId, BaseRuntimeTemplateSO template, int spawnOrdinal)
        {
            if (sceneHandle.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(sceneHandle), sceneHandle, "SceneKernel entity refs require a positive scene handle.");

            if (routeId.IsEmpty)
                throw new ArgumentException("SceneKernel entity refs require a non-empty route id.", nameof(routeId));

            if (template == null)
                throw new ArgumentNullException(nameof(template));

            if (spawnOrdinal <= 0)
                throw new ArgumentOutOfRangeException(nameof(spawnOrdinal), spawnOrdinal, "SceneKernel entity refs require a positive spawn ordinal.");

            string seed = ResolveEntitySeed(routeId, template);
            return new EntityRef("runtime:" + sceneHandle.Value + ":" + seed + ":" + spawnOrdinal);
        }

        static string ResolveEntitySeed(SceneKernelSpawnRouteId routeId, BaseRuntimeTemplateSO template)
        {
            GameObject? prefab = template.Prefab;
            if (prefab != null)
            {
                EntityIdentityMB? identity = prefab.GetComponent<EntityIdentityMB>();
                if (identity != null && identity.TryGetEntityRef(out EntityRef entityRef) && !entityRef.IsEmpty)
                    return NormalizeSeed(entityRef.Value);
            }

            string templateId = template.TemplateId;
            if (!string.IsNullOrWhiteSpace(templateId))
                return NormalizeSeed(templateId);

            return NormalizeSeed(routeId.Value);
        }

        static string NormalizeSeed(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (normalized.Length == 0)
                throw new ArgumentException("SceneKernel entity refs require a non-empty entity seed.", nameof(value));

            return normalized;
        }
    }
}