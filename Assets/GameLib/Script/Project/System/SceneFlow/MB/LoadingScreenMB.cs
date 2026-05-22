#nullable enable
using TMPro;
using UnityEngine;

namespace Game.Loading
{
    [DisallowMultipleComponent]
    public sealed class LoadingScreenMB : MonoBehaviour
    {
        const string DefaultMessage = "Loading...";
        const string DefaultPanelName = "LoadingPanel";
        const string DefaultIconName = "LoadingIcon";
        const string DefaultMessageName = "LoadingText";

        [SerializeField] GameObject? screenRoot;
        [SerializeField] RectTransform? loadingIcon;
        [SerializeField] TMP_Text? messageText;
        [SerializeField, Min(0f)] float iconRotationSpeed = 180f;
        [SerializeField] bool hideOnAwake = true;

        string _currentMessage = string.Empty;
        float _currentProgress;
        bool _isShowing;

        public bool IsShowing => _isShowing;
        public float CurrentProgress => _currentProgress;
        public string CurrentMessage => _currentMessage;

        void Reset()
        {
            AutoBind();
            ApplyVisibility(false);
            ApplyMessage();
        }

        void Awake()
        {
            AutoBind();
            if (hideOnAwake)
            {
                ApplyVisibility(false);
                ApplyMessage();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            AutoBind();
            iconRotationSpeed = Mathf.Max(0f, iconRotationSpeed);
        }
#endif

        void Update()
        {
            if (!_isShowing || loadingIcon == null || iconRotationSpeed <= 0f)
                return;

            loadingIcon.Rotate(0f, 0f, -iconRotationSpeed * Time.unscaledDeltaTime, Space.Self);
        }

        public void Show(string? message, float progress = 0f)
        {
            _isShowing = true;
            _currentProgress = Mathf.Clamp01(progress);
            _currentMessage = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;
            ApplyVisibility(true);
            ApplyMessage();
        }

        public void Hide()
        {
            _isShowing = false;
            _currentProgress = 0f;
            _currentMessage = string.Empty;
            ApplyVisibility(false);
            ApplyMessage();
        }

        public void SetProgress(float progress, string? message = null)
        {
            _currentProgress = Mathf.Clamp01(progress);
            if (message != null)
                _currentMessage = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;

            ApplyMessage();
        }

        void ApplyVisibility(bool visible)
        {
            var target = screenRoot != null ? screenRoot : gameObject;
            if (target != null && target.activeSelf != visible)
                target.SetActive(visible);
        }

        void ApplyMessage()
        {
            if (messageText == null)
                return;

            if (_isShowing)
            {
                messageText.text = string.IsNullOrWhiteSpace(_currentMessage) ? DefaultMessage : _currentMessage;
                return;
            }

            messageText.text = string.Empty;
        }

        void AutoBind()
        {
            if (screenRoot == null && TryFindDescendant(DefaultPanelName, out var panel))
                screenRoot = panel.gameObject;

            if (loadingIcon == null && TryFindDescendant(DefaultIconName, out var icon))
                loadingIcon = icon as RectTransform;

            if (messageText == null && TryFindDescendant(DefaultMessageName, out var message))
                messageText = message.GetComponent<TMP_Text>();
        }

        bool TryFindDescendant(string objectName, out Transform result)
        {
            result = null!;
            if (string.IsNullOrEmpty(objectName))
                return false;

            var queue = new System.Collections.Generic.Queue<Transform>();
            queue.Enqueue(transform);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.name == objectName)
                {
                    result = current;
                    return true;
                }

                for (var i = 0; i < current.childCount; i++)
                    queue.Enqueue(current.GetChild(i));
            }

            return false;
        }
    }
}
