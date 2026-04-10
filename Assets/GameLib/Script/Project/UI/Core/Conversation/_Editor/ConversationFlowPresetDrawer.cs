#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections;
using System.Reflection;
using Game.Conversation;
using UnityEditor;
using UnityEngine;

namespace Game.Conversation.Editor
{
    [CustomPropertyDrawer(typeof(ConversationFlowPreset))]
    public sealed class ConversationFlowPresetDrawer : PropertyDrawer
    {
        const float OpenButtonWidth = 48f;
        const float OpenButtonHeight = 18f;
        const float HorizontalSpacing = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var buttonRect = new Rect(position.x, position.y, OpenButtonWidth, OpenButtonHeight);
            var fieldRect = new Rect(
                position.x + OpenButtonWidth + HorizontalSpacing,
                position.y,
                Mathf.Max(10f, position.width - OpenButtonWidth - HorizontalSpacing),
                position.height);

            using (new EditorGUI.DisabledScope(!TryResolveConversationFlow(property, out _)))
            {
                if (GUI.Button(buttonRect, "Open"))
                {
                    if (TryResolveConversationFlow(property, out var flow) && flow != null)
                        ConversationFlowVisualEditorWindow.Open(flow, property.serializedObject.targetObject, property.propertyPath);
                }
            }

            EditorGUI.PropertyField(fieldRect, property, label, true);
        }

        static bool TryResolveConversationFlow(SerializedProperty property, out ConversationFlowPreset? flow)
        {
            flow = null;
            var value = GetTargetObjectOfProperty(property);
            if (value is not ConversationFlowPreset typed)
                return false;

            flow = typed;
            return true;
        }

        static object? GetTargetObjectOfProperty(SerializedProperty property)
        {
            var path = property.propertyPath.Replace(".Array.data[", "[");
            object? current = property.serializedObject.targetObject;
            var elements = path.Split('.');

            for (var i = 0; i < elements.Length; i++)
            {
                if (current == null)
                    return null;

                var element = elements[i];
                var bracketIndex = element.IndexOf("[", StringComparison.Ordinal);
                if (bracketIndex >= 0)
                {
                    var memberName = element.Substring(0, bracketIndex);
                    var indexText = element.Substring(bracketIndex).Replace("[", string.Empty).Replace("]", string.Empty);
                    if (!int.TryParse(indexText, out var index))
                        return null;

                    current = GetIndexedMemberValue(current, memberName, index);
                    continue;
                }

                current = GetMemberValue(current, element);
            }

            return current;
        }

        static object? GetIndexedMemberValue(object source, string memberName, int index)
        {
            var enumerable = GetMemberValue(source, memberName) as IEnumerable;
            if (enumerable == null)
                return null;

            var currentIndex = 0;
            foreach (var value in enumerable)
            {
                if (currentIndex == index)
                    return value;

                currentIndex++;
            }

            return null;
        }

        static object? GetMemberValue(object source, string memberName)
        {
            var type = source.GetType();
            while (type != null)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var field = type.GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(source);

                var property = type.GetProperty(memberName, flags);
                if (property != null)
                    return property.GetValue(source, null);

                type = type.BaseType;
            }

            return null;
        }
    }
}
#endif
