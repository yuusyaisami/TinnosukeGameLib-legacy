#nullable enable
using System;
using UnityEngine;

namespace Game.Save
{
    public sealed class UnitySaveLogger : ISaveLogger
    {
        public void LogInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            Debug.Log(message);
        }

        public void LogWarning(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            Debug.LogWarning(message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            if (!string.IsNullOrEmpty(message))
                Debug.LogError(message);

            if (ex != null)
                Debug.LogException(ex);
        }
    }
}
