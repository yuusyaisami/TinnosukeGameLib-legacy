#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Foundation
{
    /// <summary>
    /// シンプルな文字入力ポップアップ。OK 時に入力値をコールバックへ返す。
    /// </summary>
    public sealed class TextPromptWindow : EditorWindow
    {
        string _label;
        string _text;
        Action<string> _onOk;
        bool _focusRequested;

        public static void Open(string title, string label, string initialText, Action<string> onOk)
        {
            var window = CreateInstance<TextPromptWindow>();
            window.titleContent = new GUIContent(title);
            window._label = label;
            window._text = initialText ?? string.Empty;
            window._onOk = onOk;
            window._focusRequested = true;

            var pos = new Rect(Screen.width / 2f - 160f, Screen.height / 2f - 60f, 320f, 110f);
            window.position = pos;
            window.ShowUtility();
            window.Focus();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(_label ?? string.Empty);
            EditorGUILayout.Space();

            GUI.SetNextControlName("TextField");
            _text = EditorGUILayout.TextField(_text ?? string.Empty);

            if (_focusRequested)
            {
                _focusRequested = false;
                EditorGUI.FocusTextInControl("TextField");
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("OK", GUILayout.Width(80)))
                {
                    Submit();
                }

                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                {
                    Close();
                }
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Submit();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    Close();
                    e.Use();
                }
            }
        }

        void Submit()
        {
            var text = _text?.Trim() ?? string.Empty;
            _onOk?.Invoke(text);
            Close();
        }
    }
}
#endif
