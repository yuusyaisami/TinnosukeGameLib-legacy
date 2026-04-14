#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Common.Editor
{
    public abstract class ManagedReferenceTypeSelectorDrawerBase<TValue>
        : OdinValueDrawer<TValue>
        where TValue : class
    {
        static readonly Dictionary<string, bool> ExpandedStates = new();

        const float ButtonMinW = 44f;
        const float ButtonMaxW = 240f;
        const float ButtonPad = 10f;

        Type[] _allowedSourceTypes = Array.Empty<Type>();
        GUIContent[] _sourceTypeContents = Array.Empty<GUIContent>();

        protected override void Initialize()
        {
            base.Initialize();
            CacheAllowedSourceTypes();
        }

        protected virtual Type PickerBaseType => typeof(TValue);
        protected virtual Type? DefaultType => null;
        protected virtual string EmptyHelpMessage => "Source is null. Select a Type.";

        protected abstract string BuildSummary(TValue? value, InspectorProperty property);

        protected virtual string GetTypeLabel(TValue? value)
            => value != null ? value.GetType().Name : "<None>";

        protected virtual string GetTooltip(TValue? value)
            => value?.GetType().FullName ?? "None";

        protected override void DrawPropertyLayout(GUIContent label)
        {
            try
            {
                var root = Property;
                var propertyPath = root.Path;
                var target = root.Tree?.UnitySerializedObject?.targetObject;
                var key = $"{target?.GetInstanceID() ?? 0}:{propertyPath}";

                if (!ExpandedStates.TryGetValue(key, out var expanded))
                    ExpandedStates[key] = expanded = false;

                var currentValue = GetCurrentValue(propertyPath);
                if (currentValue == null && TryCreateDefaultValue(out var defaultValue) && defaultValue != null)
                {
                    if (TryAssignValue(propertyPath, defaultValue))
                        currentValue = defaultValue;
                }

                var currentType = currentValue?.GetType();
                var currentIndex = GetCurrentSourceIndex(currentType);

                EditorGUILayout.BeginHorizontal();
                {
                    if (label != null && !string.IsNullOrEmpty(label.text))
                        GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

                    EditorGUI.BeginChangeCheck();
                    var popupIndex = currentIndex >= 0 ? currentIndex : (_allowedSourceTypes.Length > 0 ? 0 : -1);
                    var nextIndex = EditorGUILayout.Popup(popupIndex, _sourceTypeContents, GUILayout.MinWidth(120f));
                    if (EditorGUI.EndChangeCheck() && nextIndex >= 0 && nextIndex < _allowedSourceTypes.Length && nextIndex != currentIndex)
                    {
                        var newType = _allowedSourceTypes[nextIndex];
                        var newValue = CreateInstance(newType);
                        if (newValue != null && TryAssignValue(propertyPath, newValue))
                            currentValue = newValue;
                    }

                    var buttonText = BuildButtonLabel(currentValue, root);
                    var btnContent = new GUIContent(buttonText)
                    {
                        tooltip = GetTooltip(currentValue)
                    };
                    var btnW = Mathf.Clamp(
                        EditorStyles.miniButton.CalcSize(btnContent).x + ButtonPad,
                        ButtonMinW, ButtonMaxW);

                    var oldBg = GUI.backgroundColor;
                    if (expanded)
                        GUI.backgroundColor = new Color(0.85f, 0.95f, 1.0f);

                    if (GUILayout.Button(btnContent, EditorStyles.miniButton, GUILayout.Width(btnW)))
                        ExpandedStates[key] = expanded = !expanded;

                    GUI.backgroundColor = oldBg;
                }
                EditorGUILayout.EndHorizontal();

                if (!expanded)
                    return;

                EditorGUI.indentLevel++;
                if (currentValue == null)
                {
                    EditorGUILayout.HelpBox(EmptyHelpMessage, MessageType.Info);
                }
                else
                {
                    root.State.Expanded = true;
                    var snapshot = new List<InspectorProperty>(root.Children.Count);
                    for (var i = 0; i < root.Children.Count; i++)
                        snapshot.Add(root.Children[i]);

                    for (var i = 0; i < snapshot.Count; i++)
                    {
                        var child = snapshot[i];
                        if (child == null)
                            continue;
                        if (!child.State.Visible)
                            continue;
                        if (!string.IsNullOrEmpty(child.Name) && child.Name[0] == '#')
                            child.State.Expanded = true;

                        DrawChildSafely(child);
                    }
                }
                EditorGUI.indentLevel--;
            }
            catch (Exception ex)
            {
                if (ex is ExitGUIException)
                    throw;

                Debug.LogWarning($"[ManagedReferenceTypeSelectorDrawer<{typeof(TValue).Name}>] Fallback to default drawer due to error: {ex.Message}");
                CallNextDrawer(label);
            }
        }

        void CacheAllowedSourceTypes()
        {
            var items = ManagedReferenceTypePickerUtility.GetTypeItems(PickerBaseType);
            var types = new Type[items.Count];
            var contents = new GUIContent[items.Count];

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                types[i] = item.Type;
                contents[i] = new GUIContent(item.Label);
            }

            _allowedSourceTypes = types;
            _sourceTypeContents = contents;
        }

        TValue? GetCurrentValue(string propertyPath)
        {
            var unityProperty = GetFreshUnityProperty(propertyPath);
            if (unityProperty != null && unityProperty.propertyType == SerializedPropertyType.ManagedReference)
                return unityProperty.managedReferenceValue as TValue;

            return ValueEntry.WeakSmartValue as TValue;
        }

        bool TryCreateDefaultValue(out TValue? value)
        {
            value = null;

            var candidateType = DefaultType ?? (_allowedSourceTypes.Length > 0 ? _allowedSourceTypes[0] : null);
            if (candidateType == null)
                return false;

            value = CreateInstance(candidateType);
            return value != null;
        }

        TValue? CreateInstance(Type concreteType)
        {
            if (!ManagedReferenceTypePickerUtility.TryCreateInstance(concreteType, out var instance))
                return null;

            return instance as TValue;
        }

        bool TryAssignValue(string propertyPath, TValue? value)
        {
            var unityProperty = GetFreshUnityProperty(propertyPath);
            if (unityProperty == null)
                return false;

            var targetObject = Property.Tree?.UnitySerializedObject?.targetObject;
            if (targetObject != null)
                Undo.RecordObject(targetObject, "Change Managed Reference Type");

            unityProperty.managedReferenceValue = value;

            var serializedObject = Property.Tree?.UnitySerializedObject;
            if (serializedObject != null)
            {
                serializedObject.ApplyModifiedProperties();
                serializedObject.UpdateIfRequiredOrScript();
            }

            Property.Tree?.UpdateTree();
            GUI.changed = true;

            if (targetObject != null)
                EditorUtility.SetDirty(targetObject);

            return true;
        }

        void DrawChildSafely(InspectorProperty child)
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

        int GetCurrentSourceIndex(Type? currentType)
        {
            if (currentType == null)
                return -1;

            for (var i = 0; i < _allowedSourceTypes.Length; i++)
            {
                if (_allowedSourceTypes[i] == currentType)
                    return i;
            }

            return -1;
        }

        string BuildButtonLabel(TValue? value, InspectorProperty property)
        {
            var summary = value != null ? BuildSummary(value, property) : string.Empty;

            if (!string.IsNullOrEmpty(summary))
                return TrimForButton(summary, 48);

            return value != null ? value.GetType().Name : "None";
        }

        static string TrimForButton(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var singleLine = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (singleLine.Length <= maxLength)
                return singleLine;

            return singleLine.Substring(0, maxLength - 3) + "...";
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
    }
}
#endif