using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.EnumLike
{
    // ================================================================
    // BaseEnumDefinitionSO - Enum-like definition base SO
    // ================================================================
    //
    // ## Overview
    //
    // Base ScriptableObject for defining enum-like choices.
    // Since C# enums cannot be inherited, this SO-based approach
    // enables "categorization" and extensibility.
    //
    // ## Key Features
    //
    // - Index-based values (auto-assigned from list order)
    // - Customizable entries via derived classes
    // - Inspector integration with dropdown support
    //
    // ================================================================

    /// <summary>
    /// Base class for enum-like entry.
    /// Value is determined by list index, not manually set.
    /// </summary>
    [Serializable]
    public class BaseEnumEntry
    {
        [Tooltip("Display name for this entry")]
        public string name;

        [Tooltip("Description of this entry")]
        [TextArea(1, 3)]
        public string description;

        [Tooltip("Explicit integer value (used only if useExplicitValues is true in the definition)")]
        public int value;
    }

    /// <summary>
    /// Base ScriptableObject for enum-like definitions.
    /// </summary>
    public abstract class BaseEnumDefinitionSO : ScriptableObject
    {
        [Header("Definition Info")]
        [Tooltip("Display name for this enum (e.g. FlashMode, BlendMode)")]
        [SerializeField] protected string displayName;

        [Tooltip("Description of this enum")]
        [SerializeField, TextArea(2, 4)] protected string description;

        [Header("Values Config")]
        [Tooltip("If true, allows setting arbitrary integer values instead of relying on list index. Useful for enums that need to be sparse (e.g., 10, 20, 30).")]
        [SerializeField] protected bool useExplicitValues;

        /// <summary>Display name</summary>
        public string DisplayName => displayName;

        /// <summary>Description</summary>
        public string Description => description;

        /// <summary>Whether to use explicit values instead of list indices</summary>
        public bool UseExplicitValues => useExplicitValues;

        /// <summary>Entry count</summary>
        public abstract int Count { get; }

        /// <summary>Get entry name by index (index = array index)</summary>
        public abstract string GetEntryName(int index);

        /// <summary>Get entry description by index</summary>
        public abstract string GetEntryDescription(int index);

        /// <summary>Get integer value of entry by index</summary>
        public abstract int GetValue(int index);

        /// <summary>Get index by name, returns -1 if not found</summary>
        public abstract int GetIndexByName(string name);

        /// <summary>Get index by value, returns -1 if not found</summary>
        public abstract int GetIndexByValue(int value);

        /// <summary>Get display options for Inspector dropdown</summary>
        public abstract string[] GetDisplayOptions();

        /// <summary>Check for duplicate entry names. Returns list of duplicate names.</summary>
        public abstract List<string> GetDuplicateNames();

        /// <summary>Check if index is valid</summary>
        public bool IsValidIndex(int index) => index >= 0 && index < Count;
    }

    /// <summary>
    /// Generic base for enum definitions with typed entries.
    /// </summary>
    /// <typeparam name="TEntry">Entry type derived from BaseEnumEntry</typeparam>
    public abstract class BaseEnumDefinitionSO<TEntry> : BaseEnumDefinitionSO
        where TEntry : BaseEnumEntry, new()
    {
        [Header("Entries")]
        [SerializeField] protected List<TEntry> entries = new();

        /// <summary>Entry list (read-only)</summary>
        public IReadOnlyList<TEntry> Entries => entries;

        /// <inheritdoc/>
        public override int Count => entries.Count;

        /// <summary>Get entry by index</summary>
        public TEntry GetEntry(int index)
        {
            if (index < 0 || index >= entries.Count) return null;
            return entries[index];
        }

        /// <inheritdoc/>
        public override string GetEntryName(int index)
        {
            var entry = GetEntry(index);
            return entry?.name ?? string.Empty;
        }

        /// <inheritdoc/>
        public override string GetEntryDescription(int index)
        {
            var entry = GetEntry(index);
            return entry?.description ?? string.Empty;
        }

        /// <inheritdoc/>
        public override int GetValue(int index)
        {
            var entry = GetEntry(index);
            if (entry == null) return -1;
            return useExplicitValues ? entry.value : index;
        }

        /// <inheritdoc/>
        public override int GetIndexByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].name, name, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        /// <inheritdoc/>
        public override int GetIndexByValue(int value)
        {
            if (!useExplicitValues)
            {
                return (value >= 0 && value < Count) ? value : -1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].value == value)
                    return i;
            }
            return -1;
        }

        /// <inheritdoc/>
        public override string[] GetDisplayOptions()
        {
            var options = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                var entryName = entries[i]?.name;
                string baseName = string.IsNullOrEmpty(entryName) ? $"Entry {i}" : entryName;

                if (useExplicitValues && entries[i] != null)
                {
                    options[i] = $"{entries[i].value}: {baseName}";
                }
                else
                {
                    options[i] = baseName;
                }
            }
            return options;
        }

        /// <inheritdoc/>
        public override List<string> GetDuplicateNames()
        {
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<string>();

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name)) continue;

                if (!usedNames.Add(entry.name))
                {
                    if (!duplicates.Contains(entry.name))
                        duplicates.Add(entry.name);
                }
            }
            return duplicates;
        }
    }
}
