#nullable enable
using Game.Common;

namespace Game.Commands.VNext
{
    public readonly struct CommandHostCallResult
    {
        public DynamicVariant Value { get; }
        public bool HasValue { get; }

        public CommandHostCallResult(in DynamicVariant value, bool hasValue)
        {
            Value = value;
            HasValue = hasValue;
        }

        public static CommandHostCallResult Failure => new(DynamicVariant.Null, false);
        public static CommandHostCallResult SuccessNoValue => new(DynamicVariant.Null, true);
        public static CommandHostCallResult FromValue(in DynamicVariant value) => new(value, true);
    }
}
