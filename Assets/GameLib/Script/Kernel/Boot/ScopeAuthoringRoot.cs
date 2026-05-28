#nullable enable

using System;
using AuthoringUnitySourceLocation = Game.Kernel.Authoring.UnitySourceLocation;
using Game.Kernel.Authoring;
using Game.Kernel.Contributions;
using Game.Kernel.IR;
using UnityEngine;

namespace Game.Kernel.Boot
{
    [DisallowMultipleComponent]
    public sealed class ScopeAuthoringRoot : MonoBehaviour
    {
        [SerializeField]
        int nextScopeAuthoringId = 1;

        [SerializeField]
        int moduleId;

        [SerializeField]
        string moduleName = string.Empty;

        [SerializeField]
        ModuleKind moduleKind = ModuleKind.Unknown;

        [SerializeField]
        int moduleVersion;

        [SerializeField]
        string availabilityProfileId = string.Empty;

        [SerializeField]
        string availabilityBuildTarget = string.Empty;

        [SerializeField]
        string availabilityPlatformFamily = string.Empty;

        [SerializeField]
        ContributionEnvironment availabilityEnvironment = ContributionEnvironment.All;

        [SerializeField]
        UnityAuthoringSourceKind sourceKind = UnityAuthoringSourceKind.Unknown;

        [SerializeField]
        string assetGuid = string.Empty;

        [SerializeField]
        string assetPath = string.Empty;

        [SerializeField]
        long localFileId;

        [SerializeField]
        string scenePath = string.Empty;

        [SerializeField]
        string gameObjectPath = string.Empty;

        [SerializeField]
        string componentType = string.Empty;

        [SerializeField]
        string propertyPath = string.Empty;

        public int NextScopeAuthoringId => nextScopeAuthoringId;

        public bool HasModuleMetadata => moduleId > 0
            && !string.IsNullOrWhiteSpace(moduleName)
            && moduleKind != ModuleKind.Unknown
            && moduleVersion > 0;

        public ModuleId ModuleId => new ModuleId(moduleId);

        public string ModuleName => moduleName;

        public ModuleKind ModuleKind => moduleKind;

        public ModuleVersion ModuleVersion => new ModuleVersion(moduleVersion);

        public ContributionAvailability Availability => new ContributionAvailability(
            NormalizeOptionalString(availabilityProfileId),
            NormalizeOptionalString(availabilityBuildTarget),
            NormalizeOptionalString(availabilityPlatformFamily),
            availabilityEnvironment);

        public UnityAuthoringSourceKind SourceKind => sourceKind;

        public bool HasSourceLocation => HasTrace(sourceKind, assetGuid, assetPath, localFileId, scenePath, gameObjectPath, componentType, propertyPath);

        public ScopeAuthoringId AllocateNextScopeAuthoringId()
        {
            EnsureEditMode();

            if (nextScopeAuthoringId <= 0)
                nextScopeAuthoringId = 1;

            ScopeAuthoringId allocatedId = new ScopeAuthoringId(nextScopeAuthoringId);
            nextScopeAuthoringId = checked(nextScopeAuthoringId + 1);
            return allocatedId;
        }

        public void RegisterExistingScopeAuthoringId(ScopeAuthoringId authoringId)
        {
            EnsureEditMode();

            if (authoringId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(authoringId), authoringId, "Scope authoring ids must be positive.");

            if (authoringId.Value >= nextScopeAuthoringId)
                nextScopeAuthoringId = checked(authoringId.Value + 1);
        }

        public void ResetNextScopeAuthoringId(int seed = 1)
        {
            EnsureEditMode();

            if (seed <= 0)
                throw new ArgumentOutOfRangeException(nameof(seed), seed, "Scope authoring id seeds must be positive.");

            nextScopeAuthoringId = seed;
        }

#if UNITY_EDITOR
        public void SetModuleMetadata(int newModuleId, string newModuleName, ModuleKind newModuleKind, int newModuleVersion)
        {
            EnsureEditMode();

            if (newModuleId <= 0)
                throw new ArgumentOutOfRangeException(nameof(newModuleId), newModuleId, "Module ids must be positive.");

            if (string.IsNullOrWhiteSpace(newModuleName))
                throw new ArgumentException("Module names must be non-empty.", nameof(newModuleName));

            if (newModuleKind == ModuleKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(newModuleKind), newModuleKind, "Module kinds must be explicit.");

            if (newModuleVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(newModuleVersion), newModuleVersion, "Module versions must be positive.");

            moduleId = newModuleId;
            moduleName = newModuleName.Trim();
            moduleKind = newModuleKind;
            moduleVersion = newModuleVersion;
        }

        public void ClearModuleMetadata()
        {
            EnsureEditMode();

            moduleId = 0;
            moduleName = string.Empty;
            moduleKind = ModuleKind.Unknown;
            moduleVersion = 0;
        }

        public void SetContributionAvailability(string? newProfileId, string? newBuildTarget, string? newPlatformFamily, ContributionEnvironment newEnvironment = ContributionEnvironment.All)
        {
            EnsureEditMode();

            availabilityProfileId = NormalizeOptionalString(newProfileId);
            availabilityBuildTarget = NormalizeOptionalString(newBuildTarget);
            availabilityPlatformFamily = NormalizeOptionalString(newPlatformFamily);
            availabilityEnvironment = newEnvironment;
        }

