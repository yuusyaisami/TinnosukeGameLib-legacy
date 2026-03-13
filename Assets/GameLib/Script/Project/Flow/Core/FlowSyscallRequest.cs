#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// ホスト（IFlowHost）に渡すシステムコールリクエストを表します。
    /// <para>sysId と引数の範囲、結果格納先をまとめた軽量構造体です。</para>
    /// </summary>
    public readonly struct FlowSyscallRequest
    {
        public int SysId { get; }
        public int ArgStart { get; }
        public int ArgCount { get; }
        public int ResultVarId { get; }

        public FlowSyscallRequest(int sysId, int argStart, int argCount, int resultVarId)
        {
            SysId = sysId;
            ArgStart = argStart;
            ArgCount = argCount;
            ResultVarId = resultVarId;
        }
    }
}
