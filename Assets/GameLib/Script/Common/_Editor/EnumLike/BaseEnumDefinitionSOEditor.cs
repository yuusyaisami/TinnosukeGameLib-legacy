#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

namespace Game.EnumLike.Editor
{
    // ================================================================
    // BaseEnumDefinitionSOEditor - Custom Editor for BaseEnumDefinitionSO
    // ================================================================
    //
    // ## Layout
    //
    // [≡] [Index] [Name________________________] [?]
    //             [Description (if toggled)_____]
    //
    // - ≡: Drag handle for reordering
    // - Index: Read-only, auto-assigned from list order
    // - Name: Editable text field (wide)
    // - ?: Toggle button to show/hide description
    //
    // ================================================================

    /// <summary>
    /// Custom editor for BaseEnumDefinitionSO derived classes.
    /// </summary>
    [CustomEditor(typeof(BaseEnumDefinitionSO), true)]
    public class BaseEnumDefinitionSOEditor : UnityEditor.Editor
    {
        SerializedProperty _displayName;
        SerializedProperty _description;
        SerializedProperty _useExplicitValues;
        SerializedProperty _entries;

        ReorderableList _reorderableList;

        // Track which entries have description expanded
        HashSet<int> _expandedDescriptions = new();

        // Layout constants
        const float IndexWidth = 40f;
        const float ToggleButtonWidth = 24f;
        const float RowHeight = 20f;
        const float DescriptionHeight = 40f;

        void OnEnable()
        {
            _displayName = serializedObject.FindProperty("displayName");
            _description = serializedObject.FindProperty("description");
            _useExplicitValues = serializedObject.FindProperty("useExplicitValues");
            _entries = serializedObject.FindProperty("entries");

            if (_entries != null)
            {
                SetupReorderableList();
            }
        }

        void SetupReorderableList()
        {
            _reorderableList = new ReorderableList(serializedObject, _entries, true, true, true, true);

            // Header
            _reorderableList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Entries (drag to reorder)");
            };

            // Element height (dynamic based on expanded state)
            _reorderableList.elementHeightCallback = index =>
            {
                float height = RowHeight + 2f;
                if (_expandedDescriptions.Contains(index))
                {
                    height += DescriptionHeight + 4f;
                }
                return height;
            };

            // Draw element
            _reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                DrawEntry(rect, index);
            };

            // On reorder - shift expanded indices
            _reorderableList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
            {
                var newExpanded = new HashSet<int>();
                foreach (var idx in _expandedDescriptions)
                {
                    if (idx == oldIndex)
                    {
                        newExpanded.Add(newIndex);
                    }
                    else if (oldIndex < newIndex)
                    {
                        // Moving down
                        if (idx > oldIndex && idx <= newIndex)
                            newExpanded.Add(idx - 1);
                        else
                            newExpanded.Add(idx);
                    }
                    else
                    {
                        // Moving up
                        if (idx >= newIndex && idx < oldIndex)
                            newExpanded.Add(idx + 1);
                        else
                            newExpanded.Add(idx);
                    }
                }
                _expandedDescriptions = newExpanded;
            };

            // On remove - shift expanded indices
            _reorderableList.onRemoveCallback = list =>
            {
                int index = list.index;
                ReorderableList.defaultBehaviours.DoRemoveButton(list);

                _expandedDescriptions.Remove(index);
                var newExpanded = new HashSet<int>();
                foreach (var idx in _expandedDescriptions)
                {
                    if (idx > index)
                        newExpanded.Add(idx - 1);
                    else
                        newExpanded.Add(idx);
                }
                _expandedDescriptions = newExpanded;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Header
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_description);
            EditorGUILayout.PropertyField(_useExplicitValues);

            EditorGUILayout.Space(8);

            // Draw reorderable list
            if (_reorderableList != null)
            {
                _reorderableList.DoLayoutList();
            }
            else
            {
                EditorGUILayout.HelpBox("Entries property not found.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();

            // Check for duplicates and show warning in Inspector (not in console)
            var definition = target as BaseEnumDefinitionSO;
            if (definition != null)
            {
                var duplicates = definition.GetDuplicateNames();
                if (duplicates.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    var msg = $"Duplicate entry names: {string.Join(", ", duplicates)}";
                    EditorGUILayout.HelpBox(msg, MessageType.Warning);
                }

                if (definition.UseExplicitValues)
                {
                    var duplicateValues = GetDuplicateValues(definition);
                    if (duplicateValues.Count > 0)
                    {
                        EditorGUILayout.Space(4);
                        var msg = $"Duplicate values found: {string.Join(", ", duplicateValues)}";
                        EditorGUILayout.HelpBox(msg, MessageType.Warning);
                    }
                }
            }
        }

        List<int> GetDuplicateValues(BaseEnumDefinitionSO definition)
        {
            var used = new HashSet<int>();
            var duplicates = new HashSet<int>();
            for (int i = 0; i < definition.Count; i++)
            {
                int val = definition.GetValue(i);
                if (!used.Add(val))
                {
                    duplicates.Add(val);
                }
            }
            return new List<int>(duplicates);
        }

        void DrawEntry(Rect rect, int index)
        {
            var entryProp = _entries.GetArrayElementAtIndex(index);
            var nameProp = entryProp.FindPropertyRelative("name");
            var descProp = entryProp.FindPropertyRelative("description");
            var valProp = entryProp.FindPropertyRelative("value");

            bool isExpanded = _expandedDescriptions.Contains(index);
            bool hasDescription = descProp != null && !string.IsNullOrEmpty(descProp.stringValue);
            bool useExplicit = _useExplicitValues != null && _useExplicitValues.boolValue;

            // Main row
            var rowRect = new Rect(rect.x, rect.y + 1f, rect.width, RowHeight);

            float x = rowRect.x;

            // Index/Value field
            var indexRect = new Rect(x, rowRect.y, IndexWidth, RowHeight);
            if (useExplicit && valProp != null)
            {
                EditorGUI.PropertyField(indexRect, valProp, GUIContent.none);
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.IntField(indexRect, index);
                EditorGUI.EndDisabledGroup();
            }
            x += IndexWidth + 4f;

            // Name field
            float nameWidth = rowRect.width - IndexWidth - ToggleButtonWidth - 12f;
            var nameRect = new Rect(x, rowRect.y, nameWidth, RowHeight);
            if (nameProp != null)
            {
                EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
            }
            x += nameWidth + 4f;

            // Description toggle button
            var buttonRect = new Rect(x, rowRect.y, ToggleButtonWidth, RowHeight);
            var buttonContent = new GUIContent(isExpanded ? "▼" : "○", hasDescription ? "Show/Hide description" : "Add description");

            // Color hint if has description
            var oldColor = GUI.backgroundColor;
            if (hasDescription && !isExpanded)
            {
                GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            }

            if (GUI.Button(buttonRect, buttonContent, EditorStyles.miniButton))
            {
                if (isExpanded)
                    _expandedDescriptions.Remove(index);
                else
                    _expandedDescriptions.Add(index);
            }
            GUI.backgroundColor = oldColor;

            // Description row (if expanded)
            if (isExpanded && descProp != null)
            {
                var descRect = new Rect(
                    rect.x + IndexWidth + 4f,
                    rowRect.y + 5f,
                    rect.width - IndexWidth - IndexWidth - 8f,
                    DescriptionHeight * 1.5f);
                EditorGUI.PropertyField(descRect, descProp, GUIContent.none);
            }
        }
    }
}
#endif
