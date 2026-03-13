using UnityEngine;

namespace Game.ActionBlock.Keys
{
    /// <summary>
    /// ActionBlock 用のキー定義。
    /// </summary>
    public static partial class ActionBlockKeys
    {
        public static partial class Entity
        {
            /// <summary>
            /// User(Player) 移動をブロックするキー。
            /// </summary>
            public const string UserMovement = "Entity.UserMovement";

            /// <summary>
            /// AI 移動をブロックするキー。
            /// </summary>
            public const string AIMovement = "Entity.AIMovement";

            /// <summary>
            /// 回転をブロックするキー。
            public const string SystemRotation = "Entity.SystemRotation";

            /// <summary>
            /// System 移動をブロックするキー。
            /// </summary>
            public const string SystemMovement = "Entity.SystemMovement";

            /// <summary>
            /// 
            /// </summary>
            public const string TransformControllerMovement = "Entity.TransformControllerMovement";

            /// <summary>
            /// ダッシュをブロックするキー。
            /// </summary>
            public const string Sprint = "Entity.Sprint";

            /// <summary>
            /// 攻撃をブロックするキー。
            /// </summary>
            public const string Attack = "Entity.Attack";

            /// <summary>
            /// AI 制御をブロックするキー。
            /// </summary>
            public const string AIControl = "Entity.AIControl";
        }
    }
}