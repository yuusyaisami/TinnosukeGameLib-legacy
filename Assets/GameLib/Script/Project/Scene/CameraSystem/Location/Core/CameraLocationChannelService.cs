#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.CameraSystem
{
    public interface ICameraLocationPlayerRuntime
    {
        string Tag { get; }
        Camera? Camera { get; }
        ICameraSystemService? CameraSystemService { get; }
        int Version { get; }
        CameraLocationChannelDefinition? Source { get; }
    }

    public interface ICameraLocationChannelService
    {
        int ChannelCount { get; }
        IReadOnlyList<ICameraLocationPlayerRuntime> Runtimes { get; }
        bool TryGetRuntime(string tag, out ICameraLocationPlayerRuntime? runtime);
        bool TryGetCamera(string tag, out Camera? camera);
        bool TryGetCameraSystemService(string tag, out ICameraSystemService? cameraSystemService);
    }

    sealed class CameraLocationPlayerRuntime : ICameraLocationPlayerRuntime
    {
        public string Tag { get; private set; }
        public Camera? Camera { get; private set; }
        public ICameraSystemService? CameraSystemService { get; private set; }
        public int Version { get; private set; }
        public CameraLocationChannelDefinition? Source { get; private set; }

        public CameraLocationPlayerRuntime(
            string tag,
            Camera? camera,
            ICameraSystemService? cameraSystemService,
            CameraLocationChannelDefinition? source,
            int version)
        {
            Tag = tag;
            Camera = camera;
            CameraSystemService = cameraSystemService;
            Source = source;
            Version = version;
        }

        public bool Update(string tag, Camera? camera, ICameraSystemService? cameraSystemService, CameraLocationChannelDefinition? source)
        {
            var changed = !string.Equals(Tag, tag, StringComparison.Ordinal)
                || !ReferenceEquals(Camera, camera)
                || !ReferenceEquals(CameraSystemService, cameraSystemService)
                || !ReferenceEquals(Source, source);

            if (!changed)
                return false;

            Tag = tag;
            Camera = camera;
            CameraSystemService = cameraSystemService;
            Source = source;
            if (Version == int.MaxValue)
                Version = 1;
            else
                Version++;
            return true;
        }
    }

    public sealed class CameraLocationChannelService : ICameraLocationChannelService, IScopeAcquireHandler, IScopeReleaseHandler, ITickable
    {
        const string DefaultTag = "default";
        const string MainTag = "main";

        readonly IScopeNode _scope;
        readonly CameraLocationChannelMB _mb;
        readonly Dictionary<string, CameraLocationPlayerRuntime> _runtimes = new(StringComparer.Ordinal);
        readonly List<ICameraLocationPlayerRuntime> _ordered = new();

        bool _acquired;

        public CameraLocationChannelService(IScopeNode scope, CameraLocationChannelMB mb)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
        }

        public int ChannelCount => _ordered.Count;

        public IReadOnlyList<ICameraLocationPlayerRuntime> Runtimes => _ordered;

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _acquired = true;
            RefreshChannels();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _acquired = false;
            _runtimes.Clear();
            _ordered.Clear();
        }

        public void Tick()
        {
            if (!_acquired)
                return;

            RefreshChannels();
        }

        public bool TryGetRuntime(string tag, out ICameraLocationPlayerRuntime? runtime)
        {
            runtime = null;
            var candidates = BuildCandidateTags(tag);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (_runtimes.TryGetValue(candidates[i], out var found) && found != null)
                {
                    runtime = found;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetCamera(string tag, out Camera? camera)
        {
            camera = null;
            if (!TryGetRuntime(tag, out var runtime) || runtime == null)
                return false;

            if (runtime.Camera != null)
            {
                camera = runtime.Camera;
                return true;
            }

            if (runtime.CameraSystemService?.Camera != null)
            {
                camera = runtime.CameraSystemService.Camera;
                return true;
            }

            return false;
        }

        public bool TryGetCameraSystemService(string tag, out ICameraSystemService? cameraSystemService)
        {
            cameraSystemService = null;
            if (TryGetRuntime(tag, out var runtime) && runtime != null && runtime.CameraSystemService != null)
            {
                cameraSystemService = runtime.CameraSystemService;
                return true;
            }

            return TryResolveCameraSystemService(out cameraSystemService);
        }

        void RefreshChannels()
        {
            var cameraSystemService = TryResolveCameraSystemService(out var resolvedSystemService)
                ? resolvedSystemService
                : null;

            var fallbackCamera = ResolveFallbackCamera(cameraSystemService);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;

            var channels = _mb.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                if (channel == null)
                    continue;

                var tag = NormalizeTag(channel.ChannelTag);
                if (!seen.Add(tag))
                {
                    Debug.LogWarning($"[CameraLocationChannelService] Duplicate camera channel tag '{tag}' was skipped.", _mb);
                    continue;
                }

                var camera = channel.Camera ?? fallbackCamera;
                if (camera == null)
                    continue;

                changed |= UpsertRuntime(tag, camera, cameraSystemService, channel);
            }

            if (!seen.Contains(DefaultTag) && fallbackCamera != null)
            {
                changed |= UpsertRuntime(DefaultTag, fallbackCamera, cameraSystemService, null);
                seen.Add(DefaultTag);
            }

            if (!seen.Contains(MainTag) && fallbackCamera != null)
            {
                changed |= UpsertRuntime(MainTag, fallbackCamera, cameraSystemService, null);
                seen.Add(MainTag);
            }

            changed |= RemoveMissingRuntimes(seen);

            if (changed)
                RebuildOrderedList();
        }

        bool UpsertRuntime(string tag, Camera? camera, ICameraSystemService? cameraSystemService, CameraLocationChannelDefinition? source)
        {
            if (string.IsNullOrWhiteSpace(tag))
                tag = DefaultTag;

            if (_runtimes.TryGetValue(tag, out var runtime) && runtime != null)
                return runtime.Update(tag, camera, cameraSystemService, source);

            _runtimes[tag] = new CameraLocationPlayerRuntime(tag, camera, cameraSystemService, source, version: 1);
            return true;
        }

        bool RemoveMissingRuntimes(HashSet<string> seen)
        {
            if (_runtimes.Count == 0)
                return false;

            bool removed = false;
            var keys = new List<string>(_runtimes.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (seen.Contains(key))
                    continue;

                _runtimes.Remove(key);
                removed = true;
            }

            return removed;
        }

        void RebuildOrderedList()
        {
            _ordered.Clear();
            if (_runtimes.Count == 0)
                return;

            var list = new List<CameraLocationPlayerRuntime>(_runtimes.Values);
            list.Sort(static (a, b) => string.CompareOrdinal(a.Tag, b.Tag));
            for (int i = 0; i < list.Count; i++)
                _ordered.Add(list[i]);
        }

        bool TryResolveCameraSystemService(out ICameraSystemService? cameraSystemService)
        {
            cameraSystemService = null;
            if (_scope.Resolver != null && _scope.Resolver.TryResolve<ICameraSystemService>(out var resolved) && resolved != null)
            {
                cameraSystemService = resolved;
                return true;
            }

            return false;
        }

        static List<string> BuildCandidateTags(string? tag)
        {
            var candidates = new List<string>(2);
            if (string.IsNullOrWhiteSpace(tag))
            {
                candidates.Add(DefaultTag);
                candidates.Add(MainTag);
                return candidates;
            }

            var normalized = NormalizeTag(tag);
            candidates.Add(normalized);
            if (string.Equals(normalized, DefaultTag, StringComparison.Ordinal))
                candidates.Add(MainTag);
            else if (string.Equals(normalized, MainTag, StringComparison.Ordinal))
                candidates.Add(DefaultTag);

            return candidates;
        }

        static string NormalizeTag(string? tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? DefaultTag : tag.Trim();
        }

        static Camera? ResolveFallbackCamera(ICameraSystemService? cameraSystemService)
        {
            if (cameraSystemService?.Camera != null)
                return cameraSystemService.Camera;

            if (Camera.main != null)
                return Camera.main;

            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (cameras != null && cameras.Length > 0)
                return cameras[0];

            return null;
        }
    }
}
