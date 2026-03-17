#nullable enable
using System;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Particle
{
    [CreateAssetMenu(menuName = "Game/Particle/Runtime Template", fileName = "ParticleRuntimeTemplate")]
    public sealed class ParticleRuntimeTemplateSO : BaseRuntimeObjectTemplate
    {
        protected override bool UsesBasePreset => false;

        [SerializeReference, InlineProperty]
        ParticleRuntimeTemplatePreset? preset = new();

        public ParticleRuntimeTemplatePreset? Preset => preset;

        public override GameObject Prefab
        {
            get
            {
                if (preset?.Prefab == null)
                    throw new InvalidOperationException($"{nameof(ParticleRuntimeTemplateSO)} requires preset prefab.");
                return preset.Prefab;
            }
        }

        public override bool UsePooling => preset?.UsePooling ?? base.UsePooling;
        public override string Category => preset?.Category ?? base.Category;
        public override string TemplateId
        {
            get
            {
                var resolvedTemplateId = preset?.TemplateId;
                return string.IsNullOrEmpty(resolvedTemplateId) ? name : resolvedTemplateId;
            }
        }

#if UNITY_EDITOR
        protected override void OnEnable()
        {
            base.OnEnable();
            EnsurePreset();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            EnsurePreset();
        }
#endif

        public override void OnAcquire(IScopeNode scope, RuntimeIdentityData identity)
        {
            preset?.OnAcquire(scope, identity);
        }

        public override void OnRelease(IScopeNode scope)
        {
            preset?.OnRelease(scope);
        }

        void EnsurePreset()
        {
            var changed = false;
            if (preset == null)
            {
                preset = new ParticleRuntimeTemplatePreset();
                changed = true;
            }

            changed |= preset.EnsureTemplateId(name);

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
