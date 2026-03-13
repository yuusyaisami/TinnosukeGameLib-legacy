#nullable enable
using System;
using System.Collections.Generic;

namespace Game.UI
{
    public interface IUIModalStackTelemetry
    {
        IUIModalRoot? CurrentInputRoot { get; }
        IReadOnlyList<UIModalActiveRoot> ActiveRoots { get; }
        bool IsEmpty { get; }
        int Depth { get; }

        event System.Action<UIModalStackChangeContext>? OnModalStackChanged;
        event System.Action<UIModalStackRootsChangeContext>? OnActiveRootsChanged;

        // Helper snapshot APIs
        string[] GetStackModalIds();
        IReadOnlyList<(string ModalId, string SelectedName)> GetSelectionHistorySnapshot();
    }
}
