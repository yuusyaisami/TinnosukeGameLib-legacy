#nullable enable
using System;
using System.Collections.Generic;
using Game;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class UISelectionDebugView : IDisposable
    {
        // Readonly snapshot fields for Inspector display
        [ShowInInspector, ReadOnly]
        public string Current = "(none)";

        [ShowInInspector, ReadOnly]
        public string Previous = "(none)";

        [ShowInInspector, ReadOnly]
        public string Hovered = "(none)";

        [ShowInInspector, ReadOnly]
        public string CandidateProvider = "(none)";

        [ShowInInspector, ReadOnly]
        public string[] NavigationCandidates = Array.Empty<string>();

        [ShowInInspector, ReadOnly]
        public string[] PointerCandidates = Array.Empty<string>();

        [ShowInInspector, ReadOnly]
        public string[] NavigationCandidateTexts = Array.Empty<string>();

        [ShowInInspector, ReadOnly]
        public string[] PointerCandidateTexts = Array.Empty<string>();

        IUISelectionTelemetry? _telemetry;

        public void Bind(IUISelectionTelemetry telemetry)
        {
            if (_telemetry != null)
                Unbind();

            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

            telemetry.OnSelectionChanged += OnSelectionChanged;
            telemetry.OnHoverChanged += OnHoverChanged;
            telemetry.OnCandidatesUpdated += OnCandidatesUpdated;

            // initialize snapshot
            OnSelectionChanged(telemetry.Current);
            OnHoverChanged(telemetry.Hovered);
            CandidateProvider = telemetry.CandidateProvider?.ToString() ?? "(none)";
            OnCandidatesUpdated();
        }

        void OnSelectionChanged(IScopeNode? e)
        {
            Current = e?.Identity?.SelfTransform != null ? e.Identity.SelfTransform.name : "(none)";
            Previous = (e == null) ? "(none)" : "(updated)"; // previous is opaque here
        }

        void OnHoverChanged(IScopeNode? e)
        {
            Hovered = e?.Identity?.SelfTransform != null ? e.Identity.SelfTransform.name : "(none)";
        }

        void OnCandidatesUpdated()
        {
            if (_telemetry == null) return;
            var nav = _telemetry.LastNavigationCandidates;
            var ptr = _telemetry.LastPointerCandidates;
            NavigationCandidates = BuildCandidateTexts(nav);
            PointerCandidates = BuildCandidateTexts(ptr);

            NavigationCandidateTexts = NavigationCandidates;
            PointerCandidateTexts = PointerCandidates;
        }

        static string[] BuildCandidateTexts(IReadOnlyList<SelectCandidate>? candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return Array.Empty<string>();

            var list = new string[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                list[i] = candidates[i].ToString();
            }
            return list;
        }

        public void Unbind()
        {
            if (_telemetry == null) return;
            _telemetry.OnSelectionChanged -= OnSelectionChanged;
            _telemetry.OnHoverChanged -= OnHoverChanged;
            _telemetry.OnCandidatesUpdated -= OnCandidatesUpdated;
            _telemetry = null;
        }

        public void Dispose() => Unbind();
    }
}
