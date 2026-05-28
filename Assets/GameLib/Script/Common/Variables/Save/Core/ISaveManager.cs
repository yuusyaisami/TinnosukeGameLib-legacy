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
        public readonly Game.Profile.ScopeBindingRegistryService? Profiles;
        public readonly Game.Common.IVarStore? Blackboard;
        public readonly Game.Common.IGridBlackboardService? GridBlackboard;
        public readonly Game.Scalar.IBaseScalarService? Scalars;
        public readonly ISavePlanSource PlanSource;

        public SaveScopeRegistration(
            ScopeKey scopeKey,
            ISavePlanSource planSource,
            Game.Profile.ScopeBindingRegistryService? profiles,
            Game.Common.IVarStore? blackboard,
            Game.Common.IGridBlackboardService? gridBlackboard,
            Game.Scalar.IBaseScalarService? scalars)
        {
            ScopeKey = scopeKey;
            PlanSource = planSource;
            Profiles = profiles;
            Blackboard = blackboard;
            GridBlackboard = gridBlackboard;
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
