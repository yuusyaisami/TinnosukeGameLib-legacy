#nullable enable
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public sealed class UIScrollRectMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Scene")]
        [LabelText("Content")]
        [Required]
        [SerializeField]
        RectTransform? _content;

        [BoxGroup("Scene")]
        [LabelText("Viewport Rect")]
        [SerializeField]
        RectTransform? _viewportRect;

        [BoxGroup("Preset")]
        [LabelText("Preset")]
        [SerializeField]
        DynamicValue<UIScrollRectPreset> _presetValue =
            DynamicValue<UIScrollRectPreset>.FromSource(
                new ManagedRefLiteralSource<UIScrollRectPreset>(new UIScrollRectPreset()));

        [BoxGroup("Debug")]
        [LabelText("Enable Debug Log")]
        [SerializeField]
        bool _enableDebugLog;

        public RectTransform? Content => _content;
        public RectTransform? ViewportRect => _viewportRect;
        public DynamicValue<UIScrollRectPreset> PresetValue => _presetValue;
        public bool EnableDebugLog => _enableDebugLog;

        public void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            builder.Register<UIScrollRectService>(RuntimeLifetime.Singleton)
                .WithParameter(scope)
                .WithParameter(this)
                .As<IUIScrollRectService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();
        }
    }
}
