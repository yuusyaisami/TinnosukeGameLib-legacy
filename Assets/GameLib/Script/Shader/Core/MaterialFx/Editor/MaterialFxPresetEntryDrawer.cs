#if UNITY_EDITOR
using System;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.MaterialFx.Editor
{
    /// <summary>
    /// MaterialFxPresetEntry 用 Odin drawer。
    /// Key から Value.Type を同期した上で、通常の Odin 描画へ流す。
    /// </summary>
    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
    public sealed class MaterialFxPresetEntryDrawer : OdinValueDrawer<MaterialFxPresetEntry>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            SyncValueType();

            var entry = ValueEntry.SmartValue;
            var displayText = string.IsNullOrEmpty(entry.Key)
                ? "<None>"
                : entry.Key.Replace("/", " › ", StringComparison.Ordinal);

            var expanded = Property.State.Expanded;
            var foldoutRect = EditorGUILayout.GetControlRect();
            expanded = EditorGUI.Foldout(foldoutRect, expanded, new GUIContent(displayText), true);
            Property.State.Expanded = expanded;

            if (!expanded)
                return;

            EditorGUI.indentLevel++;
            CallNextDrawer(label: null);
            EditorGUI.indentLevel--;
        }

        void SyncValueType()
        {
            var entry = ValueEntry.SmartValue;
            if (string.IsNullOrEmpty(entry.Key))
                return;

            var registry = MaterialFxPropertyRegistryLocator.GetOrCreate();
            if (registry == null)
                return;

            foreach (var node in registry.Nodes)
            {
                if (node == null || node.IsFolder)
                    continue;

                if (!string.Equals(node.StableKey, entry.Key, StringComparison.Ordinal))
                    continue;

                if (entry.Value.Type == node.ValueType)
                {
                    return;
                }

                entry.Value.Type = node.ValueType;
                ValueEntry.SmartValue = entry;
                return;
            }
        }
    }
}
#endif
