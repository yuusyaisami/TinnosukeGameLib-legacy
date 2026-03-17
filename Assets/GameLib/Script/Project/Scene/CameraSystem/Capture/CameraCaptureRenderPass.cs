#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.CameraSystem
{
    public sealed class CameraCaptureRenderPass : ScriptableRenderPass
    {
        public CameraCaptureRenderPass(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }

        [Obsolete]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;

            CameraCaptureRegistry.EnsureSource(renderingData.cameraData.camera, desc);
        }

        [Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            if (!CameraCaptureRegistry.TryGet(camera, out _, out _, out _))
                return;

            // RTHandle を再取得
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            var handle = CameraCaptureRegistry.EnsureSource(camera, desc);

            var cmd = CommandBufferPool.Get("CameraCapture");
            Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, handle);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            CameraCaptureRegistry.MarkCaptured(camera, Time.frameCount);
        }
    }
}
