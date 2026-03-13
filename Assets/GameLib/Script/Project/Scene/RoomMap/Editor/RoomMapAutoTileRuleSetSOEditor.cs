#nullable enable
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Game.RoomMap.Editor
{
    [CustomEditor(typeof(RoomMapAutoTileRuleSetSO))]
    public sealed class RoomMapAutoTileRuleSetSOEditor : OdinEditor
    {
        SerializedProperty? _rulesProp;
        ReorderableList? _list;
        readonly List<bool> _expanded = new();

        protected override void OnEnable()
        {
            base.OnEnable();

            _rulesProp = serializedObject.FindProperty("rules");
            if (_rulesProp == null)
                return;

            _list = new ReorderableList(serializedObject, _rulesProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);
            _list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Rules");
            _list.elementHeightCallback = GetElementHeight;
            _list.drawElementCallback = DrawElement;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_rulesProp == null || _list == null)
            {
                EditorGUILayout.HelpBox("rules プロパティが見つかりません。", MessageType.Error);
                return;
            }

            EnsureExpandedSize(_rulesProp.arraySize);

            EditorGUILayout.Space(6);
            _list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        float GetElementHeight(int index)
        {
            if (_rulesProp == null)
                return EditorGUIUtility.singleLineHeight;

            EnsureExpandedSize(_rulesProp.arraySize);
            var line = EditorGUIUtility.singleLineHeight;

            var baseH = line + 6f; // header row
            if (!_expanded[index])
                return baseH;

            var element = _rulesProp.GetArrayElementAtIndex(index);
            if (element == null)
                return baseH;

            var pattern = element.FindPropertyRelative("Pattern");
            var overrideProp = element.FindPropertyRelative("ResultOverride");

            var h = baseH;
            h += line + 4f; // priority + name row already included? no, we draw in header
            h += line + 4f; // transform toggles
            if (pattern != null)
                h += EditorGUI.GetPropertyHeight(pattern, includeChildren: true) + 4f;
            if (overrideProp != null)
                h += EditorGUI.GetPropertyHeight(overrideProp, includeChildren: true) + 4f;
            return h;
        }

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (_rulesProp == null)
                return;

            EnsureExpandedSize(_rulesProp.arraySize);
            var element = _rulesProp.GetArrayElementAtIndex(index);
            if (element == null)
                return;

            var enabledProp = element.FindPropertyRelative("Enabled");
            var nameProp = element.FindPropertyRelative("displayName");
            var priorityProp = element.FindPropertyRelative("Priority");
            var patternProp = element.FindPropertyRelative("Pattern");
            var overrideProp = element.FindPropertyRelative("ResultOverride");
            var rotProp = element.FindPropertyRelative("AllowRotate90");
            var mxProp = element.FindPropertyRelative("AllowMirrorX");
            var myProp = element.FindPropertyRelative("AllowMirrorY");

            var line = EditorGUIUtility.singleLineHeight;

            rect.y += 2f;
            rect.height = line;

            // Leave room for reorderable list drag handle.
            const float handlePad = 16f;
            var content = rect;
            content.xMin += handlePad;
            content.width = Mathf.Max(0f, content.width);

            // Header: foldout + enabled + name + priority
            var foldRect = new Rect(content.x, content.y, 14f, line);
            _expanded[index] = EditorGUI.Foldout(foldRect, _expanded[index], GUIContent.none, toggleOnLabelClick: false);

            var enabledRect = new Rect(foldRect.xMax + 2f, content.y, 18f, line);
            if (enabledProp != null)
                enabledProp.boolValue = EditorGUI.Toggle(enabledRect, enabledProp.boolValue);

            var nameRect = new Rect(enabledRect.xMax + 2f, content.y, content.width * 0.62f, line);
            if (nameProp != null)
                nameProp.stringValue = EditorGUI.TextField(nameRect, nameProp.stringValue);
            else
                EditorGUI.LabelField(nameRect, $"Rule {index}");

            var prioRect = content;
            prioRect.xMin = nameRect.xMax + 6f;
            if (priorityProp != null)
                priorityProp.intValue = EditorGUI.IntField(prioRect, priorityProp.intValue);

            if (!_expanded[index])
                return;

            // Body
            var y = rect.yMax + 4f;

            var tRect = new Rect(content.x, y, content.width, line);
            DrawTransforms(tRect, rotProp, mxProp, myProp);
            y = tRect.yMax + 4f;

            if (patternProp != null)
            {
                var h = EditorGUI.GetPropertyHeight(patternProp, includeChildren: true);
                var pRect = new Rect(content.x, y, content.width, h);
                EditorGUI.PropertyField(pRect, patternProp, includeChildren: true);
                y = pRect.yMax + 4f;
            }

            if (overrideProp != null)
            {
                var h = EditorGUI.GetPropertyHeight(overrideProp, includeChildren: true);
                var oRect = new Rect(content.x, y, content.width, h);
                EditorGUI.PropertyField(oRect, overrideProp, includeChildren: true);
            }
        }

        static void DrawTransforms(Rect rect, SerializedProperty? rot90, SerializedProperty? mx, SerializedProperty? my)
        {
            var w = rect.width;
            var a = new Rect(rect.x, rect.y, w * 0.33f - 2f, rect.height);
            var b = new Rect(a.xMax + 2f, rect.y, w * 0.33f - 2f, rect.height);
            var c = new Rect(b.xMax + 2f, rect.y, w - (a.width + b.width + 4f), rect.height);

            if (rot90 != null) rot90.boolValue = GUI.Toggle(a, rot90.boolValue, "Rotate90", EditorStyles.miniButton);
            if (mx != null) mx.boolValue = GUI.Toggle(b, mx.boolValue, "MirrorX", EditorStyles.miniButton);
            if (my != null) my.boolValue = GUI.Toggle(c, my.boolValue, "MirrorY", EditorStyles.miniButton);
        }

        void EnsureExpandedSize(int size)
        {
            while (_expanded.Count < size) _expanded.Add(false);
            while (_expanded.Count > size) _expanded.RemoveAt(_expanded.Count - 1);
        }
    }
}
