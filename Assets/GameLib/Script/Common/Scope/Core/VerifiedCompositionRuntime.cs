#nullable enable
using Game.DI;
using UnityEngine;

namespace Game
{
    public static class VerifiedCompositionRuntime
    {
        public interface IVerifiedScopeBindingSink
        {
            bool TryBindRuntimeScope(BaseRuntimeTemplateSO template, IScopeGraphHost scope, IScopeNode? explicitParent);

            void ReleaseRuntimeScope(IScopeGraphHost scope);

            bool TryUpdateRuntimeScopeState(IScopeGraphHost scope, int nextState);

            bool TryRefreshRuntimeScopeUnityLink(IScopeGraphHost scope);
        }

        static bool s_isActive;
        static IVerifiedScopeBindingSink? s_bindingSink;

        public static bool IsActive => s_isActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_isActive = false;
            s_bindingSink = null;
        }

        public static void Activate(IVerifiedScopeBindingSink? bindingSink = null)
        {
            s_isActive = true;
            s_bindingSink = bindingSink;
        }

        public static void Deactivate()
        {
            s_isActive = false;
            s_bindingSink = null;
        }

        public static bool TryBindRuntimeScope(BaseRuntimeTemplateSO template, IScopeGraphHost scope, IScopeNode? explicitParent)
        {
            if (!s_isActive || s_bindingSink == null)
                return false;

            return s_bindingSink.TryBindRuntimeScope(template, scope, explicitParent);
        }

        public static void ReleaseRuntimeScope(IScopeGraphHost scope)
        {
            if (!s_isActive || s_bindingSink == null)
                return;

            s_bindingSink.ReleaseRuntimeScope(scope);
        }

        public static bool TryUpdateRuntimeScopeState(IScopeGraphHost scope, int nextState)
        {
            if (!s_isActive || s_bindingSink == null)
                return false;

            return s_bindingSink.TryUpdateRuntimeScopeState(scope, nextState);
        }

        public static bool TryRefreshRuntimeScopeUnityLink(IScopeGraphHost scope)
        {
            if (!s_isActive || s_bindingSink == null)
                return false;

            return s_bindingSink.TryRefreshRuntimeScopeUnityLink(scope);
        }
    }
}