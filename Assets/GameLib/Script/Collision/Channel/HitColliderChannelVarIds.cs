#nullable enable

namespace Game.Collision
{
    /// <summary>
    /// HitColliderController/Channel が CommandContext.Vars へ書き込むための varId 定義。
    /// </summary>
    public static class HitColliderChannelVarIds
    {
        static readonly int s_hit = Resolve(HitColliderChannelVariableKeys.Hit);
        static readonly int s_hitMeta = Resolve(HitColliderChannelVariableKeys.HitMeta);
        static readonly int s_isOtherSide = Resolve(HitColliderChannelVariableKeys.IsOtherSide);
        static readonly int s_hitEvent = Resolve(HitColliderChannelVariableKeys.HitEvent);
        static readonly int s_selfScope = Resolve(HitColliderChannelVariableKeys.SelfScope);
        static readonly int s_otherScope = Resolve(HitColliderChannelVariableKeys.OtherScope);
        static readonly int s_selfTag = Resolve(HitColliderChannelVariableKeys.SelfTag);
        static readonly int s_otherTag = Resolve(HitColliderChannelVariableKeys.OtherTag);

        public static int Hit => s_hit;                 // object (boxed CollisionHit)
        public static int HitMeta => s_hitMeta;         // object (boxed HitFrameMeta)
        public static int IsOtherSide => s_isOtherSide; // bool
        public static int HitEvent => s_hitEvent;       // int (HitEventType)

        public static int SelfScope => s_selfScope;     // object (IScopeNode)
        public static int OtherScope => s_otherScope;   // object (IScopeNode or unset)
        public static int SelfTag => s_selfTag;         // string
        public static int OtherTag => s_otherTag;       // string

        static int Resolve(string stableKey)
        {
            return VarIdResolver.TryResolve(stableKey, out var varId) && varId != 0
                ? varId
                : 0;
        }
    }
}
