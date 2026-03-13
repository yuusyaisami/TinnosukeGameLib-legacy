#nullable enable

namespace Game.Flow
{
    /// <summary>
    /// Flow 実行の結果を表す不変値オブジェクト。
    /// </summary>
    public readonly struct FlowRunResult
    {
        public FlowRunStatus Status { get; }
        /// <summary>最後に実行した命令ポインタ（IP）</summary>
        public int LastIp { get; }
        /// <summary>エラーが発生した場合のエラー位置（IP）。未エラー時は -1。</summary>
        public int ErrorIp { get; }
        /// <summary>エラーやキャンセル時のメッセージ。</summary>
        public string Message { get; }

        public FlowRunResult(FlowRunStatus status, int lastIp, int errorIp, string message)
        {
            Status = status;
            LastIp = lastIp;
            ErrorIp = errorIp;
            Message = message ?? string.Empty;
        }

        /// <summary>正常終了の結果を作成します。</summary>
        public static FlowRunResult Completed(int lastIp) => new(FlowRunStatus.Completed, lastIp, -1, string.Empty);
        /// <summary>キャンセルの結果を作成します。</summary>
        public static FlowRunResult Canceled(int lastIp) => new(FlowRunStatus.Canceled, lastIp, -1, string.Empty);
        /// <summary>エラー結果を作成します。</summary>
        public static FlowRunResult Error(int lastIp, int errorIp, string message) => new(FlowRunStatus.Error, lastIp, errorIp, message);

        public override string ToString() => $"{Status} lastIp={LastIp} errorIp={ErrorIp} msg='{Message}'";
    }
}
