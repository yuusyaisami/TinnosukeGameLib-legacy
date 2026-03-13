#nullable enable
using System;
using System.Collections.Generic;

namespace Game.UI
{
    public interface IUISelectionTelemetry
    {
        IScopeNode? Current { get; }
        IScopeNode? Previous { get; }
        IScopeNode? Hovered { get; }
        IReadOnlyList<IUIInputConsumer> CurrentConsumers { get; }

        ISelectCandidateProvider? CandidateProvider { get; }
        IUIModalRoot? CurrentInputRoot { get; }
        UISelectionService.SelectionSource LastSelectionSource { get; }

        IReadOnlyList<SelectCandidate> LastNavigationCandidates { get; }
        IReadOnlyList<SelectCandidate> LastPointerCandidates { get; }

        event Action<IScopeNode?>? OnSelectionChanged;
        event Action<IScopeNode?>? OnHoverChanged;
        event System.Action? OnCandidatesUpdated;
    }
}
