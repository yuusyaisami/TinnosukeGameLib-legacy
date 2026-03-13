#nullable enable
using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

namespace Game.SharedTexture
{
    public sealed class SharedTextureChannelHubService
        : ISharedTextureChannelHub,
          IScopeAcquireHandler,
          IScopeReleaseHandler,
          IDisposable
    {
        readonly struct ChannelEntry
        {
            public readonly SharedTextureFrame Frame;
            public readonly RenderTexture? OwnedRT;

            public ChannelEntry(in SharedTextureFrame frame, RenderTexture? ownedRT)
            {
                Frame = frame;
                OwnedRT = ownedRT;
            }
        }

        readonly Dictionary<string, ChannelEntry> _channels = new();
        readonly Dictionary<string, List<string>> _producerToTags = new();

        int _frameCounter;
        bool _acquired;

        public int ChannelCount => _channels.Count;

        // ── Lifecycle ───────────────────────────────────────────

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _acquired = true;
            if (isReset)
                ClearAll();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _acquired = false;
            ClearAll();
        }

        public void Dispose()
        {
            ClearAll();
        }

        // ── Publish ─────────────────────────────────────────────

        public bool Publish(
            string tag,
            Texture texture,
            in SharedTextureDescriptor descriptor,
            in SharedTexturePublishOptions options)
        {
            if (!_acquired)
                return false;

            if (!SharedTextureTagValidator.IsValid(tag))
            {
                Debug.LogWarning($"[SharedTextureHub] Invalid tag: '{tag}'");
                return false;
            }

            if (texture == null)
                return false;

            // 同一タグに別 Producer が publish した場合は警告
            if (_channels.TryGetValue(tag, out var existing)
                && existing.Frame.ProducerTag != options.ProducerTag)
            {
                Debug.LogWarning(
                    $"[SharedTextureHub] Tag '{tag}' overwritten by producer '{options.ProducerTag}' " +
                    $"(was '{existing.Frame.ProducerTag}')");
            }

            _frameCounter++;

            SharedTextureCameraCaptureInfo? captureInfo = null;
            if (options.SourceKind == SharedTextureSourceKind.CameraCapture && options.CaptureCamera != null)
                captureInfo = new SharedTextureCameraCaptureInfo(options.CaptureCamera);

            var rtDesc = texture is RenderTexture rt
                ? rt.descriptor
                : new RenderTextureDescriptor(texture.width, texture.height);

            var frame = new SharedTextureFrame(
                texture,
                rtDesc,
                _frameCounter,
                options.ProducerTag,
                options.SourceKind,
                captureInfo);

            _channels[tag] = new ChannelEntry(frame, null);

            // Producer → Tag マッピングを管理
            if (!_producerToTags.TryGetValue(options.ProducerTag, out var tagList))
            {
                tagList = new List<string>();
                _producerToTags[options.ProducerTag] = tagList;
            }
            if (!tagList.Contains(tag))
                tagList.Add(tag);

            return true;
        }

        // ── Read ────────────────────────────────────────────────

        public bool TryGet(string tag, out SharedTextureFrame frame)
        {
            if (_channels.TryGetValue(tag, out var entry))
            {
                frame = entry.Frame;
                return frame.Texture != null;
            }
            frame = default;
            return false;
        }

        public bool Contains(string tag) => _channels.ContainsKey(tag);

        // ── Remove ──────────────────────────────────────────────

        public bool Remove(string tag, string producerTag)
        {
            if (!_channels.TryGetValue(tag, out var entry))
                return false;

            if (entry.Frame.ProducerTag != producerTag)
                return false;

            ReleaseEntry(entry);
            _channels.Remove(tag);

            if (_producerToTags.TryGetValue(producerTag, out var tagList))
                tagList.Remove(tag);

            return true;
        }

        public void ClearByProducer(string producerTag)
        {
            if (!_producerToTags.TryGetValue(producerTag, out var tagList))
                return;

            foreach (var tag in tagList)
            {
                if (_channels.TryGetValue(tag, out var entry))
                {
                    ReleaseEntry(entry);
                    _channels.Remove(tag);
                }
            }

            tagList.Clear();
            _producerToTags.Remove(producerTag);
        }

        public void ClearAll()
        {
            foreach (var kvp in _channels)
                ReleaseEntry(kvp.Value);

            _channels.Clear();
            _producerToTags.Clear();
        }

        // ── Internal ────────────────────────────────────────────

        static void ReleaseEntry(in ChannelEntry entry)
        {
            if (entry.OwnedRT != null && entry.OwnedRT.IsCreated())
            {
                entry.OwnedRT.Release();
                UnityEngine.Object.Destroy(entry.OwnedRT);
            }
        }
    }
}
