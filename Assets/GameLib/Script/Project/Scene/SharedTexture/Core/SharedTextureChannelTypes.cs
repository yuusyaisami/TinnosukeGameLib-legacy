#nullable enable
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Game.SharedTexture
{
    // ── SharedTextureSourceKind ──────────────────────────────────

    public enum SharedTextureSourceKind
    {
        Unknown = 0,
        CameraCapture = 10,
        ProcessorOutput = 20,
        ImportedTexture = 30,
        ExternalTexture = 40,
    }

    // ── SharedTextureDescriptor ─────────────────────────────────

    public readonly struct SharedTextureDescriptor : IEquatable<SharedTextureDescriptor>
    {
        public readonly int Width;
        public readonly int Height;
        public readonly GraphicsFormat Format;
        public readonly FilterMode FilterMode;
        public readonly TextureWrapMode WrapMode;
        public readonly int MsaaSamples;
        public readonly bool UseDynamicScale;
        public readonly bool Persistent;

        public SharedTextureDescriptor(
            int width,
            int height,
            GraphicsFormat format = GraphicsFormat.R8G8B8A8_UNorm,
            FilterMode filterMode = FilterMode.Bilinear,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp,
            int msaaSamples = 1,
            bool useDynamicScale = false,
            bool persistent = false)
        {
            Width = width;
            Height = height;
            Format = format;
            FilterMode = filterMode;
            WrapMode = wrapMode;
            MsaaSamples = msaaSamples;
            UseDynamicScale = useDynamicScale;
            Persistent = persistent;
        }

        public bool Equals(SharedTextureDescriptor other)
            => Width == other.Width
               && Height == other.Height
               && Format == other.Format
               && FilterMode == other.FilterMode
               && WrapMode == other.WrapMode
               && MsaaSamples == other.MsaaSamples
               && UseDynamicScale == other.UseDynamicScale
               && Persistent == other.Persistent;

        public override bool Equals(object? obj) => obj is SharedTextureDescriptor d && Equals(d);
        public override int GetHashCode() => HashCode.Combine(Width, Height, (int)Format, (int)FilterMode, MsaaSamples);
    }

    // ── SharedTextureCameraCaptureInfo ───────────────────────────

    public readonly struct SharedTextureCameraCaptureInfo
    {
        public readonly Camera CaptureCamera;
        public readonly Matrix4x4 ViewMatrix;
        public readonly Matrix4x4 ProjectionMatrix;
        public readonly Matrix4x4 ViewProjectionMatrix;
        public readonly Rect PixelRect;
        public readonly bool IsOrthographic;
        public readonly float OrthographicSize;
        public readonly float Aspect;
        public readonly int PixelWidth;
        public readonly int PixelHeight;

        public SharedTextureCameraCaptureInfo(Camera camera)
        {
            CaptureCamera = camera;
            ViewMatrix = camera.worldToCameraMatrix;
            ProjectionMatrix = camera.projectionMatrix;
            ViewProjectionMatrix = ProjectionMatrix * ViewMatrix;
            PixelRect = camera.pixelRect;
            IsOrthographic = camera.orthographic;
            OrthographicSize = camera.orthographicSize;
            Aspect = camera.aspect;
            PixelWidth = camera.pixelWidth;
            PixelHeight = camera.pixelHeight;
        }
    }

    // ── SharedTextureFrame ──────────────────────────────────────

    public readonly struct SharedTextureFrame
    {
        public readonly Texture? Texture;
        public readonly RenderTextureDescriptor Descriptor;
        public readonly int FrameId;
        public readonly string ProducerTag;
        public readonly SharedTextureSourceKind SourceKind;
        public readonly SharedTextureCameraCaptureInfo? CameraCapture;

        /// <summary>Descriptor.width の convenience accessor。</summary>
        public int Width => Descriptor.width;

        /// <summary>Descriptor.height の convenience accessor。</summary>
        public int Height => Descriptor.height;

        public SharedTextureFrame(
            Texture? texture,
            in RenderTextureDescriptor descriptor,
            int frameId,
            string producerTag,
            SharedTextureSourceKind sourceKind,
            in SharedTextureCameraCaptureInfo? cameraCapture)
        {
            Texture = texture;
            Descriptor = descriptor;
            FrameId = frameId;
            ProducerTag = producerTag;
            SourceKind = sourceKind;
            CameraCapture = cameraCapture;
        }
    }

    // ── SharedTexturePublishOptions ─────────────────────────────

    public readonly struct SharedTexturePublishOptions
    {
        public readonly string ProducerTag;
        public readonly SharedTextureSourceKind SourceKind;
        public readonly Camera? CaptureCamera;

        public SharedTexturePublishOptions(
            string producerTag,
            SharedTextureSourceKind sourceKind,
            Camera? captureCamera = null)
        {
            ProducerTag = producerTag;
            SourceKind = sourceKind;
            CaptureCamera = captureCamera;
        }

        public static SharedTexturePublishOptions ForCameraCapture(string producerTag, Camera camera)
            => new(producerTag, SharedTextureSourceKind.CameraCapture, camera);

        public static SharedTexturePublishOptions ForProcessor(string producerTag)
            => new(producerTag, SharedTextureSourceKind.ProcessorOutput);
    }

    // ── SharedTextureTagValidator ───────────────────────────────

    public static class SharedTextureTagValidator
    {
        static readonly Regex s_ValidPattern = new(@"^[a-z0-9][a-z0-9/.\-]*[a-z0-9]$", RegexOptions.Compiled);

        public static bool IsValid(string? tag)
        {
            if (string.IsNullOrEmpty(tag))
                return false;
            if (tag.Length == 1)
                return char.IsLetterOrDigit(tag[0]) && char.IsLower(tag[0]);
            return s_ValidPattern.IsMatch(tag);
        }
    }
}
