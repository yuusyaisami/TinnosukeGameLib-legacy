#nullable enable
// Game.Targeting
// ================================================================================
// TargetChannelHubController - Inspector 定義から Hub を構築するブリッジ MB（新方式）
// ================================================================================
//
// ・MB は IFeatureInstaller として設定のみ保持し、初期化ロジックは Service 側へ移動。
// ・BaseLifetimeScope 依存は廃止し、InstallFeature の IScopeNode を唯一のスコープ入口にする。
// ・必要ならこのコンポーネント自体を ITargetChannelHub として利用可能（薄い委譲のみ）。
// ================================================================================

using System;
using System.Collections.Generic;
using Game.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Game.DI;

namespace Game.Targeting
{
    /// <summary>
    /// 任意：Inspector で TargetChannel を定義し、実行時に Hub を構築するブリッジMB。
    /// これ自体を ITargetChannelHub として使える（必要なら DI でこのコンポーネントを登録）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetChannelHubController : MonoBehaviour, ITargetChannelHub, IFeatureInstaller
    {
        // ================================================================
        // Inspector
        // ================================================================

        [FoldoutGroup("Setup")]
        [LabelText("Auto Initialize On Start")]
        [SerializeField] bool autoInitializeOnStart = true; // Start 時に自動初期化するか

        [FoldoutGroup("Setup")]
        [LabelText("Initial Channels")]
        [ListDrawerSettings(ShowFoldout = true, DraggableItems = true, DefaultExpandedState = true)]
        [SerializeField] List<DynamicValue<TargetChannelPreset>> initialChannels = new(); // 初期登録するチャネル一覧

        // ================================================================
        // Runtime
        // ================================================================

        IScopeNode? _ownerScope;

        public int ChannelCount
        {
            get
            {
                var hub = TryResolveHub();
                return hub?.ChannelCount ?? 0;
            }
        }

        // ================================================================
        // IFeatureInstaller
        // ================================================================

        public void InstallFeature(IContainerBuilder builder, IScopeNode scope)
        {
            _ownerScope = scope;

            var config = new TargetChannelHubConfig(
                autoInitializeOnStart: autoInitializeOnStart,
                initialChannels: initialChannels);

            builder.RegisterInstance(config);

            var owner = new TargetChannelOwner(transform, scope);

            builder.Register<TargetChannelHubService>(resolver =>
            {
                resolver.TryResolve<Game.Search.IDynamicSearchService>(out var search);
                return new TargetChannelHubService(config, owner, search, config.InitialChannels.Count);
            }, Lifetime.Singleton)
                .As<ITargetChannelHub>()
                .As<IDisposable>()
                .As<IResettableService>()
                .As<IEnabledService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();
        }

        // ================================================================
        // Public API (ITargetChannelHub)
        // ================================================================

        [Button(ButtonSizes.Medium)]
        [DisableInPlayMode]
        void Editor_ValidateTags()
        {
            // エディタ用：タグの重複をチェック
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < initialChannels.Count; i++)
            {
                if (!initialChannels[i].TryGet(EmptyDynamicContext.Instance, out TargetChannelPreset? c) || c == null)
                    continue;

                if (string.IsNullOrEmpty(c.Tag))
                    continue;

                if (!set.Add(c.Tag))
                {
                    Debug.LogWarning($"[TargetChannelHubController] Duplicate tag in Initial Channels: {c.Tag}", this);
                }
            }
        }

        public bool TryGetRuntime(string tag, out ITargetChannelRuntime runtime)
        {
            var hub = TryResolveHub();
            if (hub != null)
                return hub.TryGetRuntime(tag, out runtime);
            runtime = null!;
            return false;
        }

        public ITargetChannelRuntime RegisterOrReplace(TargetChannelPreset preset)
        {
            var hub = TryResolveHub();
            if (hub == null) throw new InvalidOperationException("[TargetChannelHubController] Hub not available.");
            return hub.RegisterOrReplace(preset);
        }

        public bool SwapPreset(string tag, TargetChannelPreset preset)
        {
            var hub = TryResolveHub();
            return hub != null && hub.SwapPreset(tag, preset);
        }

        public bool MutateSettings(string tag, TargetChannelRuntimeMutation mutation)
        {
            var hub = TryResolveHub();
            return hub != null && hub.MutateSettings(tag, mutation);
        }

        public bool ResetRuntimeOverrides(string tag)
        {
            var hub = TryResolveHub();
            return hub != null && hub.ResetRuntimeOverrides(tag);
        }

        public bool SetDirectTargets(string tag, IReadOnlyList<Game.Search.DynamicSearchHit> hits)
        {
            var hub = TryResolveHub();
            return hub != null && hub.SetDirectTargets(tag, hits);
        }

        public bool ClearDirectTargets(string tag)
        {
            var hub = TryResolveHub();
            return hub != null && hub.ClearDirectTargets(tag);
        }

        public bool Unregister(string tag)
        {
            var hub = TryResolveHub();
            return hub != null && hub.Unregister(tag);
        }

        public void Clear()
        {
            TryResolveHub()?.Clear();
        }

        // ================================================================
        // Internal
        // ================================================================

        ITargetChannelHub? TryResolveHub()
        {
            var resolver = _ownerScope?.Resolver;
            if (resolver == null)
                return null;

            return resolver.TryResolve<ITargetChannelHub>(out var hub) ? hub : null;
        }
    }
}
