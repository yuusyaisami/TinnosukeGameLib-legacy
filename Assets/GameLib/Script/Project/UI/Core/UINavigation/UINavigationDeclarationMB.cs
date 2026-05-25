#nullable enable

using Game;
using Game.Input;
using Game.Kernel.Authoring;
using Game.Kernel.IR;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UINavigationDeclarationMB : EntityDeclarationMB, IEntityServiceDeclarationAuthoring
    {
        [Header("Navigation Service")]
        [LabelText("Navigation Service Id")]
        [SerializeField]
        int navigationServiceId = 0;

        [Header("Input Navigate Service")]
        [LabelText("Input Navigate Service Id")]
        [SerializeField]
        int inputNavigateServiceId = 0;

        [Header("Selection Dependency")]
        [LabelText("Selection Service Id")]
        [SerializeField]
        int selectionServiceId = 0;

        [Header("Control Scheme Dependency")]
        [LabelText("Control Scheme Service Id")]
        [SerializeField]
        int controlSchemeServiceId = 0;

        [Header("Navigation Threshold")]
        [Tooltip("Inspector setting.")]
        [Range(0.1f, 0.9f)]
        [SerializeField]
        float navigateThreshold = 0.5f;

        [Header("Repeat Settings")]
        [Tooltip("Inspector setting.")]
        [Range(0.1f, 1.0f)]
        [SerializeField]
        float repeatDelay = 0.4f;

        [Tooltip("Inspector setting.")]
        [Range(0.05f, 0.5f)]
        [SerializeField]
        float repeatRate = 0.1f;

        [Header("Debug")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        bool enableNavigationLogging;

        public int NavigationServiceId => navigationServiceId;

        public int InputNavigateServiceId => inputNavigateServiceId;

        public int SelectionServiceId => selectionServiceId;

        public int ControlSchemeServiceId => controlSchemeServiceId;

        public float NavigateThreshold => navigateThreshold;

        public float RepeatDelay => repeatDelay;

        public float RepeatRate => repeatRate;

        public bool EnableNavigationLogging => enableNavigationLogging;

        public bool TryCreateServiceDeclarations(
            in EntityDeclarationPlanInput declarationInput,
            out EntityServiceDeclarationInput[] declarations,
            out string failureReason)
        {
            if (navigationServiceId <= 0)
            {
                declarations = System.Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "UINavigationDeclarationMB requires a positive navigation service id.";
                return false;
            }

            if (inputNavigateServiceId <= 0)
            {
                declarations = System.Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "UINavigationDeclarationMB requires a positive input navigate service id.";
                return false;
            }

            if (navigationServiceId == inputNavigateServiceId)
            {
                declarations = System.Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "UINavigationDeclarationMB must use distinct service ids for navigation and input navigate services.";
                return false;
            }

            if (selectionServiceId <= 0)
            {
                declarations = System.Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "UINavigationDeclarationMB requires a positive selection service id.";
                return false;
            }

            if (controlSchemeServiceId <= 0)
            {
                declarations = System.Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "UINavigationDeclarationMB requires a positive control scheme service id.";
                return false;
            }

            if (selectionServiceId == controlSchemeServiceId
                || selectionServiceId == navigationServiceId
                || selectionServiceId == inputNavigateServiceId
                || controlSchemeServiceId == navigationServiceId
                || controlSchemeServiceId == inputNavigateServiceId)
            {
                declarations = System.Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "UINavigationDeclarationMB must use distinct service ids across navigation, input navigate, selection, and control scheme services.";
                return false;
            }

            string payloadLabel = declarationInput.PayloadType.Length == 0
                ? typeof(UINavigationOptions).FullName ?? nameof(UINavigationOptions)
                : declarationInput.PayloadType;

            string optionsDebug = "threshold=" + navigateThreshold.ToString("0.###")
                + ",delay=" + repeatDelay.ToString("0.###")
                + ",rate=" + repeatRate.ToString("0.###")
                + ",logging=" + enableNavigationLogging;

            declarations = new[]
            {
                new EntityServiceDeclarationInput(
                    declarationInput.OwnerModule,
                    declarationInput.OwnerEntityRef,
                    new ServiceId(navigationServiceId),
                    CreateStableId(declarationInput.OwnerEntityRef, navigationServiceId, "navigation"),
                    typeof(UINavigationService).Name,
                    payloadLabel + " [" + optionsDebug + "]",
                    new[]
                    {
                        typeof(IUINavigationService).FullName ?? nameof(IUINavigationService),
                        typeof(IUINavigationTelemetry).FullName ?? nameof(IUINavigationTelemetry),
                    },
                    new[]
                    {
                        new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(selectionServiceId)), DependencyStrength.Required),
                        new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(controlSchemeServiceId)), DependencyStrength.Required),
                        new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(inputNavigateServiceId)), DependencyStrength.Required),
                    },
                    SourceKind,
                    ServiceLifetimeKind.Singleton,
                    ServiceFactoryKind.GeneratedFactory,
                    declarationInput.Source),
                new EntityServiceDeclarationInput(
                    declarationInput.OwnerModule,
                    declarationInput.OwnerEntityRef,
                    new ServiceId(inputNavigateServiceId),
                    CreateStableId(declarationInput.OwnerEntityRef, inputNavigateServiceId, "input-navigate"),
                    typeof(UIInputNavigateManagerService).Name,
                    payloadLabel + " [input-navigate]",
                    new[]
                    {
                        typeof(IUIInputNavigateService).FullName ?? nameof(IUIInputNavigateService),
                        typeof(IScopeAcquireHandler).FullName ?? nameof(IScopeAcquireHandler),
                        typeof(IScopeReleaseHandler).FullName ?? nameof(IScopeReleaseHandler),
                    },
                    new[]
                    {
                        new EntityServiceDependencyInput(new DependencyNodeIR(new ServiceId(selectionServiceId)), DependencyStrength.Required),
                    },
                    SourceKind,
                    ServiceLifetimeKind.Singleton,
                    ServiceFactoryKind.GeneratedFactory,
                    declarationInput.Source),
            };

            failureReason = string.Empty;
            return true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            navigateThreshold = Mathf.Clamp(navigateThreshold, 0.1f, 0.9f);
            repeatDelay = Mathf.Clamp(repeatDelay, 0.1f, 1.0f);
            repeatRate = Mathf.Clamp(repeatRate, 0.05f, 0.5f);
        }

#if UNITY_EDITOR
        public void SetServiceIds(int newNavigationServiceId, int newInputNavigateServiceId, int newSelectionServiceId, int newControlSchemeServiceId)
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("UINavigationDeclarationMB authoring state may only be mutated in edit mode.");

            navigationServiceId = newNavigationServiceId;
            inputNavigateServiceId = newInputNavigateServiceId;
            selectionServiceId = newSelectionServiceId;
            controlSchemeServiceId = newControlSchemeServiceId;
        }
#endif

        static string CreateStableId(Game.Kernel.Abstractions.EntityRef ownerEntityRef, int serviceId, string suffix)
        {
            return "entity-service:" + ownerEntityRef.Value + ":" + suffix + ":" + serviceId.ToString("D10");
        }
    }
}
