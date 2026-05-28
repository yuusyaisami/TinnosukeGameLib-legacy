#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Abstractions;
using Game.Kernel.Authoring;
using Game.Kernel.IR;
using Game.Times;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{

    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class EntityIdentityMB : EntityAuthoringTraceMB
    {
        [SerializeField]
        [FormerlySerializedAs("id")]
        string entityRefValue = string.Empty;

        [SerializeField]
        string displayName = string.Empty;

        [SerializeField]
        string debugName = string.Empty;

        [SerializeField]
        string entityMetadata = string.Empty;

        [SerializeField]
        string[] classificationTags = Array.Empty<string>();

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("category")]
        string legacyCategory = string.Empty;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("initiallyActive")]
        bool legacyInitiallyActive = true;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("kind")]
        LifetimeScopeKind legacyScopeKind = LifetimeScopeKind.Entity;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("timeScaleBehavior")]
        TimeScaleBehavior legacyTimeScaleBehavior = TimeScaleBehavior.Scaled;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("registerToDynamicRegistry")]
        bool legacyRegisterToDynamicRegistry = true;

        [SerializeField]
        [HideInInspector]
        [FormerlySerializedAs("radius")]
        float radius;

        public static AuthoringComponentKind ComponentKind => AuthoringComponentKind.Bridge;

        public EntityRef EntityRef => new EntityRef(entityRefValue);

        public string DisplayName => displayName;

        public string DebugName => debugName;

        public string EntityMetadata => entityMetadata;

        public IReadOnlyList<string> ClassificationTags => classificationTags;

        public string id
        {
            get => entityRefValue;
            set => entityRefValue = NormalizeRequiredString(value, gameObject.name);
        }

        public string category
        {
            get => legacyCategory;
            set => legacyCategory = NormalizeOptionalString(value);
        }

        public bool initiallyActive
        {
            get => legacyInitiallyActive;
            set => legacyInitiallyActive = value;
        }

        public LifetimeScopeKind kind
        {
            get => legacyScopeKind;
            set => legacyScopeKind = value == LifetimeScopeKind.None ? LifetimeScopeKind.Entity : value;
        }

        public TimeScaleBehavior timeScaleBehavior
        {
            get => legacyTimeScaleBehavior;
            set => legacyTimeScaleBehavior = value;
        }

        public bool LegacyDynamicRegistryOptIn => legacyRegisterToDynamicRegistry;

        public float Radius => radius;

        public bool TryGetEntityRef(out EntityRef entityRef)
        {
            return Game.Kernel.Abstractions.EntityRef.TryParse(entityRefValue, out entityRef);
        }

        public bool TryCreatePlanInput(ModuleId ownerModule, out EntityAuthoringInput input, out string failureReason)
        {
            if (!TryGetEntityRef(out EntityRef entityRef))
            {
                input = default;
                failureReason = "Entity identities must provide a non-empty EntityRef.";
                return false;
            }

            if (!TryValidateSourceLocation(out failureReason))
            {
                input = default;
                return false;
            }

            input = new EntityAuthoringInput(
                ownerModule,
                entityRef,
                displayName,
                debugName,
                entityMetadata,
                CollectClassificationTags(),
                CreatePlanSourceLocation());
            failureReason = string.Empty;
            return true;
        }

        void Reset()
        {
            entityRefValue = gameObject.name;
            displayName = gameObject.name;
            debugName = gameObject.name;
            if (legacyScopeKind == LifetimeScopeKind.None)
                legacyScopeKind = LifetimeScopeKind.Entity;
        }

        void OnEnable()
        {
            if (string.IsNullOrEmpty(entityRefValue))
                entityRefValue = gameObject.name;

            if (string.IsNullOrEmpty(displayName))
                displayName = gameObject.name;

            if (string.IsNullOrEmpty(debugName))
                debugName = gameObject.name;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            entityRefValue = NormalizeRequiredString(entityRefValue, gameObject.name);
            displayName = NormalizeRequiredString(displayName, gameObject.name);
            debugName = NormalizeRequiredString(debugName, gameObject.name);
            entityMetadata = NormalizeOptionalString(entityMetadata);
            legacyCategory = NormalizeOptionalString(legacyCategory);
            radius = Mathf.Max(0f, radius);

            if (legacyScopeKind == LifetimeScopeKind.None)
                legacyScopeKind = LifetimeScopeKind.Entity;

            if (classificationTags == null || classificationTags.Length == 0)
            {
                classificationTags = string.IsNullOrEmpty(legacyCategory)
                    ? Array.Empty<string>()
                    : new[] { legacyCategory };
                return;
            }

            for (int index = 0; index < classificationTags.Length; index++)
                classificationTags[index] = NormalizeOptionalString(classificationTags[index]);
        }

        string[] CollectClassificationTags()
        {
            if (classificationTags == null || classificationTags.Length == 0)
            {
                return string.IsNullOrEmpty(legacyCategory)
                    ? Array.Empty<string>()
                    : new[] { legacyCategory };
            }

            List<string> tags = new List<string>(classificationTags.Length + 1);
            for (int index = 0; index < classificationTags.Length; index++)
            {
                string tag = NormalizeOptionalString(classificationTags[index]);
                if (tag.Length == 0 || tags.Contains(tag))
                    continue;

                tags.Add(tag);
            }

            if (!string.IsNullOrEmpty(legacyCategory) && !tags.Contains(legacyCategory))
                tags.Add(legacyCategory);

            return tags.ToArray();
        }
    }
}
