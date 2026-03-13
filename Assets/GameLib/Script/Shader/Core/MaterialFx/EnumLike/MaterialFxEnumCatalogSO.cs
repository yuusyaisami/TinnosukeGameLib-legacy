using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.MaterialFx
{
    // ================================================================
    // MaterialFxEnumCatalogSO - EnumDefinition catalog SO
    // ================================================================
    //
    // Overview:
    // Aggregates all EnumDefinitions and manages them by category.
    // Referenced by MaterialFxPropertyNode to select which EnumDefinition
    // to use for Int/Float type properties.
    //
    // ================================================================

    /// <summary>
    /// EnumDefinition catalog SO for MaterialFx.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MaterialFxEnumCatalog",
        menuName = "Game/MaterialFx/Enum Catalog",
        order = 101)]
    public class MaterialFxEnumCatalogSO : ScriptableObject
    {
        [Header("All Definitions")]
        [Tooltip("List of all MaterialFx EnumDefinitions")]
        [SerializeField] List<MaterialFxEnumDefinitionSO> definitions = new();

        /// <summary>Definition list</summary>
        public IReadOnlyList<MaterialFxEnumDefinitionSO> Definitions => definitions;

        /// <summary>Definition count</summary>
        public int Count => definitions.Count;

        /// <summary>
        /// Get definition by index.
        /// </summary>
        public MaterialFxEnumDefinitionSO GetDefinition(int index)
        {
            if (index < 0 || index >= definitions.Count) return null;
            return definitions[index];
        }

        /// <summary>
        /// Find definition by asset name.
        /// </summary>
        public MaterialFxEnumDefinitionSO FindByName(string definitionName)
        {
            if (string.IsNullOrEmpty(definitionName)) return null;
            return definitions.Find(d => d != null && d.name == definitionName);
        }

        /// <summary>
        /// Find definition by display name.
        /// </summary>
        public MaterialFxEnumDefinitionSO FindByDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            return definitions.Find(d => d != null && d.DisplayName == displayName);
        }

        /// <summary>
        /// Get index of definition, -1 if not found.
        /// </summary>
        public int IndexOf(MaterialFxEnumDefinitionSO definition)
        {
            if (definition == null) return -1;
            return definitions.IndexOf(definition);
        }

        /// <summary>
        /// Get display options for dropdown (includes "None" option).
        /// </summary>
        public string[] GetDropdownOptions()
        {
            var options = new string[definitions.Count + 1];
            options[0] = "(None)";
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                options[i + 1] = def != null ? def.DisplayName : "(Missing)";
            }
            return options;
        }

        /// <summary>
        /// Convert dropdown index to definition (0 = None).
        /// </summary>
        public MaterialFxEnumDefinitionSO GetDefinitionFromDropdownIndex(int dropdownIndex)
        {
            if (dropdownIndex <= 0) return null;
            return GetDefinition(dropdownIndex - 1);
        }

        /// <summary>
        /// Convert definition to dropdown index.
        /// </summary>
        public int GetDropdownIndex(MaterialFxEnumDefinitionSO definition)
        {
            if (definition == null) return 0;
            var idx = IndexOf(definition);
            return idx < 0 ? 0 : idx + 1;
        }
    }
}
