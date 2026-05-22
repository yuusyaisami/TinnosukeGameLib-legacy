#nullable enable

using System;
using System.Collections.Generic;
using Game.Kernel.Authoring;
using Game.Kernel.IR;
using UnityEngine;
using AuthoringUnitySourceLocation = Game.Kernel.Authoring.UnitySourceLocation;

namespace Game.Kernel.Boot
{
    [DisallowMultipleComponent]
    public sealed class ScopeAuthoringLink : MonoBehaviour
    {
        [SerializeField]
        int scopeAuthoringId;

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

        [SerializeField]
        UnityAuthoringSourceKind baseSourceKind = UnityAuthoringSourceKind.Unknown;

        [SerializeField]
        string baseAssetGuid = string.Empty;

        [SerializeField]
        string baseAssetPath = string.Empty;

        [SerializeField]
        long baseLocalFileId;

        [SerializeField]
        string baseScenePath = string.Empty;

        [SerializeField]
        string baseGameObjectPath = string.Empty;

        [SerializeField]
        string baseComponentType = string.Empty;

        [SerializeField]
        string basePropertyPath = string.Empty;

        public static AuthoringComponentKind ComponentKind => AuthoringComponentKind.Declaration;

        public bool HasScopeAuthoringId => scopeAuthoringId > 0;

        public ScopeAuthoringId ScopeAuthoringId => new ScopeAuthoringId(scopeAuthoringId);

        public UnityAuthoringSourceKind SourceKind => sourceKind;

        public UnityAuthoringSourceKind BaseSourceKind => baseSourceKind;

        public bool HasSourceLocation => HasTrace(sourceKind, assetGuid, assetPath, localFileId, scenePath, gameObjectPath, componentType, propertyPath);

        public bool HasBaseSourceLocation => HasTrace(baseSourceKind, baseAssetGuid, baseAssetPath, baseLocalFileId, baseScenePath, baseGameObjectPath, baseComponentType, basePropertyPath);

#if UNITY_EDITOR
        public void SetAuthoringId(ScopeAuthoringId authoringId)
        {
            EnsureEditMode();

            if (authoringId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(authoringId), authoringId, "Scope authoring ids must be positive.");

            scopeAuthoringId = authoringId.Value;
        }

        public void ClearAuthoringId()
        {
            EnsureEditMode();

            scopeAuthoringId = 0;
        }

        public void SetSourceLocation(
            UnityAuthoringSourceKind kind,
            string? newAssetGuid,
            string? newAssetPath,
            long newLocalFileId,
            string? newScenePath,
            string? newGameObjectPath,
            string? newComponentType,
            string? newPropertyPath)
        {
            EnsureEditMode();

            sourceKind = kind;
            assetGuid = NormalizeOptionalString(newAssetGuid);
            assetPath = NormalizeOptionalString(newAssetPath);
            localFileId = newLocalFileId;
            scenePath = NormalizeOptionalString(newScenePath);
            gameObjectPath = NormalizeOptionalString(newGameObjectPath);
            componentType = NormalizeOptionalString(newComponentType);
            propertyPath = NormalizeOptionalString(newPropertyPath);
        }

        public void SetBaseSourceLocation(
            UnityAuthoringSourceKind kind,
            string? newAssetGuid,
            string? newAssetPath,
            long newLocalFileId,
            string? newScenePath,
            string? newGameObjectPath,
            string? newComponentType,
            string? newPropertyPath)
        {
            EnsureEditMode();

            baseSourceKind = kind;
            baseAssetGuid = NormalizeOptionalString(newAssetGuid);
            baseAssetPath = NormalizeOptionalString(newAssetPath);
            baseLocalFileId = newLocalFileId;
            baseScenePath = NormalizeOptionalString(newScenePath);
            baseGameObjectPath = NormalizeOptionalString(newGameObjectPath);
            baseComponentType = NormalizeOptionalString(newComponentType);
            basePropertyPath = NormalizeOptionalString(newPropertyPath);
        }

