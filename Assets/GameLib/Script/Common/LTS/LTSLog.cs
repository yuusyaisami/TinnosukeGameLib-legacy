using UnityEngine;

namespace Game
{
    /// <summary>
    /// Lightweight runtime controllable log wrapper for LTS / GameLib code.
    /// Toggle LTSLog.Enabled to enable/disable these debug messages at runtime.
    /// </summary>
    public static class LTSLog
    {
        /// <summary>Global switch for LTS debug logs. Set to false to suppress logs.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Forwards logs to Unity <see cref="Debug"/> when true (default).</summary>
        public static bool ForwardToUnity { get; set; } = true;

        public static void SetEnabled(bool enabled) => Enabled = enabled;

        public static void Log(object message, Object context = null)
        {
            if (!Enabled) return;
            if (ForwardToUnity)
                Debug.Log(message, context);
        }

        public static void LogWarning(object message, Object context = null)
        {
            if (!Enabled) return;
            if (ForwardToUnity)
                Debug.LogWarning(message, context);
        }

        public static void LogError(object message, Object context = null)
        {
            if (!Enabled) return;
            if (ForwardToUnity)
                Debug.LogError(message, context);
        }
    }
}
