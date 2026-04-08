#nullable enable
using Game.Commands.VNext;

namespace Game.Common
{
    public static class VarStorePayloadDynamicExtensions
    {
        public static void ApplyTo(this VarStorePayload payload, IVarStore dest, IDynamicContext? context, bool overwrite)
        {
            if (payload == null || dest == null)
                return;

            var entries = payload.Entries;
            if (entries == null || entries.Count == 0)
                return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.VarId == 0)
                    continue;

                if (!overwrite && dest.Contains(entry.VarId))
                    continue;

                if (entry.StoreMode == VarStoreWriteMode.DeferredDynamic)
                {
                    if (!entry.Value.HasSource)
                    {
                        dest.TryUnset(entry.VarId);
                        continue;
                    }

                    var deferred = new DeferredDynamicVarValue(entry.Value, entry.Kind, entry.VarId, nameof(VarStorePayload));
                    dest.TrySetManagedRef(entry.VarId, deferred);
                    continue;
                }

                if (!VarStoreEntryValueKindConverter.TryConvertToVariant(in entry, context, out var value))
                    continue;

                if (value.Kind == ValueKind.ManagedRef)
                {
                    if (value.AsManagedRef != null)
                        dest.TrySetManagedRef(entry.VarId, value.AsManagedRef);
                    continue;
                }

                dest.TrySetVariant(entry.VarId, value);
            }
        }

        public static VarStore ToVarStore(this VarStorePayload payload, IDynamicContext? context)
        {
            var vars = new VarStore();
            payload?.ApplyTo(vars, context, overwrite: true);
            return vars;
        }
    }
}
