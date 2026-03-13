#nullable enable

namespace Game.Collision
{
    /// <summary>
    /// HitColliderController/Channel が CommandContext.Vars へ書き込むための varId 定義。
    ///
    /// VarKeyRegistry が未用意/未同期でもランタイムで確実に参照できるよう、固定の int varId を採用する。
    /// コマンド側は VarKeyRef.varId にこれらを指定して参照する。
    /// </summary>
    public static class HitColliderChannelVarIds
    {
        // NOTE: 既存の Generated VarIds と衝突しないよう高めのレンジを使用。
        public const int Hit = 10001;          // object (boxed CollisionHit)
        public const int HitMeta = 10002;      // object (boxed HitFrameMeta)
        public const int IsOtherSide = 10003;  // bool
        public const int HitEvent = 10004;     // int (HitEventType)

        public const int SelfScope = 10005;    // object (IScopeNode)
        public const int OtherScope = 10006;   // object (IScopeNode or unset)
    }
}
