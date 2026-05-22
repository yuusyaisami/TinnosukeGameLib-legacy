using UnityEngine;

namespace Game
{
    internal static class ScopeStatics
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ScopeNodeHierarchy.Reset();
            ScopeBuildCoordinator.Reset();
        }
    }
}

