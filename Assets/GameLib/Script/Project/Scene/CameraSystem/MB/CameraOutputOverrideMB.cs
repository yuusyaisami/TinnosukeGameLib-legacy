#nullable enable
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;

namespace Game.CameraSystem
{
    /// <summary>
    /// Camera の最終出力を SharedTexture で差し替える設定 MB。
    /// CameraSystemMB と同じ LTS に配置する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraOutputOverrideMB : MonoBehaviour, IFeatureInstaller
    {
        [BoxGroup("Override")]
        [LabelText("Enabled")]
        [SerializeField] bool overrideEnabled = false;

        [BoxGroup("Override")]
        [LabelText("Mode")]
        [SerializeField] CameraOutputOverrideMode mode = CameraOutputOverrideMode.SharedTexture;

        [BoxGroup("Override")]
        [LabelText("SharedTexture Tag")]
        [SerializeField] string sharedTextureTag = "camera/main/final";

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            var options = new CameraOutputOverrideOptions(overrideEnabled, mode, sharedTextureTag);

            builder.Register<CameraOutputOverrideService>(Lifetime.Singleton)
                .WithParameter(options)
                .As<ICameraOutputOverrideService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<CameraOutputOverrideBridge>(Lifetime.Singleton)
                .As<ITickable>();
        }
    }

    /// <summary>
    /// 毎フレーム ICameraOutputOverrideService → CameraOutputOverrideRegistry へ反映する橋渡し。
    /// </summary>
    internal sealed class CameraOutputOverrideBridge : ITickable
    {
        readonly ICameraOutputOverrideService _overrideService;
        readonly ICameraRenderContext _renderContext;

        public CameraOutputOverrideBridge(
            ICameraOutputOverrideService overrideService,
            ICameraRenderContext renderContext)
        {
            _overrideService = overrideService;
            _renderContext = renderContext;
        }

        public void Tick()
        {
            var camera = _renderContext.Camera;
            if (camera == null)
                return;

            var tex = _overrideService.ResolveOverrideTexture();
            CameraOutputOverrideRegistry.SetOverride(camera, tex);
        }
    }
}
