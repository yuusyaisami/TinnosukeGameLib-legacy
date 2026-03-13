#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    [CreateAssetMenu(menuName = "Game/Flow/Flow Definition")]
    public sealed class FlowDefinitionSO : ScriptableObject
    {
        [Header("Entry")]
        [SerializeField]
        string entryFunctionName = "Main";

        [Header("Functions")]
        [SerializeField, AssetOrInternal]
        FlowFunctionSO[] functions = Array.Empty<FlowFunctionSO>();

        [Header("Compiled")]
        [SerializeField]
        FlowProgramAssetSO? compiledAsset;

        [SerializeField]
        bool useCompiledIfAvailable = true;

        public string EntryFunctionName => entryFunctionName ?? string.Empty;
        public FlowFunctionSO[] Functions => functions;
        public FlowProgramAssetSO? CompiledAsset => compiledAsset;
        public bool UseCompiledIfAvailable => useCompiledIfAvailable;

        public void EnsureIntegrity(UnityEngine.Object? owner = null)
        {
            entryFunctionName ??= string.Empty;
            functions ??= Array.Empty<FlowFunctionSO>();
        }

#if UNTIY_EDITOR
        // コンパイルSOを捜索するエディタ専用メソッド
        public FlowProgramAssetSO? FindCompiledAssetInProject()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:FlowProgramAssetSO");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<FlowProgramAssetSO>(path);
                if (asset != null && asset.SourceDefinition == this)
                {
                    return asset;
                }
            }
            return null;
        }
#endif

#if UNITY_EDITOR
        [Button(ButtonSizes.Medium)]
        void CompileNow()
        {
            if (compiledAsset == null)
                return;

            if (FlowCompiler.TryCompile(this, compiledAsset, out var rep))
            {
                UnityEditor.EditorUtility.SetDirty(compiledAsset);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            else
            {
                UnityEditor.EditorUtility.SetDirty(compiledAsset);
                UnityEditor.AssetDatabase.SaveAssets();
            }
        }
#endif

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureIntegrity(this);
        }
#endif
    }
}
