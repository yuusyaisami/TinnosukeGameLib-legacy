// Assets/GameLib/Script/Common/Editor/DynamicValueCompactDrawer.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Game.Common;
using Sirenix.OdinInspector.Editor;

namespace Game.Common.Editor
{
    public sealed class DynamicValueCompactDrawer
        : OdinAttributeDrawer<DynamicCompactAttribute, Game.Common.DynamicValue>
    {
        static readonly Dictionary<string, bool> ExpandedStates = new();

        const int SummaryMaxChars = 18;
        const float ButtonMinW = 44f;
        const float ButtonMaxW = 180f;
        const float ButtonPad = 10f;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var root = Property;
            var sourceProp = FindChild(root, "_source");
            if (sourceProp == null)
            {
                CallNextDrawer(label);
                return;
            }

            // インスタンスIDも混ぜてキー衝突を避ける
            var target = root.Tree?.UnitySerializedObject?.targetObject;
            var key = $"{target?.GetInstanceID() ?? 0}:{root.Path}";

            if (!ExpandedStates.TryGetValue(key, out var expanded))
                ExpandedStates[key] = expanded = false;

            var src = sourceProp.ValueEntry?.WeakSmartValue as IDynamicSource;

            var summaryRaw = BuildSummary(src);
            if (string.IsNullOrEmpty(summaryRaw))
                summaryRaw = src?.SourceTypeName ?? "None";

            var buttonText = BuildButtonLabel(sourceProp, src, summaryRaw);
            var btnContent = new GUIContent(buttonText)
            {
                tooltip = src?.SourceTypeName ?? string.Empty
            };
            var btnW = Mathf.Clamp(
                EditorStyles.miniButton.CalcSize(btnContent).x + ButtonPad,
                ButtonMinW, ButtonMaxW);

            // --- 1行目：Source(型選択UIだけ) + ボタン ---
            EditorGUILayout.BeginHorizontal();
            {
                if (label != null && !Attribute.HideLabel && !string.IsNullOrEmpty(label.text))
                    GUILayout.Label(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f));

                // ここが肝：Sourceは常に「畳んで」描く -> ヘッダ(型選択)だけ出る
                sourceProp.State.Expanded = false;
                sourceProp.Draw(GUIContent.none);

                var oldBg = GUI.backgroundColor;
                if (expanded) GUI.backgroundColor = new Color(0.85f, 0.95f, 1.0f);

                if (GUILayout.Button(btnContent, EditorStyles.miniButton, GUILayout.Width(btnW)))
                    ExpandedStates[key] = expanded = !expanded;

                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndHorizontal();

            var headerRect = GUILayoutUtility.GetLastRect();
            var evt = Event.current;
            if (evt != null && evt.type == EventType.ContextClick && headerRect.Contains(evt.mousePosition))
            {
                if (src is FloatExpressionSource || src is IntExpressionSource)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Open Graph Preview"), false, () =>
                    {
                        ExpressionGraphPreviewWindow.Open(src);
                    });
                    menu.ShowAsContext();
                    evt.Use();
                }
            }

            // --- 展開：Sourceの中身(フィールド)だけ描く（ShowIfが効く） ---
            if (!expanded) return;

            EditorGUI.indentLevel++;
            if (src == null)
            {
                EditorGUILayout.HelpBox("Source is null. Select a Type.", MessageType.Info);
            }
            else
            {
                // children を確実に使うため expanded=true にしてから、header相当($... )は除外して描く
                sourceProp.State.Expanded = true;

                for (int i = 0; i < sourceProp.Children.Count; i++)
                {
                    var c = sourceProp.Children[i];

                    // SerializeReferenceの内部メタはスキップ（環境差があるので "$" 始まりを一掃）
                    if (!string.IsNullOrEmpty(c.Name) && c.Name[0] == '$')
                        continue;

                    c.Draw();
                }
            }
            EditorGUI.indentLevel--;
        }

        static InspectorProperty FindChild(InspectorProperty parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.Children.Count; i++)
            {
                var c = parent.Children[i];
                if (c.Name == name) return c;
            }
            return null;
        }

        static string BuildButtonLabel(InspectorProperty sourceProp, IDynamicSource src, string fallback)
        {
            var initials = GetTypeInitials(src?.SourceTypeName);
            if (string.IsNullOrEmpty(initials))
                return fallback;

            if (TryGetButtonDetail(sourceProp, src, out var detail) && !string.IsNullOrEmpty(detail))
                return $"{initials} {detail}";

            return initials;
        }

        static string GetTypeInitials(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var ch in typeName)
            {
                if (char.IsUpper(ch)) sb.Append(ch);
            }
            if (sb.Length > 0)
                return sb.ToString();

            return typeName.Substring(0, 1).ToUpperInvariant();
        }

        static bool TryGetButtonDetail(InspectorProperty sourceProp, IDynamicSource src, out string detail)
        {
            detail = null;
            if (sourceProp == null || src == null)
                return false;

            switch (src)
            {
                case LiteralSource _:
                    return TryGetLiteralDetail(sourceProp, out detail);
                case VarStoreSource _:
                    return TryGetVarKeyDetail(sourceProp, out detail);
                case SelfBlackboardSource _:
                case OtherBlackboardSource _:
                    return TryGetChildValueAsString(sourceProp, "blackboardKey", out detail);
                case SelfScalarSource _:
                case OtherScalarSource _:
                    return TryGetChildValueAsString(sourceProp, "scalarKey", out detail);
                case SharedActorSourceExistsSource _:
                case SharedActorSourceTagSource _:
                    return TryGetChildValueAsString(sourceProp, "sharedHubActorSource", out detail);
                case UIModalStackActorMatchSource _:
                    return TryGetChildValueAsString(sourceProp, "modalStackActorSource", out detail);
            }

            if (TryGetChildValue(sourceProp, "objectValue", out var obj) && obj is Object unityObj)
            {
                detail = !string.IsNullOrEmpty(unityObj.name) ? unityObj.name : unityObj.GetType().Name;
                return true;
            }

            return false;
        }

        static bool TryGetVarKeyDetail(InspectorProperty sourceProp, out string detail)
        {
            detail = null;
            var keyProp = FindChild(sourceProp, "key");
            if (keyProp == null)
                return false;

            if (TryGetChildValueAsString(keyProp, "stableKey", out var stable) && !string.IsNullOrEmpty(stable))
            {
                detail = stable;
                return true;
            }

            if (TryGetChildValueAsString(keyProp, "varId", out var id))
            {
                detail = id;
                return true;
            }

            return false;
        }

        static bool TryGetLiteralDetail(InspectorProperty sourceProp, out string detail)
        {
            detail = null;
            if (!TryGetChildValue(sourceProp, "type", out var typeValue))
                return false;

            if (typeValue is not LiteralSource.LiteralType literalType)
                return false;

            var fieldName = literalType switch
            {
                LiteralSource.LiteralType.Int => "intValue",
                LiteralSource.LiteralType.Float => "floatValue",
                LiteralSource.LiteralType.Bool => "boolValue",
                LiteralSource.LiteralType.String => "stringValue",
                LiteralSource.LiteralType.Vector2 => "vector2Value",
                LiteralSource.LiteralType.Vector3 => "vector3Value",
                LiteralSource.LiteralType.Vector4 => "vector4Value",
                LiteralSource.LiteralType.Color => "colorValue",
                _ => null,
            };

            if (string.IsNullOrEmpty(fieldName))
                return false;

            return TryGetChildValueAsString(sourceProp, fieldName, out detail);
        }

        static bool TryGetChildValue(InspectorProperty parent, string childName, out object value)
        {
            value = null;
            var child = FindChild(parent, childName);
            if (child?.ValueEntry == null)
                return false;

            value = child.ValueEntry.WeakSmartValue;
            return true;
        }

        static bool TryGetChildValueAsString(InspectorProperty parent, string childName, out string value)
        {
            value = null;
            if (!TryGetChildValue(parent, childName, out var raw))
                return false;

            if (raw == null)
            {
                value = "null";
                return true;
            }

            value = raw.ToString();
            return true;
        }

        // ここはあなたの反射版を戻してOK。今は最小。
        static string BuildSummary(IDynamicSource src) => src?.SourceTypeName ?? "";

        static string Ellipsis(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= maxChars) return s;
            return s.Substring(0, Mathf.Max(1, maxChars - 1)) + "…";
        }
    }
}
#endif
