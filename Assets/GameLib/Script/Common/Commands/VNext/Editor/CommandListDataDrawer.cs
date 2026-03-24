#nullable enable
#if UNITY_EDITOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Commands.VNext.Editor
{
    /// <summary>
    /// CommandListData用のOdinValueDrawer。
    /// インラインでの表示に加え、「別ウィンドウで開く」ボタンを提供します。
    /// </summary>
    [DrawerPriority(DrawerPriorityLevel.WrapperPriority)]
    public sealed class CommandListDataDrawer : OdinValueDrawer<CommandListData>
    {
        const float OpenButtonWidth = 100f;
        const float OpenButtonMinWidth = 64f;
        const float CountLabelWidth = 60f;
        const float FoldoutWidth = 14f;
        const float ElementSpacing = 4f;
        const float MinLabelWidth = 48f;

        bool _pendingExpandedSync;
        bool _pendingExpandedValue;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var value = ValueEntry.SmartValue;
            var expanded = Property.State.Expanded;

            // ヘッダー行を描画
            expanded = DrawHeader(label, value, expanded);
            if (Property.State.Expanded != expanded)
                QueueExpandedSync(expanded);

            // 展開されている場合、デフォルト描画
            if (expanded)
            {
                EditorGUI.indentLevel++;
                CallNextDrawer(label: null);
                EditorGUI.indentLevel--;
            }
        }

        bool DrawHeader(GUIContent label, CommandListData? value, bool expanded)
        {
            var rect = EditorGUILayout.GetControlRect();
            var contentRect = EditorGUI.IndentedRect(rect);
            var displayLabel = label ?? new GUIContent(Property.NiceName);

            var totalWidth = contentRect.width;
            var buttonWidth = Mathf.Min(OpenButtonWidth, Mathf.Max(OpenButtonMinWidth, totalWidth * 0.35f));
            var canShowButton = totalWidth >= (FoldoutWidth + MinLabelWidth + CountLabelWidth + buttonWidth + (ElementSpacing * 4f));
            var buttonRect = new Rect(contentRect.xMax - buttonWidth, contentRect.y, buttonWidth, contentRect.height);
            var rightBound = canShowButton ? buttonRect.x - ElementSpacing : contentRect.xMax;

            var foldoutRect = new Rect(contentRect.x, contentRect.y, FoldoutWidth, contentRect.height);
            var labelRect = new Rect(
                foldoutRect.xMax + 2f,
                contentRect.y,
                Mathf.Max(MinLabelWidth, rightBound - (foldoutRect.xMax + 2f) - CountLabelWidth - (ElementSpacing * 2f)),
                contentRect.height);

            // ラベルクリックでも開閉できるようにする
            expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
            expanded = EditorGUI.Foldout(labelRect, expanded, displayLabel, true);

            var count = value?.Count ?? 0;
            var countRect = new Rect(labelRect.xMax + ElementSpacing, contentRect.y, CountLabelWidth, contentRect.height);
            if (countRect.xMax <= rightBound)
            {
                EditorGUI.LabelField(countRect, $"[{count}]", EditorStyles.miniLabel);
            }

            var funcName = value?.FunctionName ?? string.Empty;
            var funcDisplayName = string.IsNullOrEmpty(funcName) ? string.Empty : $"({funcName})";
            if (!string.IsNullOrEmpty(funcDisplayName))
            {
                var funcRect = new Rect(countRect.xMax + ElementSpacing, contentRect.y, rightBound - (countRect.xMax + ElementSpacing), contentRect.height);
                if (funcRect.width > 8f)
                    EditorGUI.LabelField(funcRect, funcDisplayName, EditorStyles.miniLabel);
            }

            if (canShowButton && GUI.Button(buttonRect, "Open Window", EditorStyles.miniButton))
            {
                OpenInWindow();
            }

            return expanded;
        }

        void QueueExpandedSync(bool expanded)
        {
            _pendingExpandedValue = expanded;
            if (_pendingExpandedSync)
                return;

            _pendingExpandedSync = true;
            EditorApplication.delayCall += ApplyPendingExpandedSync;
        }

        void ApplyPendingExpandedSync()
        {
            _pendingExpandedSync = false;

            if (Property == null || Property.Tree == null)
                return;

            if (Property.State.Expanded != _pendingExpandedValue)
                Property.State.Expanded = _pendingExpandedValue;
        }

        void OpenInWindow()
        {
            var value = ValueEntry.SmartValue;
            if (value == null)
            {
                Debug.LogWarning("[CommandListDataDrawer] CommandListData is null.");
                return;
            }

            // オーナーオブジェクトを取得（複数の方法を試す）
            UnityEngine.Object? ownerObject = null;
            string fieldPath = Property.Path;

            // 方法1: UnitySerializedObjectから取得
            var serializedObject = Property.Tree.UnitySerializedObject;
            if (serializedObject != null)
            {
                ownerObject = serializedObject.targetObject;
                // UnityのSerializedPropertyパスを取得
                var unityProperty = Property.Tree.GetUnityPropertyForPath(Property.Path, out _);
                if (unityProperty != null)
                {
                    fieldPath = unityProperty.propertyPath;
                }
            }

            // 方法2: PropertyTreeのターゲットから取得
            if (ownerObject == null)
            {
                var targets = Property.Tree.WeakTargets;
                if (targets != null && targets.Count > 0)
                {
                    ownerObject = targets[0] as UnityEngine.Object;
                }
            }

            // 方法3: RootPropertyのValueEntryから取得
            if (ownerObject == null)
            {
                ownerObject = Property.Tree.RootProperty.ValueEntry?.WeakSmartValue as UnityEngine.Object;
            }

            if (ownerObject == null)
            {
                Debug.LogWarning("[CommandListDataDrawer] Could not find owner object.");
                return;
            }

            CommandListEditorWindow.Open(fieldPath, value, ownerObject);
        }
    }
}
#endif
