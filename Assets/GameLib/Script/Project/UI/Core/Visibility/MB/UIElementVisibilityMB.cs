#nullable enable
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.UI
{
    /// <summary>
    /// UI要素に付与するVisibility(Fade)登録用FeatureInstaller。
    /// - IUIElementStateController.SetVisible(true/false) のみで FadeIn/FadeOut を行う
    /// - GameObject.SetActive(false) は使用しない
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIElementVisibilityMB : MonoBehaviour, IFeatureInstaller
    {
        [Header("Adapter (CanvasGroup)")]
        [Tooltip("未設定の場合、このGameObjectから自動取得/自動追加する。")]
        [SerializeField]
        CanvasGroup? canvasGroup;

        [Tooltip("Render停止対象のルート。未設定の場合 transform を使用する。")]
        [SerializeField]
        Transform? graphicsRoot;

        [Header("Fade Options")]
        [InlineProperty]
        [HideLabel]
        [SerializeField]
        UIFadeOptions fadeOptions = UIFadeOptions.Default;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var group = ResolveCanvasGroup();
            var root = graphicsRoot != null ? graphicsRoot : transform;
            var graphics = root.GetComponentsInChildren<Graphic>(includeInactive: true);

            var adapter = new CanvasGroupVisibilityAdapter(group, graphics);

            builder.RegisterInstance(fadeOptions);
            builder.RegisterInstance<IUIVisibilityAdapter>(adapter);

            builder.Register<UIElementFadeService>(Lifetime.Singleton)
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

