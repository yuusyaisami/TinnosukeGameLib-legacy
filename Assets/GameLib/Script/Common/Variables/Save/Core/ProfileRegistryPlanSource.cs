#nullable enable
using System.Collections.Generic;
using Game.Profile;
using Game.Scalar;

namespace Game.Save
{
    public sealed class ProfileRegistryPlanSource : ISavePlanSource
    {
        readonly ProfileRegistryService _profiles;
        readonly ScopeKey _scopeKey;

        public ProfileRegistryPlanSource(ProfileRegistryService profiles, ScopeKey scopeKey)
        {
            _profiles = profiles;
            _scopeKey = scopeKey;
        }

        public ScopeKey ScopeKey => _scopeKey;

        public int Version => _profiles != null ? _profiles.Version : 0;

        public void CollectEntries(SaveLayer layer, List<SaveEntry> dest)
        {
            if (_profiles == null || dest == null)
                return;

            var expectedIdentity = _profiles.ScopeIdentity;

            foreach (var runtime in _profiles.EnumerateProfiles())
            {
                var list = runtime.SaveEntries;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (!e.IsValid)
                        continue;
                    if (e.SaveLayer != layer)
                        continue;
                    if (!string.Equals(e.ScopeIdentity, expectedIdentity, System.StringComparison.Ordinal))
                        continue;

                    if (e.Kind == SaveEntryKind.Blackboard)
                    {
                        if (e.BlackboardVarId == 0)
                            continue;

                        dest.Add(new SaveEntry(
                            layer,
                            SaveTargetKind.Blackboard,
                            varId: e.BlackboardVarId,
                            scalarKeyId: 0,
                            sourceProfileType: e.ProfileTypeName,
                            sourceBindingName: string.Empty));
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(e.ScalarKeyName))
                            continue;

                        var key = new ScalarKey(e.ScalarKeyName);
                        if (key.Id == 0)
                            continue;

                        dest.Add(new SaveEntry(
                            layer,
                            SaveTargetKind.Scalar,
                            varId: 0,
                            scalarKeyId: key.Id,
                            sourceProfileType: e.ProfileTypeName,
                            sourceBindingName: string.Empty));
                    }
                }
            }
        }
    }
}
