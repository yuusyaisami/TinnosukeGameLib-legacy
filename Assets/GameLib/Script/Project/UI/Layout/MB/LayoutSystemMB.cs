using UnityEngine;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;
using Game;

namespace Game.Layout
{
    [DisallowMultipleComponent]
    public sealed class LayoutSystemMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("References")]
        [LabelText("Layout Elements Root")]
        [Required]
        [SerializeField] RectTransform layoutElementsRoot;

        [BoxGroup("References")]
        [LabelText("Background Rect")]
        [Required]
        [SerializeField] RectTransform backgroundRect;

        [BoxGroup("Options")]
        [SerializeField] bool recollectOnEnable = true;

        [BoxGroup("Options")]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup("Options")]
        [SerializeField] bool forceUnityLayoutRebuildOnRebuild = false;

        [BoxGroup("Options")]
        [SerializeField] bool excludeInactive = true;

        [BoxGroup("Options")]
        [SerializeField] bool hideBackgroundWhenEmpty = true;

        [BoxGroup("Background")]
        [InlineProperty]
        [HideLabel]
        [SerializeField] LayoutBackgroundOptions backgroundOptions = default;

        ILayoutSystemService _service;
        bool _pendingMembershipDirty;
        bool _pendingContentDirty;

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var config = new LayoutSystemConfig
            {
                LayoutElementsRoot = layoutElementsRoot,
                BackgroundRect = backgroundRect,
                BackgroundOptions = backgroundOptions,
                ForceUnityLayoutRebuildOnRebuild = forceUnityLayoutRebuildOnRebuild,
                ExcludeInactive = excludeInactive,
                HideBackgroundWhenEmpty = hideBackgroundWhenEmpty,
                RunInLateUpdate = runInLateUpdate,
            };

            builder.RegisterInstance(config);

            var registration = builder.Register<LayoutSystemService>(Lifetime.Singleton)
                .As<ILayoutSystemService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            if (scope != null && scope.Kind == LifetimeScopeKind.Runtime)
            {
                registration.As<ITickable>();
            }
            else if (runInLateUpdate)
            {
                registration.As<ILateTickable>();
            }
            else
            {
                registration.As<ITickable>();
            }

            builder.RegisterBuildCallback(resolver =>
            {
                if (resolver.TryResolve<ILayoutSystemService>(out var service))
                {
                    _service = service;
                    FlushPendingDirty();
                }
            });
        }

        public void MarkMembershipDirty()
        {
            if (_service != null)
            {
                _service.MarkMembershipDirty();
                return;
            }

            _pendingMembershipDirty = true;
        }

        public void MarkContentDirty()
        {
            if (_service != null)
            {
                _service.MarkContentDirty();
                return;
            }

            _pendingContentDirty = true;
        }

        void OnEnable()
        {
            EnsureRootObserver();
            if (recollectOnEnable)
                MarkMembershipDirty();
        }

        void OnTransformChildrenChanged()
        {
            MarkMembershipDirty();
        }

        void Reset()
        {
            if (layoutElementsRoot == null)
                layoutElementsRoot = transform.Find("LayoutElements") as RectTransform;
            if (backgroundRect == null)
                backgroundRect = transform.Find("LayoutBackground") as RectTransform;

            backgroundOptions = LayoutBackgroundOptions.Default;
            recollectOnEnable = true;
            runInLateUpdate = true;
            excludeInactive = true;
            hideBackgroundWhenEmpty = true;
        }

        void EnsureRootObserver()
        {
            if (!Application.isPlaying)
                return;

            if (layoutElementsRoot == null)
                return;

            if (!layoutElementsRoot.TryGetComponent<LayoutElementsRootObserverMB>(out _))
                layoutElementsRoot.gameObject.AddComponent<LayoutElementsRootObserverMB>();
        }

#if UNITY_EDITOR
        [BoxGroup("Debug"), Button(ButtonSizes.Medium), GUIColor(0.6f, 1f, 0.6f)]
        void FixScales()
        {
            if (layoutElementsRoot != null)
                layoutElementsRoot.localScale = Vector3.one;
            if (backgroundRect != null)
                backgroundRect.localScale = Vector3.one;
        }
#endif

        void FlushPendingDirty()
        {
            if (_service == null)
                return;

            if (_pendingMembershipDirty)
            {
                _service.MarkMembershipDirty();
                _pendingMembershipDirty = false;
            }

            if (_pendingContentDirty)
            {
                _service.MarkContentDirty();
                _pendingContentDirty = false;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (layoutElementsRoot == null || backgroundRect == null)
                return;

            if (backgroundRect == layoutElementsRoot)
                Debug.LogError("[LayoutSystem] BackgroundRect must not be the same as LayoutElementsRoot.", this);

            if (backgroundRect.anchorMin != backgroundRect.anchorMax)
                Debug.LogError("[LayoutSystem] BackgroundRect anchorMin must match anchorMax.", backgroundRect);

            if (backgroundRect.parent != layoutElementsRoot)
            {
                // Keep hierarchy consistent to avoid extra transform conversions.
                // This also prevents editor-time warnings from spamming logs.
                backgroundRect.SetParent(layoutElementsRoot, worldPositionStays: true);
            }

            var rootCanvas = layoutElementsRoot.GetComponentInParent<Canvas>();
            var bgCanvas = backgroundRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null && bgCanvas != null && rootCanvas != bgCanvas)
                Debug.LogWarning("[LayoutSystem] Root and Background are under different Canvas objects.", this);

            if (IsScaled(layoutElementsRoot, out var rootScale))
                Debug.LogWarning($"[LayoutSystem] LayoutElementsRoot uses non-unit scale (lossyScale={rootScale.x:0.###},{rootScale.y:0.###},{rootScale.z:0.###}).", layoutElementsRoot);
            if (IsScaled(backgroundRect, out var bgScale))
                Debug.LogWarning($"[LayoutSystem] BackgroundRect uses non-unit scale (lossyScale={bgScale.x:0.###},{bgScale.y:0.###},{bgScale.z:0.###}).", backgroundRect);
        }

        static bool IsScaled(Transform target, out Vector3 lossy)
        {
            lossy = target.lossyScale;
            // allow tiny floating point differences but still warn for noticeable deviations
            const float epsilon = 0.001f;
            return Mathf.Abs(lossy.x - 1f) > epsilon ||
                   Mathf.Abs(lossy.y - 1f) > epsilon ||
                   Mathf.Abs(lossy.z - 1f) > epsilon;
        }
#endif
    }
}
