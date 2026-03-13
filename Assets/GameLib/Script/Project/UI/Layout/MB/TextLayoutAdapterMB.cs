using System;
using TMPro;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Game.Layout
{
    [DisallowMultipleComponent]
    public sealed class TextLayoutAdapterMB : MonoBehaviour, ITextLayoutAdapter
    {
        const string ReferencesGroup = "References";
        const string RuntimeGroup = "Runtime";

        [BoxGroup(ReferencesGroup)]
        [LabelText("Target Text")]
        [Required]
        [SerializeField] TMP_Text targetText;

        [BoxGroup(RuntimeGroup)]
        [ReadOnly]
        [ShowInInspector]
        string _fullText = string.Empty;

        public event System.Action OnLayoutContentChanged;

        public TMP_Text TargetText => targetText;

        public RectTransform Target => targetText != null ? targetText.rectTransform : null;

        public string FullText => _fullText;

        void Reset()
        {
            targetText = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            NotifyMembershipDirty();
        }

        void OnDisable()
        {
            NotifyMembershipDirty();
        }

        public void SetFullText(string fullText)
        {
            fullText ??= string.Empty;
            if (string.Equals(_fullText, fullText, StringComparison.Ordinal))
                return;

            _fullText = fullText;
            OnLayoutContentChanged?.Invoke();
        }

        public string GetLayoutText()
        {
            // Default: no sanitization. Provide a different MB implementation if tag stripping is needed.
            return _fullText;
        }

        public Vector2 GetPreferredSize(float maxWidth)
        {
            if (targetText == null)
                return Vector2.zero;

            float width = maxWidth <= 0f ? Mathf.Infinity : maxWidth;
            var layoutText = GetLayoutText() ?? string.Empty;

            // Do NOT mutate targetText.text (keeps Typewriter/animation responsibilities separate).
            return targetText.GetPreferredValues(layoutText, width, 0f);
        }

        void NotifyMembershipDirty()
        {
            var system = GetComponentInParent<LayoutSystemMB>();
            system?.MarkMembershipDirty();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (targetText == null)
                targetText = GetComponent<TMP_Text>();
        }
#endif
    }
}
