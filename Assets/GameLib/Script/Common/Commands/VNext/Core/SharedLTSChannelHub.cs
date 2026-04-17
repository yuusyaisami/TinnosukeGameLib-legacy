#nullable enable
using System;
using System.Collections.Generic;
using Game.Common;
using Game.VarStoreKeys;
using UnityEngine;
using VContainer;

namespace Game.Commands.VNext
{
    public interface ISharedLTSChannelHub
    {
        void Register(string tag, IScopeNode scope);
        bool Unregister(string tag, IScopeNode? scope = null);
        bool TryGet(string tag, out IScopeNode? scope);
        bool TryFindTag(IScopeNode scope, out string tag);
        void Clear();
    }

    public interface ISharedLTSChannelHubTelemetry
    {
        int TelemetryVersion { get; }
        SharedLTSChannelHubSnapshot GetTelemetrySnapshot();
    }

    public readonly struct SharedLTSChannelHubSnapshot
    {
        public readonly int Version;
        public readonly int ChannelCount;
        public readonly IReadOnlyList<SharedLTSChannelSnapshot> Channels;

        public SharedLTSChannelHubSnapshot(int version, IReadOnlyList<SharedLTSChannelSnapshot> channels)
        {
            Version = version;
            Channels = channels ?? Array.Empty<SharedLTSChannelSnapshot>();
            ChannelCount = Channels.Count;
        }
    }

    public readonly struct SharedLTSChannelSnapshot
    {
        public readonly string Tag;
        public readonly string Status;
        public readonly string ScopeLabel;
        public readonly string ScopePath;
        public readonly string ScopeKind;
        public readonly string ScopeId;
        public readonly string ScopeCategory;
        public readonly bool IsActive;
        public readonly bool IsVisible;
        public readonly string BlackboardStatus;
        public readonly int LocalVarCount;
        public readonly int TableCount;
        public readonly IReadOnlyList<SharedLTSChannelVarSnapshot> LocalVars;
        public readonly IReadOnlyList<SharedLTSChannelTableSnapshot> Tables;

        public SharedLTSChannelSnapshot(
            string tag,
            string status,
            string scopeLabel,
            string scopePath,
            string scopeKind,
            string scopeId,
            string scopeCategory,
            bool isActive,
            bool isVisible,
            string blackboardStatus,
            IReadOnlyList<SharedLTSChannelVarSnapshot> localVars,
            IReadOnlyList<SharedLTSChannelTableSnapshot> tables)
        {
            Tag = tag;
            Status = status;
            ScopeLabel = scopeLabel;
            ScopePath = scopePath;
            ScopeKind = scopeKind;
            ScopeId = scopeId;
            ScopeCategory = scopeCategory;
            IsActive = isActive;
            IsVisible = isVisible;
            BlackboardStatus = blackboardStatus;
            LocalVars = localVars ?? Array.Empty<SharedLTSChannelVarSnapshot>();
            Tables = tables ?? Array.Empty<SharedLTSChannelTableSnapshot>();
            LocalVarCount = LocalVars.Count;
            TableCount = Tables.Count;
        }
    }

    public readonly struct SharedLTSChannelVarSnapshot
    {
        public readonly int VarId;
        public readonly string Key;
        public readonly string Kind;
        public readonly int Version;
        public readonly string Value;

        public SharedLTSChannelVarSnapshot(int varId, string key, string kind, int version, string value)
        {
            VarId = varId;
            Key = key;
            Kind = kind;
            Version = version;
            Value = value;
        }
    }

    public readonly struct SharedLTSChannelTableSnapshot
    {
        public readonly int TableVarId;
        public readonly string Key;
        public readonly int Version;
        public readonly int RowCount;
        public readonly int ColumnCount;
        public readonly int CellCount;

        public SharedLTSChannelTableSnapshot(int tableVarId, string key, int version, int rowCount, int columnCount, int cellCount)
        {
            TableVarId = tableVarId;
            Key = key;
            Version = version;
            RowCount = rowCount;
            ColumnCount = columnCount;
            CellCount = cellCount;
        }
    }

    public sealed class SharedLTSChannelHub : ISharedLTSChannelHub, ISharedLTSChannelHubTelemetry, IScopeReleaseHandler
    {
        readonly Dictionary<string, IScopeNode> _scopes = new(StringComparer.Ordinal);
        int _version;

        public int TelemetryVersion => _version;

        public void Register(string tag, IScopeNode scope)
        {
            if (string.IsNullOrWhiteSpace(tag) || scope == null)
                return;

            _scopes[tag] = scope;
            Touch();
        }

