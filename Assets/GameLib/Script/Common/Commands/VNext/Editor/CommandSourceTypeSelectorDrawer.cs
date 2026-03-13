#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Commands.VNext.Editor
{
    [DrawerPriority(DrawerPriorityLevel.SuperPriority)]
    public sealed class CommandSourceTypeSelectorDrawer : OdinValueDrawer<ICommandSource>
    {
        static Type[] _cachedTypes;
        const int SummaryMaxChars = 32;

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
            var current = ValueEntry.SmartValue;
            var typeLabel = current != null ? current.GetType().Name : "<None>";
            var debugName = current?.DebugName ?? "<None>";
            var summary = BuildSummary(StripSourcePrefix(debugName));

            var expanded = Property.State.Expanded;
            var rowRect = EditorGUILayout.GetControlRect();
            var contentRect = EditorGUI.IndentedRect(rowRect);
            var labelContent = label ?? GUIContent.none;
            var labelSize = EditorStyles.label.CalcSize(labelContent);
            var labelRectWidth = Mathf.Min(contentRect.width, 16f + labelSize.x + 4f);
            var labelRect = new Rect(contentRect.x, contentRect.y, labelRectWidth, contentRect.height);
            var fieldRect = new Rect(labelRect.xMax + 4f, contentRect.y, Mathf.Max(0f, contentRect.xMax - (labelRect.xMax + 4f)), contentRect.height);

            expanded = EditorGUI.Foldout(labelRect, expanded, labelContent, true);
            Property.State.Expanded = expanded;

            var popupSize = EditorStyles.popup.CalcSize(new GUIContent(typeLabel));
            var popupWidth = Mathf.Clamp(popupSize.x + 16f, 60f, 220f);
            var spacing = 4f;
            var popupRect = new Rect(fieldRect.x, fieldRect.y, Mathf.Min(popupWidth, fieldRect.width), fieldRect.height);
            var control = current as ICommandSourceExecutionControl;
            var hasExecutionToggle = control != null;
            var toggleWidth = hasExecutionToggle ? 18f : 0f;
            var toggleRect = hasExecutionToggle
                ? new Rect(fieldRect.xMax - toggleWidth, fieldRect.y, toggleWidth, fieldRect.height)
                : Rect.zero;
            if (GUI.Button(popupRect, typeLabel, EditorStyles.popup))
            {
                ShowSelector();
            }

            var oldBg = GUI.backgroundColor;
            if (Property.State.Expanded)
                GUI.backgroundColor = new Color(0.85f, 0.95f, 1.0f);
            var content = new GUIContent(summary) { tooltip = debugName };
            var btnW = Mathf.Clamp(EditorStyles.miniButton.CalcSize(content).x + 8f, 48f, 220f);
            var summaryX = popupRect.xMax + spacing;
            var summaryMaxX = hasExecutionToggle ? toggleRect.xMin - spacing : fieldRect.xMax;
            var remaining = Mathf.Max(0f, summaryMaxX - summaryX);
            if (remaining > 8f)
            {
                var summaryRect = new Rect(summaryX, fieldRect.y, Mathf.Min(btnW, remaining), fieldRect.height);
                if (GUI.Button(summaryRect, content, EditorStyles.miniButton))
                    Property.State.Expanded = !Property.State.Expanded;
            }

            if (hasExecutionToggle && control != null)
            {
                var newValue = GUI.Toggle(toggleRect, control.IsExecutionEnabled, GUIContent.none);
                if (newValue != control.IsExecutionEnabled)
                {
                    control.SetExecutionEnabled(newValue);
                    GUI.changed = true;
                }
            }
            GUI.backgroundColor = oldBg;

            if (current != null && Property.State.Expanded)
            {
                var snapshot = new List<InspectorProperty>(Property.Children.Count);
                for (int i = 0; i < Property.Children.Count; i++)
                    snapshot.Add(Property.Children[i]);

                for (int i = 0; i < snapshot.Count; i++)
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
                    if (string.Equals(child.Name, "enabled", StringComparison.OrdinalIgnoreCase))
                        continue;
                    DrawChildSafely(child);
                }
            }
        }

        void ShowSelector()
        {
            var types = GetCommandSourceTypes();
            var selector = new GenericSelector<Type>("Command Source", false, t => t.Name, types);
            selector.FlattenedTree = true;
            selector.SelectionConfirmed += selection =>
            {
                var selected = selection.FirstOrDefault();
                if (selected == null)
                    return;

                if (selected.GetConstructor(Type.EmptyTypes) == null)
                    return;

                ValueEntry.SmartValue = (ICommandSource)Activator.CreateInstance(selected);
            };

            selector.ShowInPopup();
        }

        static IEnumerable<Type> GetCommandSourceTypes()
        {
            if (_cachedTypes != null)
                return _cachedTypes;

            var types = TypeCache.GetTypesDerivedFrom<ICommandSource>();
            var list = new List<Type>();
            for (int i = 0; i < types.Count; i++)
            {
                var t = types[i];
                if (t == null || t.IsAbstract || t.IsInterface)
                    continue;
                if (t.GetConstructor(Type.EmptyTypes) == null)
                    continue;
                list.Add(t);
            }

            _cachedTypes = list.ToArray();
            return _cachedTypes;
        }

        static string BuildSummary(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "<None>";
            if (text.Length <= SummaryMaxChars)
                return text;
            return text.Substring(0, SummaryMaxChars - 3) + "...";
        }

        static string StripSourcePrefix(string debugName)
        {
            if (string.IsNullOrEmpty(debugName))
                return debugName;

            var openIdx = debugName.IndexOf('(');
            var closeIdx = debugName.LastIndexOf(')');
            if (openIdx >= 0 && closeIdx > openIdx)
            {
                var inner = debugName.Substring(openIdx + 1, closeIdx - openIdx - 1);
                return string.IsNullOrEmpty(inner) ? debugName : inner;
            }

            return debugName;
        }
    }
}
#endif
