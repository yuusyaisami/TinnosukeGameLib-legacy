#nullable enable
using Game;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using VContainer.Unity;
using Game.SharedTexture;

namespace Game.CameraSystem
{
    public sealed class CameraCaptureService
        : ICameraCaptureService,
          IScopeAcquireHandler,
          IScopeReleaseHandler,
          ITickable
    {
        readonly ICameraRenderContext _renderContext;
        readonly ISharedTextureChannelHub _hub;
        readonly CameraCaptureOptions _options;

        string _producerTag = string.Empty;
        string _channelTag = string.Empty;
        int _lastPublishedFrame;
        bool _acquired;

        public bool IsCapturing => _acquired;
        public string ChannelTag => _channelTag;
        public Texture? CurrentTexture { get; private set; }

        public CameraCaptureService(
            ICameraRenderContext renderContext,
            ISharedTextureChannelHub hub,
            CameraCaptureOptions options)
        {
            _renderContext = renderContext;
            _hub = hub;
            _options = options;
        }

        // ── Lifecycle ───────────────────────────────────────────

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquired = true;
            var cameraTag = _renderContext.CameraTag;
            _channelTag = $"camera/{cameraTag}/source";
            _producerTag = $"camera-capture/{cameraTag}";
            _lastPublishedFrame = -1;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _acquired = false;
            CurrentTexture = null;

            if (!string.IsNullOrEmpty(_producerTag))
                _hub.ClearByProducer(_producerTag);
        }

        // ── Tick ────────────────────────────────────────────────

        public void Tick()
        {
            if (!_acquired)
                return;

            var camera = _renderContext.Camera;
            if (camera == null)
                return;

            if (!CameraCaptureRegistry.TryGet(camera, out var texture, out var descriptor, out var frameId))
                return;

            // 同じフレームの publish は skip
            if (frameId == _lastPublishedFrame)
                return;

            _lastPublishedFrame = frameId;
            CurrentTexture = texture;

            var sharedDesc = new SharedTextureDescriptor(
                descriptor.width,
                descriptor.height,
                descriptor.graphicsFormat,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                descriptor.msaaSamples);

            var publishOptions = SharedTexturePublishOptions.ForCameraCapture(_producerTag, camera);
            _hub.Publish(_channelTag, texture!, sharedDesc, publishOptions);
        }
    }

    // ── Options ─────────────────────────────────────────────────

    public readonly struct CameraCaptureOptions
    {
        public readonly float ResolutionScale;

        public CameraCaptureOptions(float resolutionScale = 1.0f)
        {
            ResolutionScale = Mathf.Clamp(resolutionScale, 0.1f, 1.0f);
        }
    }
}
