// Game.Profile.ProfileRegistryMB.cs
//
// ProfileRegistryService の DI 登録と、Inspector からの初期 Profile 設定を行う。
// Pool(RuntimeLifetimeScope) での再利用ロジックは ProfileRegistryInstallService が担当する。

using System;
using UnityEngine;
using VContainer;
using Game;
using Game.Common;
using Game.Save;

namespace Game.Profile
{
    [DisallowMultipleComponent]
    public sealed class ProfileRegistryMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Profiles")]
        [Tooltip("外部 Profile が未指定の場合に登録する Profile")]
        [SerializeField] BaseProfileSO[] _profilesFromInspector = Array.Empty<BaseProfileSO>();
        [SerializeReference]
        [Tooltip("Inline Profile 定義（SerializeReference）。外部 Profile が未指定の場合に登録する。")]
        [SerializeField] IProfileDefinition[] _profileDefinitionsFromInspector = Array.Empty<IProfileDefinition>();

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

            builder.Register<ProfileRegistryService>(resolver =>
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
                                Debug.Log($"[ProfileRegistryMB] Using parent scope identity '{scopeIdentity}' from scope kind={cur.Kind} for ProfileRegistry (current scope had none).");
                            break;
                        }
                    }
                }

                return new ProfileRegistryService(blackboard, scalar, scopeIdentity, scope);
            }, Lifetime.Singleton)
                .As<IProfileRegistry>()
                .As<ProfileRegistryService>();

            var options = new ProfileRegistryInstallService.Options(
                inspectorProfiles: _profilesFromInspector,
                inspectorProfileDefinitions: _profileDefinitionsFromInspector,
                resetOnAcquire: _resetOnAcquire,
                clearOnRelease: _clearOnRelease
            );

            builder.RegisterInstance(options);

            builder.Register<ProfileRegistryInstallService>(Lifetime.Singleton)
                .WithParameter(scope)
                .As<IProfileRegistryConfigurator>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .AsSelf();

            builder.Register<SaveScopeRegistrationService>(resolver =>
            {
                var profiles = resolver.TryResolve<ProfileRegistryService>(out var p) ? p : null;
                var blackboard = resolver.TryResolve<IBlackboardService>(out var b) ? b : null;
                var scalar = resolver.TryResolve<Game.Scalar.IBaseScalarService>(out var s) ? s : null;
                return new SaveScopeRegistrationService(profiles, blackboard, scalar, scope);
            }, Lifetime.Transient)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterBuildCallback(container =>
            {
                var installer = container.Resolve<ProfileRegistryInstallService>();
                installer.InstallInitialIfNeeded();
            });
        }
    }
}
