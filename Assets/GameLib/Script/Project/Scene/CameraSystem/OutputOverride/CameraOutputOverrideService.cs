#nullable enable
using Game;
using Game.SharedTexture;
using UnityEngine;

namespace Game.CameraSystem
{
    public sealed class CameraOutputOverrideService
        : ICameraOutputOverrideService,
          IScopeAcquireHandler,
          IScopeReleaseHandler
    {
        readonly ISharedTextureChannelHub _hub;

        bool _acquired;

        public bool Enabled { get; set; }
        public CameraOutputOverrideMode Mode { get; set; }
        public string SharedTextureTag { get; set; } = string.Empty;

        public CameraOutputOverrideService(
            ISharedTextureChannelHub hub,
            CameraOutputOverrideOptions options)
        {
            _hub = hub;
            Enabled = options.Enabled;
            Mode = options.Mode;
            SharedTextureTag = options.SharedTextureTag;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquired = true;
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _acquired = false;
        }

        public Texture? ResolveOverrideTexture()
        {
            if (!_acquired || !Enabled)
                return null;

            if (Mode != CameraOutputOverrideMode.SharedTexture)
                return null;

            if (string.IsNullOrEmpty(SharedTextureTag))
                return null;

            if (_hub.TryGet(SharedTextureTag, out var frame) && frame.Texture != null)
                return frame.Texture;

            return null;
        }
    }

    // ── Options ─────────────────────────────────────────────────

    public readonly struct CameraOutputOverrideOptions
    {
        public readonly bool Enabled;
        public readonly CameraOutputOverrideMode Mode;
        public readonly string SharedTextureTag;

        public CameraOutputOverrideOptions(bool enabled, CameraOutputOverrideMode mode, string sharedTextureTag)
        {
            Enabled = enabled;
            Mode = mode;
            SharedTextureTag = sharedTextureTag;
        }
    }
}
