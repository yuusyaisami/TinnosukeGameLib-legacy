#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// Flow 実行時のオプション。
    /// </summary>
    public sealed class FlowRunOptions
    {
        /// <summary>最大コールスタック深度（安全上の上限）</summary>
        public int MaxCallDepth = 64;
        /// <summary>1フレーム内で実行する最大命令数（無限ループ防止）</summary>
        public int MaxInstructionsPerFrame = 5000;
        /// <summary>ホスト呼び出しが失敗した場合も継続するかどうか</summary>
        public bool ContinueOnHostCallFailure = false;
        /// <summary>実行中のテレメトリ受け取り用コールバック</summary>
        public IFlowTelemetry? Telemetry;
    }
}
