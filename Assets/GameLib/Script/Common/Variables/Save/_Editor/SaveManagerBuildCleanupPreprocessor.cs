#if UNITY_EDITOR
#nullable enable
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Save
{
    public sealed class SaveManagerBuildCleanupPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!ShouldDeleteBeforeBuild())
                return;

            if (SaveManagerDebugView.TryDeletePersistedDataFromEditor(out var status))
            {
                Debug.Log($"[SaveManagerBuildCleanupPreprocessor] Deleted persisted save data before build. Target={report.summary.platform}");
                return;
            }

            Debug.LogWarning($"[SaveManagerBuildCleanupPreprocessor] Failed to delete persisted save data before build: {status}");
        }

        static bool ShouldDeleteBeforeBuild()
        {
            if (HasEnabledSaveManagerInOpenScenes())
                return true;

            return HasEnabledSaveManagerInPrefabAssets();
        }

        static bool HasEnabledSaveManagerInOpenScenes()
        {
            var instances = Resources.FindObjectsOfTypeAll<SaveManagerMB>();
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance == null || EditorUtility.IsPersistent(instance))
                    continue;

                if (instance.DeleteAllSaveDataBeforeBuild)
                    return true;
            }

            return false;
        }

        static bool HasEnabledSaveManagerInPrefabAssets()
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                var saveManager = prefab.GetComponentInChildren<SaveManagerMB>(true);
                if (saveManager != null && saveManager.DeleteAllSaveDataBeforeBuild)
                    return true;
            }

            return false;
        }
    }
}
#endif
