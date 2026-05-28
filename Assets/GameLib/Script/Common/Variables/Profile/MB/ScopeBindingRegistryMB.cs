// Game.Profile.ScopeBindingRegistryMB.cs
//
// ScopeBindingRegistryService 縺ｮ DI 逋ｻ骭ｲ縺ｨ縲！nspector 縺九ｉ縺ｮ蛻晄悄 Profile 險ｭ螳壹ｒ陦後≧縲・
// Pool(RuntimeLifetimeScope) 縺ｧ縺ｮ蜀榊茜逕ｨ繝ｭ繧ｸ繝・け縺ｯ ScopeBindingRegistryInstallService 縺梧球蠖薙☆繧九・

using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using Game;
using Game.Common;
using Game.Save;
using Game.Profile;
using Sirenix.OdinInspector;

namespace Game.Profile
{
    [DisallowMultipleComponent]
    public sealed class ScopeBindingRegistryMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Profiles")]
        [Tooltip("Inspector setting.")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowPaging = false, CustomAddFunction = nameof(AddProfileInternal))]
        [SerializeField]
        List<DynamicValue<BaseProfileData>> _profiles = new();

        [Header("Pool / Runtime")]
        [Tooltip("RuntimeLifetimeScope(Pool) 縺ｮ Acquire 譎ゅ↓ Registry 繧偵Μ繧ｻ繝・ヨ縺励※蜀咲匳骭ｲ縺吶ｋ")]
        [SerializeField] bool _resetOnAcquire = true;

        [Tooltip("RuntimeLifetimeScope(Pool) 縺ｮ Release 譎ゅ↓ Registry 繧偵け繝ｪ繧｢縺吶ｋ")]
        [SerializeField] bool _clearOnRelease = true;

        [Header("Scope Identity")]
        [Tooltip("Inspector setting.")]
        [SerializeField] bool _enableSaveInRuntimeScope = false;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var isRuntime = scope.Kind == LifetimeScopeKind.Runtime;

            builder.Register<ScopeBindingRegistryService>(resolver =>
            {
                var blackboard = resolver.TryResolve<IVarStore>(out var b) ? b : null;
                var scalar = resolver.TryResolve<Game.Scalar.IBaseScalarService>(out var s) ? s : null;
                var scopeIdentity = string.Empty;

                // First, try resolving identity via this scope's resolver (preferred)
                if (resolver.TryResolve<ILTSIdentityService>(out var identity) && identity != null && (!isRuntime || _enableSaveInRuntimeScope))
                {
                    scopeIdentity = identity.Id ?? string.Empty;
                }

                // If not found via resolver, use this scope's own identity (strong preference)
                if (string.IsNullOrEmpty(scopeIdentity) && scope.Identity != null && (!isRuntime || _enableSaveInRuntimeScope))
                {
                    scopeIdentity = scope.Identity.Id ?? string.Empty;
                }

                // Last resort: walk up scope chain to find nearest ILTSIdentityService (fallback for edge cases)
                if (string.IsNullOrEmpty(scopeIdentity))
                {
                    for (IScopeNode cur = scope.Parent; cur != null; cur = cur.Parent)
                    {
                        var id = cur.Identity;
                        if (id != null && (!isRuntime || _enableSaveInRuntimeScope))
                        {
                            scopeIdentity = id.Id ?? string.Empty;
                            if (!string.IsNullOrEmpty(scopeIdentity))
                                Debug.Log($"[ScopeBindingRegistryMB] Using parent scope identity '{scopeIdentity}' from scope kind={cur.Kind} for ProfileRegistry (current scope had none).");
                            break;
                        }
                    }
                }

                return new ScopeBindingRegistryService(blackboard, scalar, scopeIdentity, scope);
            }, RuntimeLifetime.Singleton)
                .As<IScopeBindingRegistry>()
                .As<ScopeBindingRegistryService>();

            // Evaluate DynamicValue entries to collect profile definitions
            var allProfiles = new List<IProfileDefinition>();
            foreach (var dv in _profiles)
            {
                if (!dv.HasSource) continue;
                if (dv.TryGet(null, out BaseProfileData preset) && preset != null)
                    allProfiles.Add(preset);
            }

            var options = new ScopeBindingRegistryInstallService.Options(
                inspectorProfiles: allProfiles.ToArray(),
                resetOnAcquire: _resetOnAcquire,
                clearOnRelease: _clearOnRelease
            );

            builder.RegisterInstance(options);

            builder.Register<ScopeBindingRegistryInstallService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .As<IScopeBindingRegistryConfigurator>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();

            builder.Register<SaveScopeRegistrationService>(resolver =>
            {
                var profiles = resolver.TryResolve<ScopeBindingRegistryService>(out var p) ? p : null;
                var blackboard = resolver.TryResolve<IVarStore>(out var b) ? b : null;
                var gridBlackboard = resolver.TryResolve<IGridBlackboardService>(out var gb) ? gb : null;
                var scalar = resolver.TryResolve<Game.Scalar.IBaseScalarService>(out var s) ? s : null;
                return new SaveScopeRegistrationService(profiles, blackboard, gridBlackboard, scalar, scope);
            }, RuntimeLifetime.Transient)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterBuildCallback(container =>
            {
                var installer = container.Resolve<ScopeBindingRegistryInstallService>();
                installer.InstallInitialIfNeeded();
            });
        }

        void AddProfileInternal()
        {
            _profiles ??= new List<DynamicValue<BaseProfileData>>();
            _profiles.Add(DynamicValue<BaseProfileData>.FromSource(new ManagedRefLiteralSource<BaseProfileData>(new CustomProfileDefinition())));
        }
    }
}
