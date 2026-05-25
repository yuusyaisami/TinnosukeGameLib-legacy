#nullable enable

using System;
using Game.Kernel.Abstractions;
using Game.Kernel.IR;
using UnityEngine;

namespace Game.Kernel.Authoring
{
    public readonly struct EntityAuthoringInput
    {
        public EntityAuthoringInput(
            ModuleId ownerModule,
            EntityRef entityRef,
            string displayName,
            string debugName,
            string metadata,
            string[] classificationTags,
            SourceLocationIR source)
        {
            OwnerModule = ownerModule;
            EntityRef = entityRef;
            DisplayName = displayName ?? string.Empty;
            DebugName = debugName ?? string.Empty;
            Metadata = metadata ?? string.Empty;
            ClassificationTags = classificationTags ?? Array.Empty<string>();
            Source = source;
        }

        public ModuleId OwnerModule { get; }
        public EntityRef EntityRef { get; }
        public string DisplayName { get; }
        public string DebugName { get; }
        public string Metadata { get; }
        public string[] ClassificationTags { get; }
        public SourceLocationIR Source { get; }
    }

    public readonly struct EntityDeclarationPlanInput
    {
        public EntityDeclarationPlanInput(
            ModuleId ownerModule,
            EntityRef ownerEntityRef,
            string declarationType,
            string declarationId,
            string serviceId,
            string payloadType,
            string uiNodeId,
            string lifecycleId,
            SourceLocationIR source)
        {
            OwnerModule = ownerModule;
            OwnerEntityRef = ownerEntityRef;
            DeclarationType = declarationType ?? string.Empty;
            DeclarationId = declarationId ?? string.Empty;
            ServiceId = serviceId ?? string.Empty;
            PayloadType = payloadType ?? string.Empty;
            UINodeId = uiNodeId ?? string.Empty;
            LifecycleId = lifecycleId ?? string.Empty;
            Source = source;
        }

        public ModuleId OwnerModule { get; }
        public EntityRef OwnerEntityRef { get; }
        public string DeclarationType { get; }
        public string DeclarationId { get; }
        public string ServiceId { get; }
        public string PayloadType { get; }
        public string UINodeId { get; }
        public string LifecycleId { get; }
        public SourceLocationIR Source { get; }
    }

    public abstract class EntityDeclarationMB : EntityAuthoringTraceMB
    {
        [SerializeField]
        EntityIdentityMB? entityIdentity;

        [SerializeField]
        string declarationId = string.Empty;

        [SerializeField]
        string serviceId = string.Empty;

        [SerializeField]
        string payloadType = string.Empty;

        [SerializeField]
        string uiNodeId = string.Empty;

        [SerializeField]
        string lifecycleId = string.Empty;

        public static AuthoringComponentKind ComponentKind => AuthoringComponentKind.Declaration;

        public EntityIdentityMB? EntityIdentity => entityIdentity;

        public string DeclarationId => declarationId;

        public string ServiceId => serviceId;

        public string PayloadType => payloadType;

        public string UINodeId => uiNodeId;

        public string LifecycleId => lifecycleId;

        public bool IsBoundToExplicitAncestorEntity()
        {
            return entityIdentity != null
                && (transform == entityIdentity.transform || transform.IsChildOf(entityIdentity.transform));
        }

        public bool TryCreatePlanInput(ModuleId ownerModule, out EntityDeclarationPlanInput input, out string failureReason)
        {
            if (entityIdentity == null)
            {
                input = default;
                failureReason = "Declaration MBs must bind to an explicit EntityIdentityMB.";
                return false;
            }

            if (!entityIdentity.TryGetEntityRef(out EntityRef entityRef))
            {
                input = default;
                failureReason = "Declaration MBs require a non-empty EntityRef.";
                return false;
            }

            if (!IsBoundToExplicitAncestorEntity())
            {
                input = default;
                failureReason = "Declaration MBs must live on the bound EntityIdentityMB or one of its children.";
                return false;
            }

            if (!TryValidateSourceLocation(out failureReason))
            {
                input = default;
                return false;
            }

            input = new EntityDeclarationPlanInput(
                ownerModule,
                entityRef,
                GetType().FullName ?? GetType().Name,
                NormalizeOptionalString(declarationId),
                NormalizeOptionalString(serviceId),
                NormalizeOptionalString(payloadType),
                NormalizeOptionalString(uiNodeId),
                NormalizeOptionalString(lifecycleId),
                CreatePlanSourceLocation());
            failureReason = string.Empty;
            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            declarationId = NormalizeOptionalString(declarationId);
            serviceId = NormalizeOptionalString(serviceId);
            payloadType = NormalizeOptionalString(payloadType);
            uiNodeId = NormalizeOptionalString(uiNodeId);
            lifecycleId = NormalizeOptionalString(lifecycleId);
        }

#if UNITY_EDITOR
        public void SetEntityIdentity(EntityIdentityMB? identity)
        {
            EnsureEditModeForAuthoring();
            entityIdentity = identity;
        }

        static void EnsureEditModeForAuthoring()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("Entity declaration bindings may only be mutated in edit mode.");
        }
#endif
    }
}
