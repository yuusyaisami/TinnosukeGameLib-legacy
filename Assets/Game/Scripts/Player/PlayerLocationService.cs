using System;
using UnityEngine;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Commands;

namespace Game
{
    public interface IPlayerLocationService
    {
        bool TryGetPlayerScope(out IScopeNode scope);
        UniTask<IScopeNode> GetPlayerScopeAsync(CancellationToken cancellationToken = default);
    }

    public sealed class PlayerLocationService : IPlayerLocationService, IScopeAcquireHandler, IScopeReleaseHandler
    {
        readonly IScopeNode _originScope;
        readonly IPlayerLocationSettings _settings;
        IBaseLifetimeScopeRegistry _registry;
        IScopeNode _cachedScope;

        public PlayerLocationService(IScopeNode originScope, IPlayerLocationSettings settings)
        {
            _originScope = originScope;
            _settings = settings;
        }

        public bool TryGetPlayerScope(out IScopeNode scope)
        {
            if (TryGetCachedScope(out scope))
                return true;

            var resolved = ResolveFromRegistry();
            if (IsValidScope(resolved))
            {
                _cachedScope = resolved;
                scope = resolved;
                return true;
            }

            _cachedScope = null;
            scope = null;
            return false;
        }

        public async UniTask<IScopeNode> GetPlayerScopeAsync(CancellationToken cancellationToken = default)
        {
            if (TryGetPlayerScope(out var scope))
                return scope;

            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (TryGetPlayerScope(out scope))
                    return scope;
            }

            return null;
        }

        void IScopeAcquireHandler.OnAcquire(IScopeNode scope, bool isReset)
        {
            TryGetPlayerScope(out _);
            //Debug.Log($"[PlayerLocationService] OnAcquire: isReset={isReset}, ResolvedPlayerScope={_cachedScope?.Identity.Id ?? "null"}");
        }

        void IScopeReleaseHandler.OnRelease(IScopeNode scope, bool isReset)
        {
            _cachedScope = null;
            if (isReset)
                _registry = null;
        }

        bool TryGetCachedScope(out IScopeNode scope)
        {
            if (IsValidScope(_cachedScope))
            {
                scope = _cachedScope;
                return true;
            }

            _cachedScope = null;
            scope = null;
            return false;
        }

        bool IsValidScope(IScopeNode scope)
        {
            if (scope == null)
                return false;

            var identity = scope.Identity;
            if (identity == null)
                return false;

            if (!string.IsNullOrEmpty(_settings.PlayerId) &&
                !string.Equals(identity.Id, _settings.PlayerId, StringComparison.Ordinal))
                return false;

            if (_settings.PlayerKind != LifetimeScopeKind.None && scope.Kind != _settings.PlayerKind)
                return false;

            if (!string.IsNullOrEmpty(_settings.PlayerCategory) &&
                !string.Equals(identity.Category, _settings.PlayerCategory, StringComparison.Ordinal))
                return false;

            if (_settings.RequireActive && !identity.IsActive)
                return false;

            if (scope.Resolver == null)
                return false;

            return true;
        }

        IScopeNode ResolveFromRegistry()
        {
            if (!TryGetRegistry(out var registry))
                return null;

            var filter = BuildFilter();
            if (string.IsNullOrEmpty(filter.id) &&
                filter.kind == LifetimeScopeKind.None &&
                string.IsNullOrEmpty(filter.category))
            {
                return null;
            }

            return registry.Resolve(filter, _originScope);
        }

        bool TryGetRegistry(out IBaseLifetimeScopeRegistry registry)
        {
            if (_registry != null)
            {
                registry = _registry;
                return true;
            }

            if (_originScope != null &&
                _originScope.TryResolveInAncestors<IBaseLifetimeScopeRegistry>(out var resolved) &&
                resolved != null)
            {
                _registry = resolved;
                registry = resolved;
                return true;
            }

            registry = null;
            return false;
        }

        CommandTargetIdentityFilter BuildFilter()
        {
            return new CommandTargetIdentityFilter
            {
                id = _settings.PlayerId ?? string.Empty,
                kind = _settings.PlayerKind,
                category = _settings.PlayerCategory ?? string.Empty,
                requireActive = _settings.RequireActive,
                searchScope = _settings.SearchScope
            };
        }
    }
}
