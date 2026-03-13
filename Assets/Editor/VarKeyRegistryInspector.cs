#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.VarStoreKeys;

static class VarKeyRegistryInspector
{
    [MenuItem("Tools/VarKeyRegistry/Inspect")]
    static void Inspect()
    {
        Debug.Log("[VarKeyRegistryInspector] Starting inspection...");

        var guids = AssetDatabase.FindAssets("t:VarKeyRegistry");
        Debug.Log($"[VarKeyRegistryInspector] Found {guids.Length} VarKeyRegistry assets via AssetDatabase.FindAssets().");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[VarKeyRegistryInspector] No VarKeyRegistry assets found via AssetDatabase.FindAssets.");
        }

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var reg = AssetDatabase.LoadAssetAtPath<VarKeyRegistry>(path);
            Debug.Log($"[VarKeyRegistryInspector] Asset[{i}] path={path}, registeredKeyCount={(reg?.RegisteredKeyCount ?? -1)}");
            if (reg != null)
            {
                if (reg.TryResolve("emitterPosition", out var varId))
                    Debug.Log($"[VarKeyRegistryInspector] 'emitterPosition' -> varId={varId} in asset {path}");
                else
                    Debug.LogWarning($"[VarKeyRegistryInspector] 'emitterPosition' NOT found in asset {path}");
            }
        }

        var res = Resources.Load<VarKeyRegistry>("VarKeyRegistry");
        Debug.Log($"[VarKeyRegistryInspector] Resources.Load('VarKeyRegistry') => {(res != null ? "FOUND" : "null")}, registeredKeyCount={(res?.RegisteredKeyCount ?? -1)}");
        if (res != null)
        {
            if (res.TryResolve("emitterPosition", out var varId2))
                Debug.Log($"[VarKeyRegistryInspector] 'emitterPosition' -> varId={varId2} in Resources asset");
            else
                Debug.LogWarning("[VarKeyRegistryInspector] 'emitterPosition' NOT found in Resources asset");

            // Print a short list of keys that look similar (helps detect whitespace/case issues)
            try
            {
                var csv = res.ExportToCsv(includeHeader: true);
                var lines = csv?.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
                var matches = lines.Where(l => l.IndexOf("emitterPosition", System.StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (matches.Length > 0)
                {
                    Debug.Log($"[VarKeyRegistryInspector] Found {matches.Length} matching CSV lines for 'emitterPosition' (case-insensitive). Sample: {matches.FirstOrDefault()}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        // Also print what the locator returns in the editor context
        var loc = VarKeyRegistryLocator.GetOrCreate();
        Debug.Log($"[VarKeyRegistryInspector] VarKeyRegistryLocator.GetOrCreate() returned {(loc != null ? "non-null" : "null")}, RegisteredKeyCount={(loc?.RegisteredKeyCount ?? -1)}");
        if (loc != null)
            Debug.Log($"[VarKeyRegistryInspector] Locator.TryResolve('emitterPosition') => {loc.TryResolve("emitterPosition", out var id)} id={id}");

        Debug.Log("[VarKeyRegistryInspector] Inspection complete.");
    }

    [MenuItem("Tools/VarKeyRegistry/Print Runtime Locator Info")]
    static void PrintLocatorInfo()
    {
        var reg = VarKeyRegistryLocator.GetOrCreate();
        Debug.Log($"[VarKeyRegistryInspector] VarKeyRegistryLocator.GetOrCreate() returned {(reg != null ? "non-null" : "null")} (Editor context). RegisteredKeyCount={(reg?.RegisteredKeyCount ?? -1)}");
        if (reg != null)
            Debug.Log($"[VarKeyRegistryInspector] TryResolve('emitterPosition') => {reg.TryResolve("emitterPosition", out var varId)} varId={varId}");
    }
}
#endif