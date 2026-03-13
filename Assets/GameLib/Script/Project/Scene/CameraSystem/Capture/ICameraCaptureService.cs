#nullable enable
using UnityEngine;

namespace Game.CameraSystem
{
    public interface ICameraCaptureService
    {
        bool IsCapturing { get; }
        string ChannelTag { get; }
        Texture? CurrentTexture { get; }
    }
}
