#nullable enable
using System;
using Game.Flow;
using Game.Kernel.Abstractions;
using Game.Kernel.Boot;
using Game.Kernel.Diagnostics;
using Game.Kernel.Generation;
using Game.Kernel.IR;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Project.Bootstrap
{
    public enum KernelLiveBootInitialSceneMode
    {
        GameScene = 10,
        SceneName = 20,
    }

    public enum KernelLiveBootPersistentRootRole
    {
        Project = 10,
        Global = 20,
    }

    public readonly struct KernelLiveBootPersistentRootInstance
    {
        public KernelLiveBootPersistentRootInstance(
            KernelLiveBootPersistentRootRole role,
            global::Game.IScopeGraphHost rootScope)
        {
            Role = role;
            RootScope = rootScope ?? throw new ArgumentNullException(nameof(rootScope));
        }

        public KernelLiveBootPersistentRootRole Role { get; }

        public global::Game.IScopeGraphHost RootScope { get; }

        public GameObject RootGameObject => RootScope.HostGameObject;

        public Transform RootTransform => RootScope.HostTransform;
    }

    [CreateAssetMenu(fileName = "KernelLiveBootBundle", menuName = "Game/Kernel/V21/Live Boot Bundle")]
    public sealed class KernelLiveBootBundleAsset : ScriptableObject
    {
        [BoxGroup("Topology"), Required]
        [SerializeField] GameObject projectRootPrefab = null!;

        [BoxGroup("Topology"), Required]
        [SerializeField] GameObject globalRootPrefab = null!;

        [BoxGroup("Topology")]
        [SerializeField] KernelLiveBootLoadingParentKind loadingParentKind = KernelLiveBootLoadingParentKind.GlobalRoot;

        [BoxGroup("Published Bundle")]
        [SerializeField, Min(1)] int profileId = 21001;

        [BoxGroup("Published Bundle")]
        [SerializeField] KernelProfileKind profileKind = KernelProfileKind.Development;

        [BoxGroup("Published Bundle")]
        [SerializeField, Min(1)] int manifestId = 21001;

        [BoxGroup("Published Bundle")]
        [SerializeField, Min(1)] int bootPolicyId = 21001;

        [BoxGroup("Published Bundle")]
        [SerializeField, Min(1)] int planId = 21001;

        [BoxGroup("Published Bundle")]
        [SerializeField, Min(1)] int artifactSetId = 21001;

        [BoxGroup("Published Bundle")]
        [SerializeField, Min(1)] int formatVersion = 1;

        [BoxGroup("Published Bundle")]
        [SerializeField] string generatorVersion = "V21-M1";

        [BoxGroup("Verified Scope Binding")]
        [SerializeField, Min(0)] int projectRootScopePlanId;

        [BoxGroup("Verified Scope Binding")]
        [SerializeField, Min(0)] int platformRootScopePlanId;

        [BoxGroup("Verified Scope Binding")]
        [SerializeField, Min(0)] int globalRootScopePlanId;

        [BoxGroup("Initial Scene")]
        [SerializeField] bool autoLoadInitialScene = true;

        [BoxGroup("Initial Scene")]
        [SerializeField] KernelLiveBootInitialSceneMode initialSceneMode = KernelLiveBootInitialSceneMode.GameScene;

        [BoxGroup("Initial Scene"), ShowIf(nameof(UsesGameScene))]
        [SerializeField] GameScene initialGameScene = GameScene.Title;

        [BoxGroup("Initial Scene"), ShowIf(nameof(UsesSceneName))]
        [SerializeField] string initialSceneName = "TitleScene";

        public GameObject ProjectRootPrefab => projectRootPrefab;

        public GameObject GlobalRootPrefab => globalRootPrefab;

        public KernelLiveBootLoadingParentKind LoadingParentKind => loadingParentKind;

        public bool AutoLoadInitialScene => autoLoadInitialScene;

        bool UsesGameScene => initialSceneMode == KernelLiveBootInitialSceneMode.GameScene;

        bool UsesSceneName => initialSceneMode == KernelLiveBootInitialSceneMode.SceneName;

        public KernelBootPublishedArtifactBundle CreatePublishedArtifactBundle()
        {
            ValidateConfiguration();

            KernelProfile profile = new KernelProfile(new KernelProfileId(profileId), profileKind);
            return KernelBootPublishedArtifactBundleFactory.CreateMinimal(
                profile,
                new ManifestId(manifestId),
                new BootPolicyId(bootPolicyId),
                new PlanId(planId),
                new ArtifactSetId(artifactSetId),
                formatVersion,
                generatorVersion,
                requiredRootScopes: CreateRequiredRootScopeIdentities());
        }

        public string ResolveInitialSceneName()
        {
            if (!autoLoadInitialScene)
                return string.Empty;

            return initialSceneMode == KernelLiveBootInitialSceneMode.GameScene
                ? initialGameScene.ToSceneName()
                : (initialSceneName ?? string.Empty).Trim();
        }

        public KernelLiveBootPersistentRootInstance InstantiateProjectRootHost()
        {
            ValidateConfiguration();

            return InstantiatePersistentRoot(
                projectRootPrefab,
                KernelLiveBootPersistentRootRole.Project,
                parent: null,
                expectedKind: global::Game.LifetimeScopeKind.Project);
        }

        public KernelLiveBootPersistentRootInstance InstantiateGlobalRootHost(Transform parent)
        {
            ValidateConfiguration();

            return InstantiatePersistentRoot(
                globalRootPrefab,
                KernelLiveBootPersistentRootRole.Global,
                parent,
                expectedKind: global::Game.LifetimeScopeKind.Global);
        }

        public bool TryGetProjectRootScopePlanId(out ScopePlanId planId)
        {
            return TryGetScopePlanId(projectRootScopePlanId, out planId);
        }

        public bool TryGetPlatformRootScopePlanId(out ScopePlanId planId)
        {
            return TryGetScopePlanId(platformRootScopePlanId, out planId);
        }

        public bool TryGetGlobalRootScopePlanId(out ScopePlanId planId)
        {
            return TryGetScopePlanId(globalRootScopePlanId, out planId);
        }

        void ValidateConfiguration()
        {
            if (projectRootPrefab == null)
                throw new InvalidOperationException("Kernel live boot requires an explicit project root host prefab.");

            if (globalRootPrefab == null)
                throw new InvalidOperationException("Kernel live boot requires an explicit global root host prefab.");

            if (!Enum.IsDefined(typeof(KernelProfileKind), profileKind))
                throw new InvalidOperationException("Kernel live boot requires a concrete kernel profile kind.");

            if (string.IsNullOrWhiteSpace(generatorVersion))
                throw new InvalidOperationException("Kernel live boot requires a generator version.");

            if (projectRootScopePlanId <= 0 || platformRootScopePlanId <= 0 || globalRootScopePlanId <= 0)
            {
                throw new InvalidOperationException(
                    "Kernel live boot requires explicit Project, Platform, and Global root scope plan ids for verified composition.");
            }

            if (projectRootScopePlanId == platformRootScopePlanId
                || projectRootScopePlanId == globalRootScopePlanId
                || platformRootScopePlanId == globalRootScopePlanId)
            {
                throw new InvalidOperationException("Kernel live boot root scope plan ids must be unique.");
            }

            if (autoLoadInitialScene && string.IsNullOrWhiteSpace(ResolveInitialSceneName()))
                throw new InvalidOperationException("Kernel live boot requires an explicit initial scene when auto load is enabled.");
        }

        RuntimeIdentityRef[] CreateRequiredRootScopeIdentities()
        {
            return new[]
            {
                new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, projectRootScopePlanId),
                new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, platformRootScopePlanId),
                new RuntimeIdentityRef(RuntimeIdentityKind.ScopePlan, globalRootScopePlanId),
            };
        }

        static bool TryGetScopePlanId(int rawPlanId, out ScopePlanId planId)
        {
            if (rawPlanId <= 0)
            {
                planId = default;
                return false;
            }

            planId = new ScopePlanId(rawPlanId);
            return true;
        }

        static KernelLiveBootPersistentRootInstance InstantiatePersistentRoot(
            GameObject prefab,
            KernelLiveBootPersistentRootRole role,
            Transform? parent,
            global::Game.LifetimeScopeKind expectedKind)
        {
            GameObject instance = parent == null ? Instantiate(prefab) : Instantiate(prefab, parent);
            global::Game.IScopeGraphHost? rootScope = null;
            MonoBehaviour[] components = instance.GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is not global::Game.IScopeGraphHost candidate)
                    continue;

                if (rootScope != null)
                {
                    throw new InvalidOperationException(
                        $"Kernel live boot {role.ToString().ToLowerInvariant()} root host prefab must expose exactly one scope-graph host on its root object.");
                }

                rootScope = candidate;
            }

            if (rootScope == null)
            {
                throw new InvalidOperationException(
                    $"Kernel live boot {role.ToString().ToLowerInvariant()} root host prefab must expose a scope-graph host on its root object.");
            }

            global::Game.LifetimeScopeKind resolvedKind = global::Game.ScopeIdentityMB.PredictKindFromComponent(rootScope.HostComponent, rootScope.Kind);
            if (resolvedKind != expectedKind)
            {
                throw new InvalidOperationException(
                    $"Kernel live boot {role.ToString().ToLowerInvariant()} root host prefab must resolve as LifetimeScopeKind.{expectedKind} on its root object.");
            }

            return new KernelLiveBootPersistentRootInstance(role, rootScope);
        }
    }
}
