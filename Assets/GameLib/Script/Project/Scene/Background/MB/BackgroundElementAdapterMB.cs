#nullable enable
using Game;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace Game.Background
{
    [DisallowMultipleComponent]
    public sealed class BackgroundElementAdapterMB : MonoBehaviour, IScopeInstaller, IBackgroundElementAdapterOptions
    {
        const string TargetGroup = "Targets";
        const string OptionGroup = "Options";

        [BoxGroup(TargetGroup)]
        [LabelText("RectTransform")]
        [SerializeField] RectTransform? rectTransform;

        [BoxGroup(TargetGroup)]
        [LabelText("SpriteRenderer")]
        [SerializeField] SpriteRenderer? spriteRenderer;

        [BoxGroup(OptionGroup)]
        [LabelText("Apply RectTransform Size")]
        [SerializeField] bool applyRectTransformSize = true;

        [BoxGroup(OptionGroup)]
        [LabelText("Apply SpriteRenderer Size")]
        [SerializeField] bool applySpriteRendererSize = true;

        [BoxGroup(OptionGroup)]
        [LabelText("Apply Sorting Order")]
        [SerializeField] bool applySortingOrder = true;

        [BoxGroup(OptionGroup)]
        [LabelText("Sorting Order Offset")]
        [SerializeField] int sortingOrderOffset = 0;

        public RectTransform? RectTransform => rectTransform;
        public SpriteRenderer? SpriteRenderer => spriteRenderer;
        public bool ApplyRectTransformSize => applyRectTransformSize;
        public bool ApplySpriteRendererSize => applySpriteRendererSize;
        public bool ApplySortingOrder => applySortingOrder;
        public int SortingOrderOffset => sortingOrderOffset;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode owner)
        {
            builder.Register<BackgroundElementAdapterService>(RuntimeLifetime.Singleton)
                .WithParameter<IBackgroundElementAdapterOptions>(this)
                .As<IBackgroundElementAdapter>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        void Reset()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
#endif
    }
}

