#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.CameraSystem
{
    /// <summary>
    /// Camera の最終出力を SharedTexture で差し替える Render Pass。
    /// CameraOutputOverrideRegistry から差し替え Texture を取得する。
    /// </summary>
    public sealed class CameraOutputOverrideRenderPass : ScriptableRenderPass
    {
        RTHandle? _cachedHandle;
        Texture? _cachedTexture;

        public CameraOutputOverrideRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRendering;
        }

#pragma warning disable CS0618
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            var overrideTex = CameraOutputOverrideRegistry.GetOverrideTexture(camera);
            if (overrideTex == null)
                return;

            // Cache RTHandle to avoid per-frame allocation
            if (_cachedTexture != overrideTex)
            {
                _cachedHandle?.Release();
                _cachedHandle = RTHandles.Alloc(overrideTex);
                _cachedTexture = overrideTex;
            }

            if (_cachedHandle == null)
                return;

            var cmd = CommandBufferPool.Get("CameraOutputOverride");
            var cameraColorHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            Blitter.BlitCameraTexture(cmd, _cachedHandle, cameraColorHandle);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore CS0618
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

            // Only add pass if there's an override registered for this camera
            if (!CameraOutputOverrideRegistry.HasOverride(cameraData.camera))
                return;

            if (_pass == null)
                Create();

            if (_pass != null)
                renderer.EnqueuePass(_pass);
        }
    }
}
