#nullable enable
using System.Collections.Generic;

namespace Game.Save
{
    public interface ISavePlanSource
    {
        ScopeKey ScopeKey { get; }
        int Version { get; }
        void CollectEntries(SaveLayer layer, List<SaveEntry> dest);
    }
}
