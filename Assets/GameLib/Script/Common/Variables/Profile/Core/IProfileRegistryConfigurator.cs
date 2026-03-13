using System;
using System.Collections.Generic;

namespace Game.Profile
{
    /// <summary>
    /// ProfileRegistry の登録ソースを動的に差し替えるための操作API。
    /// Pool / Template など、RuntimeLifetimeScope 再利用時の差し替えに使用する。
    /// </summary>
    public interface IScopeBindingRegistryConfigurator
    {
        /// <summary>Profile 定義を外部ソースとして一括設定する。</summary>
        void SetExternalProfileDefinitions(IReadOnlyList<IProfileDefinition> profiles, bool applyImmediately = false);

        /// <summary>Profile 定義を外部ソースとして追加する。</summary>
        void AddExternalProfileDefinition(IProfileDefinition profile, bool applyImmediately = false);

        /// <summary>外部ソースを全てクリアする。</summary>
        void ClearExternalProfiles(bool applyImmediately = false);
    }
}
