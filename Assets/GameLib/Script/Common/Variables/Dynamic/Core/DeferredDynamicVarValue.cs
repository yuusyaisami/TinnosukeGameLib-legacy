#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Game.Common
{
    [Serializable]
    public sealed class DeferredDynamicVarValue : IDynamicSourceDependencyRevisionProvider
    {
        readonly DynamicValue _value;
        readonly VarStorePayload.EntryValueKind _expectedKind;
        readonly int _varId;
        readonly string _owner;

        public DeferredDynamicVarValue(
            in DynamicValue value,
            VarStorePayload.EntryValueKind expectedKind,
            int varId,
            string owner)
        {
            _value = value;
            _expectedKind = expectedKind;
            _varId = varId;
            _owner = owner ?? string.Empty;
        }

        public bool HasSource => _value.HasSource;
        public DynamicValue Value => _value;
        public VarStorePayload.EntryValueKind ExpectedKind => _expectedKind;
        public int VarId => _varId;
        public string Owner => _owner;

        public int GetSourceDependencyRevision(IDynamicContext context)
        {
            return _value.GetSourceDependencyRevision(context);
        }
    }

    static class DeferredDynamicVarResolver
    {
        sealed class ResolveEntry
        {
            public readonly string Key;
            public readonly ResolveEntry? Previous;

            public ResolveEntry(string key, ResolveEntry? previous)
            {
                Key = key;
                Previous = previous;
            }
        }

        readonly struct ResolveToken : IDisposable
        {
            readonly ResolveEntry? _entry;

            public ResolveToken(ResolveEntry? entry)
            {
                _entry = entry;
            }

            public void Dispose()
            {
                if (_entry == null)
                    return;

                var current = CurrentEntry.Value;
                if (ReferenceEquals(current, _entry))
                {
                    CurrentEntry.Value = _entry.Previous;
                    return;
                }

                CurrentEntry.Value = _entry.Previous;
            }
        }

        static readonly AsyncLocal<ResolveEntry?> CurrentEntry = new();

        public static bool TryResolve(object managedRef, IDynamicContext? context, string readPoint, out DynamicVariant value)
        {
            if (managedRef is not DeferredDynamicVarValue deferred)
            {
                value = default;
                return false;
            }

            value = ResolveDeferred(deferred, context, readPoint);
            return true;
        }

        static DynamicVariant ResolveDeferred(DeferredDynamicVarValue deferred, IDynamicContext? context, string readPoint)
        {
            if (context == null)
                return DynamicVariant.Null;

            if (!deferred.HasSource)
            {
                return BlackboardSourceUtility.FailOrNull(
                    context,
                    $"DeferredDynamic resolve failed: source missing. owner='{deferred.Owner}' varId={deferred.VarId} readPoint='{readPoint}'.");
            }

            var resolveKey = BuildResolveKey(context, deferred, readPoint);
            if (!TryEnter(resolveKey, out var token, out var chain))
            {
                return BlackboardSourceUtility.FailOrNull(
                    context,
                    $"DeferredDynamic recursion detected. chain={chain}");
            }

            using (token)
            {
                var evaluated = deferred.Value.Evaluate(context);
                if (!VarStoreEntryValueKindConverter.TryCoerceToKind(deferred.ExpectedKind, in evaluated, out var coerced))
                {
                    return BlackboardSourceUtility.FailOrNull(
                        context,
                        $"DeferredDynamic coercion failed. expected={deferred.ExpectedKind} actual={evaluated.Kind} owner='{deferred.Owner}' varId={deferred.VarId} readPoint='{readPoint}'.");
                }

                return coerced;
            }
        }

        static string BuildResolveKey(IDynamicContext context, DeferredDynamicVarValue deferred, string readPoint)
        {
            var scopeId = context.Scope?.Identity?.Id ?? "(none)";
            return $"scope={scopeId}|owner={deferred.Owner}|varId={deferred.VarId}|point={readPoint}";
        }

        static bool TryEnter(string key, out ResolveToken token, out string chain)
        {
            chain = "<empty>";
            var current = CurrentEntry.Value;
            for (var node = current; node != null; node = node.Previous)
            {
                if (!string.Equals(node.Key, key, StringComparison.Ordinal))
                    continue;

                chain = BuildChain(current, key);
                token = default;
                return false;
            }

            var entry = new ResolveEntry(key, current);
            CurrentEntry.Value = entry;
            token = new ResolveToken(entry);
            return true;
        }

        static string BuildChain(ResolveEntry? current, string append)
        {
            if (current == null)
                return append;

            var chain = new List<string>(8);
            for (var node = current; node != null; node = node.Previous)
                chain.Add(node.Key);

            chain.Reverse();
            chain.Add(append);
            return string.Join(" -> ", chain);
        }
    }
}
