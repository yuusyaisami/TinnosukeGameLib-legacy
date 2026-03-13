using System.Collections.Generic;

namespace Game.Profile
{
    /// <summary>
    /// ProfileRegistry の登録ソースを動的に差し替えるための操作API。
    /// Pool / Template など、RuntimeLifetimeScope 再利用時の差し替えに使用する。
    /// </summary>
    public interface IProfileRegistryConfigurator
    {
        void SetExternalProfiles(IReadOnlyList<BaseProfileSO> profiles, bool applyImmediately = false);
        void AddExternalProfile(BaseProfileSO profile, bool applyImmediately = false);
        void SetExternalProfileDefinitions(IReadOnlyList<IProfileDefinition> profiles, bool applyImmediately = false);
        void AddExternalProfileDefinition(IProfileDefinition profile, bool applyImmediately = false);
        void ClearExternalProfiles(bool applyImmediately = false);
    }
}
