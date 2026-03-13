using System;
using System.Collections.Generic;

namespace Game.Common
{
    public interface IRichTextRefService
    {
        bool TryRegister(string refKey, IRichTextProvider provider, bool overwrite = false);
        bool TryUnregister(string refKey);
        bool TryEvaluate(string refKey, IDynamicContext ctx, out string text);
        bool TryGetDependentKeys(string refKey, out IReadOnlyList<string> keys);
    }

    public interface IRichTextProvider
    {
        string Evaluate(IDynamicContext ctx);
        IReadOnlyList<string> GetDependentKeys();
    }

    public sealed class RichTextProvider : IRichTextProvider
    {
        readonly RichTextSource _source;

        public RichTextProvider(RichTextSource source)
        {
            _source = source;
        }

        public string Evaluate(IDynamicContext ctx)
        {
            if (_source == null)
                return string.Empty;
            return _source.Evaluate(ctx).AsString;
        }

        public IReadOnlyList<string> GetDependentKeys()
        {
            return _source?.GetDependentKeys();
        }
    }

    public sealed class RichTextRefService : IRichTextRefService
    {
        readonly Dictionary<string, IRichTextProvider> _providers = new(StringComparer.Ordinal);

        public bool TryRegister(string refKey, IRichTextProvider provider, bool overwrite = false)
        {
            if (string.IsNullOrEmpty(refKey) || provider == null)
                return false;

            if (_providers.ContainsKey(refKey))
            {
                if (!overwrite)
                    return false;
                _providers[refKey] = provider;
                return true;
            }

            _providers.Add(refKey, provider);
            return true;
        }

        public bool TryUnregister(string refKey)
        {
            if (string.IsNullOrEmpty(refKey))
                return false;

            return _providers.Remove(refKey);
        }

        public bool TryEvaluate(string refKey, IDynamicContext ctx, out string text)
        {
            text = string.Empty;
            if (string.IsNullOrEmpty(refKey))
                return false;

            if (!_providers.TryGetValue(refKey, out var provider) || provider == null)
                return false;

            text = provider.Evaluate(ctx) ?? string.Empty;
            return true;
        }

        public bool TryGetDependentKeys(string refKey, out IReadOnlyList<string> keys)
        {
            keys = System.Array.Empty<string>();
            if (string.IsNullOrEmpty(refKey))
                return false;

            if (!_providers.TryGetValue(refKey, out var provider) || provider == null)
                return false;

            var result = provider.GetDependentKeys();
            if (result == null)
                return false;

            keys = result;
            return true;
        }
    }
}
