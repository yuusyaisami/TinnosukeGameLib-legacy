#nullable enable
using System;
using Game.Common;

namespace Game.UI
{
    /// <summary>
    /// Specification for awaited dialog events. The awaited event keys should be stable keys
    /// that the dialog publishes when the user selects an option (e.g. "Dialog/Confirm").
    /// </summary>
    [Serializable]
    public sealed class DialogAwaitSpec
    {
        /// <summary>Event keys to wait for (first matching key completes the wait).</summary>
        public string[] EventKeys = Array.Empty<string>();

        /// <summary>If true, the dialog will be closed (Hide) when an awaited event is received.</summary>
        public bool CloseAfterEvent = true;

        /// <summary>Optional: map event key to an integer index for the result. By default, index = position in EventKeys.</summary>
        public int MapKeyToIndex(string key)
        {
            if (EventKeys == null)
                return -1;
            for (int i = 0; i < EventKeys.Length; i++) if (string.Equals(EventKeys[i], key, StringComparison.Ordinal)) return i;
            return -1;
        }
    }

    /// <summary>Result returned by <see cref="DialogChannelRuntime.ShowAndWaitAsync"/>.</summary>
    public sealed class DialogAwaitResult
    {
        public string EventKey { get; set; } = string.Empty;
        public int SelectedIndex { get; set; } = -1;
        public IVarStore? Payload { get; set; }
        public bool WasCancelled { get; set; } = false;
        public DialogCloseReason? CloseReason { get; set; }
    }
}
