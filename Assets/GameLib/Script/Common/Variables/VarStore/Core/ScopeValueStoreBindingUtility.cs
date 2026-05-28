#nullable enable
using System;

namespace Game.Common
{
    static class ScopeValueStoreBindingUtility
    {
        public static bool TryResolveScopeVars(IScopeNode? scope, out IVarStore vars)
        {
            vars = NullVarStore.Instance;
            var resolver = scope?.Resolver;
            if (resolver == null)
                return false;

            if (!resolver.TryResolve<IVarStore>(out var resolved) || resolved == null)
                return false;

            vars = resolved;
            return true;
        }

        public static bool TryWriteVariant(IVarStore? vars, int varId, in DynamicVariant value, bool overwriteExisting)
        {
            if (!CanWrite(vars, varId, overwriteExisting))
                return false;

            return vars!.TrySetVariant(varId, in value);
        }

        public static bool TryWriteManagedRef(IVarStore? vars, int varId, object? value, bool overwriteExisting)
        {
            if (!CanWrite(vars, varId, overwriteExisting))
                return false;

            if (value == null)
                return vars!.TryUnset(varId);

            return vars!.TrySetManagedRef(varId, value);
        }

        public static bool CanWrite(IVarStore? vars, int varId, bool overwriteExisting)
        {
            return vars != null
                && varId != 0
                && (overwriteExisting || !vars.Contains(varId));
        }
    }
}