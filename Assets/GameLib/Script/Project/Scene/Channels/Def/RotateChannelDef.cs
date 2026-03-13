// Game.Rotation.RotateChannelDef.cs
//
// Rotate チャネル定義（通常クラス、SO 禁止）。

namespace Game.Rotation
{
    /// <summary>
    /// Rotate チャネル合成演算。
    /// </summary>
    public enum RotateBlendOp
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
    /// Rotate チャネル定義（通常クラス、SO 禁止）。
    /// </summary>
    public sealed class RotateChannelDef
    {
        /// <summary>チャネルタグ（識別用）</summary>
        public string Tag { get; set; }

        /// <summary>優先度（昇順で処理、低い方が先）</summary>
        public int Priority { get; set; }

        /// <summary>合成演算</summary>
        public RotateBlendOp BlendOp { get; set; } = RotateBlendOp.Add;

        /// <summary>影響度（0〜1）</summary>
        public float Influence { get; set; } = 1f;

        /// <summary>デフォルトで有効か</summary>
        public bool EnabledByDefault { get; set; } = true;

        /// <summary>
        /// デフォルト設定でチャネル定義を作成。
        /// </summary>
        public static RotateChannelDef Default(string tag) => new RotateChannelDef
        {
            Tag = tag,
            Priority = 0,
            BlendOp = RotateBlendOp.Add,
            Influence = 1f,
            EnabledByDefault = true,
        };

        /// <summary>
        /// 入力チャネル用プリセット。
        /// </summary>
        public static RotateChannelDef Input(string tag = "input") => new RotateChannelDef
        {
            Tag = tag,
            Priority = 0,
            BlendOp = RotateBlendOp.Add,
            Influence = 1f,
            EnabledByDefault = true,
        };

        /// <summary>
        /// 弾幕パターン用プリセット（Override）。
        /// </summary>
        public static RotateChannelDef Pattern(string tag = "pattern") => new RotateChannelDef
        {
            Tag = tag,
            Priority = 50,
            BlendOp = RotateBlendOp.Override,
            Influence = 1f,
            EnabledByDefault = true,
        };

        /// <summary>
        /// 強制回転用プリセット（Override）。
        /// </summary>
        public static RotateChannelDef Forced(string tag = "forced") => new RotateChannelDef
        {
            Tag = tag,
            Priority = 100,
            BlendOp = RotateBlendOp.Override,
            Influence = 1f,
            EnabledByDefault = false,
        };

        /// <summary>
        /// 減速効果用プリセット（Multiply）。
        /// </summary>
        public static RotateChannelDef SlowEffect(string tag = "slow") => new RotateChannelDef
        {
            Tag = tag,
            Priority = 200,
            BlendOp = RotateBlendOp.Multiply,
            Influence = 0.5f,
            EnabledByDefault = false,
        };
    }
}
