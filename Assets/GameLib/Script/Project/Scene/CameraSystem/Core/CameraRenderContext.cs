#nullable enable
using UnityEngine;

namespace Game.CameraSystem
{
    public sealed class CameraRenderContext : ICameraRenderContext
    {
        public Camera Camera { get; }
        public Transform CameraTransform { get; }
        public Transform FxTransform { get; }
        public string CameraTag { get; }

        public CameraRenderContext(
            Camera camera,
            Transform cameraTransform,
            Transform fxTransform,
            string cameraTag)
        {
            Camera = camera;
            CameraTransform = cameraTransform;
            FxTransform = fxTransform;
            CameraTag = cameraTag;
        }
    }
}
