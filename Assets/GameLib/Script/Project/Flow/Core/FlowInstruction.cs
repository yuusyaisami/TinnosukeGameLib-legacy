#nullable enable

using System;

namespace Game.Flow
{
    /// <summary>
    /// 中間コードの命令（1 命令 = 1 オペコード + 最大 4 つの整数オペランド）を表します。
    /// </summary>
    [Serializable]
    public struct FlowInstruction
    {
        /// <summary>命令のオペコード</summary>
        public FlowOpCode Op;
        /// <summary>第1オペランド（用途は Op に依存）</summary>
        public int A;
        /// <summary>第2オペランド</summary>
        public int B;
        /// <summary>第3オペランド</summary>
        public int C;
        /// <summary>第4オペランド</summary>
        public int D;

        public FlowInstruction(FlowOpCode op, int a, int b, int c, int d)
        {
            Op = op;
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public override string ToString() => $"{Op} A={A} B={B} C={C} D={D}";
    }
}
