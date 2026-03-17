// Game.Profile.ScopeBindingRegistryMB.cs
//
// ScopeBindingRegistryService の DI 登録と、Inspector からの初期 Profile 設定を行う。
// Pool(RuntimeLifetimeScope) での再利用ロジックは ScopeBindingRegistryInstallService が担当する。

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
        [Tooltip("登録する Profile Preset。Literal (inline) または Asset (SO参照) で指定可能。")]
        [SerializeField]
        List<DynamicValue<BaseProfileData>> _profiles = new();

        [Header("Pool / Runtime")]
        [Tooltip("RuntimeLifetimeScope(Pool) の Acquire 時に Registry をリセットして再登録する")]
        [SerializeField] bool _resetOnAcquire = true;

        [Tooltip("RuntimeLifetimeScope(Pool) の Release 時に Registry をクリアする")]
        [SerializeField] bool _clearOnRelease = true;

        [Header("Scope Identity")]
        [Tooltip("RuntimeLifetimeScope でも Scope ID を設定して Save 対象にする（非推奨）")]
        [SerializeField] bool _enableSaveInRuntimeScope = false;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var isRuntime = scope.Kind == LifetimeScopeKind.Runtime;

            builder.Register<ScopeBindingRegistryService>(resolver =>
            {
                var blackboard = resolver.TryResolve<IBlackboardService>(out var b) ? b : null;
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
            }, Lifetime.Singleton)
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

            builder.Register<ScopeBindingRegistryInstallService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IScopeBindingRegistryConfigurator>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();

            builder.Register<SaveScopeRegistrationService>(resolver =>
            {
                var profiles = resolver.TryResolve<ScopeBindingRegistryService>(out var p) ? p : null;
                var blackboard = resolver.TryResolve<IBlackboardService>(out var b) ? b : null;
                var scalar = resolver.TryResolve<Game.Scalar.IBaseScalarService>(out var s) ? s : null;
                return new SaveScopeRegistrationService(profiles, blackboard, scalar, scope);
            }, Lifetime.Transient)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterBuildCallback(container =>
            {
                var installer = container.Resolve<ScopeBindingRegistryInstallService>();
                installer.InstallInitialIfNeeded();
            });
        }
    }
}
