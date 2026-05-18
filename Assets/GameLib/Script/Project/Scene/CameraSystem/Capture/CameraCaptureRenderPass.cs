#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.CameraSystem
{
    public sealed class CameraCaptureRenderPass : ScriptableRenderPass
    {
        sealed class PassData
        {
            public TextureHandle Source;
            public Camera? Camera;
        }

        public CameraCaptureRenderPass(RenderPassEvent passEvent)
        {
            renderPassEvent = passEvent;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var camera = cameraData.camera;
            if (camera == null)
                return;

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;

            var handle = CameraCaptureRegistry.EnsureSource(camera, desc);
            var destination = renderGraph.ImportTexture(handle);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CameraCapture", out var passData))
            {
                passData.Source = resourceData.activeColorTexture;
                passData.Camera = camera;

                builder.UseTexture(passData.Source);
                builder.SetRenderAttachment(destination, 0);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                    if (data.Camera != null)
                        CameraCaptureRegistry.MarkCaptured(data.Camera, Time.frameCount);
                });
            }
        }
    }
}
