#nullable enable
namespace Game.Common
{
    public static class VarStoreMergeExtensions
    {
        public static void MergeInto(this IVarStore source, IVarStore dest, bool overwrite)
        {
            if (source == null || dest == null)
                return;

            foreach (var varId in source.EnumerateVarIds())
            {
                if (varId == 0)
                    continue;

                if (!overwrite && dest.Contains(varId))
                    continue;

                var kind = source.GetVarKind(varId);
                if (kind == ValueKind.ManagedRef)
                {
                    if (source.TryGetManagedRef(varId, out var managed))
                        dest.TrySetManagedRef(varId, managed);
                    continue;
                }

                if (source.TryGetVariant(varId, out var variant))
                {
                    if (variant.Kind == ValueKind.Null)
                        dest.TryUnset(varId);
                    else
                        dest.TrySetVariant(varId, variant);
                }
            }
        }
    }
}