        public bool Unregister(string tag, IScopeNode? scope = null)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (!_scopes.TryGetValue(tag, out var registered) || registered == null)
                return false;

            if (scope != null && !ReferenceEquals(scope, registered))
                return false;

            _scopes.Remove(tag);
            Touch();
            return true;
        }

        public bool TryGet(string tag, out IScopeNode? scope)
        {
            scope = null;
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (!_scopes.TryGetValue(tag, out var registered) || registered == null)
                return false;

            var identity = registered.Identity;
            if (identity != null && !identity.IsActive)
            {
                _scopes.Remove(tag);
                Touch();
                return false;
            }

            scope = registered;
            return true;
        }

        public bool TryFindTag(IScopeNode scope, out string tag)
        {
            tag = string.Empty;
            if (scope == null)
                return false;

            string? foundTag = null;
            List<string>? staleTags = null;
            foreach (var pair in _scopes)
            {
                var registered = pair.Value;
                if (registered == null)
                {
                    staleTags ??= new List<string>();
                    staleTags.Add(pair.Key);
                    continue;
                }

                var identity = registered.Identity;
                if (identity != null && !identity.IsActive)
                {
                    staleTags ??= new List<string>();
                    staleTags.Add(pair.Key);
                    continue;
                }

                if (!ReferenceEquals(registered, scope))
                    continue;

                if (foundTag == null || string.CompareOrdinal(pair.Key, foundTag) < 0)
                    foundTag = pair.Key;
            }

            if (staleTags != null)
            {
                for (int i = 0; i < staleTags.Count; i++)
                    _scopes.Remove(staleTags[i]);

                if (staleTags.Count > 0)
                    Touch();
            }

            if (string.IsNullOrEmpty(foundTag))
                return false;

            tag = foundTag;
            return true;
        }

        public void Clear()
        {
            if (_scopes.Count == 0)
                return;

            _scopes.Clear();
            Touch();
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            _ = scope;
            _ = isReset;

            if (_scopes.Count == 0)
                return;

            _scopes.Clear();
            Touch();
        }

        public SharedLTSChannelHubSnapshot GetTelemetrySnapshot()
        {
            if (_scopes.Count == 0)
                return new SharedLTSChannelHubSnapshot(_version, Array.Empty<SharedLTSChannelSnapshot>());

            var tags = new List<string>(_scopes.Count);
            foreach (var tag in _scopes.Keys)
                tags.Add(tag);

            tags.Sort(StringComparer.Ordinal);

            var channels = new List<SharedLTSChannelSnapshot>(tags.Count);
            for (int i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                if (!_scopes.TryGetValue(tag, out var scope) || scope == null)
                    continue;

                channels.Add(BuildChannelSnapshot(tag, scope));
            }

            return new SharedLTSChannelHubSnapshot(_version, channels);
        }

        void Touch()
        {
            _version++;
        }

        SharedLTSChannelSnapshot BuildChannelSnapshot(string tag, IScopeNode scope)
        {
            var isDestroyed = IsDestroyed(scope);
            var identity = !isDestroyed ? scope.Identity : null;
            var identityActive = identity == null || identity.IsActive;
            var isActive = !isDestroyed && scope.IsActive && identityActive;
            var status = isDestroyed
                ? "Destroyed"
                : !isActive
                    ? "Inactive"
                    : "Active";

            var scopeKind = !isDestroyed ? scope.Kind.ToString() : "None";
            var scopeId = identity != null ? identity.Id ?? string.Empty : string.Empty;
            var scopeCategory = identity != null ? identity.Category ?? string.Empty : string.Empty;
            var scopeLabel = BuildScopeLabel(scope, isDestroyed, identity, scopeKind);
            var scopePath = BuildScopePath(scope, isDestroyed);
            var blackboard = !isDestroyed ? TryResolveBlackboard(scope) : null;
            var blackboardStatus = blackboard != null ? "Present" : "Missing";

            var localVars = new List<SharedLTSChannelVarSnapshot>();
            var tables = new List<SharedLTSChannelTableSnapshot>();
            if (blackboard != null)
            {
                BuildLocalVarSnapshots(blackboard.LocalVars, localVars);
                BuildTableSnapshots(blackboard.LocalVars, tables);
            }

            return new SharedLTSChannelSnapshot(
                tag,
                status,
                scopeLabel,
                scopePath,
                scopeKind,
                scopeId,
                scopeCategory,
                isActive,
                !isDestroyed && scope.IsVisible,
                blackboardStatus,
                localVars,
                tables);
        }

