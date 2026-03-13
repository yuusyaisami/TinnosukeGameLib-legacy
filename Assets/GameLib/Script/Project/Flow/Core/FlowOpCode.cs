#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// バイトコードで使用されるオペコードの列挙です。
    /// <para>各 opcode は FlowInstruction のオペランドを解釈して挙動を決定します。</para>
    /// </summary>
    public enum FlowOpCode : byte
    {
        Nop = 0,

        // Control
        Jump = 10,
        BranchFalse = 11,
        Call = 12,
        Return = 13,

        // Vars
        SetVar = 20, // id=A, targetScope=B(0=Local,1=Shared), valueArg=C

        // Syscall
        HostCall = 30, // sysId=A, argsStart=B, argsCount=C, resultVarId=D(0=none)
    }
}
