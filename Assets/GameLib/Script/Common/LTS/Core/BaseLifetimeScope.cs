#nullable enable
using System;
using UnityEngine;

namespace Game
{
    public interface IFeatureInstaller
    {
        void InstallFeature(IRuntimeContainerBuilder builder, IScopeNode scope);
    }

    [Obsolete("Legacy BaseLifetimeScope name is kept only for migration. Use RuntimeLifetimeScopeBase/RuntimeLifetimeScope for new scopes.", false)]
    [RequireComponent(typeof(EntityIdentityMB))]
    public abstract class BaseLifetimeScope : RuntimeLifetimeScopeBase
    {
        // Legacy compatibility shell. The VContainer LifetimeScope inheritance was intentionally removed.
    }

    [Obsolete("Legacy typed BaseLifetimeScope<TParent> name is kept only for migration. Use RuntimeLifetimeScopeBase and RequiredParentKind instead.", false)]
    public abstract class BaseLifetimeScope<TParent> : BaseLifetimeScope
        where TParent : RuntimeLifetimeScopeBase
    {
        // Legacy typed-parent shell. Runtime scopes resolve parents by Transform hierarchy and LifetimeScopeKind.
    }
}
