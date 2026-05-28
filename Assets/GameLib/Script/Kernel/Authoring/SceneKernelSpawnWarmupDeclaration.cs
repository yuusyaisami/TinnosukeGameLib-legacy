#nullable enable

using System;
using Game.Common;
using Game.DI;
using Game.Kernel.Layers;
using Game.Spawn;
using UnityEngine;

namespace Game.Kernel.Authoring
{
    [Serializable]
    public sealed class SceneKernelSpawnWarmupDeclaration
    {
        [SerializeField]
        SpawnerKind kind = SpawnerKind.RuntimeEntity;

        [SerializeField]
        string tag = string.Empty;

        [SerializeField]
        DynamicValue<BaseRuntimeTemplatePreset> template =
            DynamicValue<BaseRuntimeTemplatePreset>.FromSource(
                new ManagedRefLiteralSource<BaseRuntimeTemplatePreset>(new BaseRuntimeTemplatePreset()));

        [SerializeField]
        int count;

        public SceneKernelSpawnWarmupDeclaration()
        {
        }

        public SceneKernelSpawnWarmupDeclaration(SpawnerKind kind, string? tag = null, int count = 0)
        {
            this.kind = kind;
            this.tag = tag ?? string.Empty;
            this.count = count;
            Normalize();
        }

        public SceneKernelSpawnWarmupDeclaration(
            SpawnerKind kind,
            string? tag,
            DynamicValue<BaseRuntimeTemplatePreset> template,
            int count)
        {
            this.kind = kind;
            this.tag = tag ?? string.Empty;
            this.template = template;
            this.count = count;
            Normalize();
        }

        public SpawnerKind Kind => kind;

        public string Tag => NormalizeTag(tag);

        public DynamicValue<BaseRuntimeTemplatePreset> Template => template;

        public int Count => count < 0 ? 0 : count;

        public bool IsRuntimeKind => kind == SpawnerKind.RuntimeEntity || kind == SpawnerKind.RuntimeUIElement;

        public SceneKernelSpawnRouteId KernelRouteId => SceneKernelSpawnRouteId.FromParts(kind.ToString(), Tag);

        public bool TryResolveTemplate(out BaseRuntimeTemplateSO runtimeTemplate, out string failureReason)
        {
            BaseRuntimeTemplatePreset? resolvedPreset = template.GetOrDefaultWithoutContext();
            if (resolvedPreset == null)
            {
                runtimeTemplate = null!;
                failureReason = "SceneKernel warmup entries require a runtime template preset.";
                return false;
            }

            runtimeTemplate = RuntimeTemplatePresetResolver.ResolveTemplateSO(resolvedPreset);
            if (runtimeTemplate == null)
            {
                failureReason = "SceneKernel warmup entries require a preset with a prefab.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public bool TryValidate(out string failureReason)
        {
            if (!IsRuntimeKind)
            {
                failureReason = "SceneKernel warmup entries only support RuntimeEntity or RuntimeUIElement kinds in the new path.";
                return false;
            }

            if (count < 0)
            {
                failureReason = "SceneKernel warmup counts must be zero or positive.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        internal void Normalize()
        {
            tag = NormalizeTag(tag);
            count = Math.Max(0, count);
        }

        static string NormalizeTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
                return string.Empty;

            string normalizedTag = rawTag.Trim();
            return string.Equals(normalizedTag, "default", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : normalizedTag;
        }
    }
}