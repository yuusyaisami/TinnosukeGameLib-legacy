#nullable enable
using System;
using System.Threading;

namespace Game.Save
{
    public sealed class UnitySaveThreadGuard : ISaveThreadGuard
    {
        readonly int _mainThreadId;

        public UnitySaveThreadGuard()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool TryAssertMainThread(string operation, out string message)
        {
            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                message = string.Empty;
                return true;
            }

            message = string.IsNullOrEmpty(operation)
                ? $"Not running on main thread. main={_mainThreadId}, current={Thread.CurrentThread.ManagedThreadId}."
                : $"{operation} must run on main thread. main={_mainThreadId}, current={Thread.CurrentThread.ManagedThreadId}.";
            return false;
        }
    }
}
