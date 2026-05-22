#nullable enable
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Sirenix.OdinInspector;
using Game;
using Game.Times;

namespace Game.CameraSystem
{
    [DisallowMultipleComponent]
    public sealed class CameraSystemMB : MonoBehaviour, IScopeInstaller
    {
        [BoxGroup("References")]
        [LabelText("Fx Transform")]
        [Required]
        [SerializeField] Transform? fxTransform;

        [BoxGroup("References")]
        [LabelText("Camera Transform")]
        [Required]
        [SerializeField] Transform? cameraTransform;

        [BoxGroup("References")]
        [LabelText("Camera")]
        [Required]
        [SerializeField] Camera? cameraComponent;

        [BoxGroup("References")]
        [LabelText("Volume")]
        [Required]
        [SerializeField] UnityEngine.Rendering.Volume? volume;

        [BoxGroup("Camera Tag")]
        [LabelText("Camera Tag")]
        [SerializeField] string cameraTag = "main";

        [BoxGroup("Move")]
        [LabelText("Move Channel Tag")]
        [SerializeField] string moveChannelTag = "camera";

        [BoxGroup("Tick")]
        [LabelText("Run In LateUpdate")]
        [SerializeField] bool runInLateUpdate = true;

        [BoxGroup("Tick")]
        [LabelText("Time Scale Behavior")]
        [SerializeField] TimeScaleBehavior timeScaleBehavior = TimeScaleBehavior.Scaled;

        [BoxGroup("Zoom")]
        [LabelText("Min Size")]
        [MinValue(0.001f)]
        [SerializeField] float zoomMinSize = 1f;

        [BoxGroup("Zoom")]
        [LabelText("Max Size")]
        [MinValue(0.001f)]
        [SerializeField] float zoomMaxSize = 20f;

        public void InstallScopeServices(IRuntimeContainerBuilder builder, IScopeNode scope)
        {
            ResolveReferences();

            var camera = cameraComponent;
            var fx = fxTransform;
            var camTransform = cameraTransform;
            var vol = volume;

            if (camera == null || fx == null || camTransform == null || vol == null)
                return;

            var systemOptions = new CameraSystemOptions(
                moveChannelTag,
                runInLateUpdate,
                timeScaleBehavior,
                zoomMinSize,
                zoomMaxSize);

            builder.RegisterInstance(systemOptions);

            var renderContext = new CameraRenderContext(camera, camTransform, fx, cameraTag);
            builder.RegisterInstance<ICameraRenderContext>(renderContext);

            builder.Register<CameraZoomService>(RuntimeLifetime.Singleton)
                .WithParameter(camera.orthographicSize)
                .WithParameter(zoomMinSize)
                .WithParameter(zoomMaxSize)
                .AsSelf()
                .As<ICameraZoomService>();

            builder.Register<CameraPostProcessService>(RuntimeLifetime.Singleton)
                .WithParameter(vol)
                .AsSelf()
                .As<ICameraPostProcessService>();

            builder.Register<CameraFxService>(RuntimeLifetime.Singleton)
                .AsSelf()
                .As<ICameraFxService>();

            var registration = builder.Register<CameraSystemService>(RuntimeLifetime.Singleton)
                .WithParameter(fx)
                .WithParameter(camTransform)
                .WithParameter(camera)
                .WithParameter(systemOptions)
                .AsSelf()
                .As<ICameraSystemService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            if (scope != null && scope.Kind == LifetimeScopeKind.Runtime)
            {
                registration.As<IScopeTickHandler>();
            }
            else if (runInLateUpdate)
            {
                registration.As<IScopeLateTickHandler>();
            }
            else
            {
                registration.As<IScopeTickHandler>();
            }
        }

        void Reset()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
            ValidateHierarchy();
            ValidateCamera();
            ValidateVolume();
        }

        void ResolveReferences()
        {
            if (cameraComponent == null)
                cameraComponent = GetComponentInChildren<Camera>();

            if (cameraTransform == null && cameraComponent != null)
                cameraTransform = cameraComponent.transform;

            if (fxTransform == null && cameraTransform != null && cameraTransform.parent != null)
                fxTransform = cameraTransform.parent;

            if (volume == null)
                volume = GetComponentInChildren<UnityEngine.Rendering.Volume>();
        }

        void ValidateHierarchy()
        {
            if (fxTransform == null || cameraTransform == null)
                return;

            if (cameraTransform.parent != fxTransform)
            { }
        }

        void ValidateCamera()
        {
            if (cameraComponent == null)
            {
                return;
            }

            if (!cameraComponent.orthographic)
            { }
        }

        void ValidateVolume()
        {
            if (volume == null)
            {
                return;
            }

            if (volume.sharedProfile == null)
            { }
        }

    }
}

