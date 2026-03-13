// Game.Save.ISaveManager.cs
//
// SaveManager v2 interface

#nullable enable
using System;
using System.Collections.Generic;

namespace Game.Save
{
    public readonly struct SaveScopeRegistration
    {
        public readonly ScopeKey ScopeKey;
        public readonly Game.Profile.ProfileRegistryService? Profiles;
        public readonly Game.Common.IBlackboardService? Blackboard;
        public readonly Game.Scalar.IBaseScalarService? Scalars;
        public readonly ISavePlanSource PlanSource;

        public SaveScopeRegistration(
            ScopeKey scopeKey,
            ISavePlanSource planSource,
            Game.Profile.ProfileRegistryService? profiles,
            Game.Common.IBlackboardService? blackboard,
            Game.Scalar.IBaseScalarService? scalars)
        {
            ScopeKey = scopeKey;
            PlanSource = planSource;
            Profiles = profiles;
            Blackboard = blackboard;
            Scalars = scalars;
        }
    }

    public interface ISaveManager
    {
        int ActiveProfileId { get; }
        SavePlatformCaps PlatformCaps { get; }

        IDisposable RegisterScope(in SaveScopeRegistration reg);
        void GetRegisteredScopeKeys(List<ScopeKey> results);

        SaveResult Save(in SaveContext ctx, bool updateBackup = false);
        SaveResult Load(in SaveContext ctx);
        SaveResult Clear(in SaveContext ctx);
        SaveResult ChangeActiveProfile(int profileId);
        SaveResult DeleteAllPersistedData();
    }
}
