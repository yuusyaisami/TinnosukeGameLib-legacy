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

        [FoldoutGroup("Debug")]
        [SerializeField, InlineProperty, HideLabel]
        TargetChannelHubDebugViewer debugViewer = new();

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
                resolver.TryResolve<Game.Collision.IUnityCollisionManager>(out var collisionManager);
                resolver.TryResolve<Game.Collision.IHitColliderScopeRegistry>(out var hitScopeRegistry);
                return new TargetChannelHubService(config, owner, search, collisionManager, hitScopeRegistry, config.InitialChannels.Count);
            }, Lifetime.Singleton)
                .As<ITargetChannelHub>()
                .As<ITargetChannelHubTelemetry>()
                .As<IDisposable>()
                .As<IResettableService>()
                .As<IEnabledService>()
                .As<IScopeAcquireHandler>()
                .As<IScopeReleaseHandler>();

            builder.RegisterBuildCallback(container =>
            {
                if (debugViewer != null && container.TryResolve<ITargetChannelHubTelemetry>(out var telemetry) && telemetry != null)
                    debugViewer.Bind(telemetry);
            });
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

    [Serializable]
    public sealed class TargetChannelHubDebugViewer
    {
        [ShowInInspector, ReadOnly, LabelText("Bound")]
        public bool IsBound => _telemetry != null;

        [ShowInInspector, ReadOnly, LabelText("Telemetry Version")]
        public int TelemetryVersion
        {
            get
            {
                AutoRefresh();
                return _snapshot.Version;
            }
        }

        [ShowInInspector, ReadOnly, LabelText("Channel Count")]
        public int ChannelCount
        {
            get
            {
                AutoRefresh();
                return _snapshot.ChannelCount;
            }
        }

        [ShowInInspector]
        [TableList(IsReadOnly = true, AlwaysExpanded = true, NumberOfItemsPerPage = 8)]
        [LabelText("Runtime Channel Settings")]
        public List<ChannelRow> Channels
        {
            get
            {
                AutoRefresh();
                return _rows;
            }
        }

        [SerializeField, LabelText("Auto Refresh Every N Frames"), MinValue(1)]
        int autoRefreshEveryNFrames = 1;

        ITargetChannelHubTelemetry? _telemetry;
        TargetChannelHubTelemetrySnapshot _snapshot;
        int _lastVersion = -1;
        int _lastRefreshFrame = -1;
        readonly List<ChannelRow> _rows = new();

        public void Bind(ITargetChannelHubTelemetry telemetry)
        {
            _telemetry = telemetry;
            _lastVersion = -1;
            _lastRefreshFrame = -1;
            Refresh();
        }

        [Button(ButtonSizes.Small)]
        public void Refresh()
        {
            if (_telemetry == null)
                return;

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
        }

        void AutoRefresh()
        {
            if (_telemetry == null)
                return;

            var frame = Time.frameCount;
            var interval = Mathf.Max(1, autoRefreshEveryNFrames);
            if (_lastRefreshFrame >= 0 && frame - _lastRefreshFrame < interval)
                return;

            var telemetryVersion = _telemetry.TelemetryVersion;
            if (telemetryVersion == _lastVersion)
            {
                _lastRefreshFrame = frame;
                return;
            }

            ApplySnapshot(_telemetry.GetTelemetrySnapshot());
        }

        void ApplySnapshot(in TargetChannelHubTelemetrySnapshot snapshot)
        {
            _snapshot = snapshot;
            _lastVersion = snapshot.Version;
            _lastRefreshFrame = Time.frameCount;

            _rows.Clear();
            var channels = snapshot.Channels;
            if (channels == null)
                return;

            for (int i = 0; i < channels.Count; i++)
            {
                var channel = channels[i];
                _rows.Add(new ChannelRow
                {
                    Tag = channel.Tag,
                    Enabled = channel.Enabled,
                    QuerySummary = BuildQuerySummary(channel),
                    SettingsSummary = BuildSettingsSummary(channel),
                });
            }
        }

        static string BuildQuerySummary(in TargetChannelTelemetrySnapshot channel)
        {
            var parts = new List<string>(6);

            if (channel.SearchType == TargetChannelSearchType.DynamicSearch)
            {
                parts.Add($"{ShortSearchType(channel.SearchType)}/{channel.Kind}");
                parts.Add($"R={channel.Radius:0.##}");

                if (channel.Kind == TargetQueryKind.Cone)
                    parts.Add($"A={channel.HalfAngleDeg:0.##}");
            }
            else
            {
                parts.Add(ShortSearchType(channel.SearchType));
            }

            parts.Add($"Refresh={channel.RefreshIntervalFrames}");
            parts.Add($"Expect={channel.ExpectedResultCount}");

            return string.Join(" ", parts);
        }

        static string BuildSettingsSummary(in TargetChannelTelemetrySnapshot channel)
        {
            var parts = new List<string>(8);

            parts.Add($"Mask={channel.KindMask}");

            if (channel.SearchType == TargetChannelSearchType.DynamicSearch)
            {
                parts.Add($"ExSelf={ShortBool(channel.ExcludeSelf)}");
                parts.Add($"Origin={channel.OriginSource}");

                if (channel.Kind == TargetQueryKind.Cone)
                    parts.Add($"Fwd={channel.ForwardSource}");

                if (!string.IsNullOrWhiteSpace(channel.FilterId))
                    parts.Add($"Id={channel.FilterId}");

                if (!string.IsNullOrWhiteSpace(channel.FilterCategory))
                    parts.Add($"Cat={channel.FilterCategory}");
            }
            else if (channel.SearchType == TargetChannelSearchType.ScopeSearch)
            {
                parts.Add($"Scope={ShortBool(channel.ScopeRequireActive)}");
            }
            else if (channel.SearchType == TargetChannelSearchType.None)
            {
                parts.Add($"Monitor={ShortBool(channel.MonitorActiveState)}");

                if (channel.DirectTargetValidDistance > 0f)
                    parts.Add($"Dist={channel.DirectTargetValidDistance:0.##}");
            }
            else if (channel.SearchType == TargetChannelSearchType.CollisionSearch)
            {
                parts.Add($"Range={channel.CollisionRangeSource}");

                if (!string.IsNullOrWhiteSpace(channel.CollisionAreaTag))
                    parts.Add($"Area={channel.CollisionAreaTag}");

                if (!string.IsNullOrWhiteSpace(channel.CollisionFilterSummary))
                    parts.Add($"Filter={channel.CollisionFilterSummary}");
            }

            return string.Join(" ", parts);
        }

        static string ShortSearchType(TargetChannelSearchType searchType)
        {
            return searchType switch
            {
                TargetChannelSearchType.DynamicSearch => "Dynamic",
                TargetChannelSearchType.ScopeSearch => "Scope",
                TargetChannelSearchType.CollisionSearch => "Collision",
                TargetChannelSearchType.None => "None",
                _ => searchType.ToString(),
            };
        }

        static string ShortBool(bool value)
        {
            return value ? "Y" : "N";
        }

        [Serializable]
        public sealed class ChannelRow
        {
            [TableColumnWidth(120)] public string Tag = string.Empty;
            [TableColumnWidth(60)] public bool Enabled;
            [LabelText("Query")]
            [TableColumnWidth(240)] public string QuerySummary = string.Empty;
            [LabelText("Settings")]
            [TableColumnWidth(400)] public string SettingsSummary = string.Empty;
        }
    }
}
