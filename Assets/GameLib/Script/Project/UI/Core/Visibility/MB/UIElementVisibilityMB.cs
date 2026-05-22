#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI
{
    /// <summary>
    /// UI隕∫ｴ縺ｫ莉倅ｸ弱☆繧儀isibility(Fade)逋ｻ骭ｲ逕ｨFeatureInstaller縲・
    /// - IUIElementStateController.SetVisible(true/false) 縺ｮ縺ｿ縺ｧ FadeIn/FadeOut 繧定｡後≧
    /// - GameObject.SetActive(false) 縺ｯ菴ｿ逕ｨ縺励↑縺・
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIElementVisibilityMB : MonoBehaviour, IScopeInstaller
    {
        [Header("Adapter (CanvasGroup)")]
        [Tooltip("Inspector setting.")]
        [SerializeField]
        CanvasGroup? canvasGroup;

        [Tooltip("Inspector setting.")]
        [SerializeField]
        Transform? graphicsRoot;

        [Header("Fade Options")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        UIFadeOptions fadeOptions = UIFadeOptions.Default;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var group = ResolveCanvasGroup();
            var root = graphicsRoot != null ? graphicsRoot : transform;
            var graphics = root.GetComponentsInChildren<Graphic>(includeInactive: true);

            var adapter = new CanvasGroupVisibilityAdapter(group, graphics);

            builder.RegisterInstance(fadeOptions);
            builder.RegisterInstance<IUIVisibilityAdapter>(adapter);

            builder.Register<UIElementFadeService>(RuntimeLifetime.Singleton)
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Reset()
        {
            ResolveCanvasGroup();
            if (graphicsRoot == null)
            {
                graphicsRoot = transform;
            }

            fadeOptions = UIFadeOptions.Default;
        }

        CanvasGroup ResolveCanvasGroup()
        {
            if (canvasGroup != null) return canvasGroup;

            if (TryGetComponent(out CanvasGroup found))
            {
                canvasGroup = found;
                return found;
            }

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            return canvasGroup;
        }
    }
}


