// Game.Entity.Movement.MovementChannelDef.cs
//
// Movement チャネル定義（通常クラス、SO 禁止）。
// MovementクラスはEntity専用ではありません

namespace Game.Movement
{
    /// <summary>
    /// Movement チャネル合成演算。
    /// </summary>
    public enum MovementBlendOp
    {
        /// <summary>加算（デフォルト）</summary>
        Add = 0,
        /// <summary>乗算（スケーリング）</summary>
        Multiply = 1,
        /// <summary>上書き（自身より低優先度を置換）</summary>
        Override = 2,
        /// <summary>最大値</summary>
        Max = 3,
        /// <summary>線形補間</summary>
        Lerp = 4,
    }

    /// <summary>
    /// Movement チャネル定義（通常クラス、SO 禁止）。
    /// </summary>

    public sealed class MovementChannelDef
    {
        /// <summary>チャネルタグ（識別用）</summary>
        public string Tag { get; set; }

        /// <summary>優先度（昇順で処理、低い方が先）</summary>
        public int Priority { get; set; }

        /// <summary>合成演算</summary>
        public MovementBlendOp BlendOp { get; set; } = MovementBlendOp.Add;

        /// <summary>影響度（0〜1）</summary>
        public float Influence { get; set; } = 1f;

        /// <summary>デフォルトで有効か</summary>
        public bool EnabledByDefault { get; set; } = true;

        /// <summary>滑らかな遷移を制御するラムダ。0 なら即時切り替え。</summary>
        public float SmoothingLambda { get; set; } = 0f;

        /// <summary>減速専用ラムダ。0 なら減速処理なし。</summary>
        public float DecelerationLambda { get; set; } = 0f;

        /// <summary>
        /// デフォルト設定でチャネル定義を作成。
        /// </summary>
        public static MovementChannelDef Default(string tag) => new MovementChannelDef
        {
            Tag = tag,
            Priority = 0,
            BlendOp = MovementBlendOp.Add,
            Influence = 1f,
            EnabledByDefault = true,
            SmoothingLambda = 0f,
            DecelerationLambda = 0f,
        };

        /// <summary>
        /// 入力チャネル用プリセット。
        /// </summary>
        public static MovementChannelDef Input(string tag = "input") => new MovementChannelDef
        {
            Tag = tag,
            Priority = 0,
            BlendOp = MovementBlendOp.Add,
            Influence = 1f,
            EnabledByDefault = true,
            SmoothingLambda = 0f,
            DecelerationLambda = 0f,
        };

        /// <summary>
        /// ノックバック用プリセット。
        /// </summary>
        public static MovementChannelDef Knockback(string tag = "knockback") => new MovementChannelDef
        {
            Tag = tag,
            Priority = 10,
            BlendOp = MovementBlendOp.Add,
            Influence = 1f,
            EnabledByDefault = true,
            SmoothingLambda = 0f,
            DecelerationLambda = 0f,
        };

        /// <summary>
        /// 強制移動用プリセット（Override）。
        /// </summary>
        public static MovementChannelDef ForcedMove(string tag = "forced") => new MovementChannelDef
        {
            Tag = tag,
            Priority = 100,
            BlendOp = MovementBlendOp.Override,
            Influence = 1f,
            EnabledByDefault = false,
            SmoothingLambda = 0f,
            DecelerationLambda = 0f,
        };

        /// <summary>
        /// スロー効果用プリセット（Multiply）。
        /// </summary>
        public static MovementChannelDef SlowEffect(string tag = "slow") => new MovementChannelDef
        {
            Tag = tag,
            Priority = 200,
            BlendOp = MovementBlendOp.Multiply,
            Influence = 1f,
            EnabledByDefault = false,
            SmoothingLambda = 0f,
            DecelerationLambda = 0f,
        };
    }
}
