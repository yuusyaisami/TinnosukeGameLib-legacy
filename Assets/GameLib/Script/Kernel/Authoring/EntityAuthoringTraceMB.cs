#nullable enable

using System;
using Game.Kernel.IR;
using UnityEngine;

namespace Game.Kernel.Authoring
{
    public abstract class EntityAuthoringTraceMB : MonoBehaviour
    {
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

        public UnityAuthoringSourceKind SourceKind => sourceKind;

        public bool HasSourceLocation => HasTrace(sourceKind, assetGuid, assetPath, localFileId, scenePath, gameObjectPath, componentType, propertyPath);

        public UnitySourceLocation CreateSourceLocation()
        {
            return new UnitySourceLocation(
                sourceKind,
                assetGuid,
                assetPath,
                localFileId,
                scenePath,
                gameObjectPath,
                componentType,
                propertyPath);
        }

        public SourceLocationIR CreatePlanSourceLocation()
        {
            return UnityAuthoringBridge.ToKernelSourceLocation(CreateSourceLocation());
        }

        public bool TryValidateSourceLocation(out string failureReason)
        {
            return TryValidateSourceTrace(sourceKind, assetGuid, assetPath, localFileId, scenePath, gameObjectPath, componentType, propertyPath, out failureReason);
        }

#if UNITY_EDITOR
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

        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            RefreshSourceLocationFromUnityObject();
#endif
            assetGuid = NormalizeOptionalString(assetGuid);
            assetPath = NormalizeOptionalString(assetPath);
            scenePath = NormalizeOptionalString(scenePath);
            gameObjectPath = NormalizeOptionalString(gameObjectPath);
            componentType = NormalizeOptionalString(componentType);
            propertyPath = NormalizeOptionalString(propertyPath);
        }

        protected static string NormalizeOptionalString(string? value)
        {
            if (value == null)
                return string.Empty;

            return value.Trim();
        }

        protected static string NormalizeRequiredString(string? value, string fallback)
        {
            string normalized = NormalizeOptionalString(value);
            return normalized.Length == 0 ? fallback : normalized;
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

            if (traceLocalFileId < 0)
                return false;

            return !string.IsNullOrEmpty(traceAssetGuid)
                || !string.IsNullOrEmpty(traceAssetPath)
                || !string.IsNullOrEmpty(traceScenePath)
                || !string.IsNullOrEmpty(traceGameObjectPath)
                || !string.IsNullOrEmpty(traceComponentType)
                || !string.IsNullOrEmpty(tracePropertyPath);
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
                failureReason = "Authoring source kind must be explicit.";
                return false;
            }

            if (traceLocalFileId < 0)
            {
                failureReason = "Authoring local file ids must be non-negative.";
                return false;
            }

            if (!HasTrace(kind, traceAssetGuid, traceAssetPath, traceLocalFileId, traceScenePath, traceGameObjectPath, traceComponentType, tracePropertyPath))
            {
                failureReason = "Authoring provenance must preserve at least one traceability field.";
                return false;
            }

            try
            {
                _ = new UnitySourceLocation(
                    kind,
                    traceAssetGuid,
                    traceAssetPath,
                    traceLocalFileId,
                    traceScenePath,
                    traceGameObjectPath,
                    traceComponentType,
                    tracePropertyPath);
                failureReason = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                failureReason = exception.Message;
                return false;
            }
        }

        static void EnsureEditMode()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                throw new InvalidOperationException("Entity authoring trace state may only be mutated in edit mode.");
#else
            throw new NotSupportedException("Entity authoring trace state is editor-authoring state and cannot be mutated at runtime.");
#endif
        }