        public void ClearBaseSourceLocation()
        {
            EnsureEditMode();

            baseSourceKind = UnityAuthoringSourceKind.Unknown;
            baseAssetGuid = string.Empty;
            baseAssetPath = string.Empty;
            baseLocalFileId = 0;
            baseScenePath = string.Empty;
            baseGameObjectPath = string.Empty;
            baseComponentType = string.Empty;
            basePropertyPath = string.Empty;
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

        public bool TryGetBaseSourceLocation(out AuthoringUnitySourceLocation sourceLocation)
        {
            if (!HasBaseSourceLocation)
            {
                sourceLocation = default;
                return false;
            }

            sourceLocation = new AuthoringUnitySourceLocation(
                baseSourceKind,
                baseAssetGuid,
                baseAssetPath,
                baseLocalFileId,
                baseScenePath,
                baseGameObjectPath,
                baseComponentType,
                basePropertyPath);
            return true;
        }

        public bool TryValidate(out string failureReason)
        {
            if (!HasScopeAuthoringId)
            {
                failureReason = "ScopeAuthoringId must be assigned through explicit authoring flow.";
                return false;
            }

            if (!TryValidateSourceTrace(sourceKind, assetGuid, assetPath, localFileId, scenePath, gameObjectPath, componentType, propertyPath, out failureReason))
                return false;

            if (RequiresBaseTrace(sourceKind))
            {
                if (!HasBaseSourceLocation)
                {
                    failureReason = "Prefab instance and variant scope authoring must preserve base source trace.";
                    return false;
                }

                if (!TryValidateSourceTrace(baseSourceKind, baseAssetGuid, baseAssetPath, baseLocalFileId, baseScenePath, baseGameObjectPath, baseComponentType, basePropertyPath, out failureReason))
                    return false;
            }
            else if (HasBaseSourceLocation)
            {
                if (!TryValidateSourceTrace(baseSourceKind, baseAssetGuid, baseAssetPath, baseLocalFileId, baseScenePath, baseGameObjectPath, baseComponentType, basePropertyPath, out failureReason))
                    return false;
            }

            failureReason = string.Empty;
            return true;
        }

        public static bool TryFindDuplicateIds(IReadOnlyList<ScopeAuthoringLink> links, out IReadOnlyList<ScopeAuthoringId> duplicateIds)
        {
            if (links == null)
                throw new ArgumentNullException(nameof(links));

            List<ScopeAuthoringId> duplicates = new List<ScopeAuthoringId>();
            HashSet<int> seen = new HashSet<int>();
            HashSet<int> recorded = new HashSet<int>();

            for (int index = 0; index < links.Count; index++)
            {
                ScopeAuthoringLink link = links[index];
                if (link == null || link.scopeAuthoringId <= 0)
                    continue;

                if (!seen.Add(link.scopeAuthoringId) && recorded.Add(link.scopeAuthoringId))
                    duplicates.Add(new ScopeAuthoringId(link.scopeAuthoringId));
            }

            duplicateIds = duplicates;
            return duplicates.Count == 0;
        }

        void OnValidate()
        {
            assetGuid = NormalizeOptionalString(assetGuid);
            assetPath = NormalizeOptionalString(assetPath);
            scenePath = NormalizeOptionalString(scenePath);
            gameObjectPath = NormalizeOptionalString(gameObjectPath);
            componentType = NormalizeOptionalString(componentType);
            propertyPath = NormalizeOptionalString(propertyPath);
            baseAssetGuid = NormalizeOptionalString(baseAssetGuid);
            baseAssetPath = NormalizeOptionalString(baseAssetPath);
            baseScenePath = NormalizeOptionalString(baseScenePath);
            baseGameObjectPath = NormalizeOptionalString(baseGameObjectPath);
            baseComponentType = NormalizeOptionalString(baseComponentType);
            basePropertyPath = NormalizeOptionalString(basePropertyPath);
        }

        static bool RequiresBaseTrace(UnityAuthoringSourceKind kind)
        {
            return kind == UnityAuthoringSourceKind.PrefabInstance || kind == UnityAuthoringSourceKind.PrefabVariant;
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
            return kind != UnityAuthoringSourceKind.Unknown
                && (!string.IsNullOrEmpty(traceAssetGuid)
                    || !string.IsNullOrEmpty(traceAssetPath)
                    || traceLocalFileId > 0
                    || !string.IsNullOrEmpty(traceScenePath)
                    || !string.IsNullOrEmpty(traceGameObjectPath)
                    || !string.IsNullOrEmpty(traceComponentType)
                    || !string.IsNullOrEmpty(tracePropertyPath));
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
            if (kind == UnityAuthoringSourceKind.Unknown)
            {
                failureReason = "Unity authoring source kind must be specified.";
                return false;
            }

            if (traceLocalFileId < 0)
            {
                failureReason = "Unity authoring source locations must use a non-negative local file id.";
                return false;
            }

            switch (kind)
            {
                case UnityAuthoringSourceKind.SceneObject:
                    if (string.IsNullOrEmpty(traceScenePath) || string.IsNullOrEmpty(traceGameObjectPath) || string.IsNullOrEmpty(traceComponentType))
                    {
                        failureReason = "Scene object authoring sources must provide scene path, game object path, and component type.";
                        return false;
                    }

                    break;
                case UnityAuthoringSourceKind.PrefabInstance:
                    if (string.IsNullOrEmpty(traceScenePath) || string.IsNullOrEmpty(traceGameObjectPath))
                    {
                        failureReason = "Prefab instance authoring sources must provide scene path and game object path.";
                        return false;
                    }

                    if (!HasAssetTrace(traceAssetGuid, traceAssetPath))
                    {
                        failureReason = "Prefab instance authoring sources must retain prefab traceability.";
                        return false;
                    }

                    break;
                case UnityAuthoringSourceKind.PrefabAsset:
                case UnityAuthoringSourceKind.PrefabVariant:
                case UnityAuthoringSourceKind.ScriptableObjectAsset:
                    if (!HasAssetTrace(traceAssetGuid, traceAssetPath))
                    {
                        failureReason = "Asset-backed authoring sources must provide asset GUID or asset path.";
                        return false;
                    }

                    break;
                case UnityAuthoringSourceKind.GeneratedAsset:
                    if (!HasAny(traceAssetGuid, traceAssetPath, traceComponentType))
                    {
                        failureReason = "Generated asset sources must provide asset or generator traceability.";
                        return false;
                    }

                    break;
                case UnityAuthoringSourceKind.CodeDefinedModule:
                    if (!HasAny(traceComponentType, traceAssetPath))
                    {
                        failureReason = "Code-defined module sources must provide a code or asset trace.";
                        return false;
                    }

                    break;
                case UnityAuthoringSourceKind.LegacyBridge:
                    if (!HasAny(traceAssetGuid, traceAssetPath, traceScenePath, traceComponentType))
                    {
                        failureReason = "Legacy bridge sources must preserve at least one traceability field.";
                        return false;
                    }

                    break;
                default:
                    failureReason = "Unsupported Unity authoring source kind.";
                    return false;
            }

            if (!HasTrace(kind, traceAssetGuid, traceAssetPath, traceLocalFileId, traceScenePath, traceGameObjectPath, traceComponentType, tracePropertyPath))
            {
                failureReason = "Unity authoring source locations must provide at least one traceability field.";
                return false;
            }

            failureReason = string.Empty;
            return true;
        }

        static bool HasAssetTrace(string? traceAssetGuid, string? traceAssetPath)
        {
            return !string.IsNullOrEmpty(traceAssetGuid) || !string.IsNullOrEmpty(traceAssetPath);
        }

        static bool HasAny(string? first, string? second)
        {
            return !string.IsNullOrEmpty(first) || !string.IsNullOrEmpty(second);
        }

        static bool HasAny(string? first, string? second, string? third)
        {
            return !string.IsNullOrEmpty(first) || !string.IsNullOrEmpty(second) || !string.IsNullOrEmpty(third);
        }

        static bool HasAny(string? first, string? second, string? third, string? fourth)
        {
            return !string.IsNullOrEmpty(first)
                || !string.IsNullOrEmpty(second)
                || !string.IsNullOrEmpty(third)
                || !string.IsNullOrEmpty(fourth);
        }

        static string? NormalizeOptionalString(string? value)
        {
            if (value == null)
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

#if UNITY_EDITOR
        static void EnsureEditMode()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                throw new InvalidOperationException("Scope authoring identity may only be mutated in edit mode through explicit authoring flow.");
#else
            throw new NotSupportedException("Scope authoring identity is editor-authoring state and cannot be mutated at runtime.");
#endif
        }
#endif
    }
}