#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// 実行中のテレメトリを受け取るためのコールバックインターフェース。
    /// </summary>
    public interface IFlowTelemetry
    {
        void OnStart(IFlowProgramData program, string entryFunctionName);
        void OnInstruction(int ip, in FlowInstruction instr);
        void OnSyscall(int sysId, int argStart, int argCount);
        void OnEnd(in FlowRunResult result);
    }
}
