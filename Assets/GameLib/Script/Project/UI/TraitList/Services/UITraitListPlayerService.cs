#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using Game.Commands.VNext;
using Game.Common;
using VContainer;
using Game.Trait;
using UnityEngine;
using VContainer.Unity;

namespace Game.UI.TraitList
{
    public interface IUITraitListPlayerService
    {
        UITraitListRuntime? CurrentRuntime { get; }
        ITraitHolderService? BoundHolder { get; }
        string BoundHolderKey { get; }

        UniTask<UITraitListRuntime?> BuildAsync(
            UITraitListProfileSO profile,
            ITraitHolderService holder,
            string holderKey,
            UITraitListRange range,
            Transform parent,
            IScopeNode scopeParent,
            CancellationToken ct);

        UniTask RefreshAsync(UITraitListRefreshMode mode, CancellationToken ct);
        UniTask SetRangeAsync(UITraitListRange range, bool rebuild, CancellationToken ct);
        UniTask ClearAsync(bool keepBinding, CancellationToken ct);

        bool TryResolveInstanceByRowColumn(int row, int column, out ITraitInstance? instance);
    }

    public sealed class UITraitListPlayerService :
        IUITraitListPlayerService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode _owner;
        readonly IUITraitListBuilderService _builder;
        readonly IUITraitListSystemOptions _options;
        readonly ICommandRunner _runner;

        ITraitHolderService? _boundHolder;
        string _boundHolderKey = string.Empty;

        public UITraitListRuntime? CurrentRuntime => _builder.CurrentRuntime;
        public ITraitHolderService? BoundHolder => _boundHolder;
        public string BoundHolderKey => _boundHolderKey;

        public UITraitListPlayerService(
            IScopeNode owner,
            IUITraitListBuilderService builder,
            IUITraitListSystemOptions options,
            ICommandRunner runner)
        {
            _owner = owner;
            _builder = builder;
            _options = options;
            _runner = runner;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!_options.AutoBuildOnAcquire || _options.DefaultProfile == null)
                return;

            var parent = _options.DefaultParentTransform != null
                ? _options.DefaultParentTransform
                : (_owner as Component)?.transform;
            if (parent == null)
                return;

            UniTask.Void(async () =>
            {
                try
                {
                    var ctx = new CommandContext(_owner, new VarStore(), _runner);
                    var (hubScope, error) = await ActorScopeResolver.ResolveAsync(_options.HolderHubSource, ctx, CancellationToken.None);
                    if (hubScope == null)
                    {
                        var msg = string.IsNullOrEmpty(error) ? "HolderHubSource could not be resolved." : error;
                        Debug.LogError($"[UITraitListPlayer] AutoBuild failed: {msg}");
                        return;
                    }

                    EnsureScopeBuiltIfNeeded(hubScope);
                    if (hubScope.Resolver == null ||
                        !hubScope.Resolver.TryResolve<ITraitHolderHubService>(out var hub) ||
                        hub == null)
                    {
                        Debug.LogError("[UITraitListPlayer] AutoBuild failed: TraitHolderHubService not found.");
                        return;
                    }

                    ITraitHolderService? holder = null;
                    var retryCount = Mathf.Max(0, _options.AutoBuildRetryCount);
                    var retryFrames = Mathf.Max(1, _options.AutoBuildRetryFrameInterval);
                    for (int attempt = 0; attempt <= retryCount; attempt++)
                    {
                        if (hub.TryGetHolder(_options.HolderKey, out holder) && holder != null)
                            break;

                        if (attempt < retryCount)
                            await UniTask.DelayFrame(retryFrames, cancellationToken: CancellationToken.None);
                    }

                    if (holder == null)
                    {
                        Debug.LogError($"[UITraitListPlayer] AutoBuild failed: Holder '{_options.HolderKey}' not found.");
                        return;
                    }

                    await BuildAsync(
                        _options.DefaultProfile,
                        holder,
                        _options.HolderKey,
                        _options.AutoBuildRange,
                        parent,
                        _owner,
                        CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UITraitListPlayer] AutoBuild failed: {ex.Message}");
                }
            });
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            UnbindHolder();
            UniTask.Void(async () =>
            {
                try
                {
                    await _builder.ClearAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UITraitListPlayer] Clear failed: {ex.Message}");
                }
            });
        }

        public async UniTask<UITraitListRuntime?> BuildAsync(
            UITraitListProfileSO profile,
            ITraitHolderService holder,
            string holderKey,
            UITraitListRange range,
            Transform parent,
            IScopeNode scopeParent,
            CancellationToken ct)
        {
            var runtime = await _builder.BuildAsync(profile, holder, holderKey, range, parent, scopeParent, ct);
            if (runtime != null)
                BindHolder(holder, holderKey);
            return runtime;
        }

        public UniTask RefreshAsync(UITraitListRefreshMode mode, CancellationToken ct)
        {
            return _builder.RefreshAsync(mode, ct);
        }

        public UniTask SetRangeAsync(UITraitListRange range, bool rebuild, CancellationToken ct)
        {
            return _builder.SetRangeAsync(range, rebuild, ct);
        }

        public async UniTask ClearAsync(bool keepBinding, CancellationToken ct)
        {
            await _builder.ClearAsync(ct);
            if (!keepBinding)
                UnbindHolder();
        }

        public bool TryResolveInstanceByRowColumn(int row, int column, out ITraitInstance? instance)
        {
            instance = null;
            var runtime = _builder.CurrentRuntime;
            if (runtime == null || runtime.Instances == null)
                return false;

            var instances = runtime.Instances;
            for (int i = 0; i < instances.Count; i++)
            {
                var visual = instances[i];
                if (visual == null)
                    continue;
                if (visual.Row == row && visual.Column == column)
                {
                    instance = visual.Trait;
                    return true;
                }
            }

            return false;
        }

        void BindHolder(ITraitHolderService holder, string holderKey)
        {
            if (holder == null)
                return;

            if (ReferenceEquals(_boundHolder, holder) && _boundHolderKey == holderKey)
                return;

            UnbindHolder();
            _boundHolder = holder;
            _boundHolderKey = holderKey ?? string.Empty;
            _boundHolder.OnTraitsChanged += OnTraitsChanged;
        }

        void UnbindHolder()
        {
            if (_boundHolder != null)
                _boundHolder.OnTraitsChanged -= OnTraitsChanged;
            _boundHolder = null;
            _boundHolderKey = string.Empty;
        }

        void OnTraitsChanged(IReadOnlyList<ITraitInstance> traits)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await _builder.RefreshAsync(UITraitListRefreshMode.Incremental, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UITraitListPlayer] Refresh failed: {ex.Message}");
                }
            });
        }

        static void EnsureScopeBuiltIfNeeded(IScopeNode scope)
        {
            if (scope is BaseLifetimeScope baseScope)
            {
                baseScope.EnsureScopeBuilt();
                return;
            }

            if (scope is RuntimeLifetimeScope runtimeScope)
            {
                runtimeScope.EnsureScopeBuilt();
            }
        }
    }
}
