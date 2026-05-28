#nullable enable

using System;
using System.Collections.Generic;
using Game;
using Game.Commands;
using Game.Common;
using Game.Kernel.Authoring;
using Game.Kernel.Layers.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TinnosukeGameLib.Editor.KernelBoot
{
    public static class SceneAssetMigrationReportService
    {
        const string GameScenePath = "Assets/Scenes/GameScene.unity";
        const string TitleScenePath = "Assets/Scenes/TitleScene.unity";

        [MenuItem("Tools/Kernel/M11.4/Print Asset Migration Report")]
        static void PrintWorkspaceBaselineReportMenu()
        {
            SceneAssetMigrationReport report = BuildWorkspaceBaselineReport();
            AuthoringValidationReport validation = SceneAssetMigrationValidationService.Validate(report);

            Debug.Log($"[SceneAssetMigration] assets={report.AssetRecords.Count}, unexpectedPrefabs={report.UnexpectedPrefabPaths.Count}, unresolved={report.UnresolvedItemCount}");
            for (int index = 0; index < validation.Issues.Count; index++)
                Debug.LogError("[SceneAssetMigration] " + validation.Issues[index]);
        }

        public static SceneAssetMigrationReport BuildWorkspaceBaselineReport()
        {
            return BuildReport(CreateDefaultTargets(), CollectPrefabPaths());
        }

        public static SceneAssetMigrationReport BuildReport(IReadOnlyList<SceneAssetMigrationTarget> targets, IReadOnlyList<string>? unexpectedPrefabPaths = null)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));

            SceneSetup[] sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            List<SceneAssetMigrationAssetRecord> records = new List<SceneAssetMigrationAssetRecord>(targets.Count);
            try
            {
                for (int index = 0; index < targets.Count; index++)
                    records.Add(ScanTarget(targets[index]));
            }
            finally
            {
                if (sceneSetup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
            }

            return new SceneAssetMigrationReport(records, unexpectedPrefabPaths);
        }

        public static SceneAssetMigrationAssetRecord ScanSceneRoots(SceneAssetMigrationTarget target, IReadOnlyList<GameObject> roots)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (roots == null)
                throw new ArgumentNullException(nameof(roots));

            if (target.AssetKind != SceneAssetMigrationAssetKind.Scene)
                throw new ArgumentException("Scene root scanning only supports scene migration targets.", nameof(target));

            List<SceneAssetMigrationAnchorRecord> requiredAnchors = new List<SceneAssetMigrationAnchorRecord>();
            List<SceneAssetMigrationAnchorRecord> legacyAnchors = new List<SceneAssetMigrationAnchorRecord>();
            HashSet<string> satisfiedRequiredTypes = new HashSet<string>(StringComparer.Ordinal);
            bool hasRoots = false;

            for (int index = 0; index < roots.Count; index++)
            {
                GameObject root = roots[index];
                if (root == null)
                    continue;

                hasRoots = true;
                CollectAnchors(target, root.transform, requiredAnchors, legacyAnchors, satisfiedRequiredTypes);
            }

            List<string> missingRequiredAnchorTypeNames = new List<string>();
            IReadOnlyList<string> requiredTypeNames = target.RequiredAnchorTypeNames;
            for (int index = 0; index < requiredTypeNames.Count; index++)
            {
                string requiredTypeName = requiredTypeNames[index];
                if (!satisfiedRequiredTypes.Contains(requiredTypeName))
                    missingRequiredAnchorTypeNames.Add(requiredTypeName);
            }

            return new SceneAssetMigrationAssetRecord(target, requiredAnchors, legacyAnchors, missingRequiredAnchorTypeNames, hasRoots);
        }

        public static IReadOnlyList<SceneAssetMigrationTarget> CreateDefaultTargets()
        {
            return new[]
            {
                CreateSceneTarget(
                    TitleScenePath,
                    typeof(EntityIdentityMB),
                    typeof(SceneKernelHostMB),
                    legacyAnchorTypes: new[]
                    {
                        typeof(CommandRunnerMB),
                    }),
                CreateSceneTarget(
                    GameScenePath,
                    typeof(EntityIdentityMB),
                    typeof(SceneKernelHostMB),
                    typeof(SceneKernelSpawnDeclarationMB),
                    typeof(SceneKernelSpawnHostMB),
                    legacyAnchorTypes: new[]
                    {
                        typeof(RuntimeLifetimeScope),
                        typeof(CommandRunnerMB),
                    }),
            };
        }

        static SceneAssetMigrationTarget CreateSceneTarget(string assetPath, params Type[] requiredAnchorTypes)
        {
            return CreateSceneTarget(assetPath, requiredAnchorTypes, Array.Empty<Type>());
        }

        static SceneAssetMigrationTarget CreateSceneTarget(string assetPath, Type[] requiredAnchorTypes, Type[] legacyAnchorTypes)
        {
            string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            return new SceneAssetMigrationTarget(
                SceneAssetMigrationAssetKind.Scene,
                assetPath,
                string.IsNullOrWhiteSpace(assetGuid) ? null : assetGuid,
                ToTypeNameList(requiredAnchorTypes),
                ToTypeNameList(legacyAnchorTypes));
        }

        static SceneAssetMigrationAssetRecord ScanTarget(SceneAssetMigrationTarget target)
        {
            if (target.AssetKind != SceneAssetMigrationAssetKind.Scene)
                return new SceneAssetMigrationAssetRecord(target, Array.Empty<SceneAssetMigrationAnchorRecord>(), Array.Empty<SceneAssetMigrationAnchorRecord>(), target.RequiredAnchorTypeNames, hasRoots: false);

            Scene scene = EditorSceneManager.OpenScene(target.AssetPath, OpenSceneMode.Additive);
            try
            {
                return ScanSceneRoots(target, scene.GetRootGameObjects());
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static string[] CollectPrefabPaths()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            if (prefabGuids.Length == 0)
                return Array.Empty<string>();

            List<string> prefabPaths = new List<string>(prefabGuids.Length);
            for (int index = 0; index < prefabGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
                if (!string.IsNullOrWhiteSpace(path))
                    prefabPaths.Add(path);
            }

            prefabPaths.Sort(StringComparer.Ordinal);
            return prefabPaths.ToArray();
        }

        static void CollectAnchors(
            SceneAssetMigrationTarget target,
            Transform current,
            List<SceneAssetMigrationAnchorRecord> requiredAnchors,
            List<SceneAssetMigrationAnchorRecord> legacyAnchors,
            HashSet<string> satisfiedRequiredTypes)
        {
            Component[] components = current.GetComponents<Component>();
            string gameObjectPath = BuildGameObjectPath(current);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null)
                    continue;

                Type componentType = component.GetType();
                string fullTypeName = GetComponentTypeName(componentType);
                bool matchedRequired = false;

                IReadOnlyList<string> requiredTypeNames = target.RequiredAnchorTypeNames;
                for (int requiredIndex = 0; requiredIndex < requiredTypeNames.Count; requiredIndex++)
                {
                    string requiredTypeName = requiredTypeNames[requiredIndex];
                    if (!MatchesTypeName(requiredTypeName, componentType))
                        continue;

                    requiredAnchors.Add(new SceneAssetMigrationAnchorRecord(fullTypeName, gameObjectPath, CreateSourceLocation(target, component, gameObjectPath, fullTypeName)));
                    satisfiedRequiredTypes.Add(requiredTypeName);
                    matchedRequired = true;
                    break;
                }

                IReadOnlyList<string> legacyTypeNames = target.LegacyAnchorTypeNames;
                for (int legacyIndex = 0; legacyIndex < legacyTypeNames.Count; legacyIndex++)
                {
                    string legacyTypeName = legacyTypeNames[legacyIndex];
                    if (!MatchesTypeName(legacyTypeName, componentType))
                        continue;

                    legacyAnchors.Add(new SceneAssetMigrationAnchorRecord(fullTypeName, gameObjectPath, CreateSourceLocation(target, component, gameObjectPath, fullTypeName)));
                    break;
                }

                if (matchedRequired)
                    continue;
            }

            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                CollectAnchors(target, current.GetChild(childIndex), requiredAnchors, legacyAnchors, satisfiedRequiredTypes);
        }

        static UnitySourceLocation CreateSourceLocation(SceneAssetMigrationTarget target, Component component, string gameObjectPath, string componentTypeName)
        {
            string? assetGuid = target.AssetGuid;
            long localFileId = 0L;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out string resolvedGuid, out long resolvedLocalFileId))
            {
                if (!string.IsNullOrWhiteSpace(resolvedGuid))
                    assetGuid = resolvedGuid;

                localFileId = resolvedLocalFileId;
            }

            return new UnitySourceLocation(
                UnityAuthoringSourceKind.SceneObject,
                assetGuid,
                target.AssetPath,
                localFileId,
                target.AssetPath,
                gameObjectPath,
                componentTypeName,
                componentTypeName);
        }

        static string BuildGameObjectPath(Transform current)
        {
            if (current == null)
                return string.Empty;

            Stack<string> names = new Stack<string>();
            for (Transform? node = current; node != null; node = node.parent)
                names.Push(node.name);

            return string.Join("/", names.ToArray());
        }

        static string GetComponentTypeName(Type type)
        {
            return string.IsNullOrWhiteSpace(type.FullName) ? type.Name : type.FullName;
        }

        static bool MatchesTypeName(string expectedTypeName, Type actualType)
        {
            if (StringComparer.Ordinal.Equals(expectedTypeName, actualType.Name))
                return true;

            return StringComparer.Ordinal.Equals(expectedTypeName, actualType.FullName);
        }

        static string[] ToTypeNameList(IReadOnlyList<Type> sourceTypes)
        {
            if (sourceTypes == null || sourceTypes.Count == 0)
                return Array.Empty<string>();

            string[] typeNames = new string[sourceTypes.Count];
            for (int index = 0; index < sourceTypes.Count; index++)
            {
                Type sourceType = sourceTypes[index] ?? throw new ArgumentException("Scene asset migration target type lists must not contain null items.", nameof(sourceTypes));
                typeNames[index] = GetComponentTypeName(sourceType);
            }

            return typeNames;
        }
    }
}