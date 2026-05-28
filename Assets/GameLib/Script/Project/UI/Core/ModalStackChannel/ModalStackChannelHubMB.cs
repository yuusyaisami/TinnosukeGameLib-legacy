#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Kernel.Authoring;
using Game.Kernel.IR;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [Serializable]
    public sealed class ModalLayerPresetEntry
    {
        [BoxGroup("Layer")]
        [LabelText("Layer Key")]
        public string LayerKey = "default";

        [BoxGroup("Layer")]
        [LabelText("Order")]
        public int Order = 0;

        [BoxGroup("Layer")]
        [LabelText("Tie Policy")]
        public ModalLayerTiePolicy TiePolicy = ModalLayerTiePolicy.FirstCome;

        [BoxGroup("Layer")]
        [LabelText("Allow Simultaneous Input")]
        public bool AllowSimultaneousInputInSameOrder = false;

        [BoxGroup("Top Order")]
        [LabelText("Top Order Effect")]
        public ModalLayerTopOrderEffect TopOrderEffect = ModalLayerTopOrderEffect.None;

        [BoxGroup("Layer")]
        [LabelText("Keep Non Active Visible")]
        public bool KeepNonActiveInLayerVisible = false;

        [BoxGroup("Layer")]
        [LabelText("Keep Non Active InputActive")]
        public bool KeepNonActiveInLayerInputActive = false;

        public ModalLayerPreset ToPreset()
        {
            return new ModalLayerPreset
            {
                LayerKey = string.IsNullOrWhiteSpace(LayerKey) ? "default" : LayerKey.Trim(),
                Order = Order,
                TiePolicy = TiePolicy,
                AllowSimultaneousInputInSameOrder = AllowSimultaneousInputInSameOrder,
                TopOrderEffect = TopOrderEffect,
                KeepNonActiveInLayerVisible = KeepNonActiveInLayerVisible,
                KeepNonActiveInLayerInputActive = KeepNonActiveInLayerInputActive,
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class ModalStackChannelHubMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Initial Root")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        UIElementLifetimeScope? _initialRoot;

        [Header("Debug")]
        [SerializeField]
        ModalStackChannelHubDebugView _debugView = new();

        [BoxGroup("Layers")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true)]
        [SerializeField]
        List<ModalLayerPresetEntry> _layers = new() { new ModalLayerPresetEntry() };

        IScopeNode? _ownerScope;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            _ownerScope = scope;

            builder.Register<ModalStackChannelHubService>(RuntimeLifetime.Singleton)
                .As<IModalStackChannelHubService>()
                .As<IModalStackChannelTelemetry>();

            builder.RegisterInstance(_debugView);

            builder.RegisterBuildCallback(container =>
            {
                if (!container.TryResolve<IModalStackChannelHubService>(out var hub) || hub == null)
                    return;

                for (var i = 0; i < _layers.Count; i++)
                {
                    var layer = _layers[i];
                    if (layer == null)
                        continue;

                    hub.RegisterLayer(layer.ToPreset());
                }

                if (container.TryResolve<IModalStackChannelTelemetry>(out var telemetry))
                    _debugView.Bind(telemetry);

                SetupInitialRoot(hub);
            });
        }

        void SetupInitialRoot(IModalStackChannelHubService hub)
        {
            if (_ownerScope == null)
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    if (_initialRoot != null)
                    {
                        await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(_initialRoot, CancellationToken.None);
                        var initResolver = _initialRoot.Resolver;
                        if (initResolver != null && initResolver.TryResolve<IUIModalRoot>(out var initRoot) && initRoot != null)
                        {
                            hub.SetDefaultRoot("default", initRoot);
                            return;
                        }
                    }

                    await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(_ownerScope, CancellationToken.None);
                    if (_ownerScope.Resolver != null && _ownerScope.Resolver.TryResolve<IUIModalRoot>(out var ownerRoot) && ownerRoot != null)
                    {
                        hub.SetDefaultRoot("default", ownerRoot);
                        return;
                    }

                    var searchRoot = transform != null ? transform : _ownerScope.Identity?.SelfTransform;
                    if (searchRoot != null)
                    {
                        var lifetimeScopes = searchRoot.GetComponentsInChildren<VContainer.Unity.LifetimeScope>(includeInactive: true);
                        for (var i = 0; i < lifetimeScopes.Length; i++)
                        {
                            var lifetimeScope = lifetimeScopes[i];

                            IScopeNode? nodeFound = null;
                            if (lifetimeScope is Component compRoot)
                            {
                                var components = compRoot.GetComponents<Component>();
                                for (var ci = 0; ci < components.Length; ci++)
                                {
                                    if (components[ci] is IScopeNode scopeNode)
                                    {
                                        nodeFound = scopeNode;
                                        break;
                                    }
                                }
                            }

                            if (nodeFound == null)
                                continue;

                            await ScopeFeatureInstallerUtility.WaitForResolverBuiltAsync(nodeFound, CancellationToken.None);
                            var resolver = nodeFound.Resolver;
                            if (resolver != null && resolver.TryResolve<IUIModalRoot>(out var root) && root != null)
                            {
                                hub.SetDefaultRoot("default", root);
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            });
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            _layers ??= new List<ModalLayerPresetEntry>();
            if (_layers.Count == 0)
                _layers.Add(new ModalLayerPresetEntry());
        }
#endif
    }

    [DisallowMultipleComponent]
    public sealed class ModalStackChannelHubDeclarationMB : EntityDeclarationMB, IEntityServiceDeclarationAuthoring
    {
        [Header("Modal Stack Hub Service")]
        [LabelText("Modal Stack Hub Service Id")]
        [SerializeField]
        int modalStackChannelHubServiceId;

        public int ModalStackChannelHubServiceId => modalStackChannelHubServiceId;

        public bool TryCreateServiceDeclarations(
            in EntityDeclarationPlanInput declarationInput,
            out EntityServiceDeclarationInput[] declarations,
            out string failureReason)
        {
            if (modalStackChannelHubServiceId <= 0)
            {
                declarations = Array.Empty<EntityServiceDeclarationInput>();
                failureReason = "ModalStackChannelHubDeclarationMB requires a positive service id.";
                return false;
            }

            declarations = new[]
            {
                new EntityServiceDeclarationInput(
                    declarationInput.OwnerModule,
                    declarationInput.OwnerEntityRef,
                    new ServiceId(modalStackChannelHubServiceId),
                    CreateStableId(declarationInput.OwnerEntityRef, modalStackChannelHubServiceId),
                    typeof(ModalStackChannelHubService).Name,
                    typeof(ModalStackChannelHubService).Name,
                    new[]
                    {
                        typeof(IModalStackChannelHubService).FullName ?? nameof(IModalStackChannelHubService),
                        typeof(IModalStackChannelTelemetry).FullName ?? nameof(IModalStackChannelTelemetry),
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

            modalStackChannelHubServiceId = Mathf.Max(0, modalStackChannelHubServiceId);
        }

#if UNITY_EDITOR
        public void EnsureLegacyMigrationBindings()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("ModalStackChannelHubDeclarationMB authoring state may only be mutated in edit mode.");

            if (EntityIdentity == null)
            {
                EntityIdentityMB? entityIdentity = GetComponent<EntityIdentityMB>();
                if (entityIdentity == null)
                    entityIdentity = GetComponentInParent<EntityIdentityMB>(true);

                if (entityIdentity != null)
                    SetEntityIdentity(entityIdentity);
            }

            if (modalStackChannelHubServiceId <= 0 && HasSourceLocation)
                modalStackChannelHubServiceId = ComputeMigratedServiceId(CreateSourceLocation(), 1_200_000_000);
        }

        public void SetServiceId(int newServiceId)
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("ModalStackChannelHubDeclarationMB authoring state may only be mutated in edit mode.");

            modalStackChannelHubServiceId = Math.Max(0, newServiceId);
        }
#endif

        static string CreateStableId(Game.Kernel.Abstractions.EntityRef ownerEntityRef, int serviceId)
        {
            return "entity-service:" + ownerEntityRef.Value + ":modal-stack-hub:" + serviceId.ToString("D10");
        }

        static int ComputeMigratedServiceId(UnitySourceLocation sourceLocation, int baseServiceId)
        {
            const int rangeFloor = 100_000_000;
            int range = Math.Max(rangeFloor, int.MaxValue - baseServiceId);

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
    }
}
