#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.CameraSystem
{
    [DisallowMultipleRendererFeature("Camera Capture")]
    public sealed class CameraCaptureRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public RenderPassEvent captureEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public Settings settings = new();

        CameraCaptureRenderPass? _capturePass;

        public override void Create()
        {
            _capturePass = new CameraCaptureRenderPass(settings.captureEvent);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
                return;
            if (cameraData.renderType == CameraRenderType.Overlay)
                return;

            if (_capturePass == null)
                Create();

            if (_capturePass != null)
                renderer.EnqueuePass(_capturePass);
        }
    }
}
