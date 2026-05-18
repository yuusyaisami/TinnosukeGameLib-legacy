#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.CameraSystem
{
    /// <summary>
    /// Camera output override texture to active color target bridge.
    /// </summary>
    public sealed class CameraOutputOverrideRenderPass : ScriptableRenderPass
    {
        sealed class PassData
        {
            public TextureHandle Source;
        }

        RTHandle? _cachedHandle;
        Texture? _cachedTexture;

        public CameraOutputOverrideRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRendering;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var camera = cameraData.camera;
            if (camera == null)
                return;

            var overrideTex = CameraOutputOverrideRegistry.GetOverrideTexture(camera);
            if (overrideTex == null)
                return;

            if (_cachedTexture != overrideTex)
            {
                _cachedHandle?.Release();
                _cachedHandle = RTHandles.Alloc(overrideTex);
                _cachedTexture = overrideTex;
            }

            if (_cachedHandle == null)
                return;

            var source = renderGraph.ImportTexture(_cachedHandle);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CameraOutputOverride", out var passData))
            {
                passData.Source = source;

                builder.UseTexture(passData.Source);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), 0f, false);
                });
            }
        }
    }

    [DisallowMultipleRendererFeature("Camera Output Override")]
    public sealed class CameraOutputOverrideRendererFeature : ScriptableRendererFeature
    {
        CameraOutputOverrideRenderPass? _pass;

        public override void Create()
        {
            _pass = new CameraOutputOverrideRenderPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (cameraData.cameraType != CameraType.Game)
                return;
            if (cameraData.renderType == CameraRenderType.Overlay)
                return;

            if (!CameraOutputOverrideRegistry.HasOverride(cameraData.camera))
                return;

            if (_pass == null)
                Create();

            if (_pass != null)
                renderer.EnqueuePass(_pass);
        }
    }
}
