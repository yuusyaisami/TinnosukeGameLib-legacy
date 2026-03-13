#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.CameraSystem
{
    /// <summary>
    /// Render Pipeline と CameraCaptureService を橋渡しする static レジストリ。
    /// CameraCaptureRenderPass が書き込み、CameraCaptureService が読み取る。
    /// </summary>
    public static class CameraCaptureRegistry
    {
        sealed class Entry
        {
            public RTHandle? Source;
            public RenderTextureDescriptor Descriptor;
            public int FrameId;
        }

        static readonly Dictionary<Camera, Entry> s_Entries = new();

        // ── Render Pass 側 ──────────────────────────────────────

        public static RTHandle EnsureSource(Camera camera, in RenderTextureDescriptor desc)
        {
            if (!s_Entries.TryGetValue(camera, out var entry))
            {
                entry = new Entry();
                s_Entries[camera] = entry;
            }

            if (entry.Source != null
                && entry.Descriptor.width == desc.width
                && entry.Descriptor.height == desc.height
                && entry.Descriptor.graphicsFormat == desc.graphicsFormat)
            {
                return entry.Source;
            }

            entry.Source?.Release();
            entry.Source = RTHandles.Alloc(
                desc.width, desc.height,
                depthBufferBits: DepthBits.None,
                colorFormat: desc.graphicsFormat,
                filterMode: FilterMode.Bilinear,
                wrapMode: TextureWrapMode.Clamp,
                name: $"CameraCapture_{camera.name}");
            entry.Descriptor = desc;

            return entry.Source;
        }

        public static void MarkCaptured(Camera camera, int frameId)
        {
            if (s_Entries.TryGetValue(camera, out var entry))
                entry.FrameId = frameId;
        }

        // ── Service 側 ──────────────────────────────────────────

        public static bool TryGet(Camera camera, out Texture? texture, out RenderTextureDescriptor descriptor, out int frameId)
        {
            if (s_Entries.TryGetValue(camera, out var entry) && entry.Source != null)
            {
                texture = entry.Source.rt;
                descriptor = entry.Descriptor;
                frameId = entry.FrameId;
                return texture != null;
            }
            texture = null;
            descriptor = default;
            frameId = 0;
            return false;
        }

        // ── Cleanup ─────────────────────────────────────────────

        public static void Release(Camera camera)
        {
            if (!s_Entries.TryGetValue(camera, out var entry))
                return;

            entry.Source?.Release();
            entry.Source = null;
            s_Entries.Remove(camera);
        }

        public static void ReleaseAll()
        {
            foreach (var kvp in s_Entries)
                kvp.Value.Source?.Release();
            s_Entries.Clear();
        }
    }
}
