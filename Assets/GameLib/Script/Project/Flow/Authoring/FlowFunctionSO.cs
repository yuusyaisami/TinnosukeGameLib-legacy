#nullable enable

using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Flow
{
    [CreateAssetMenu(menuName = "Game/Flow/Flow Function")]
    public sealed class FlowFunctionSO : ScriptableObject
    {
        [SerializeField]
        string functionName = "";

        [SerializeField]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [SerializeReference]
        FlowStatement[] statements = Array.Empty<FlowStatement>();

        public string Name => functionName ?? string.Empty;
        public FlowStatement[] Statements => statements;

        public void EnsureIntegrity(UnityEngine.Object? owner = null)
        {
            functionName ??= string.Empty;
            statements ??= Array.Empty<FlowStatement>();
            for (int i = 0; i < statements.Length; i++)
                statements[i]?.EnsureIntegrity();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureIntegrity(this);

            // functionNameが設定されている場合、アセット名をそれに合わせる（空文字/空白は無視）
            if (!string.IsNullOrWhiteSpace(functionName))
            {
                var desired = functionName.Trim();
                // ファイル名として使えない文字を除去
                var invalid = System.IO.Path.GetInvalidFileNameChars();
                var sb = new System.Text.StringBuilder();
                foreach (var c in desired)
                {
                    if (Array.IndexOf(invalid, c) < 0)
                        sb.Append(c);
                }
                desired = sb.ToString();

                if (!string.IsNullOrEmpty(desired))
                {
                    var path = UnityEditor.AssetDatabase.GetAssetPath(this);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var current = System.IO.Path.GetFileNameWithoutExtension(path);
                        if (!string.Equals(current, desired, StringComparison.Ordinal))
                        {
                            UnityEditor.AssetDatabase.RenameAsset(path, desired);
                            UnityEditor.AssetDatabase.SaveAssets();
                        }
                    }

                    if (this.name != desired)
                    {
                        this.name = desired;
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
                }
            }
        }
#endif
    }
}
