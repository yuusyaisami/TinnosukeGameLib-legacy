#nullable enable

using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Input;
using Game.Kernel.Authoring;
using Game.Kernel.IR;
using Game.SelectRuntime;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.UI
{
    [Serializable]
    public sealed class ButtonChannelOptions : IButtonChannelOptions
    {
        public DynamicValue<ButtonChannelPreset> PresetValue { get; set; } =
            DynamicValue<ButtonChannelPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonChannelPreset>(new ButtonChannelPreset()));

        public Transform OwnerTransform { get; set; } = null!;
    }

    [Serializable]
    public sealed class ButtonChannelDefinition
    {
        [BoxGroup("Channel")]
        [LabelText("Channel Tag")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        string _channelTag = "default";

        [BoxGroup("Preset")]
        [LabelText("Preset")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        DynamicValue<ButtonChannelPreset> _presetValue =
            DynamicValue<ButtonChannelPreset>.FromSource(
                new ManagedRefLiteralSource<ButtonChannelPreset>(new ButtonChannelPreset()));

        public string ChannelTag => string.IsNullOrWhiteSpace(_channelTag) ? "default" : _channelTag.Trim();

        internal ButtonChannelOptions CreateOptions(Transform ownerTransform)
        {
            return new ButtonChannelOptions
            {
                PresetValue = _presetValue,
                OwnerTransform = ownerTransform,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class ButtonChannelHubDeclarationMB : EntityDeclarationMB, IEntityServiceDeclarationAuthoring
    {
        [Header("Button Channel Hub Service")]
        [LabelText("Button Channel Hub Service Id")]
        [SerializeField]
        int buttonChannelHubServiceId;

        [BoxGroup("Binding")]
        [LabelText("Binding Mode")]
        [SerializeField]
        ButtonChannelBindingMode bindingMode = ButtonChannelBindingMode.Auto;

        [BoxGroup("Binding")]
        [LabelText("UI Selection Source")]
        [SerializeField]
        UISelectionMB? uiSelectionSource;

        [BoxGroup("Binding")]
        [LabelText("Game Root Input Source")]
        [SerializeField]
        InputMB? gameRootInputSource;

        [BoxGroup("Binding")]
        [LabelText("World Manager Source")]
        [SerializeField]
        SelectRuntimeManagerMB? worldManagerSource;

        [BoxGroup("Binding")]
        [LabelText("World Selectable")]
        [SerializeField]
        SelectableRuntimeMB? worldSelectable;

        [BoxGroup("Binding")]
        [LabelText("World Pointer Target")]
        [SerializeField]
        WorldPointerTargetMB? worldPointerTarget;

        [BoxGroup("Channels")]
        [LabelText("Channels")]
        [ListDrawerSettings(DefaultExpandedState = true, DraggableItems = true, ShowFoldout = true)]
        [FormerlySerializedAs("_channels")]
        [SerializeField]
        List<ButtonChannelDefinition> channels = new() { new ButtonChannelDefinition() };

        public int ButtonChannelHubServiceId => buttonChannelHubServiceId;
        public ButtonChannelBindingMode BindingMode => ResolveBindingMode();

        public IReadOnlyList<ButtonChannelDefinition> Channels => channels;
        public UISelectionMB? UISelectionSource => uiSelectionSource;
        public InputMB? GameRootInputSource => gameRootInputSource;
        public SelectRuntimeManagerMB? WorldManagerSource => worldManagerSource;
        public SelectableRuntimeMB? WorldSelectable => worldSelectable;
        public WorldPointerTargetMB? WorldPointerTarget => worldPointerTarget;

        public bool TryCreateServiceDeclarations(
            in EntityDeclarationPlanInput declarationInput,
            out EntityServiceDeclarationInput[] declarations,
            out string failureReason)
        {
#if UNITY_EDITOR
            EnsureLegacyMigrationBindings();
#endif

            if (buttonChannelHubServiceId <= 0)
            {
                declarations = Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "ButtonChannelHubDeclarationMB requires a positive button channel hub service id.";
                return false;
            }

            declarations = new[]
            {
                new EntityServiceDeclarationInput(
                    declarationInput.OwnerModule,
                    declarationInput.OwnerEntityRef,
                    new ServiceId(buttonChannelHubServiceId),
                    CreateStableId(declarationInput.OwnerEntityRef, buttonChannelHubServiceId),
                    typeof(ButtonChannelHubService).Name,
                    typeof(ButtonChannelHubService).Name + " [" + BuildChannelDebugName() + "]",
                    new[]
                    {
                        typeof(IButtonChannelHubService).FullName ?? nameof(IButtonChannelHubService),
                    },
                    Array.Empty<EntityServiceDependencyInput>(),
                    Array.Empty<ServiceLifecycleContributionInput>(),
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

#if UNITY_EDITOR
            EnsureLegacyMigrationBindings();
#endif

            buttonChannelHubServiceId = Mathf.Max(0, buttonChannelHubServiceId);
        }

#if UNITY_EDITOR
        public void EnsureLegacyMigrationBindings()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("ButtonChannelHubDeclarationMB authoring state may only be mutated in edit mode.");

            if (EntityIdentity == null)
            {
                EntityIdentityMB? entityIdentity = GetComponent<EntityIdentityMB>();
                if (entityIdentity == null)
                    entityIdentity = GetComponentInParent<EntityIdentityMB>(true);

                if (entityIdentity != null)
                    SetEntityIdentity(entityIdentity);
            }

            EnsureBindingSources();

            if (buttonChannelHubServiceId <= 0 && HasSourceLocation)
                buttonChannelHubServiceId = ComputeMigratedServiceId(CreateSourceLocation());
        }

        public void SetServiceId(int newServiceId)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("ButtonChannelHubDeclarationMB authoring state may only be mutated in edit mode.");

            buttonChannelHubServiceId = Math.Max(0, newServiceId);
        }
#endif

        ButtonChannelBindingMode ResolveBindingMode()
        {
            if (bindingMode != ButtonChannelBindingMode.Auto)
                return bindingMode;

            if (HasLocalUIElementState() && uiSelectionSource != null)
                return ButtonChannelBindingMode.UI;

            if (worldManagerSource != null || worldPointerTarget != null || worldSelectable != null)
                return ButtonChannelBindingMode.World;

            if (gameRootInputSource != null)
                return ButtonChannelBindingMode.GameRoot;

            if (HasLocalUIElementState())
                return ButtonChannelBindingMode.UI;

            return ButtonChannelBindingMode.Auto;
        }

#if UNITY_EDITOR
        void EnsureBindingSources()
        {
            if (HasLocalUIElementState() && uiSelectionSource == null)
                uiSelectionSource = GetComponentInParent<UISelectionMB>(true);

            if (worldSelectable == null)
            {
                worldSelectable = GetComponent<SelectableRuntimeMB>();
                if (worldSelectable == null)
                    worldSelectable = GetComponentInChildren<SelectableRuntimeMB>(true);
            }

            if (worldPointerTarget == null)
            {
                worldPointerTarget = worldSelectable != null
                    ? worldSelectable.ResolveTarget()
                    : GetComponent<WorldPointerTargetMB>();

                if (worldPointerTarget == null)
                    worldPointerTarget = GetComponentInChildren<WorldPointerTargetMB>(true);
            }

            if (worldManagerSource == null && (worldSelectable != null || worldPointerTarget != null))
                worldManagerSource = SelectRuntimeBridgeResolver.FindNearestManager(transform);

            if (gameRootInputSource == null)
                gameRootInputSource = GetComponentInParent<InputMB>(true);

            if (bindingMode == ButtonChannelBindingMode.Auto)
                bindingMode = ResolveBindingMode();
        }
#endif

        bool HasLocalUIElementState()
        {
            return GetComponent<UIElementStateMB>() != null;
        }

        static string CreateStableId(Game.Kernel.Abstractions.EntityRef ownerEntityRef, int serviceId)
        {
            return "entity-service:" + ownerEntityRef.Value + ":button-channel-hub:" + serviceId.ToString("D10");
        }

        static int ComputeMigratedServiceId(UnitySourceLocation sourceLocation)
        {
            const int baseServiceId = 1_000_000_000;
            const int range = int.MaxValue - baseServiceId;

            unchecked
            {
                uint hash = 2166136261;
                string seed = sourceLocation.ToString();
                for (int index = 0; index < seed.Length; index++)
                {
                    hash ^= seed[index];
                    hash *= 16777619;
                }

                int serviceId = baseServiceId + (int)(hash % (uint)range);
                return serviceId <= 0 ? baseServiceId : serviceId;
            }
        }

        string BuildChannelDebugName()
        {
            if (channels == null || channels.Count == 0)
                return "channels=0";

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append("channels=").Append(channels.Count).Append(",tags=");

            var wroteAnyTag = false;
            for (int index = 0; index < channels.Count; index++)
            {
                ButtonChannelDefinition? definition = channels[index];
                if (definition == null)
                    continue;

                string tag = definition.ChannelTag;
                if (tag.Length == 0)
                    continue;

                if (wroteAnyTag)
                    builder.Append('|');

                builder.Append(tag);
                wroteAnyTag = true;
            }

            if (!wroteAnyTag)
                builder.Append("default");

            return builder.ToString();
        }
    }
}