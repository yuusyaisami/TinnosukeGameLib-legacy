#nullable enable
using Game.Common;
using Game.Vars.Generated;

namespace Game.Trait
{
    public static class TraitHolderDataWriteUtility
    {
        public static void WriteHolderDataToVarStore(
            ITraitHolderService? holder,
            string? holderKey,
            IVarStore destination,
            bool overwrite)
        {
            if (destination == null)
                return;

            var normalizedKey = string.IsNullOrWhiteSpace(holderKey) ? string.Empty : holderKey.Trim();
            var traitCount = holder?.Traits?.Count ?? 0;
            var holderId = string.Empty;
            var heldCount = 0;

            if (holder is TraitHolderService concrete)
            {
                holderId = concrete.HolderId;
                heldCount = concrete.HeldCount;
                if (string.IsNullOrEmpty(normalizedKey))
                    normalizedKey = concrete.HolderKey;
            }

            WriteVariant(destination, VarIds.GameLib.Base.Trait.Holder.holderKey, DynamicVariant.FromString(normalizedKey), overwrite);
            WriteVariant(destination, VarIds.GameLib.Base.Trait.Holder.holderId, DynamicVariant.FromString(holderId), overwrite);
            WriteVariant(destination, VarIds.GameLib.Base.Trait.Holder.traitCount, DynamicVariant.FromInt(traitCount), overwrite);
            WriteVariant(destination, VarIds.GameLib.Base.Trait.Holder.heldCount, DynamicVariant.FromInt(heldCount), overwrite);
        }

        static void WriteVariant(IVarStore destination, int varId, DynamicVariant value, bool overwrite)
        {
            if (destination == null || varId == 0)
                return;

            if (!overwrite && destination.Contains(varId))
                return;

            destination.TrySetVariant(varId, value);
        }
    }
}
