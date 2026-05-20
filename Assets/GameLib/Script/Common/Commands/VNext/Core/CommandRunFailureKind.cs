#nullable enable

namespace Game.Commands.VNext
{
    public enum CommandRunFailureKind
    {
        None = 0,
        ResolveFailed = 1,
        ExecutorMissing = 2,
        InvalidArgs = 3,
        Exception = 4,
        Canceled = 5,
        PayloadInvalid = 6,
        Timeout = 7,
        DetachedPolicyMissing = 8,
        FailureBoundaryViolation = 9,
        CommandLocalInvalid = 10,
        LoopBoundMissing = 11,
    }
}
