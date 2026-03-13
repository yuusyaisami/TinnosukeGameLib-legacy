// Game.Save.IScalarSaveDefinitionProvider.cs
//
// Provides save-definition data for Scalar keys sourced from baseline databases.

using System.Collections.Generic;
using Game.Scalar;

namespace Game.Save
{
    /// <summary>
    /// Exposes Scalar save definition data (whitelist + metadata) to SaveSystem.
    /// Implemented by baseline registries that own ScalarDatabaseEntry collections.
    /// </summary>
    public interface IScalarSaveDefinitionProvider
    {
        /// <summary>Enumerate all known entries (regardless of save eligibility).</summary>
        IEnumerable<ScalarDatabaseEntry> EnumerateAllEntries();

        /// <summary>Enumerate entries belonging to the specified save layer.</summary>
        IEnumerable<ScalarDatabaseEntry> EnumerateByLayer(SaveLayer layer);

        /// <summary>Enumerate entries that are save-enabled for the specified layer.</summary>
        IEnumerable<ScalarDatabaseEntry> EnumerateSaveTargets(SaveLayer layer);

        /// <summary>Lookup an entry by key id (ScalarKey.Id).</summary>
        bool TryGetById(int keyId, out ScalarDatabaseEntry entry);
    }
}
