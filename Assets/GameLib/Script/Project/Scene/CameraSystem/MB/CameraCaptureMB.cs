#nullable enable
using Game;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using Game.SharedTexture;

namespace Game.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class CameraCaptureMB : MonoBehaviour, IScopeInstaller
    {
        [BoxGroup("Capture")]
        [LabelText("Resolution Scale")]
        [Range(0.1f, 1.0f)]
        [SerializeField] float resolutionScale = 1.0f;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            var options = new CameraCaptureOptions(resolutionScale);

            builder.Register<CameraCaptureService>(RuntimeLifetime.Singleton)
                .WithParameter(options)
                .As<ICameraCaptureService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>()
                .As<IScopeTickHandler>();
        }
    }
}

