// Game.Commands.CompareOp.cs
//
// 比較演算子の列挙型。
// DynamicCondition で使用。

namespace Game.Commands
{
    /// <summary>
    /// 比較演算子。
    /// </summary>
    public enum CompareOp
    {
        // 等価比較
        Equals,
        NotEquals,

        // 数値比較
        LessThan,
        LessOrEqual,
        GreaterThan,
        GreaterOrEqual,

        // 文字列比較
        Contains,
        StartsWith,
        EndsWith,

        // 真偽値（単項演算）
        /// <summary>A != 0 (B は無視)</summary>
        IsTrue,
        /// <summary>A == 0 (B は無視)</summary>
        IsFalse,
    }
}
