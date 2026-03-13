#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace Game.Common
{
    /// <summary>
    /// Scene name literal source with Build Settings dropdown (Editor only).
    /// </summary>
    [Serializable]
    public sealed class SceneNameSource : IDynamicSource
    {
        [SerializeField]
        [LabelText("Scene")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetBuildSceneOptions))]
#endif
        string sceneName = string.Empty;

        public string SourceTypeName => "SceneName";
        public string GetDebugData => string.IsNullOrWhiteSpace(sceneName) ? "<empty>" : sceneName;

        public DynamicVariant Evaluate(IDynamicContext context)
        {
            _ = context;
            return DynamicVariant.FromString(sceneName ?? string.Empty);
        }

#if UNITY_EDITOR
        static IEnumerable<string> GetBuildSceneOptions()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null)
                yield break;

            for (int i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.path))
                    continue;

                var name = Path.GetFileNameWithoutExtension(scene.path);
                if (!string.IsNullOrEmpty(name))
                    yield return name;
            }
        }
#endif
    }
}
