#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Scalar.Editor
{
    /// <summary>
    /// ScalarKey のカスタムドロワー。
    /// ScalarKeyRegistry からドロップダウンで選択可能。
    /// </summary>
    [CustomPropertyDrawer(typeof(ScalarKey))]
    public class ScalarKeyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var nameProp = property.FindPropertyRelative("Name");
            var idProp = property.FindPropertyRelative("Id");

            var rect = EditorGUI.PrefixLabel(position, label);
            if (rect.height < EditorGUIUtility.singleLineHeight)
                rect.height = EditorGUIUtility.singleLineHeight;

            string current = nameProp.stringValue ?? string.Empty;
            var display = string.IsNullOrEmpty(current)
                ? "<None>"
                : current.Replace(".", " › ");

            if (GUI.Button(rect, display, EditorStyles.popup))
            {
                ShowDropdown(rect, nameProp, idProp, current);
            }

            EditorGUI.EndProperty();
        }

        void ShowDropdown(Rect rect, SerializedProperty nameProp, SerializedProperty idProp, string currentPath)
        {
            var registry = ScalarKeyRegistryLocator.GetOrCreate();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("<None>"), string.IsNullOrEmpty(currentPath), () =>
            {
                nameProp.stringValue = string.Empty;
                idProp.intValue = 0;
                nameProp.serializedObject.ApplyModifiedProperties();
            });

            menu.AddSeparator(string.Empty);

            // Registry からノードを取得してメニューを構築
            AddMenuItemsFromRegistry(menu, registry, nameProp, idProp, currentPath, visited);

            menu.DropDown(rect);
        }

        void AddMenuItemsFromRegistry(
            GenericMenu menu,
            ScalarKeyRegistry registry,
            SerializedProperty nameProp,
            SerializedProperty idProp,
            string currentPath,
            HashSet<string> visited)
        {
            if (registry == null) return;

            // すべての葉ノードを取得してメニューに追加
            foreach (var node in registry.Nodes)
            {
                if (node == null || node.IsFolder) continue;

                var keyString = registry.GetKeyString(node);
                if (string.IsNullOrEmpty(keyString)) continue;

                // メニューパスに変換（ドット→スラッシュ）
                var menuPath = keyString.Replace(".", "/");

                if (visited.Add(menuPath))
                {
                    var isOn = string.Equals(currentPath, keyString, StringComparison.Ordinal);
                    var capturedKey = keyString;
                    menu.AddItem(new GUIContent(menuPath), isOn, () =>
                    {
                        nameProp.stringValue = capturedKey;
                        idProp.intValue = Animator.StringToHash(capturedKey);
                        nameProp.serializedObject.ApplyModifiedProperties();
                    });
                }
            }
        }
    }
}
#endif
