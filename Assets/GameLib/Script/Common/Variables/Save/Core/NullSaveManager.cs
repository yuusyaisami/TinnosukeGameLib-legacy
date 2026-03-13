#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Save
{
    public sealed class NullSaveManager : ISaveManager
    {
        public int ActiveProfileId => 0;
        public SavePlatformCaps PlatformCaps => default;

        public IDisposable RegisterScope(in SaveScopeRegistration reg) => new NoopDisposable();
        public void GetRegisteredScopeKeys(List<ScopeKey> results) => results?.Clear();

        public SaveResult Save(in SaveContext ctx, bool updateBackup = false) => SaveResult.Success();
        public SaveResult Load(in SaveContext ctx) => SaveResult.NoData();
        public SaveResult Clear(in SaveContext ctx) => SaveResult.Success();
        public SaveResult ChangeActiveProfile(int profileId) => profileId < 0
            ? SaveResult.Failed(SaveError.InvalidKey, "ProfileId must be non-negative.")
            : SaveResult.Success();
        public SaveResult DeleteAllPersistedData() => SaveResult.Success();

        sealed class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
