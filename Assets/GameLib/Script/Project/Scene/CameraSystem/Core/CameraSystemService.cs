#nullable enable
using UnityEngine;
using VContainer.Unity;
using Game.Times;
using Game.TransformSystem;

namespace Game.CameraSystem
{
    public sealed class CameraSystemService
        : ICameraSystemService,
          ITickable,
          ILateTickable,
          ITickPhase,
          IScopeAcquireHandler,
          IScopeReleaseHandler
    {
        readonly Transform _fx;
        readonly Transform _cameraTransform;
        readonly Camera _camera;
        readonly CameraSystemOptions _options;
        readonly CameraPostProcessService _postProcessService;
        readonly CameraZoomService _zoomService;
        readonly CameraFxService _fxService;

        bool _acquired;
        bool _disposed = false;

        public ICameraZoomService Zoom => _zoomService;
        public ICameraPostProcessService PostProcess => _postProcessService;
        public ICameraFxService Fx => _fxService;
        public string MoveChannelTag => _options.MoveChannelTag;
        public TickPhase Phase => _options.RunInLateUpdate ? TickPhase.Late : TickPhase.Default;

        public CameraSystemService(
            Transform fx,
            Transform cameraTransform,
            Camera camera,
            CameraPostProcessService postProcessService,
            CameraZoomService zoomService,
            CameraFxService fxService,
            CameraSystemOptions options)
        {
            _fx = fx;
            _cameraTransform = cameraTransform;
            _camera = camera;
            _postProcessService = postProcessService;
            _zoomService = zoomService;
            _fxService = fxService;
            _options = options;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _acquired = true;
            _postProcessService.Initialize();
            _zoomService.ResetBase(_camera.orthographicSize);
            _zoomService.SetClamp(_options.ZoomMinSize, _options.ZoomMaxSize);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_disposed)
                return;

            _acquired = false;
            ResetAll();
        }

        public void Tick()
        {
            TickInternal();
        }

        public void LateTick()
        {
            TickInternal();
        }

        void TickInternal()
        {
            if (!_acquired || _disposed)
                return;

            float dtScaled = Time.deltaTime;
            float dtUnscaled = Time.unscaledDeltaTime;
            float dt = _options.TimeScaleBehavior == TimeScaleBehavior.Unscaled ? dtUnscaled : dtScaled;

            _fxService.Tick(dtScaled, dtUnscaled);
            ApplyFx();

            _zoomService.Tick(dt);
            ApplyZoom();

            _postProcessService.Tick(dt);
        }

        void ApplyFx()
        {
            _fx.localPosition = _fxService.CurrentOffset;
            _fx.localRotation = Quaternion.Euler(0f, 0f, _fxService.CurrentRotationZ);
        }

        void ApplyZoom()
        {
            _camera.orthographicSize = _zoomService.Current;
        }

        public void ResetZoom()
        {
            _zoomService.ResetToBase(immediate: true);
            _camera.orthographicSize = _zoomService.BaseZoom;
        }

        public void ResetPostProcess()
        {
            _postProcessService.ResetToBase(immediate: true);
        }

        public void ResetFx()
        {
            _fxService.StopAll(0f);
            _fx.localPosition = Vector3.zero;
            _fx.localRotation = Quaternion.identity;
        }

        public void ResetAll()
        {
            ResetZoom();
            ResetPostProcess();
            ResetFx();
        }
    }
}