        static void BuildLocalVarSnapshots(IVarStore store, List<SharedLTSChannelVarSnapshot> destination)
        {
            if (store == null || destination == null)
                return;

            var varIds = new List<int>();
            foreach (var varId in store.EnumerateVarIds())
                varIds.Add(varId);

            varIds.Sort();

            for (int i = 0; i < varIds.Count; i++)
            {
                var varId = varIds[i];
                var kind = store.GetVarKind(varId);
                var version = store.GetVarVersion(varId);
                var key = VarIdResolver.TryGetIdToStable(varId) ?? $"varId={varId}";
                var value = DescribeVarValue(store, varId, kind);
                destination.Add(new SharedLTSChannelVarSnapshot(varId, key, kind.ToString(), version, value));
            }
        }

        static void BuildTableSnapshots(IVarStore store, List<SharedLTSChannelTableSnapshot> destination)
        {
            if (store == null || destination == null)
                return;

            var tableVarIds = new List<int>();
            foreach (var tableVarId in store.EnumerateTableVarIds())
                tableVarIds.Add(tableVarId);

            tableVarIds.Sort();

            for (int i = 0; i < tableVarIds.Count; i++)
            {
                var tableVarId = tableVarIds[i];
                store.TryGetTableRowCount(tableVarId, out var rowCount);
                var maxColumnCount = 0;
                var cellCount = 0;

                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    if (!store.TryGetTableColumnCount(tableVarId, rowIndex, out var columnCount))
                        continue;

                    if (columnCount > maxColumnCount)
                        maxColumnCount = columnCount;

                    for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                    {
                        if (store.TryHasTableCell(tableVarId, rowIndex, columnIndex))
                            cellCount++;
                    }
                }

                var key = VarIdResolver.TryGetIdToStable(tableVarId) ?? $"tableVarId={tableVarId}";
                var version = store.GetTableVersion(tableVarId);
                destination.Add(new SharedLTSChannelTableSnapshot(tableVarId, key, version, rowCount, maxColumnCount, cellCount));
            }
        }

        static string DescribeVarValue(IVarStore store, int varId, ValueKind kind)
        {
            if (kind == ValueKind.ManagedRef)
            {
                if (store.TryGetManagedRef(varId, out var managedRef))
                    return managedRef?.ToString() ?? "(null)";

                return "(null)";
            }

            if (store.TryGetVariant(varId, out var variant))
                return variant.ToString();

            return "(unavailable)";
        }

        static bool IsDestroyed(IScopeNode scope)
        {
            return scope is Component component && !component;
        }

        static string BuildScopeLabel(IScopeNode scope, bool isDestroyed, ILTSIdentityService? identity, string scopeKind)
        {
            if (isDestroyed)
                return "Destroyed";

            var name = scope is Component component && component && component.gameObject != null
                ? component.gameObject.name
                : scope.GetType().Name;

            var idText = identity != null && !string.IsNullOrWhiteSpace(identity.Id)
                ? $" id={identity.Id}"
                : string.Empty;
            var categoryText = identity != null && !string.IsNullOrWhiteSpace(identity.Category)
                ? $" cat={identity.Category}"
                : string.Empty;

            return $"{scopeKind}:{name}{idText}{categoryText}";
        }

        static string BuildScopePath(IScopeNode scope, bool isDestroyed)
        {
            if (isDestroyed)
                return "Destroyed";

            var path = scope.GetPathFromRoot();
            if (path == null || path.Count == 0)
                return string.Empty;

            var names = new List<string>(path.Count);
            for (int i = 0; i < path.Count; i++)
                names.Add(DescribeScopePathSegment(path[i]));

            return string.Join(" / ", names);
        }

        static string DescribeScopePathSegment(IScopeNode node)
        {
            if (node == null)
                return "(null)";

            if (node is Component component && !component)
                return "Destroyed";

            var name = node is Component aliveComponent && aliveComponent.gameObject != null
                ? aliveComponent.gameObject.name
                : node.GetType().Name;

            var identity = node.Identity;
            var kind = node.Kind;
            if (identity == null)
                return $"{name}[{kind}]";

            var idText = string.IsNullOrWhiteSpace(identity.Id) ? string.Empty : $":{identity.Id}";
            return $"{name}[{kind}{idText}]";
        }

        static IBlackboardService? TryResolveBlackboard(IScopeNode scope)
        {
            var resolver = scope.Resolver;
            if (resolver != null && resolver.TryResolve<IBlackboardService>(out var blackboard) && blackboard != null)
                return blackboard;

            return null;
        }
    }
}
