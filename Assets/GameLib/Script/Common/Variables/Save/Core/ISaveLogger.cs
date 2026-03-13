#nullable enable
using System;

namespace Game.Save
{
    public interface ISaveLogger
    {
        void LogInfo(string msg);
        void LogWarning(string msg);
        void LogError(string msg, Exception? ex);
    }
}
