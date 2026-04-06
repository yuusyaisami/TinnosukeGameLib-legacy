#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using Game.Common.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Profile.Editor
{
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class ProfileValueBindingTypeSelectorDrawer : OdinValueDrawer<IProfileValueBinding>
    {
        static void DrawChildSafely(InspectorProperty child)
        {
            if (child == null)
                return;

            var oldLabelWidth = EditorGUIUtility.labelWidth;
            var oldIndent = EditorGUI.indentLevel;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldContentColor = GUI.contentColor;
            var oldBackgroundColor = GUI.backgroundColor;
            try
            {
                child.Draw();
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
                EditorGUI.indentLevel = oldIndent;
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContentColor;
                GUI.backgroundColor = oldBackgroundColor;
            }
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var pickerItems = ManagedReferenceTypePickerUtility.GetTypeItems(typeof(IProfileValueBinding));

            var propertyPath = Property.Path;
            var current = GetCurrentBinding(propertyPath);
            var typeLabel = current != null ? current.GetType().Name : "<None>";
            var summary = current != null ? ProfileBindingInspectorLabelUtility.Build(current) : "<None>";

            var rowRect = EditorGUILayout.GetControlRect();
            var contentRect = EditorGUI.IndentedRect(rowRect);
            var foldoutRect = new Rect(contentRect.x, contentRect.y, 14f, contentRect.height);
            var fieldRect = new Rect(foldoutRect.xMax + 4f, contentRect.y, Mathf.Max(0f, contentRect.xMax - (foldoutRect.xMax + 4f)), contentRect.height);

            Property.State.Expanded = EditorGUI.Foldout(foldoutRect, Property.State.Expanded, GUIContent.none, true);

            var popupSize = EditorStyles.popup.CalcSize(new GUIContent(typeLabel));
            var popupWidth = Mathf.Clamp(popupSize.x + 16f, 72f, 220f);
            var popupRect = new Rect(fieldRect.x, fieldRect.y, Mathf.Min(popupWidth, fieldRect.width), fieldRect.height);
            ManagedReferenceCompactPickerUI.DrawTypeDropdownButton(
                popupRect,
                typeLabel,
                current?.GetType(),
                pickerItems,
                selectedType => ScheduleSelection(selectedType));

            var summaryX = popupRect.xMax + 4f;
            var summaryWidth = Mathf.Max(0f, fieldRect.xMax - summaryX);
            if (summaryWidth > 8f)
            {
                var summaryContent = new GUIContent(Trim(summary, 56)) { tooltip = summary };
                var buttonWidth = Mathf.Min(EditorStyles.miniButton.CalcSize(summaryContent).x + 8f, summaryWidth);
                var summaryRect = new Rect(summaryX, fieldRect.y, buttonWidth, fieldRect.height);
                if (GUI.Button(summaryRect, summaryContent, EditorStyles.miniButton))
                    Property.State.Expanded = !Property.State.Expanded;
            }

            if (current == null || !Property.State.Expanded)
                return;

            var snapshot = new List<InspectorProperty>(Property.Children.Count);
            for (var i = 0; i < Property.Children.Count; i++)
                snapshot.Add(Property.Children[i]);

            EditorGUI.indentLevel++;
            for (var i = 0; i < snapshot.Count; i++)
            {
                var child = snapshot[i];
                if (child == null)
                    continue;
                if (!child.State.Visible)
                    continue;
                if (!string.IsNullOrEmpty(child.Name) && child.Name[0] == '$')
                    continue;
                if (!string.IsNullOrEmpty(child.Name) && child.Name[0] == '#')
                    child.State.Expanded = true;

                DrawChildSafely(child);
            }
            EditorGUI.indentLevel--;
        }

        void ScheduleSelection(Type? selectedType)
        {
            var propertyPath = Property.Path;

            // The popup closes during the same GUI event that selects the type.
            // Applying the managed-reference replacement through delayCall avoids
            // Odin/Unity state from being read and written in the middle of that event.
            EditorApplication.delayCall += () =>
            {
                ApplySelection(propertyPath, selectedType);
            };
        }

        void ApplySelection(string propertyPath, Type? selectedType)
        {
            IProfileValueBinding? binding = null;

            if (selectedType != null)
            {
                if (!ManagedReferenceTypePickerUtility.TryCreateInstance(selectedType, out var instance)
                    || instance is not IProfileValueBinding created)
                {
                    Debug.LogWarning($"[ProfileValueBindingTypeSelectorDrawer] create failed path={Property.Path} selected={DescribeType(selectedType)}");
                    return;
                }

                binding = created;
            }

            var targetObject = Property.Tree?.UnitySerializedObject?.targetObject;
            var tree = Property.Tree;
            var serializedObject = tree?.UnitySerializedObject;
            var unityProperty = GetFreshUnityProperty(propertyPath);

            if (unityProperty == null)
            {
                return;
            }

            if (targetObject != null)
                Undo.RecordObject(targetObject, "Change Profile Binding Type");

            unityProperty.managedReferenceValue = binding;

            if (serializedObject != null)
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.UpdateIfRequiredOrScript();
            }

            if (tree != null)
                tree.UpdateTree();

            Property.State.Expanded = binding != null;
            GUI.changed = true;

            if (targetObject != null)
                EditorUtility.SetDirty(targetObject);

            EditorApplication.delayCall += () => UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        IProfileValueBinding? GetCurrentBinding(string propertyPath)
        {
            var unityProperty = GetFreshUnityProperty(propertyPath);
            if (unityProperty != null && unityProperty.propertyType == SerializedPropertyType.ManagedReference)
                return unityProperty.managedReferenceValue as IProfileValueBinding;

            return ValueEntry.WeakSmartValue as IProfileValueBinding;
        }

        SerializedProperty? GetFreshUnityProperty(string propertyPath)
        {
            var tree = Property.Tree;
            var serializedObject = tree?.UnitySerializedObject;
            if (tree == null || serializedObject == null)
                return null;

            var mappedProperty = tree.GetUnityPropertyForPath(propertyPath, out _);
            if (mappedProperty == null)
                return null;

            return serializedObject.FindProperty(mappedProperty.propertyPath) ?? mappedProperty;
        }

        static string DescribeType(object? value)
        {
            return value?.GetType().FullName ?? "<None>";
        }

        static string DescribeType(Type? value)
        {
            return value?.FullName ?? "<None>";
        }

        static string Trim(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var singleLine = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (singleLine.Length <= maxLength)
                return singleLine;

            return singleLine.Substring(0, maxLength - 3) + "...";
        }
    }
}
#endif