#if UNITY_EDITOR
        void RefreshSourceLocationFromUnityObject()
        {
            if (Application.isPlaying)
                return;

            if (!TryDetectSourceLocation(out UnityAuthoringSourceKind detectedSourceKind, out string detectedAssetGuid, out string detectedAssetPath, out long detectedLocalFileId, out string detectedScenePath, out string detectedGameObjectPath, out string detectedComponentType, out string detectedPropertyPath))
                return;

            sourceKind = detectedSourceKind;
            assetGuid = detectedAssetGuid;
            assetPath = detectedAssetPath;
            localFileId = detectedLocalFileId;
            scenePath = detectedScenePath;
            gameObjectPath = detectedGameObjectPath;
            componentType = detectedComponentType;
            propertyPath = detectedPropertyPath;
        }

        bool TryDetectSourceLocation(
            out UnityAuthoringSourceKind detectedSourceKind,
            out string detectedAssetGuid,
            out string detectedAssetPath,
            out long detectedLocalFileId,
            out string detectedScenePath,
            out string detectedGameObjectPath,
            out string detectedComponentType,
            out string detectedPropertyPath)
        {
            detectedSourceKind = UnityAuthoringSourceKind.Unknown;
            detectedAssetGuid = string.Empty;
            detectedAssetPath = string.Empty;
            detectedLocalFileId = 0L;
            detectedScenePath = string.Empty;
            detectedGameObjectPath = BuildGameObjectPath(transform);
            detectedComponentType = GetType().Name;
            detectedPropertyPath = detectedComponentType;

            string assetPathCandidate = UnityEditor.AssetDatabase.GetAssetPath(this);
            string globalObjectId = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(this).ToString();

            if (!TryParseGlobalObjectId(globalObjectId, out string parsedGuid, out long parsedLocalFileId))
                return false;

            detectedLocalFileId = parsedLocalFileId;
            detectedAssetPath = NormalizeOptionalString(assetPathCandidate);
            detectedAssetGuid = NormalizeOptionalString(parsedGuid);

            bool isPersistent = UnityEditor.EditorUtility.IsPersistent(this);
            if (!isPersistent)
            {
                detectedScenePath = NormalizeOptionalString(gameObject.scene.path);
                detectedSourceKind = UnityEditor.PrefabUtility.IsPartOfPrefabInstance(gameObject)
                    ? UnityAuthoringSourceKind.PrefabInstance
                    : UnityAuthoringSourceKind.SceneObject;

                if (detectedAssetPath.Length == 0)
                    detectedAssetPath = detectedScenePath;

                if (detectedAssetGuid.Length == 0 && detectedAssetPath.Length > 0)
                    detectedAssetGuid = NormalizeOptionalString(UnityEditor.AssetDatabase.AssetPathToGUID(detectedAssetPath));

                return detectedScenePath.Length > 0 || detectedAssetPath.Length > 0;
            }

            detectedSourceKind = UnityEditor.PrefabUtility.IsPartOfVariantPrefab(gameObject)
                ? UnityAuthoringSourceKind.PrefabVariant
                : UnityAuthoringSourceKind.PrefabAsset;

            if (detectedAssetGuid.Length == 0 && detectedAssetPath.Length > 0)
                detectedAssetGuid = NormalizeOptionalString(UnityEditor.AssetDatabase.AssetPathToGUID(detectedAssetPath));

            return detectedAssetGuid.Length > 0 || detectedAssetPath.Length > 0;
        }

        static bool TryParseGlobalObjectId(string globalObjectId, out string guid, out long localFileId)
        {
            guid = string.Empty;
            localFileId = 0L;

            if (string.IsNullOrWhiteSpace(globalObjectId))
                return false;

            string[] parts = globalObjectId.Split('-');
            if (parts.Length < 5)
                return false;

            guid = NormalizeOptionalString(parts[2]);
            return long.TryParse(parts[3], out localFileId);
        }

        static string BuildGameObjectPath(Transform current)
        {
            if (current == null)
                return string.Empty;

            System.Collections.Generic.Stack<string> names = new System.Collections.Generic.Stack<string>();
            for (Transform? node = current; node != null; node = node.parent)
                names.Push(node.name);

            return string.Join("/", names.ToArray());
        }
#endif
    }
}
