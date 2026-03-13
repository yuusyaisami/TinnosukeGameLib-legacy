#nullable enable
using System;
using UnityEngine;

namespace Game.Common
{
    /// <summary>
    /// Indicates an int field should be selected from the generated VarIds tree in the Inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter)]
    public sealed class VarIdDropdownAttribute : PropertyAttribute
    {
        /// <summary>
        /// Optional prefix filter ("GameLib/RoomMap" etc.) used to limit the dropdown entries.
        /// </summary>
        public string Filter { get; }

        /// <summary>
        /// If true the dropdown includes a "<None>" entry that clears the selection.
        /// </summary>
        public bool AllowNone { get; }

        public VarIdDropdownAttribute(string filter = "", bool allowNone = true)
        {
            Filter = filter ?? string.Empty;
            AllowNone = allowNone;
        }
    }
}