        public void SetSourceLocation(
            UnityAuthoringSourceKind newSourceKind,
            string? newAssetGuid,
            string? newAssetPath,
            long newLocalFileId,
            string? newScenePath,
            string? newGameObjectPath,
            string? newComponentType,
            string? newPropertyPath)
        {
            EnsureEditMode();

            sourceKind = newSourceKind;
            assetGuid = NormalizeOptionalString(newAssetGuid);
            assetPath = NormalizeOptionalString(newAssetPath);
            localFileId = newLocalFileId;
            scenePath = NormalizeOptionalString(newScenePath);
            gameObjectPath = NormalizeOptionalString(newGameObjectPath);
            componentType = NormalizeOptionalString(newComponentType);
            propertyPath = NormalizeOptionalString(newPropertyPath);
        }

        public void ClearSourceLocation()
        {
            EnsureEditMode();

            sourceKind = UnityAuthoringSourceKind.Unknown;
            assetGuid = string.Empty;
            assetPath = string.Empty;
            localFileId = 0;
            scenePath = string.Empty;
            gameObjectPath = string.Empty;
            componentType = string.Empty;
            propertyPath = string.Empty;
        }
#endif

        public AuthoringUnitySourceLocation CreateSourceLocation()
        {
            return new AuthoringUnitySourceLocation(
                sourceKind,
                assetGuid,
                assetPath,
                localFileId,
                scenePath,
                gameObjectPath,
                componentType,
                propertyPath);
        }

        public bool TryValidate(out string failureReason)
        {
            if (!HasModuleMetadata)
            {
                failureReason = "Scope authoring roots must declare explicit module metadata.";
                return false;
            }

            if (!TryValidateSourceTrace(sourceKind, assetGuid, assetPath, localFileId, scenePath, gameObjectPath, componentType, propertyPath, out failureReason))
                return false;

            failureReason = string.Empty;
            return true;
        }

        void OnValidate()
        {
            if (nextScopeAuthoringId <= 0)
                nextScopeAuthoringId = 1;

            moduleName = NormalizeRequiredString(moduleName);
            availabilityProfileId = NormalizeOptionalString(availabilityProfileId);
            availabilityBuildTarget = NormalizeOptionalString(availabilityBuildTarget);
            availabilityPlatformFamily = NormalizeOptionalString(availabilityPlatformFamily);
            assetGuid = NormalizeOptionalString(assetGuid);
            assetPath = NormalizeOptionalString(assetPath);
            scenePath = NormalizeOptionalString(scenePath);
            gameObjectPath = NormalizeOptionalString(gameObjectPath);
            componentType = NormalizeOptionalString(componentType);
            propertyPath = NormalizeOptionalString(propertyPath);
        }

        static void EnsureEditMode()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                throw new InvalidOperationException("Scope authoring root state may only be mutated in edit mode.");
#else
            throw new NotSupportedException("Scope authoring root state is editor-authoring state and cannot be mutated at runtime.");
#endif
        }

        static bool HasTrace(
            UnityAuthoringSourceKind kind,
            string? traceAssetGuid,
            string? traceAssetPath,
            long traceLocalFileId,
            string? traceScenePath,
            string? traceGameObjectPath,
            string? traceComponentType,
            string? tracePropertyPath)
        {
            if (kind == UnityAuthoringSourceKind.Unknown)
                return false;

            if (string.IsNullOrEmpty(traceAssetGuid) || string.IsNullOrEmpty(traceAssetPath))
                return false;

            if (traceLocalFileId == 0)
                return false;

            if (string.IsNullOrEmpty(traceGameObjectPath) || string.IsNullOrEmpty(traceComponentType) || string.IsNullOrEmpty(tracePropertyPath))
                return false;

            return kind == UnityAuthoringSourceKind.SceneObject
                || kind == UnityAuthoringSourceKind.PrefabAsset
                || kind == UnityAuthoringSourceKind.PrefabInstance
                || kind == UnityAuthoringSourceKind.PrefabVariant
                || kind == UnityAuthoringSourceKind.ScriptableObjectAsset
                || kind == UnityAuthoringSourceKind.GeneratedAsset;
        }

        static bool TryValidateSourceTrace(
            UnityAuthoringSourceKind kind,
            string? traceAssetGuid,
            string? traceAssetPath,
            long traceLocalFileId,
            string? traceScenePath,
            string? traceGameObjectPath,
            string? traceComponentType,
            string? tracePropertyPath,
            out string failureReason)
        {
            if (!HasTrace(kind, traceAssetGuid, traceAssetPath, traceLocalFileId, traceScenePath, traceGameObjectPath, traceComponentType, tracePropertyPath))
            {
                failureReason = "Scope authoring roots must provide a valid Unity source trace.";
                return false;
            }

            if (kind != UnityAuthoringSourceKind.SceneObject
                && kind != UnityAuthoringSourceKind.PrefabAsset
                && kind != UnityAuthoringSourceKind.PrefabInstance
                && kind != UnityAuthoringSourceKind.PrefabVariant
                && kind != UnityAuthoringSourceKind.ScriptableObjectAsset
                && kind != UnityAuthoringSourceKind.GeneratedAsset)
            {
                failureReason = "Scope authoring roots must use a supported Unity authoring source kind.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        static string? NormalizeOptionalString(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        static string NormalizeRequiredString(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}