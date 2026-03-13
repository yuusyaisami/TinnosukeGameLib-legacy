#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// 実行可能な Flow プログラムデータの読み取りインターフェース。
    /// </summary>
    public interface IFlowProgramData
    {
        int Version { get; }
        FlowInstruction[] Code { get; }
        FlowArg[] Args { get; }
        string[] StringTable { get; }
        FlowFunctionInfo[] Functions { get; }
    }
}
