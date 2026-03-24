#nullable enable
using Game.Common;
using Game.Vars.Generated;

namespace Game.Trait
{
    public static class TraitDataWriteUtility
    {
        public static void WriteTraitDataToVarStore(
            TraitDefinitionSO? traitDefinition,
            IVarStore? source,
            IVarStore destination,
            bool overwrite)
        {
            if (destination == null)
                return;

            traitDefinition?.ApplyCommonVars(destination, overwrite);
            CopyTraitDataVars(source, destination, overwrite);
        }

        public static void WriteTraitDataToStores(
            TraitDefinitionSO? traitDefinition,
            IVarStore? source,
            IVarStore destination,
            IGridBlackboardService? grid,
            bool overwrite)
        {
            if (destination == null)
                return;

            traitDefinition?.ApplyCommonVars(destination, overwrite);
            traitDefinition?.ApplyCommonGridTable(grid, overwrite);
            CopyTraitDataVars(source, destination, overwrite);
        }

        public static void CopyTraitDataVars(IVarStore? source, IVarStore destination, bool overwrite)
        {
            if (source == null || destination == null)
                return;

            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.instanceId, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.definitionId, overwrite);
            CopyValue(source, destination, VarIds.GameLib.Base.Trait.Element.definitionAsset, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.weight, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.nameTemplate, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.descriptionTemplate, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.nameKey, overwrite);
            CopyVariant(source, destination, VarIds.GameLib.Base.Trait.Element.descriptionKey, overwrite);
        }

        static void CopyVariant(IVarStore source, IVarStore destination, int varId, bool overwrite)
        {
            if (varId == 0)
                return;

            if (!overwrite && destination.Contains(varId))
                return;

            if (!source.TryGetVariant(varId, out var value))
                return;

            destination.TrySetVariant(varId, value);
        }

        static void CopyManagedRef(IVarStore source, IVarStore destination, int varId, bool overwrite)
        {
            if (varId == 0)
                return;

            if (!overwrite && destination.Contains(varId))
                return;

            if (!source.TryGetManagedRef(varId, out var value) || value == null)
                return;

            destination.TrySetManagedRef(varId, value);
        }

        static void CopyValue(IVarStore source, IVarStore destination, int varId, bool overwrite)
        {
            if (varId == 0)
                return;

            if (!overwrite && destination.Contains(varId))
                return;

            var kind = source.GetVarKind(varId);
            if (kind == ValueKind.ManagedRef)
            {
                CopyManagedRef(source, destination, varId, overwrite: true);
                return;
            }

            CopyVariant(source, destination, varId, overwrite: true);
        }
    }
}