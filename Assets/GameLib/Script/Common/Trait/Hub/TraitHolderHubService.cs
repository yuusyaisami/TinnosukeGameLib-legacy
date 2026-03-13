#nullable enable
using System.Collections.Generic;
using Game.Common;
using VContainer;

namespace Game.Trait
{
    // NOTE: External systems must obtain ITraitHolderService via this hub with a key.
    public interface ITraitHolderHubService
    {
        IReadOnlyList<string> Keys { get; }
        bool TryGetHolder(string key, out ITraitHolderService? holder);
    }

    public sealed class TraitHolderHubService :
        ITraitHolderHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly Dictionary<string, TraitHolderService> _holders = new(System.StringComparer.Ordinal);
        readonly List<TraitHolderService> _holderList = new();
        readonly List<string> _keys = new();
        IRichTextRefService? _richTextRefService;

        public TraitHolderHubService(IScopeNode? scope, IReadOnlyList<TraitHolderSettings>? settings)
        {
            if (settings == null)
                return;

            if (settings.Count == 0)
                return;

            for (int i = 0; i < settings.Count; i++)
            {
                var setting = settings[i];
                if (setting == null)
                    continue;

                var key = setting.NormalizedKey;
                if (string.IsNullOrEmpty(key))
                    continue;

                var service = new TraitHolderService(scope);
                setting.ApplyTo(service);

                if (_holders.ContainsKey(key))
                    continue;

                _holders.Add(key, service);
                _holderList.Add(service);
                _keys.Add(key);
            }

        }

        public IReadOnlyList<string> Keys => _keys;

        public bool TryGetHolder(string key, out ITraitHolderService? holder)
        {
            holder = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var normalized = key.Trim();
            if (!_holders.TryGetValue(normalized, out var service))
                return false;

            holder = service;
            return true;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (scope.Resolver != null && scope.Resolver.TryResolve<IRichTextRefService>(out var service) && service != null)
            {
                _richTextRefService = service;
            }
            else
            {
                _richTextRefService = ResolveRichTextRefServiceFromAncestors(scope);
            }

            if (_richTextRefService != null)
            {
                for (int i = 0; i < _holderList.Count; i++)
                    _holderList[i].SetRichTextRefService(_richTextRefService);
            }

            if (_holderList.Count == 0)
                return;

            for (int i = 0; i < _holderList.Count; i++)
                _holderList[i].OnAcquire(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (_holderList.Count == 0)
                return;

            for (int i = 0; i < _holderList.Count; i++)
                _holderList[i].SetRichTextRefService(null);

            _richTextRefService = null;

            for (int i = 0; i < _holderList.Count; i++)
                _holderList[i].OnRelease(scope, isReset);
        }

        static IRichTextRefService? ResolveRichTextRefServiceFromAncestors(IScopeNode? origin)
        {
            var current = origin?.Parent;
            while (current != null)
            {
                var resolver = current.Resolver;
                if (resolver != null && resolver.TryResolve<IRichTextRefService>(out var service) && service != null)
                    return service;

                current = current.Parent;
            }

            return null;
        }
    }
}
