#nullable enable
using System;
using Game.DI;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Fire
{
    [CreateAssetMenu(menuName = "Game/FirePattern/Runtime Template", fileName = "FirePatternRuntimeTemplate")]
    public sealed class FirePatternRuntimeTemplateSO : BaseRuntimeObjectTemplate
    {
        protected override bool UsesBasePreset => false;

        [SerializeReference, InlineProperty]
        FirePatternRuntimeTemplatePreset? preset = new();

        public FirePatternRuntimeTemplatePreset? Preset => preset;

        public override GameObject Prefab
        {
            get
            {
                if (preset?.Prefab == null)
                    throw new InvalidOperationException($"{nameof(FirePatternRuntimeTemplateSO)} requires preset prefab.");
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

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

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
                preset = new FirePatternRuntimeTemplatePreset();
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
