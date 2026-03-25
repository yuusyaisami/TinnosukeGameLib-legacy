#nullable enable
// Game.Targeting
// ================================================================================
// TargetChannelHubService - Tag -> TargetChannelRuntime の管理ハブ
// ================================================================================
//
// ・TargetChannelRuntime をタグで管理し、必要に応じて生成/取得/破棄を行う。
// ・単一 Owner（Transform/FootTransformMB/EntityLifetimeScope）に紐づく前提。
// ・MainThread でのみ操作する想定。
// ================================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Common;
using Game.DI;
using Game.Search;
using VContainer;

namespace Game.Targeting
{
    /// <summary>
    /// Tag -> TargetChannelRuntime を管理する Hub 実装。
    /// </summary>
    public sealed class TargetChannelHubService :
        ITargetChannelHub,
        IDisposable,
        IResettableService,
        IEnabledService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly TargetChannelHubConfig _config;
        readonly IDynamicSearchService? _search;     // 検索サービス（DI）
        readonly TargetChannelOwner _owner;         // Hub が扱う Owner 情報

        readonly Dictionary<string, ITargetChannelRuntime> _channels; // Tag -> Runtime
        bool _disposed;
        bool _enabled = true;
        bool _initialized;

        public TargetChannelHubService(
            TargetChannelHubConfig config,
            in TargetChannelOwner owner,
            IDynamicSearchService? search,
            int initialCapacity = 8)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _search = search;
            _owner = owner;

            _channels = new Dictionary<string, ITargetChannelRuntime>(
                Mathf.Max(0, initialCapacity),
                StringComparer.Ordinal);
        }

        public int ChannelCount => _channels.Count;

        /// <inheritdoc/>
        public bool IsEnabled => !_disposed && _enabled;

        public bool TryGetRuntime(string tag, out ITargetChannelRuntime runtime)
        {
            if (!IsEnabled)
            {
                runtime = null!;
                return false;
            }

            if (!string.IsNullOrEmpty(tag) && _channels.TryGetValue(tag, out var r))
            {
                runtime = r;
                return true;
            }
            runtime = null!;
            return false;
        }

        public ITargetChannelRuntime RegisterOrReplace(TargetChannelPreset preset)
        {
            MainThread.AssertMainThread();

            if (_disposed) throw new ObjectDisposedException(nameof(TargetChannelHubService));
            if (!IsEnabled) throw new InvalidOperationException("TargetChannelHubService is disabled.");
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (string.IsNullOrEmpty(preset.Tag)) throw new ArgumentException("TargetChannelPreset.Tag is null or empty.", nameof(preset));

            var runtime = CreateRuntime(preset);
            _channels[preset.Tag] = runtime;
            return runtime;
        }

        public bool SwapPreset(string tag, TargetChannelPreset preset)
        {
            MainThread.AssertMainThread();

            if (_disposed || string.IsNullOrWhiteSpace(tag) || preset == null)
                return false;

            if (!_channels.TryGetValue(tag, out var runtime) || runtime == null)
                return false;

            if (!string.Equals(tag, preset.Tag, StringComparison.Ordinal))
                return false;

            return runtime.SwapPreset(preset);
        }

        public bool MutateSettings(string tag, TargetChannelRuntimeMutation mutation)
        {
            MainThread.AssertMainThread();

            if (_disposed || string.IsNullOrWhiteSpace(tag) || mutation == null)
                return false;

            return _channels.TryGetValue(tag, out var runtime) &&
                   runtime != null &&
                   runtime.MutateSettings(mutation);
        }

        public bool ResetRuntimeOverrides(string tag)
        {
            MainThread.AssertMainThread();

            if (_disposed || string.IsNullOrWhiteSpace(tag))
                return false;

            return _channels.TryGetValue(tag, out var runtime) &&
                   runtime != null &&
                   runtime.ResetRuntimeOverrides();
        }

        public bool SetDirectTargets(string tag, IReadOnlyList<DynamicSearchHit> hits)
        {
            MainThread.AssertMainThread();

            if (_disposed || string.IsNullOrWhiteSpace(tag))
                return false;

            return _channels.TryGetValue(tag, out var runtime) &&
                   runtime != null &&
                   runtime.SetDirectTargets(hits);
        }

        public bool ClearDirectTargets(string tag)
        {
            MainThread.AssertMainThread();

            if (_disposed || string.IsNullOrWhiteSpace(tag))
                return false;

            return _channels.TryGetValue(tag, out var runtime) &&
                   runtime != null &&
                   runtime.ClearDirectTargets();
        }

        public bool Unregister(string tag)
        {
            MainThread.AssertMainThread();

            if (_disposed) return false;
            if (string.IsNullOrEmpty(tag)) return false;

            return _channels.Remove(tag);
        }

        public void Clear()
        {
            MainThread.AssertMainThread();

            if (_disposed) return;
            _channels.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _initialized = false;
            _channels.Clear(); // Runtime 自体は軽量なため破棄のみ
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _disposed = false;
            _enabled = true;
            _initialized = false;
            _channels.Clear();
        }

        /// <inheritdoc/>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _initialized = false;
                _channels.Clear();
            }
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            _ = scope;
            if (_disposed || !_enabled)
                return;

            if (isReset)
                _initialized = false;

            if (_initialized || !_config.AutoInitializeOnStart)
                return;

            _initialized = true;
            RegisterInitialChannels();
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;
            _initialized = false;
            Clear();
        }

        void RegisterInitialChannels()
        {
            var list = _config.InitialChannels;
            if (list == null || list.Count == 0)
                return;

            var vars = ResolveVars(_owner.OwnerScope);
            var context = new SimpleDynamicContext(vars, _owner.OwnerScope);

            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].TryGet(context, out TargetChannelPreset? preset) || preset == null)
                    continue;

                if (string.IsNullOrEmpty(preset.Tag))
                    continue;

                RegisterOrReplace(preset);
            }
        }

        ITargetChannelRuntime CreateRuntime(TargetChannelPreset preset)
        {
            if (preset.SearchType == TargetChannelSearchType.DynamicSearch && _search == null)
            {
                Debug.LogError($"[TargetChannelHubService] IDynamicSearchService not found for '{preset.Tag}'.");
                return new NullTargetChannelRuntime(preset);
            }

            return new TargetChannelRuntime(_search, _owner, preset);
        }

        static IVarStore ResolveVars(IScopeNode? scope)
        {
            if (scope?.Resolver != null &&
                scope.Resolver.TryResolve<IVarStore>(out var vars) &&
                vars != null)
            {
                return vars;
            }

            return NullVarStore.Instance;
        }
    }
}
