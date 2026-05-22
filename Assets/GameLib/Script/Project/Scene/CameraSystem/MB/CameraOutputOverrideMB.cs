#nullable enable
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;

namespace Game.CameraSystem
{
    /// <summary>
    /// Camera の最終�E力を SharedTexture で差し替える設宁EMB、E
    /// CameraSystemMB と同じ LTS に配置する、E
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraOutputOverrideMB : MonoBehaviour, IScopeInstaller
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

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var options = new CameraOutputOverrideOptions(overrideEnabled, mode, sharedTextureTag);

            builder.Register<CameraOutputOverrideService>(RuntimeLifetime.Singleton)
                .WithParameter(options)
                .As<ICameraOutputOverrideService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.Register<CameraOutputOverrideBridge>(RuntimeLifetime.Singleton)
                .As<IScopeTickHandler>();
        }
    }

    /// <summary>
    /// 毎フレーム ICameraOutputOverrideService ↁECameraOutputOverrideRegistry へ反映する橋渡し、E
    /// </summary>
    internal sealed class CameraOutputOverrideBridge : IScopeTickHandler
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

