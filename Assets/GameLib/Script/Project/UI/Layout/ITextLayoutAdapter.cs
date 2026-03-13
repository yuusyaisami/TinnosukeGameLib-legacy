using System;
using UnityEngine;
using TMPro;

namespace Game.Layout
{
    /// <summary>
    /// Adapter facade that owns the canonical full text and provides layout-safe size queries.
    /// Responsibilities:
    /// - Hold the FullText used for layout calculations
    /// - Provide GetPreferredSize(maxWidth) which must use TMP's GetPreferredValues
    /// - Provide an optional GetLayoutText() to return a sanitized string for measurement
    /// </summary>
    public interface ITextLayoutAdapter : IRectTransformSizeAdapter
    {
        TMP_Text TargetText { get; }

        /// <summary>Canonical full text used for layout measurements.</summary>
        string FullText { get; }

        /// <summary>Set the canonical full text. Implementations should only trigger layout dirty when this changes.</summary>
        void SetFullText(string fullText);

        /// <summary>Optional: return a sanitized string for layout measurement (e.g., strip animator tags).</summary>
        string GetLayoutText();

        /// <summary>Raised when FullText changes in a way that affects layout measurements.</summary>
        event System.Action OnLayoutContentChanged;
    }
}
