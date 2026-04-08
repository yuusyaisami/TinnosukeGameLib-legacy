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
        bool TryGetPlacementSettings(string key, out TraitHolderPlacementSettings? settings);
    }

    public sealed class TraitHolderHubService :
        ITraitHolderHubService,
        IScopeAcquireHandler,
        IScopeReleaseHandler
    {
        readonly IScopeNode? _ownerScope;
        readonly Dictionary<string, TraitHolderService> _holders = new(System.StringComparer.Ordinal);
        readonly Dictionary<string, TraitHolderSettings> _settingsByKey = new(System.StringComparer.Ordinal);
        readonly List<TraitHolderService> _holderList = new();
        readonly List<string> _keys = new();
        IRichTextRefService? _richTextRefService;

        public TraitHolderHubService(IScopeNode? scope, IReadOnlyList<TraitHolderSettings>? settings)
        {
            _ownerScope = scope;
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
                _settingsByKey.Add(key, setting.Clone());
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

        public bool TryGetPlacementSettings(string key, out TraitHolderPlacementSettings? settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var normalized = key.Trim();
            if (!_settingsByKey.TryGetValue(normalized, out var setting) || setting == null)
                return false;

            return setting.TryGetPlacementSettings(out settings) && settings != null;
        }

        public void OnAcquire(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_ownerScope, scope))
                return;

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
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"[TraitHolderHubService][RichText] IRichTextRefService was not found on acquire. " +
                    $"scope='{DescribeScope(scope)}' holderCount={_holderList.Count}");
            }

            if (_holderList.Count == 0)
                return;

            for (int i = 0; i < _holderList.Count; i++)
                _holderList[i].OnAcquire(scope, isReset);
        }

        public void OnRelease(IScopeNode scope, bool isReset)
        {
            if (!ReferenceEquals(_ownerScope, scope))
                return;

            //UnityEngine.Debug.Log(
            //    $"[TraitHolderHubService][OnRelease] scope='{DescribeScope(scope)}' isReset={isReset} holderCount={_holderList.Count}");
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

        static string DescribeScope(IScopeNode? scope)
        {
            if (scope == null)
                return "<null>";

            if (scope is UnityEngine.Component component && component != null)
                return $"{scope.GetType().Name}:{component.name}";

            return scope.GetType().Name;
        }
    }
}
