#nullable enable

using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.DI
{
    /// <summary>
    /// Base ScriptableObject template for pooled KernelScopeHost prefabs.
    /// New Runtime system will use this to configure the scope on Acquire.
    /// </summary>
    public abstract class BaseRuntimeTemplateSO : ScriptableObject
    {
        [SerializeReference, InlineProperty, HideLabel]
        [ShowIf(nameof(UsesBasePreset))]
        BaseRuntimeTemplatePreset? preset = new();

        /// <summary>
        /// Derived classes may override this to fix pooling behavior at the type level.
        /// Return null to allow per-preset inspector control.
        /// Return true/false to force pooling on/off for that derived template type.
        /// </summary>
        protected virtual bool? FixedUsePooling => null;

        protected virtual bool UsesBasePreset => true;

        protected BaseRuntimeTemplatePreset? BasePreset
        {
            get
            {
                EnsurePreset();
                return preset;
            }
        }

        /// <summary>Prefab that holds KernelScopeHost.</summary>
        public virtual GameObject Prefab
        {
            get
            {
                var resolvedPreset = BasePreset;
                if (resolvedPreset?.Prefab == null)
                    throw new System.InvalidOperationException($"{GetType().Name} requires preset prefab.");
                return resolvedPreset.Prefab;
            }
        }

        /// <summary>
        /// Whether instances created from this template should be pooled.
        /// If a derived class overrides <see cref="FixedUsePooling"/>, that fixed value takes precedence.
        /// </summary>
        public virtual bool UsePooling
        {
            get
            {
                var fixedUsePooling = FixedUsePooling;
                if (fixedUsePooling != null)
                    return fixedUsePooling.Value;

                return BasePreset?.UsePooling ?? true;
            }
        }

        public virtual string Category => BasePreset?.Category ?? "Runtime";

        public virtual string TemplateId
        {
            get
            {
                var resolvedTemplateId = BasePreset?.TemplateId;
                return string.IsNullOrEmpty(resolvedTemplateId) ? name : resolvedTemplateId;
            }
        }

        public virtual int VerifiedScopePlanId => BasePreset?.VerifiedScopePlanId ?? 0;

        /// <summary>
        /// Pool key for grouping templates into the same pool.
        /// Default is template itself (one pool per template).
        /// </summary>
        public virtual BaseRuntimeTemplateSO PoolKey => this;

        /// <summary>
        /// Hook called after the KernelScopeHost is acquired and its container is built.
        /// </summary>
        public virtual void OnAcquire(IScopeNode scope, RuntimeIdentityData identity)
        {
            BasePreset?.OnAcquire(scope, identity);
        }

        /// <summary>
        /// Hook called before the KernelScopeHost is released to the pool.
        /// </summary>
        public virtual void OnRelease(IScopeNode scope)
        {
            BasePreset?.OnRelease(scope);
        }

#if UNITY_EDITOR
        protected virtual void OnEnable()
        {
            EnsurePreset();
        }

        protected virtual void OnValidate()
        {
            EnsurePreset();
        }
#endif

        void EnsurePreset()
        {
            if (!UsesBasePreset)
                return;

            var resolvedBasePreset = preset;
            var changed = false;
            if (resolvedBasePreset == null)
            {
                resolvedBasePreset = new BaseRuntimeTemplatePreset();
                preset = resolvedBasePreset;
                changed = true;
            }

            if (FixedUsePooling != null)
                resolvedBasePreset.ForceUsePooling(FixedUsePooling.Value);
            changed |= resolvedBasePreset.EnsureTemplateId(name);

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}


