#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.DI
{
    [Serializable]
    public class BaseRuntimeTemplatePreset
    {
        [Header("Prefab")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        GameObject? prefab;

        [Header("Pooling")]
        [SerializeField]
        [Tooltip("Inspector setting.")]
        bool usePooling = true;

        [Header("Identity Settings")]
        [SerializeField]
        string category = "Runtime";

        [SerializeField]
        string templateId = "";

        [SerializeField]
        int verifiedScopePlanId;

        public virtual GameObject? Prefab => prefab;
        public virtual bool UsePooling => usePooling;
        public virtual string Category => category ?? "Runtime";
        public virtual string TemplateId => string.IsNullOrEmpty(templateId) ? string.Empty : templateId;
        public virtual int VerifiedScopePlanId => verifiedScopePlanId;

        public virtual void OnAcquire(IScopeNode scope, RuntimeIdentityData identity) { }
        public virtual void OnRelease(IScopeNode scope) { }

        internal void ForceUsePooling(bool value)
        {
            usePooling = value;
        }

        internal bool EnsureTemplateId(string fallbackTemplateId)
        {
            if (!string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(fallbackTemplateId))
                return false;

            templateId = fallbackTemplateId;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "Game/Runtime/Runtime Template Preset", fileName = "RuntimeTemplatePreset")]
    public sealed class BaseRuntimeTemplatePresetAssetSO : ScriptableObject
    {
        [SerializeReference]
        public BaseRuntimeTemplatePreset? preset = new();

        public BaseRuntimeTemplatePreset? Preset => preset;

        void OnEnable()
        {
            EnsurePreset();
        }

        void OnValidate()
        {
            EnsurePreset();
        }

        void EnsurePreset()
        {
            var changed = false;
            if (preset == null)
            {
                preset = new BaseRuntimeTemplatePreset();
                changed = true;
            }

            changed |= preset.EnsureTemplateId(name);

#if UNITY_EDITOR
            if (changed)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    public static class RuntimeTemplatePresetResolver
    {
        static readonly Dictionary<BaseRuntimeTemplatePreset, BaseRuntimeTemplateSO> InlineBridgeCache = new(ReferenceComparer.Instance);

        public static BaseRuntimeTemplateSO? ResolveTemplateSO(BaseRuntimeTemplatePreset? preset)
        {
            if (preset == null)
                return null;
            if (preset.Prefab == null)
            {
                InlineBridgeCache.Remove(preset);
                return null;
            }

            if (InlineBridgeCache.TryGetValue(preset, out var cached) && cached != null)
            {
                if (cached is RuntimeTemplatePresetBridgeSO cachedBridge &&
                    cachedBridge.IsValidFor(preset))
                {
                    return cachedBridge;
                }

                InlineBridgeCache.Remove(preset);
            }

            var bridge = ScriptableObject.CreateInstance<RuntimeTemplatePresetBridgeSO>();
            bridge.Initialize(preset);
            InlineBridgeCache[preset] = bridge;
            return bridge;
        }

        sealed class ReferenceComparer : IEqualityComparer<BaseRuntimeTemplatePreset>
        {
            public static readonly ReferenceComparer Instance = new();

            public bool Equals(BaseRuntimeTemplatePreset? x, BaseRuntimeTemplatePreset? y)
                => ReferenceEquals(x, y);

            public int GetHashCode(BaseRuntimeTemplatePreset obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    sealed class RuntimeTemplatePresetBridgeSO : BaseRuntimeTemplateSO
    {
        BaseRuntimeTemplatePreset? _preset;

        protected override bool UsesBasePreset => false;

        public void Initialize(BaseRuntimeTemplatePreset preset)
        {
            _preset = preset;
            name = !string.IsNullOrEmpty(preset.TemplateId)
                ? preset.TemplateId
                : preset.Prefab != null
                    ? $"{preset.Prefab.name}_RuntimeTemplate"
                    : nameof(RuntimeTemplatePresetBridgeSO);
        }

        public bool IsValidFor(BaseRuntimeTemplatePreset preset)
        {
            return ReferenceEquals(_preset, preset) && _preset.Prefab != null;
        }

        public override GameObject Prefab
        {
            get
            {
                if (_preset?.Prefab == null)
                    throw new InvalidOperationException("RuntimeTemplatePresetBridgeSO requires preset prefab.");
                return _preset.Prefab;
            }
        }

        public override bool UsePooling => _preset?.UsePooling ?? false;
        public override string Category => _preset?.Category ?? "Runtime";
        public override string TemplateId
        {
            get
            {
                var resolvedTemplateId = _preset?.TemplateId;
                return string.IsNullOrEmpty(resolvedTemplateId) ? name : resolvedTemplateId;
            }
        }
        public override int VerifiedScopePlanId => _preset?.VerifiedScopePlanId ?? 0;
        public override BaseRuntimeTemplateSO PoolKey => this;

        public override void OnAcquire(IScopeNode scope, RuntimeIdentityData identity)
        {
            _preset?.OnAcquire(scope, identity);
        }

        public override void OnRelease(IScopeNode scope)
        {
            _preset?.OnRelease(scope);
        }
    }
}
