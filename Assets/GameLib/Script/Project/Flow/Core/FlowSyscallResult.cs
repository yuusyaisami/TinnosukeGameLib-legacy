#nullable enable

using Game.Common;

namespace Game.Flow
{
    /// <summary>
    /// ホストコールの結果を表す構造体。成功時は HasValue=true で Value に値が入ります（null を含む）。
    /// 失敗時は HasValue=false を返します。
    /// </summary>
    public readonly struct FlowSyscallResult
    {
        /// <summary>戻り値のダイナミックな値（成功時に有効）</summary>
        public DynamicVariant Value { get; }

        /// <summary>
        /// Indicates whether the syscall succeeded.
        /// - true: success (Value may still be DynamicVariant.Null)
        /// - false: failure
        /// </summary>
        public bool HasValue { get; }

        public FlowSyscallResult(in DynamicVariant value, bool hasValue)
        {
            Value = value;
            HasValue = hasValue;
        }

        public static FlowSyscallResult Failure => new(DynamicVariant.Null, false);
        public static FlowSyscallResult SuccessNoValue => new(DynamicVariant.Null, true);
        public static FlowSyscallResult FromValue(in DynamicVariant value) => new(value, true);
    }
}
